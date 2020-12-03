# This Powershell script is an example for the usage of the Azure Remote Rendering service
# Documentation: https://docs.microsoft.com/en-us/azure/remote-rendering/samples/powershell-example-scripts
#
# Usage: 
# Fill out the assetConversionSettings in arrconfig.json next to this file or provide all necessary parameters on the commandline 
# This script is using the ARR REST API to convert assets to the asset format used in rendering sessions

# Conversion.ps1 [-ConfigFile <pathtoconfig>]
#   Requires that the ARR account has access to the storage account. The documentation explains how to grant access. 
#   Will:
#   - Load config from arrconfig.json or optional file provided by the -ConfigFile parameter
#   - Retrieve Azure Storage credentials for the configured storage account and logged in user
#   - Upload the directory pointed to in assetConversionSettings.localAssetDirectoryPath to the storage input container
#      using the provided storage account.
#   - Start a conversion using the ARR Conversion REST API and retrieves a conversion Id 
#   - Poll the conversion status until the conversion succeeded or failed

# Conversion.ps1 -UseContainerSas [-ConfigFile <pathtoconfig>]
#   The -UseContainerSas will convert the input asset using the conversions/createWithSharedAccessSignature REST API. 
#   Will also perform upload and polling of conversion status. 
#   The script will access the provided storage account and generate SAS URIs which are used in the call to give access to the 
#   blob containers of the storage account.

# The individual stages -Upload -ConvertAsset and -GetConversionStatus can be executed individually like: 
# Conversion.ps1 -Upload
#   Only executes the Upload of the asset directory to the input storage container and terminates 

# Conversion.ps1 -ConvertAsset
#  Only executes the convert asset step

# Conversion.ps1 -GetConversionStatus -Id <ConversionId> [-Poll]
#   Retrieves the status of the conversion with the provided conversion id. 
#   -Poll will poll the conversion with given id until the conversion succeeds or fails 

# Optional parameters:
# individual settings in the config file can be overridden on the command line:

Param(
    [switch] $Upload, #if set the local asset directory will be uploaded to the inputcontainer and the script exits
    [switch] $ConvertAsset,
    [switch] $GetConversionStatus,
    [switch] $Poll,
    [switch] $UseContainerSas, #If provided the script will generate container SAS tokens to be used with the conversions/createWithSharedAccessSignature REST API
    [string] $ConfigFile,
    [string] $Id, #optional ConversionId used with GetConversionStatus
    [string] $ArrAccountId, #optional override for arrAccountId of accountSettings in config file
    [string] $ArrAccountKey, #optional override for arrAccountKey of accountSettings in config file
    [string] $Region, #optional override for region of accountSettings in config file
    [string] $ResourceGroup, # optional override for resourceGroup of assetConversionSettings in config file
    [string] $StorageAccountName, # optional override for storageAccountName of assetConversionSettings in config file
    [string] $BlobInputContainerName, # optional override for blobInputContainer of assetConversionSettings in config file
    [string] $BlobOutputContainerName, # optional override for blobOutputContainerName of assetConversionSettings in config file
    [string] $InputAssetPath, # path under inputcontainer/InputFolderPath pointing to the asset to be converted e.g model\box.fbx 
    [string] $InputFolderPath, # optional path in input container, all data under this path will be retrieved by the conversion service , if empty all data from the input storage container will copied
    [string] $OutputFolderPath, # optional override for the path in output container, conversion result will be copied there
    [string] $OutputAssetFileName, # optional filename of the outputAssetFileName of assetConversionSettings in config file. needs to end in .arrAsset
    [string] $LocalAssetDirectoryPath, # Path to directory containing all input asset data (e.g. fbx and textures referenced by it)
    [string] $AuthenticationEndpoint,
    [string] $ServiceEndpoint,
    [hashtable] $AdditionalParameters
)

. "$PSScriptRoot\ARRUtils.ps1"

Set-StrictMode -Version Latest
$PrerequisitesInstalled = CheckPrerequisites
if (-Not $PrerequisitesInstalled) {
    WriteError("Prerequisites not installed - Exiting.")
    exit 1
}

$LoggedIn = CheckLogin
if (-Not $LoggedIn) {
    WriteError("User not logged in - Exiting.")
    exit 1
}

if (-Not ($Upload -or $ConvertAsset -or $GetConversionStatus )) {
    # if none of the three stages is explicitly asked for execute all stages and poll for conversion status until finished
    $Upload = $true
    $ConvertAsset = $true
    $GetConversionStatus = $true
    $Poll = $true
}

# Upload asset directory to the configured azure blob storage account and input container under given inputFolder
function UploadAssetDirectory($assetSettings) {
    $localAssetFile = Join-Path -Path $assetSettings.localAssetDirectoryPath -ChildPath $assetSettings.inputAssetPath
    $assetFileExistsLocally = Test-Path $localAssetFile
    if(!$assetFileExistsLocally)
    {
        WriteError("Unable to upload asset file from local asset directory '$($assetSettings.localAssetDirectoryPath)'. File '$localAssetFile' does not exist.")
        WriteError("'$($assetSettings.localAssetDirectoryPath)' must include the provided input asset path '$($assetSettings.inputAssetPath)' as a child.")
        return $false
    }

    WriteInformation ("Uploading asset directory from $($assetSettings.localAssetDirectoryPath) to blob storage input container ...")
    
    if ($assetSettings.localAssetDirectoryPath -notmatch '\\$') {
        $assetSettings.localAssetDirectoryPath += '\'
    }

    $inputDirExists = Test-Path  -Path $assetSettings.localAssetDirectoryPath
    if ($false -eq $inputDirExists) {
        WriteError("Unable to upload files from asset directory $($assetSettings.localAssetDirectoryPath). Directory does not exist.")
        return $false
    }        
    
    $filesToUpload = @(Get-ChildItem -Path $assetSettings.localAssetDirectoryPath -File -Recurse) 

    if (0 -eq $filesToUpload.Length) {
        WriteError("Unable to upload files from asset directory $($assetSettings.localAssetDirectoryPath). Directory is empty.")
    }

    WriteInformation ("Uploading $($filesToUpload.Length) files to input storage container")
    $filesUploaded = 0
    
    foreach ($fileToUpload in $filesToUpload) {
        $relativePathInFolder = $fileToUpload.FullName.Substring($assetSettings.localAssetDirectoryPath.Length).Replace("\", "/")
        $remoteBlobpath = $assetSettings.inputFolderPath + $relativePathInFolder

        WriteSuccess ("Uploading file $($fileToUpload.FullName) to input blob storage container")
        $blob = Set-AzStorageBlobContent -File $fileToUpload.FullName -Container $assetSettings.blobInputContainerName -Context $assetSettings.storageContext -Blob $remoteBlobpath -Force
        $filesUploaded++
        if ($null -ne $blob ) {
            WriteSuccess ("Uploaded file $filesUploaded/$($filesToUpload.Length) $($fileToUpload.FullName) to blob storage ...")
        }
        else {
            WriteError("Unable to upload file $fileToUpload from local asset directory location $($assetSettings.localAssetDirectoryPath) ...")
            return $false
        }
    }
    WriteSuccess ("Uploaded asset directory to input storage container")
    return $true
}

# Asset Conversion
# Starts a remote asset conversion by using the ARR conversion REST API <endPoint>/v1/accounts/<accountId>/conversions/createWithSharedAccessSignature
# All files present in the input container under the (optional) folderPath will be copied to the ARR conversion service
# the output .arrAsset file will be written back to the provided outputcontainer under the given (optional) folderPath
# Immediately returns a conversion id which can be used to query the status of the conversion process (see below)
function ConvertAssetWithSharedAccessSignature(
    $accountSettings,
    $authenticationEndpoint, 
    $serviceEndpoint, 
    $accountId, 
    $accountKey,
    $assetConversionSettings,
    $inputAssetPath,
    $additionalParameters) {
    try {
        WriteLine
        $body = 
        @{ 
            input  =
            @{
                storageAccountName   = $assetConversionSettings.storageAccountName;
                blobContainerName    = $assetConversionSettings.blobInputContainerName;
                containerReadListSas = $assetConversionSettings.inputContainerSAS;
                folderPath           = $assetConversionSettings.inputFolderPath;
                inputAssetPath       = $assetConversionSettings.inputAssetPath;
            };
            output =
            @{
                storageAccountName  = $assetConversionSettings.storageAccountName;
                blobContainerName   = $assetConversionSettings.blobOutputContainerName;
                containerWriteSas   = $assetConversionSettings.outputContainerSAS;
                folderPath          = $assetConversionSettings.outputFolderPath;
                outputAssetFileName = $assetConversionSettings.outputAssetFileName;
            }
        }
        
        if ($additionalParameters) {
            $additionalParameters.Keys | % { $body += @{ $_ = $additionalParameters.Item($_) } }
        }

        $url = "$serviceEndpoint/v1/accounts/$accountId/conversions/createWithSharedAccessSignature"

        WriteInformation("Converting Asset using container Shared Access Signatures ...")
        WriteInformation("  authentication endpoint: $authenticationEndpoint")
        WriteInformation("  service endpoint: $serviceEndpoint")
        WriteInformation("  accountId: $accountId")
        WriteInformation("Input:")       
        WriteInformation("    storageAccount: $($assetConversionSettings.storageAccountName)")  
        WriteInformation("    inputContainer: $($assetConversionSettings.blobInputContainerName)") 
        WriteInformation("    inputContainerSAS: $($assetConversionSettings.inputContainerSAS)")   
        WriteInformation("    folderPath: $($assetConversionSettings.inputFolderPath)") 
        WriteInformation("    inputAssetPath: $($assetConversionSettings.inputAssetPath)")
        WriteInformation("Output:")       
        WriteInformation("    storageAccount: $($assetConversionSettings.storageAccountName)")  
        WriteInformation("    outputContainer: $($assetConversionSettings.blobOutputContainerName)") 
        WriteInformation("    outputContainerSAS: $($assetConversionSettings.outputContainerSAS)")   
        WriteInformation("    folderPath: $($assetConversionSettings.outputFolderPath)") 
        WriteInformation("    outputAssetFilename: $($assetConversionSettings.outputAssetFileName)")

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method POST -ContentType "application/json" -Body ($body | ConvertTo-Json) -Headers @{ Authorization = "Bearer $token" }
        $conversionId = (GetResponseBody($response)).conversionId
        WriteSuccess("Successfully started the conversion with Id: $conversionId")
        WriteSuccessResponse($response.RawContent)

        return $conversionId
    }
    catch {
        WriteError("Unable to start conversion of the asset ...")
        HandleException($_.Exception)
        throw
    }
}

# Asset Conversion
# Starts a remote asset conversion by using the ARR conversion REST API <endPoint>/v1/accounts/<accountId>/conversions/create
# The ARR account needs to be granted access to the storage account for this to work. Consult documentation how to grant access.
# All files present in the input container under the (optional) folderPath will be copied to the ARR conversion service
# the output .arrAsset file will be written back to the provided outputcontainer under the given (optional) folderPath
# Immediately returns a conversion id which can be used to query the status of the conversion process (see below)
function ConvertAsset(
    $accountSettings,
    $authenticationEndpoint, 
    $serviceEndpoint, 
    $accountId, 
    $accountKey, 
    $assetConversionSettings,
    $inputAssetPath,
    $additionalParameters) {
    try {
        WriteLine
        $body = 
        @{ 
            input  =
            @{
                storageAccountName = $assetConversionSettings.storageAccountName;
                blobContainerName  = $assetConversionSettings.blobInputContainerName;
                folderPath         = $assetConversionSettings.inputFolderPath;
                inputAssetPath     = $assetConversionSettings.inputAssetPath;
            };
            output =
            @{
                storageAccountName  = $assetConversionSettings.storageAccountName;
                blobContainerName   = $assetConversionSettings.blobOutputContainerName;
                folderPath          = $assetConversionSettings.outputFolderPath;
                outputAssetFileName = $assetConversionSettings.outputAssetFileName;
            }
        }
        
        if ($additionalParameters) {
            $additionalParameters.Keys | % { $body += @{ $_ = $additionalParameters.Item($_) } }
        }

        $url = "$serviceEndpoint/v1/accounts/$accountId/conversions/create"

        WriteInformation("Converting Asset using linked storage account ...")
        WriteInformation("  authentication endpoint: $authenticationEndpoint")
        WriteInformation("  service endpoint: $serviceEndpoint")
        WriteInformation("  accountId: $accountId")
        WriteInformation("Input:")       
        WriteInformation("    storageAccount: $($assetConversionSettings.storageAccountName)")  
        WriteInformation("    inputContainer: $($assetConversionSettings.blobInputContainerName)") 
        WriteInformation("    folderPath: $($assetConversionSettings.inputFolderPath)") 
        WriteInformation("    inputAssetPath: $($assetConversionSettings.inputAssetPath)")
        WriteInformation("Output:")       
        WriteInformation("    storageAccount: $($assetConversionSettings.storageAccountName)")  
        WriteInformation("    outputContainer: $($assetConversionSettings.blobOutputContainerName)") 
        WriteInformation("    folderPath: $($assetConversionSettings.outputFolderPath)") 
        WriteInformation("    outputAssetFilename: $($assetConversionSettings.outputAssetFileName)")

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method POST -ContentType "application/json" -Body ($body | ConvertTo-Json) -Headers @{ Authorization = "Bearer $token" }
        $conversionId = (GetResponseBody($response)).conversionId
        WriteSuccess("Successfully started the conversion with Id: $conversionId")
        WriteSuccessResponse($response.RawContent)

        return $conversionId
    }
    catch {
        WriteError("Unable to start conversion of the asset ...")
        HandleException($_.Exception)
        throw
    }
}

# calls the conversion status ARR REST API "<endPoint>/v1/accounts/<accountId>/conversions/<conversionId>"
# returns if the conversion process is still running, succeeded or failed
function GetConversionStatus($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $conversionId) {
    try {
        $url = "$serviceEndpoint/v1/accounts/$accountId/conversions/${conversionId}"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method GET -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

        WriteSuccessResponse($response.RawContent)
        return $response
    }
    catch {
        WriteError("Unable to get the status of the conversion with Id: $conversionId ...")
        HandleException($_.Exception)
        throw
    }
}

# repeatedly poll the conversion status ARR REST API until success of failure
function PollConversionStatus($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $conversionId) {
    $conversionStatus = "Running"
    $startTime = Get-Date

    $convertedAsset = $null

    while ($true) {
        Start-Sleep -Seconds 10
        WriteProgress  -activity "Ongoing asset conversion with conversion id: '$conversionId'" -status "Since $([int]((Get-Date) - $startTime).TotalSeconds) seconds"

        $response = GetConversionStatus $authenticationEndpoint $serviceEndpoint $accountId $accountKey $conversionId
        $responseJson = ($response.Content | ConvertFrom-Json)
        $conversionStatus = $responseJson.status

        if ("success" -eq $conversionStatus.ToLower()) {
            $convertedAsset = $responseJson.convertedAsset
            break
        }

        if ("failure" -eq $conversionStatus.ToLower()) {
            break
        }
    }
   
    $totalTimeElapsed = $(New-TimeSpan $startTime $(get-date)).TotalSeconds


    if ("success" -eq $conversionStatus.ToLower()) {
        WriteProgress -activity "Your asset conversion is complete" -status "Completed..."
        WriteSuccess("The conversion with Id: $conversionId was successful ...")
        WriteInformation ("Total time elapsed: $totalTimeElapsed  ...")
        WriteInformation($response)
    }
    if ("failure" -eq $conversionStatus.ToLower()) {
        WriteError("The asset conversion with Id: $conversionId resulted in an error")
        WriteInformation ("Total time elapsed: $totalTimeElapsed  ...")
        WriteInformation($response)
        exit 1
    }

    return $convertedAsset
}

# Execution of script starts here

if ([string]::IsNullOrEmpty($ConfigFile)) {
    $ConfigFile = "$PSScriptRoot\arrconfig.json"
}

$config = LoadConfig `
    -fileLocation $ConfigFile `
    -ArrAccountId $ArrAccountId `
    -ArrAccountKey $ArrAccountKey `
    -Region $Region `
    -AuthenticationEndpoint $AuthenticationEndpoint `
    -ServiceEndpoint $ServiceEndpoint `
    -StorageAccountName $StorageAccountName `
    -ResourceGroup $ResourceGroup `
    -BlobInputContainerName $BlobInputContainerName `
    -BlobOutputContainerName $BlobOutputContainerName `
    -LocalAssetDirectoryPath $LocalAssetDirectoryPath `
    -InputAssetPath $InputAssetPath `
    -InputFolderPath $InputFolderPath `
    -OutputAssetFileName $OutputAssetFileName

if ($null -eq $config) {
    WriteError("Error reading config file - Exiting.")
    exit 1
}

$defaultConfig = GetDefaultConfig

$accountOkay = VerifyAccountSettings $config $defaultConfig $ServiceEndpoint
if ($false -eq $accountOkay) {
    WriteError("Error reading accountSettings in $ConfigFile - Exiting.")
    exit 1
}

if ($ConvertAsset -or $Upload -or $UseContainerSas) {
    $storageSettingsOkay = VerifyStorageSettings $config $defaultConfig
    if ($false -eq $storageSettingsOkay) {
        WriteError("Error reading assetConversionSettings in $ConfigFile - Exiting.")
        exit 1
    }
    
    # if we do any conversion related things we need to validate storage settings
    # we do not need the storage settings if we only want to spin up a session
    $isValid = ValidateConversionSettings $config $defaultConfig $ConvertAsset
    if ($false -eq $isValid) {
        WriteError("The config file is not valid. Please ensure the required values are filled in - Exiting.")
        exit 1
    }
    WriteSuccess("Successfully Loaded Configurations from file : $ConfigFile ...")
    
    $config = AddStorageAccountInformationToConfig $config
    
    if ($null -eq $config) {
        WriteError("Azure settings not valid. Please ensure the required values are filled in correctly in the config file $ConfigFile")
        exit 1
    }
}

if ($Upload) {
    $uploadSuccessful = UploadAssetDirectory $config.assetConversionSettings
    if ($false -eq $uploadSuccessful) {
        WriteError("Upload failed - Exiting.")
        exit 1
    }
}

if ($ConvertAsset) {
    if ($UseContainerSas) { 
        # Generate SAS and provide it in rest call - this is used if your storage account is not connected with your ARR account
        $inputContainerSAS = GenerateInputContainerSAS $config.assetConversionSettings.storageContext.BlobEndPoint $config.assetConversionSettings.blobInputContainerName $config.assetConversionSettings.storageContext
        $config.assetConversionSettings.inputContainerSAS = $inputContainerSAS

        $outputContainerSAS = GenerateOutputContainerSAS -blobEndPoint $config.assetConversionSettings.storageContext.blobEndPoint  -blobContainerName $config.assetConversionSettings.blobOutputContainerName -storageContext $config.assetConversionSettings.storageContext
        $config.assetConversionSettings.outputContainerSAS = $outputContainerSAS

        $Id = ConvertAssetWithSharedAccessSignature -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -assetConversionSettings $config.assetConversionSettings -AdditionalParameters $AdditionalParameters
    }
    else { 
        # The ARR account has read/write access to the blob containers of the storage account - so we do not need to generate SAS tokens for access
        $Id = ConvertAsset -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -assetConversionSettings $config.assetConversionSettings -AdditionalParameters $AdditionalParameters
    }
}

$convertedAssetLocation = $null
if ($GetConversionStatus) {
    if ([string]::IsNullOrEmpty($Id)) {
        $Id = Read-Host "Please enter the conversion Id"
    }

    if ($Poll) {
        $convertedAssetLocation = PollConversionStatus -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -conversionId $Id
    }
    else {
        $response = GetConversionStatus -serviceEndpoint $config.accountSettings.serviceEndpoint -authenticationEndpoint $config.accountSettings.authenticationEndpoint -accountId  $config.accountSettings.arrAccountId  -accountKey $config.accountSettings.arrAccountKey -conversionId $Id
        $responseJson = ($response.Content | ConvertFrom-Json)
        $conversionStatus = $responseJson.status

        if ("success" -eq $conversionStatus.ToLower()) {
            $convertedAssetLocation = $responseJson.convertedAsset
        }
    }
}

if ($null -ne $convertedAssetLocation) {
    WriteSuccess("Successfully converted asset. Converted asset uploaded to:")
    WriteSuccess("  storage account: $($convertedAssetLocation.storageAccountName)")
    WriteSuccess("  output blob container: $($convertedAssetLocation.blobContainerName)")
    WriteSuccess("  converted asset file path: $($convertedAssetLocation.assetFilePath)")
    
    if ($UseContainerSas) {
        # now retrieve the converted model SAS URI - you will need to call the ARR SDK API with a URL to your model to load a model in your application
        $sasUrl = GenerateOutputmodelSASUrl $convertedAssetLocation $config.assetConversionSettings.storageContext.BlobEndPoint $config.assetConversionSettings.storageContext
        WriteInformation("model SAS URI: $sasUrl")
    }
}

# SIG # Begin signature block
# MIInOAYJKoZIhvcNAQcCoIInKTCCJyUCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCCdWwCUX1hj/rtB
# dKlVDejW5IOmng/NeO5wlh5WswkNbqCCEWUwggh3MIIHX6ADAgECAhM2AAABCXeq
# 7JKeP287AAEAAAEJMA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMDAyMDkxMzIzMzFaFw0yMTAyMDgxMzIzMzFaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQCYbOXmlszFnuCPBMUV6BC1J1QOI7+cqKNtzF8TpVghtmKqz3XY46b/YxOH
# xTQp9J6ewnLLEzC2tP5AAx+Txqr1HpNy63D2U8WbBuRpXBk0bb8f8xrJoK7IVtSJ
# i0dNXCQ0E/XDCw6EARlDrZPfRYR26xk1eCHoX6dWBSKzkrQuOAkcTomU0diFVKW1
# O5A77bXGyF+l31eB5GyLjaPd52G6GzJBIYYKAtrfWRdH0Lei/LPqTcm1z3gLML0B
# GKRNvxi4M21jZBE9LwCvkIKLIZiL/PM6IPXdCVhevxTfjfZuB/GEp0SWmL+EdRoe
# ex084siN8ItdLPdg6hvpqXse/idLAgMBAAGjggWDMIIFfzApBgkrBgEEAYI3FQoE
# HDAaMAwGCisGAQQBgjdbAQEwCgYIKwYBBQUHAwMwPQYJKwYBBAGCNxUHBDAwLgYm
# KwYBBAGCNxUIhpDjDYTVtHiE8Ys+hZvdFs6dEoFgg93NZoaUjDICAWQCAQwwggJ2
# BggrBgEFBQcBAQSCAmgwggJkMGIGCCsGAQUFBzAChlZodHRwOi8vY3JsLm1pY3Jv
# c29mdC5jb20vcGtpaW5mcmEvQ2VydHMvQlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1F
# JTIwQ1MlMjBDQSUyMDAxKDEpLmNydDBSBggrBgEFBQcwAoZGaHR0cDovL2NybDEu
# YW1lLmdibC9haWEvQlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1FJTIwQ1MlMjBDQSUy
# MDAxKDEpLmNydDBSBggrBgEFBQcwAoZGaHR0cDovL2NybDIuYW1lLmdibC9haWEv
# QlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1FJTIwQ1MlMjBDQSUyMDAxKDEpLmNydDBS
# BggrBgEFBQcwAoZGaHR0cDovL2NybDMuYW1lLmdibC9haWEvQlkyUEtJQ1NDQTAx
# LkFNRS5HQkxfQU1FJTIwQ1MlMjBDQSUyMDAxKDEpLmNydDBSBggrBgEFBQcwAoZG
# aHR0cDovL2NybDQuYW1lLmdibC9haWEvQlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1F
# JTIwQ1MlMjBDQSUyMDAxKDEpLmNydDCBrQYIKwYBBQUHMAKGgaBsZGFwOi8vL0NO
# PUFNRSUyMENTJTIwQ0ElMjAwMSxDTj1BSUEsQ049UHVibGljJTIwS2V5JTIwU2Vy
# dmljZXMsQ049U2VydmljZXMsQ049Q29uZmlndXJhdGlvbixEQz1BTUUsREM9R0JM
# P2NBQ2VydGlmaWNhdGU/YmFzZT9vYmplY3RDbGFzcz1jZXJ0aWZpY2F0aW9uQXV0
# aG9yaXR5MB0GA1UdDgQWBBSFf5cqMUbeKYe6lzBbR/KdqpCuUjAOBgNVHQ8BAf8E
# BAMCB4AwUAYDVR0RBEkwR6RFMEMxKTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRp
# b25zIFB1ZXJ0byBSaWNvMRYwFAYDVQQFEw0yMzYxNjcrNDU3Nzg5MIIB1AYDVR0f
# BIIByzCCAccwggHDoIIBv6CCAbuGPGh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9w
# a2lpbmZyYS9DUkwvQU1FJTIwQ1MlMjBDQSUyMDAxLmNybIYuaHR0cDovL2NybDEu
# YW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxLmNybIYuaHR0cDovL2NybDIu
# YW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxLmNybIYuaHR0cDovL2NybDMu
# YW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxLmNybIYuaHR0cDovL2NybDQu
# YW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxLmNybIaBumxkYXA6Ly8vQ049
# QU1FJTIwQ1MlMjBDQSUyMDAxLENOPUJZMlBLSUNTQ0EwMSxDTj1DRFAsQ049UHVi
# bGljJTIwS2V5JTIwU2VydmljZXMsQ049U2VydmljZXMsQ049Q29uZmlndXJhdGlv
# bixEQz1BTUUsREM9R0JMP2NlcnRpZmljYXRlUmV2b2NhdGlvbkxpc3Q/YmFzZT9v
# YmplY3RDbGFzcz1jUkxEaXN0cmlidXRpb25Qb2ludDAfBgNVHSMEGDAWgBQbZqIZ
# /JvrpdqEjxiY6RCkw3uSvTAfBgNVHSUEGDAWBgorBgEEAYI3WwEBBggrBgEFBQcD
# AzANBgkqhkiG9w0BAQsFAAOCAQEAQYd76y+ByMUA3+A7WsHFVJZpuEyh9fqDOhQ3
# f18LCatNSqBNUP0PYlEVimWJUvrr3RAHWBG2nr3inRaaivvveR51LkM1TH098qVj
# v+7MNcwu8IkQ2d0+OoAfQXSsnFMNXsJBZsT6W3zscdK6YCFmyrPkYOI0NTPhoX+i
# ZvhwtmR9x9UI3dDrd/Lg+9L+H5Cn4UI00llmM89XBpich2vzQR9N+3J98TJn5Yxf
# IXoDYhX6zHu+cKilOjs2uwg3wCL3VekflxyOeyFC7hFTRFWAdWKJ+QM78WCFOElB
# 38ah1U43wk7u+BruSXE/gXyGUi5NIfUsPmEE/S8l9UewoIPcIzCCCOYwggbOoAMC
# AQICEx8AAAAUtMUfxvKAvnEAAAAAABQwDQYJKoZIhvcNAQELBQAwPDETMBEGCgmS
# JomT8ixkARkWA0dCTDETMBEGCgmSJomT8ixkARkWA0FNRTEQMA4GA1UEAxMHYW1l
# cm9vdDAeFw0xNjA5MTUyMTMzMDNaFw0yMTA5MTUyMTQzMDNaMEExEzARBgoJkiaJ
# k/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBD
# UyBDQSAwMTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBANVXgQLW+frQ
# 9xuAud03zSTcZmH84YlyrSkM0hsbmr+utG00tVRHgw40pxYbJp5W+hpDwnmJgicF
# oGRrPt6FifMmnd//1aD/fW1xvGs80yZk9jxTNcisVF1CYIuyPctwuJZfwE3wcGxh
# kVw/tj3ZHZVacSls3jRD1cGwrcVo1IR6+hHMvUejtt4/tv0UmUoH82HLQ8w1oTX9
# D7xj35Zt9T0pOPqM3Gt9+/zs7tPp2gyoOYv8xR4X0iWZKuXTzxugvMA63YsB4ehu
# SBqzHdkF55rxH47aT6hPhvDHlm7M2lsZcRI0CUAujwcJ/vELeFapXNGpt2d3wcPJ
# M0bpzrPDJ/8CAwEAAaOCBNowggTWMBAGCSsGAQQBgjcVAQQDAgEBMCMGCSsGAQQB
# gjcVAgQWBBSR/DPOQp72k+bifVTXCBi7uNdxZTAdBgNVHQ4EFgQUG2aiGfyb66Xa
# hI8YmOkQpMN7kr0wggEEBgNVHSUEgfwwgfkGBysGAQUCAwUGCCsGAQUFBwMBBggr
# BgEFBQcDAgYKKwYBBAGCNxQCAQYJKwYBBAGCNxUGBgorBgEEAYI3CgMMBgkrBgEE
# AYI3FQYGCCsGAQUFBwMJBggrBgEFBQgCAgYKKwYBBAGCN0ABAQYLKwYBBAGCNwoD
# BAEGCisGAQQBgjcKAwQGCSsGAQQBgjcVBQYKKwYBBAGCNxQCAgYKKwYBBAGCNxQC
# AwYIKwYBBQUHAwMGCisGAQQBgjdbAQEGCisGAQQBgjdbAgEGCisGAQQBgjdbAwEG
# CisGAQQBgjdbBQEGCisGAQQBgjdbBAEGCisGAQQBgjdbBAIwGQYJKwYBBAGCNxQC
# BAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMBIGA1UdEwEB/wQIMAYBAf8CAQAw
# HwYDVR0jBBgwFoAUKV5RXmSuNLnrrJwNp4x1AdEJCygwggFoBgNVHR8EggFfMIIB
# WzCCAVegggFToIIBT4YjaHR0cDovL2NybDEuYW1lLmdibC9jcmwvYW1lcm9vdC5j
# cmyGMWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2lpbmZyYS9jcmwvYW1lcm9v
# dC5jcmyGI2h0dHA6Ly9jcmwyLmFtZS5nYmwvY3JsL2FtZXJvb3QuY3JshiNodHRw
# Oi8vY3JsMy5hbWUuZ2JsL2NybC9hbWVyb290LmNybIaBqmxkYXA6Ly8vQ049YW1l
# cm9vdCxDTj1BTUVST09ULENOPUNEUCxDTj1QdWJsaWMlMjBLZXklMjBTZXJ2aWNl
# cyxDTj1TZXJ2aWNlcyxDTj1Db25maWd1cmF0aW9uLERDPUFNRSxEQz1HQkw/Y2Vy
# dGlmaWNhdGVSZXZvY2F0aW9uTGlzdD9iYXNlP29iamVjdENsYXNzPWNSTERpc3Ry
# aWJ1dGlvblBvaW50MIIBqwYIKwYBBQUHAQEEggGdMIIBmTA3BggrBgEFBQcwAoYr
# aHR0cDovL2NybDEuYW1lLmdibC9haWEvQU1FUk9PVF9hbWVyb290LmNydDBHBggr
# BgEFBQcwAoY7aHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraWluZnJhL2NlcnRz
# L0FNRVJPT1RfYW1lcm9vdC5jcnQwNwYIKwYBBQUHMAKGK2h0dHA6Ly9jcmwyLmFt
# ZS5nYmwvYWlhL0FNRVJPT1RfYW1lcm9vdC5jcnQwNwYIKwYBBQUHMAKGK2h0dHA6
# Ly9jcmwzLmFtZS5nYmwvYWlhL0FNRVJPT1RfYW1lcm9vdC5jcnQwgaIGCCsGAQUF
# BzAChoGVbGRhcDovLy9DTj1hbWVyb290LENOPUFJQSxDTj1QdWJsaWMlMjBLZXkl
# MjBTZXJ2aWNlcyxDTj1TZXJ2aWNlcyxDTj1Db25maWd1cmF0aW9uLERDPUFNRSxE
# Qz1HQkw/Y0FDZXJ0aWZpY2F0ZT9iYXNlP29iamVjdENsYXNzPWNlcnRpZmljYXRp
# b25BdXRob3JpdHkwDQYJKoZIhvcNAQELBQADggIBACi3Soaajx+kAWjNwgDqkIvK
# AOFkHmS1t0DlzZlpu1ANNfA0BGtck6hEG7g+TpUdVrvxdvPQ5lzU3bGTOBkyhGmX
# oSIlWjKC7xCbbuYegk8n1qj3rTcjiakdbBqqHdF8J+fxv83E2EsZ+StzfCnZXA62
# QCMn6t8mhCWBxpwPXif39Ua32yYHqP0QISAnLTjjcH6bAV3IIk7k5pQ/5NA6qIL8
# yYD6vRjpCMl/3cZOyJD81/5+POLNMx0eCClOfFNxtaD0kJmeThwL4B2hAEpHTeRN
# tB8ib+cze3bvkGNPHyPlSHIuqWoC31x2Gk192SfzFDPV1PqFOcuKjC8049SSBtC1
# X7hyvMqAe4dop8k3u25+odhvDcWdNmimdMWvp/yZ6FyjbGlTxtUqE7iLTLF1eaUL
# SEobAap16hY2N2yTJTISKHzHI4rjsEQlvqa2fj6GLxNj/jC+4LNy+uRmfQXShd30
# lt075qTroz0Nt680pXvVhsRSdNnzW2hfQu2xuOLg8zKGVOD/rr0GgeyhODjKgL2G
# Hxctbb9XaVSDf6ocdB//aDYjiabmWd/WYmy7fQ127KuasMh5nSV2orMcAed8CbIV
# I3NYu+sahT1DRm/BGUN2hSpdsPQeO73wYvp1N7DdLaZyz7XsOCx1quCwQ+bojWVQ
# TmKLGegSoUpZNfmP9MtSMYIVKTCCFSUCAQEwWDBBMRMwEQYKCZImiZPyLGQBGRYD
# R0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxBTUUgQ1MgQ0EgMDEC
# EzYAAAEJd6rskp4/bzsAAQAAAQkwDQYJYIZIAWUDBAIBBQCgga4wGQYJKoZIhvcN
# AQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisGAQQBgjcCARUw
# LwYJKoZIhvcNAQkEMSIEIMUmgZrTbIdyw0kniB5TgY6SVDLtrMXE4j1oHy/DbE26
# MEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8AcwBvAGYAdKEagBhodHRw
# Oi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEAhm+1uxKcrAoO
# HqbzE7vClolh0NLE6r61yTVQ2V9QKGzV/gujdp+t3OG1p6w7L9gkr4JGQ8AsL2nU
# PsgVa4HI5Jb6MYMCzKRMJhedVsFp5GgwQyAajhqCV0BBg6tUD0po45nboL3Xlvh6
# B/TjqDAsJCkfT9BWnS8+lvSx8Bqm4eHV9Fx2px58DYI+ysTMTYn5FwrzgRsLgqLL
# +3Yt/bsLpWfR2BN2r/QN173krVAtKPBrLm3H4WkEF6vLF4XFGDBuUFwndXBKnrtR
# n6kyyg14Y0fOMBy4oSQQjAcsyaaJJYmYsk4FV1gbNuCOrP/A0adNzuPckTTy2NlG
# bk7rWU35HqGCEvEwghLtBgorBgEEAYI3AwMBMYIS3TCCEtkGCSqGSIb3DQEHAqCC
# EsowghLGAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFVBgsqhkiG9w0BCRABBKCCAUQE
# ggFAMIIBPAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFlAwQCAQUABCCHz/6J40AU
# D4ODM1OThetm3scPhpWbN0IZd9eVhSzRsAIGX7vpeINaGBMyMDIwMTIwMzIwMzk0
# MC4yMDJaMASAAgH0oIHUpIHRMIHOMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2Fz
# aGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENv
# cnBvcmF0aW9uMSkwJwYDVQQLEyBNaWNyb3NvZnQgT3BlcmF0aW9ucyBQdWVydG8g
# UmljbzEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046RDlERS1FMzlBLTQzRkUxJTAj
# BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2Wggg5EMIIE9TCCA92g
# AwIBAgITMwAAAS0uTUHKY2UzoAAAAAABLTANBgkqhkiG9w0BAQsFADB8MQswCQYD
# VQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEe
# MBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3Nv
# ZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDAeFw0xOTEyMTkwMTE1MDRaFw0yMTAzMTcw
# MTE1MDRaMIHOMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSkw
# JwYDVQQLEyBNaWNyb3NvZnQgT3BlcmF0aW9ucyBQdWVydG8gUmljbzEmMCQGA1UE
# CxMdVGhhbGVzIFRTUyBFU046RDlERS1FMzlBLTQzRkUxJTAjBgNVBAMTHE1pY3Jv
# c29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAw
# ggEKAoIBAQCpYY30dUk6mEU0t7NIuIyHEjFfhuDdKGIMJUCvqZeA1TBZq9Yk2RxS
# 4907e1ehfjPwV8dIfYtLrtcgZ6gqjTpLiREU21ZzOLtyb0cc7EHwowX6g/wjcKDD
# XpKMpSAc8T+dEpI6daT7J0ASh2gj/LYLr2Fc6E0OeKtlaWBD//jmE0HhD6lhYvFo
# IL7HJLnq3FBpIWFjPA1f+CVOzf62w67WpmG3vC7ZFYk0GG4oFEggKK/Q4bQGb6vA
# NAO91xR9nX9sA5S7QJygnLFb10pmd+WwKp3jeLhEFcvDUHUXhiNbSOlMaAu154xr
# yuDHA3SoWrzSewwJ0j+fhvw05HVg/pTfAgMBAAGjggEbMIIBFzAdBgNVHQ4EFgQU
# 2WxkfEIBIfhODor/L0O+NPKdhs0wHwYDVR0jBBgwFoAU1WM6XIoxkPNDe3xGG8Uz
# aFqFbVUwVgYDVR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29t
# L3BraS9jcmwvcHJvZHVjdHMvTWljVGltU3RhUENBXzIwMTAtMDctMDEuY3JsMFoG
# CCsGAQUFBwEBBE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQu
# Y29tL3BraS9jZXJ0cy9NaWNUaW1TdGFQQ0FfMjAxMC0wNy0wMS5jcnQwDAYDVR0T
# AQH/BAIwADATBgNVHSUEDDAKBggrBgEFBQcDCDANBgkqhkiG9w0BAQsFAAOCAQEA
# fWsDZPdOq3nAbqDuxM6uwfFBrvRKBV4i/GnNG27GQwWPc/96fGrVWUQsRzeN2t1h
# mwjGaCmMUgR+lApHA3MFyHzShG1TM5xdZo6UBBm6oUfqzzah12aLUlfr5/OQMZnn
# wDN23C7fljQaRLjmWeJD2VXBbTKOGMkI8aDUT4sJqfgdB5IULP5f1SINFyWOpORU
# ShPyRRHFWONuejXalGft46Lt2+DgJLRNrq6+FelUcNX33zpcWW/DMrxOZqr01STk
# rVcQrobqoNayHvJWtYGeYoriMlnn7TjXzMNJ0mXIRi4oA3iJ8Ol38MIBZwuUJfD2
# 39ozsQlJbgGG88pCPjJwYjCCBnEwggRZoAMCAQICCmEJgSoAAAAAAAIwDQYJKoZI
# hvcNAQELBQAwgYgxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# MjAwBgNVBAMTKU1pY3Jvc29mdCBSb290IENlcnRpZmljYXRlIEF1dGhvcml0eSAy
# MDEwMB4XDTEwMDcwMTIxMzY1NVoXDTI1MDcwMTIxNDY1NVowfDELMAkGA1UEBhMC
# VVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNV
# BAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRp
# bWUtU3RhbXAgUENBIDIwMTAwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIB
# AQCpHQ28dxGKOiDs/BOX9fp/aZRrdFQQ1aUKAIKF++18aEssX8XD5WHCdrc+Zitb
# 8BVTJwQxH0EbGpUdzgkTjnxhMFmxMEQP8WCIhFRDDNdNuDgIs0Ldk6zWczBXJoKj
# RQ3Q6vVHgc2/JGAyWGBG8lhHhjKEHnRhZ5FfgVSxz5NMksHEpl3RYRNuKMYa+YaA
# u99h/EbBJx0kZxJyGiGKr0tkiVBisV39dx898Fd1rL2KQk1AUdEPnAY+Z3/1ZsAD
# lkR+79BL/W7lmsqxqPJ6Kgox8NpOBpG2iAg16HgcsOmZzTznL0S6p/TcZL2kAcEg
# CZN4zfy8wMlEXV4WnAEFTyJNAgMBAAGjggHmMIIB4jAQBgkrBgEEAYI3FQEEAwIB
# ADAdBgNVHQ4EFgQU1WM6XIoxkPNDe3xGG8UzaFqFbVUwGQYJKwYBBAGCNxQCBAwe
# CgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMBAf8wHwYDVR0j
# BBgwFoAU1fZWy4/oolxiaNE9lJBb186aGMQwVgYDVR0fBE8wTTBLoEmgR4ZFaHR0
# cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWljUm9vQ2Vy
# QXV0XzIwMTAtMDYtMjMuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggrBgEFBQcwAoY+
# aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNSb29DZXJBdXRf
# MjAxMC0wNi0yMy5jcnQwgaAGA1UdIAEB/wSBlTCBkjCBjwYJKwYBBAGCNy4DMIGB
# MD0GCCsGAQUFBwIBFjFodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vUEtJL2RvY3Mv
# Q1BTL2RlZmF1bHQuaHRtMEAGCCsGAQUFBwICMDQeMiAdAEwAZQBnAGEAbABfAFAA
# bwBsAGkAYwB5AF8AUwB0AGEAdABlAG0AZQBuAHQALiAdMA0GCSqGSIb3DQEBCwUA
# A4ICAQAH5ohRDeLG4Jg/gXEDPZ2joSFvs+umzPUxvs8F4qn++ldtGTCzwsVmyWrf
# 9efweL3HqJ4l4/m87WtUVwgrUYJEEvu5U4zM9GASinbMQEBBm9xcF/9c+V4XNZgk
# Vkt070IQyK+/f8Z/8jd9Wj8c8pl5SpFSAK84Dxf1L3mBZdmptWvkx872ynoAb0sw
# RCQiPM/tA6WWj1kpvLb9BOFwnzJKJ/1Vry/+tuWOM7tiX5rbV0Dp8c6ZZpCM/2pi
# f93FSguRJuI57BlKcWOdeyFtw5yjojz6f32WapB4pm3S4Zz5Hfw42JT0xqUKloak
# vZ4argRCg7i1gJsiOCC1JeVk7Pf0v35jWSUPei45V3aicaoGig+JFrphpxHLmtgO
# R5qAxdDNp9DvfYPw4TtxCd9ddJgiCGHasFAeb73x4QDf5zEHpJM692VHeOj4qEir
# 995yfmFrb3epgcunCaw5u+zGy9iCtHLNHfS4hQEegPsbiSpUObJb2sgNVZl6h3M7
# COaYLeqN4DMuEin1wC9UJyH3yKxO2ii4sanblrKnQqLJzxlBTeCG+SqaoxFmMNO7
# dDJL32N79ZmKLxvHIa9Zta7cRDyXUHHXodLFVeNp3lfB0d4wwP3M5k37Db9dT+md
# Hhk4L7zPWAUu7w2gUDXa7wknHNWzfjUeCLraNtvTX4/edIhJEqGCAtIwggI7AgEB
# MIH8oYHUpIHRMIHOMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSkwJwYDVQQLEyBNaWNyb3NvZnQgT3BlcmF0aW9ucyBQdWVydG8gUmljbzEmMCQG
# A1UECxMdVGhhbGVzIFRTUyBFU046RDlERS1FMzlBLTQzRkUxJTAjBgNVBAMTHE1p
# Y3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVAJ/OX8d+
# h3uxdL4JslJc9sPNpdCxoIGDMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENB
# IDIwMTAwDQYJKoZIhvcNAQEFBQACBQDjc5ZcMCIYDzIwMjAxMjAzMjA1MjQ0WhgP
# MjAyMDEyMDQyMDUyNDRaMHcwPQYKKwYBBAGEWQoEATEvMC0wCgIFAONzllwCAQAw
# CgIBAAICHugCAf8wBwIBAAICEWwwCgIFAON059wCAQAwNgYKKwYBBAGEWQoEAjEo
# MCYwDAYKKwYBBAGEWQoDAqAKMAgCAQACAwehIKEKMAgCAQACAwGGoDANBgkqhkiG
# 9w0BAQUFAAOBgQDDL6oxE9inyZTTQs+OMQ6CDAc+wPkgZeXHjQ0hOxIYW6Kk0KI6
# FQ5/OyMJ4/U0OYyMOS5TLGYAimlsZQRbOjzEg8t6HbBy8UR0KNLX1No4q9xZ4EVz
# RoZRxUE9mJLjllaI+306fF2AWQsknQdEx2Jo5LkNQeo1RCthr1+BJHGdgDGCAw0w
# ggMJAgEBMIGTMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# JjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABLS5N
# QcpjZTOgAAAAAAEtMA0GCWCGSAFlAwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYL
# KoZIhvcNAQkQAQQwLwYJKoZIhvcNAQkEMSIEIESfl8n6Q/WywLCmaS0cfbo3Ayse
# agR7pxRCCfNqMEswMIH6BgsqhkiG9w0BCRACLzGB6jCB5zCB5DCBvQQgjvFacnJ9
# IAeP4pUQDzw20vgpm6o/7gtNdZqHOaHhg8EwgZgwgYCkfjB8MQswCQYDVQQGEwJV
# UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UE
# ChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGlt
# ZS1TdGFtcCBQQ0EgMjAxMAITMwAAAS0uTUHKY2UzoAAAAAABLTAiBCC/p/y8QXGL
# fk2Z74jslQNxU35IonEQ4/5RxyUzIf6IbDANBgkqhkiG9w0BAQsFAASCAQAWRH/e
# uq8rwjXBXAXkDw/I5sC7gsxLNR3vL7nod7Dbzl+5koZKzEvMM8Ax+uAQNrn62SWW
# v1bmIvJJauifyVtwCFXaGZp+S1Ol15N24k9QCPtVrAVY4Nxv+A5GGRDOxDpWqetG
# I38DwPN5iRJxSWFASr+k8cu7pOTyb3LaMATdrda0keIYqapZpmmA2Odxh6JW1w78
# txcmwPWq3tJYKnoQXJuNtD1WAOlA6BySiPPksEecmXvVO3xMJJGb2uZYvyciWVSs
# 8UdCsyac+51Shc3RpGn+7KSd0wFp+b79wCAuNmRvwun4PPgD2qD+FOhkGQKBppCG
# LJCIhGEnDDsATUZJ
# SIG # End signature block
