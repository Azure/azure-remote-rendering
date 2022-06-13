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
# Starts a remote asset conversion by using the ARR conversion REST API <endPoint>/accounts/<accountId>/conversions/<conversionId>
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
    [switch] $useContainerSas,
    $conversionId,
    $additionalParameters) {
    try {
        WriteLine
        $inputContainerUri = "https://$($assetConversionSettings.storageAccountName).blob.core.windows.net/$($assetConversionSettings.blobInputContainerName)"
        $outputContainerUri = "https://$($assetConversionSettings.storageAccountName).blob.core.windows.net/$($assetConversionSettings.blobOutputContainerName)"
        $settings =
        @{
            inputLocation  =
            @{
                storageContainerUri    = $inputContainerUri;
                blobPrefix             = $assetConversionSettings.inputFolderPath;
                relativeInputAssetPath = $assetConversionSettings.inputAssetPath;
            };
            outputLocation =
            @{
                storageContainerUri    = $outputContainerUri;
                blobPrefix             = $assetConversionSettings.outputFolderPath;
                outputAssetFilename    = $assetConversionSettings.outputAssetFileName;
            }
        }

        if ($useContainerSas)
        {
            $settings.inputLocation  += @{ storageContainerReadListSas = $assetConversionSettings.inputContainerSAS }
            $settings.outputLocation += @{ storageContainerWriteSas    = $assetConversionSettings.outputContainerSAS }
        }

        if ($additionalParameters) {
            $additionalParameters.Keys | % { $settings += @{ $_ = $additionalParameters.Item($_) } }
        }

        $body =
        @{
            settings = $settings
        }

        if ([string]::IsNullOrEmpty($conversionId))
        {
            $conversionId = "Sample-Conversion-$(New-Guid)"
        }

        $url = "$serviceEndpoint/accounts/$accountId/conversions/${conversionId}?api-version=2021-01-01-preview"

        $conversionType = if ($useContainerSas) {"container Shared Access Signatures"} else {"linked storage account"}

        WriteInformation("Converting Asset using $conversionType ...")
        WriteInformation("  authentication endpoint: $authenticationEndpoint")
        WriteInformation("  service endpoint: $serviceEndpoint")
        WriteInformation("  accountId: $accountId")
        WriteInformation("Input:")
        WriteInformation("    storageContainerUri: $inputContainerUri")
        if ($useContainerSas) { WriteInformation("    inputContainerSAS: $($assetConversionSettings.inputContainerSAS)") }
        WriteInformation("    blobPrefix: $($assetConversionSettings.inputFolderPath)")
        WriteInformation("    relativeInputAssetPath: $($assetConversionSettings.inputAssetPath)")
        WriteInformation("Output:")
        WriteInformation("    storageContainerUri: $outputContainerUri")
        if ($useContainerSas) { WriteInformation("    outputContainerSAS: $($assetConversionSettings.outputContainerSAS)") }
        WriteInformation("    blobPrefix: $($assetConversionSettings.outputFolderPath)")
        WriteInformation("    outputAssetFilename: $($assetConversionSettings.outputAssetFileName)")

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method PUT -ContentType "application/json" -Body ($body | ConvertTo-Json) -Headers @{ Authorization = "Bearer $token" }
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

# calls the conversion status ARR REST API "<endPoint>/accounts/<accountId>/conversions/<conversionId>"
# returns the conversion process state
function GetConversionStatus($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $conversionId) {
    try {
        $url = "$serviceEndpoint/accounts/$accountId/conversions/${conversionId}?api-version=2021-01-01-preview"

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

    $conversionResponse = $null
    $conversionSucceeded = $true;

    while ($true) {
        Start-Sleep -Seconds 10
        WriteProgress  -activity "Ongoing asset conversion with conversion id: '$conversionId'" -status "Since $([int]((Get-Date) - $startTime).TotalSeconds) seconds"

        $response = GetConversionStatus $authenticationEndpoint $serviceEndpoint $accountId $accountKey $conversionId
        $responseJson = ($response.Content | ConvertFrom-Json)

        $conversionStatus = $responseJson.status

        if ("succeeded" -ieq $conversionStatus) {
            $conversionResponse = $responseJson
            break
        }

        if ($conversionStatus -iin "failed", "cancelled") {
            $conversionSucceeded = $false
            break
        }
    }

    $totalTimeElapsed = $(New-TimeSpan $startTime $(get-date)).TotalSeconds

    if ($conversionSucceeded) {
        WriteProgress -activity "Your asset conversion is complete" -status "Completed..."
        WriteSuccess("The conversion with Id: $conversionId was successful ...")
        WriteInformation ("Total time elapsed: $totalTimeElapsed  ...")
        WriteInformation($response)
    }
    else {
        WriteError("The asset conversion with Id: $conversionId resulted in an error")
        WriteInformation ("Total time elapsed: $totalTimeElapsed  ...")
        WriteInformation($response)
        exit 1
    }

    return $conversionResponse
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

        $Id = ConvertAsset -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -assetConversionSettings $config.assetConversionSettings -useContainerSas -conversionId $Id -AdditionalParameters $AdditionalParameters 
    }
    else {
        # The ARR account has read/write access to the blob containers of the storage account - so we do not need to generate SAS tokens for access
        $Id = ConvertAsset -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -assetConversionSettings $config.assetConversionSettings  -conversionId $Id -AdditionalParameters $AdditionalParameters
    }
}

$conversionResponse = $null
if ($GetConversionStatus) {
    if ([string]::IsNullOrEmpty($Id)) {
        $Id = Read-Host "Please enter the conversion Id"
    }

    if ($Poll) {
        $conversionResponse = PollConversionStatus -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -conversionId $Id
    }
    else {
        $response = GetConversionStatus -serviceEndpoint $config.accountSettings.serviceEndpoint -authenticationEndpoint $config.accountSettings.authenticationEndpoint -accountId  $config.accountSettings.arrAccountId  -accountKey $config.accountSettings.arrAccountKey -conversionId $Id
        $responseJson = ($response.Content | ConvertFrom-Json)
        $conversionStatus = $responseJson.status

        if ("succeeded" -ieq $conversionStatus) {
            $conversionResponse = $responseJson
        }
    }
}

if ($null -ne $conversionResponse) {
    WriteSuccess("Successfully converted asset.")
    WriteSuccess("Converted asset uri: $($conversionResponse.output.outputAssetUri)")

    if ($UseContainerSas) {
        $blobPath = "$($conversionResponse.settings.outputLocation.blobPrefix)$($conversionResponse.settings.outputLocation.outputAssetFilename)"
        # now retrieve the converted model SAS URI - you will need to call the ARR SDK API with a URL to your model to load a model in your application
        $sasUrl = GenerateOutputmodelSASUrl $config.assetConversionSettings.blobOutputContainerName $blobPath $config.assetConversionSettings.storageContext
        WriteInformation("model SAS URI: $sasUrl")
    }
}

# SIG # Begin signature block
# MIIrWQYJKoZIhvcNAQcCoIIrSjCCK0YCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCC1ZyZYo1o4XWd0
# fxOQhu8EE5FfF4oC8GXpp6d0/4BHTKCCEW4wggh+MIIHZqADAgECAhM2AAABfS9g
# xXjWqxyUAAIAAAF9MA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMTA5MDkwMTI2MjZaFw0yMjA5MDkwMTI2MjZaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQCxDjrIsLrnzISHuU969Ihnf0M1l83iB5bMfwoeA4Q3P/vyN6iV0SC1VIh1
# 3ISUWBlwl0mQgQbUMpXse9/ESIO6gHoa4sR8bMP814jzYc4WvbqnZBub4+964sQb
# Bxh+on59uF10CrmdGMHIFBDr/y2TgelGaMDAIUVWXB2tQARag8gdEfzLCXsObPQq
# ejRJF1E+c89RkkKBJF6sRXlsbHOhmaDo9KKipP+6tyugZcARtPV5WzBIuDtkBZ2m
# PXNXDpM4l6ENITDZiWCbNIFNbROqqF0ESJlaGNDGxs/vvD4Y9zo9Pgf5g7GOHHMI
# XT/OiGZLMlKWgWCXCRCvxvKhtI4HAgMBAAGjggWKMIIFhjApBgkrBgEEAYI3FQoE
# HDAaMAwGCisGAQQBgjdbAQEwCgYIKwYBBQUHAwMwPQYJKwYBBAGCNxUHBDAwLgYm
# KwYBBAGCNxUIhpDjDYTVtHiE8Ys+hZvdFs6dEoFgg93NZoaUjDICAWQCAQwwggJ2
# BggrBgEFBQcBAQSCAmgwggJkMGIGCCsGAQUFBzAChlZodHRwOi8vY3JsLm1pY3Jv
# c29mdC5jb20vcGtpaW5mcmEvQ2VydHMvQlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1F
# JTIwQ1MlMjBDQSUyMDAxKDIpLmNydDBSBggrBgEFBQcwAoZGaHR0cDovL2NybDEu
# YW1lLmdibC9haWEvQlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1FJTIwQ1MlMjBDQSUy
# MDAxKDIpLmNydDBSBggrBgEFBQcwAoZGaHR0cDovL2NybDIuYW1lLmdibC9haWEv
# QlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1FJTIwQ1MlMjBDQSUyMDAxKDIpLmNydDBS
# BggrBgEFBQcwAoZGaHR0cDovL2NybDMuYW1lLmdibC9haWEvQlkyUEtJQ1NDQTAx
# LkFNRS5HQkxfQU1FJTIwQ1MlMjBDQSUyMDAxKDIpLmNydDBSBggrBgEFBQcwAoZG
# aHR0cDovL2NybDQuYW1lLmdibC9haWEvQlkyUEtJQ1NDQTAxLkFNRS5HQkxfQU1F
# JTIwQ1MlMjBDQSUyMDAxKDIpLmNydDCBrQYIKwYBBQUHMAKGgaBsZGFwOi8vL0NO
# PUFNRSUyMENTJTIwQ0ElMjAwMSxDTj1BSUEsQ049UHVibGljJTIwS2V5JTIwU2Vy
# dmljZXMsQ049U2VydmljZXMsQ049Q29uZmlndXJhdGlvbixEQz1BTUUsREM9R0JM
# P2NBQ2VydGlmaWNhdGU/YmFzZT9vYmplY3RDbGFzcz1jZXJ0aWZpY2F0aW9uQXV0
# aG9yaXR5MB0GA1UdDgQWBBTNPf032JeslerhQm8AuxJuuT0UNTAOBgNVHQ8BAf8E
# BAMCB4AwRQYDVR0RBD4wPKQ6MDgxHjAcBgNVBAsTFU1pY3Jvc29mdCBDb3Jwb3Jh
# dGlvbjEWMBQGA1UEBRMNMjM2MTY3KzQ2Nzk3MzCCAeYGA1UdHwSCAd0wggHZMIIB
# 1aCCAdGgggHNhj9odHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtpaW5mcmEvQ1JM
# L0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmwxLmFtZS5nYmwv
# Y3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmwyLmFtZS5n
# YmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmwzLmFt
# ZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmw0
# LmFtZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGgb1sZGFwOi8v
# L0NOPUFNRSUyMENTJTIwQ0ElMjAwMSgyKSxDTj1CWTJQS0lDU0NBMDEsQ049Q0RQ
# LENOPVB1YmxpYyUyMEtleSUyMFNlcnZpY2VzLENOPVNlcnZpY2VzLENOPUNvbmZp
# Z3VyYXRpb24sREM9QU1FLERDPUdCTD9jZXJ0aWZpY2F0ZVJldm9jYXRpb25MaXN0
# P2Jhc2U/b2JqZWN0Q2xhc3M9Y1JMRGlzdHJpYnV0aW9uUG9pbnQwHwYDVR0jBBgw
# FoAUllGE4Gtve/7YBqvD8oXmKa5q+dQwHwYDVR0lBBgwFgYKKwYBBAGCN1sBAQYI
# KwYBBQUHAwMwDQYJKoZIhvcNAQELBQADggEBALJt0CX0joH8RLWbu0fscVYHOgeY
# 5PCnQXYmhX2i0noyi+UC0jxrwziZGOOLVjfJqQODln/sJf16Sb6ay2BwT1OPDh6d
# Na/jCageEEbUgt8xZZ0EbW6lNs7pwUjS346yqvdwU5xoa/zz2p6JtM93YDtET5vn
# PRATUa1sw8iN3akopHGqzSw3AmZ5Pdmty4i/ptEgOysBP0PWvOaRWWRm3izyAa5N
# z491JdMjJy3mFYh15fKjFbsMM9QC6fk8X8n+OOaH2w7WO+g1pKxuu9ckQBPN9CGr
# 4Wl6rsBCg9c0Wk12fXQ3LXOn/ve0VM+ogz8JLnA+vFIG94GLLOLi+XC+8iAwggjo
# MIIG0KADAgECAhMfAAAAUeqP9pxzDKg7AAAAAABRMA0GCSqGSIb3DQEBCwUAMDwx
# EzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxEDAOBgNV
# BAMTB2FtZXJvb3QwHhcNMjEwNTIxMTg0NDE0WhcNMjYwNTIxMTg1NDE0WjBBMRMw
# EQYKCZImiZPyLGQBGRYDR0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQD
# EwxBTUUgQ1MgQ0EgMDEwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDJ
# mlIJfQGejVbXKpcyFPoFSUllalrinfEV6JMc7i+bZDoL9rNHnHDGfJgeuRIYO1LY
# /1f4oMTrhXbSaYRCS5vGc8145WcTZG908bGDCWr4GFLc411WxA+Pv2rteAcz0eHM
# H36qTQ8L0o3XOb2n+x7KJFLokXV1s6pF/WlSXsUBXGaCIIWBXyEchv+sM9eKDsUO
# LdLTITHYJQNWkiryMSEbxqdQUTVZjEz6eLRLkofDAo8pXirIYOgM770CYOiZrcKH
# K7lYOVblx22pdNawY8Te6a2dfoCaWV1QUuazg5VHiC4p/6fksgEILptOKhx9c+ia
# piNhMrHsAYx9pUtppeaFAgMBAAGjggTcMIIE2DASBgkrBgEEAYI3FQEEBQIDAgAC
# MCMGCSsGAQQBgjcVAgQWBBQSaCRCIUfL1Gu+Mc8gpMALI38/RzAdBgNVHQ4EFgQU
# llGE4Gtve/7YBqvD8oXmKa5q+dQwggEEBgNVHSUEgfwwgfkGBysGAQUCAwUGCCsG
# AQUFBwMBBggrBgEFBQcDAgYKKwYBBAGCNxQCAQYJKwYBBAGCNxUGBgorBgEEAYI3
# CgMMBgkrBgEEAYI3FQYGCCsGAQUFBwMJBggrBgEFBQgCAgYKKwYBBAGCN0ABAQYL
# KwYBBAGCNwoDBAEGCisGAQQBgjcKAwQGCSsGAQQBgjcVBQYKKwYBBAGCNxQCAgYK
# KwYBBAGCNxQCAwYIKwYBBQUHAwMGCisGAQQBgjdbAQEGCisGAQQBgjdbAgEGCisG
# AQQBgjdbAwEGCisGAQQBgjdbBQEGCisGAQQBgjdbBAEGCisGAQQBgjdbBAIwGQYJ
# KwYBBAGCNxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMBIGA1UdEwEB/wQI
# MAYBAf8CAQAwHwYDVR0jBBgwFoAUKV5RXmSuNLnrrJwNp4x1AdEJCygwggFoBgNV
# HR8EggFfMIIBWzCCAVegggFToIIBT4YxaHR0cDovL2NybC5taWNyb3NvZnQuY29t
# L3BraWluZnJhL2NybC9hbWVyb290LmNybIYjaHR0cDovL2NybDIuYW1lLmdibC9j
# cmwvYW1lcm9vdC5jcmyGI2h0dHA6Ly9jcmwzLmFtZS5nYmwvY3JsL2FtZXJvb3Qu
# Y3JshiNodHRwOi8vY3JsMS5hbWUuZ2JsL2NybC9hbWVyb290LmNybIaBqmxkYXA6
# Ly8vQ049YW1lcm9vdCxDTj1BTUVSb290LENOPUNEUCxDTj1QdWJsaWMlMjBLZXkl
# MjBTZXJ2aWNlcyxDTj1TZXJ2aWNlcyxDTj1Db25maWd1cmF0aW9uLERDPUFNRSxE
# Qz1HQkw/Y2VydGlmaWNhdGVSZXZvY2F0aW9uTGlzdD9iYXNlP29iamVjdENsYXNz
# PWNSTERpc3RyaWJ1dGlvblBvaW50MIIBqwYIKwYBBQUHAQEEggGdMIIBmTBHBggr
# BgEFBQcwAoY7aHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraWluZnJhL2NlcnRz
# L0FNRVJvb3RfYW1lcm9vdC5jcnQwNwYIKwYBBQUHMAKGK2h0dHA6Ly9jcmwyLmFt
# ZS5nYmwvYWlhL0FNRVJvb3RfYW1lcm9vdC5jcnQwNwYIKwYBBQUHMAKGK2h0dHA6
# Ly9jcmwzLmFtZS5nYmwvYWlhL0FNRVJvb3RfYW1lcm9vdC5jcnQwNwYIKwYBBQUH
# MAKGK2h0dHA6Ly9jcmwxLmFtZS5nYmwvYWlhL0FNRVJvb3RfYW1lcm9vdC5jcnQw
# gaIGCCsGAQUFBzAChoGVbGRhcDovLy9DTj1hbWVyb290LENOPUFJQSxDTj1QdWJs
# aWMlMjBLZXklMjBTZXJ2aWNlcyxDTj1TZXJ2aWNlcyxDTj1Db25maWd1cmF0aW9u
# LERDPUFNRSxEQz1HQkw/Y0FDZXJ0aWZpY2F0ZT9iYXNlP29iamVjdENsYXNzPWNl
# cnRpZmljYXRpb25BdXRob3JpdHkwDQYJKoZIhvcNAQELBQADggIBAFAQI7dPD+jf
# XtGt3vJp2pyzA/HUu8hjKaRpM3opya5G3ocprRd7vdTHb8BDfRN+AD0YEmeDB5HK
# QoG6xHPI5TXuIi5sm/LeADbV3C2q0HQOygS/VT+m1W7a/752hMIn+L4ZuyxVeSBp
# fwf7oQ4YSZPh6+ngZvBHgfBaVz4O9/wcfw91QDZnTgK9zAh9yRKKls2bziPEnxeO
# ZMVNaxyV0v152PY2xjqIafIkUjK6vY9LtVFjJXenVUAmn3WCPWNFC1YTIIHw/mD2
# cTfPy7QA1pT+GPARAKt0bKtq9aCd/Ym0b5tPbpgCiRtzyb7fbNS1dE740re0COE6
# 7YV2wbeo2sXixzvLftH8L7s9xv9wV+G22qyKt6lmKLjFK1yMw4Ni5fMabcgmzRvS
# jAcbqgp3tk4a8emaaH0rz8MuuIP+yrxtREPXSqL/C5bzMzsikuDW9xH10graZzSm
# PjilzpRfRdu20/9UQmC7eVPZ4j1WNa1oqPHfzET3ChIzJ6Q9G3NPCB+7KwX0OQmK
# yv7IDimj8U/GlsHD1z+EF/fYMf8YXG15LamaOAohsw/ywO6SYSreVW+5Y0mzJutn
# BC9Cm9ozj1+/4kqksrlhZgR/CSxhFH3BTweH8gP2FEISRtShDZbuYymynY1un+Ry
# fiK9+iVTLdD1h/SxyxDpZMtimb4CgJQlMYIZQTCCGT0CAQEwWDBBMRMwEQYKCZIm
# iZPyLGQBGRYDR0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxBTUUg
# Q1MgQ0EgMDECEzYAAAF9L2DFeNarHJQAAgAAAX0wDQYJYIZIAWUDBAIBBQCgga4w
# GQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisG
# AQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEIAppIO37nxYp5lg+XcaX6J/ou0m0aGP1
# qONSgCjrzvjkMEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8AcwBvAGYA
# dKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEA
# RVfSeuByJy10r0isF0iIQdtUpNGipbny6vSDtViSPL7AxTVuVPUR8FbngeIWLl+K
# GWL1O88jOvC2wFdxXkuUsgxWHx9E0L+lLweYg5xzh2OrU5gisoU+ZX8T/AgoaJAg
# OkS6jmJsdMGb/uuCSRtdGPPeSTYKpA++YTAnbo77R/Fw3XDqCDIeLVTUIHpetsAX
# o6X3sdS3BD5hu61/zIgLBAS2rFtJO92ybu2SKAOxvEjIS1tINzE58z2EL2iYWeQe
# UwXR6Ruf+50hA7l30/pD+OFjN5v6QyEVSNXcgwqDVuYBqXIzWs9jA9qBOPuk1j6b
# ff6uyHrnJG5SDSbd40tK6KGCFwkwghcFBgorBgEEAYI3AwMBMYIW9TCCFvEGCSqG
# SIb3DQEHAqCCFuIwghbeAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFVBgsqhkiG9w0B
# CRABBKCCAUQEggFAMIIBPAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFlAwQCAQUA
# BCC4enyo+IeVBPROK6Oufre66afZKBlyO4OwRXWU1Dt8sgIGYoTw3mmwGBMyMDIy
# MDYxMzA5MTcyOS41OThaMASAAgH0oIHUpIHRMIHOMQswCQYDVQQGEwJVUzETMBEG
# A1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWlj
# cm9zb2Z0IENvcnBvcmF0aW9uMSkwJwYDVQQLEyBNaWNyb3NvZnQgT3BlcmF0aW9u
# cyBQdWVydG8gUmljbzEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046Rjc3Ri1FMzU2
# LTVCQUUxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2WgghFc
# MIIHEDCCBPigAwIBAgITMwAAAaqlMZsLy7IIDgABAAABqjANBgkqhkiG9w0BAQsF
# ADB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQD
# Ex1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDAeFw0yMjAzMDIxODUxMjZa
# Fw0yMzA1MTExODUxMjZaMIHOMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGlu
# Z3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
# cmF0aW9uMSkwJwYDVQQLEyBNaWNyb3NvZnQgT3BlcmF0aW9ucyBQdWVydG8gUmlj
# bzEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046Rjc3Ri1FMzU2LTVCQUUxJTAjBgNV
# BAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggIiMA0GCSqGSIb3DQEB
# AQUAA4ICDwAwggIKAoICAQCgT+xyudW1h3/hQ0ofTu2Mq0LZDTL3R8x4ms7znSPT
# zho8iSGK7NVjjJkgqd6P5r7Lj5xUj+XNHQngblKuruid9DPNWWjTj/2m2a08GK2D
# fjeZ0razhnQrUQbpu+ocu069wGQ1AKy8L4bBpV4S5Q1NcIqGsTPgVcAjSOy5k2mC
# qo5ufIRILGLSiB5OfS8zpyOGnp2zywT/1WGIyOmuCiHLp9BGRKwLpLeTwv5ilGjq
# YVDBmJtD8X6WPQZBubD33MxciHwNdyy0UuLBoW1K3DOeBLxNhZVgUGiaO36yluwl
# YyEyxF+BNpccEBvzLmftcA2IPTjhK0+Yfus3nI+u3np8MXlKGjhGyrYlMWiVGJ8k
# CsQlk5DXVkV0ykpiMcdLW7D+Yq1o6l70+rf83iSsNOTWPIT0+er1ttKtA2CtjbXj
# ggw9FA+mTQBS1fOxjpJdHgal3E6BVXXicMDkxOmgOEamKDa9kFDwSFOiRIlBgbPX
# OKguZgR02OOlWkf6HWhQy3MUCODj5J+WpfyD7HfP62g5jHyopOusXDYdqjeMsrWD
# N7og3p1+anhXcd6XYuN6WABTf0tf91UTZPvxkVVFGFmAYw2UqsbJYnRPIbMQuyvK
# i35jaGkNmgLLtd4dX2kzEmSBFcaLM9W/ciHl5rTOjZa41d3rcEuyV2MBoRzHVWBC
# 9QIDAQABo4IBNjCCATIwHQYDVR0OBBYEFD+aFLxThy7YX3dFs94RrZ0FRqSeMB8G
# A1UdIwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8GA1UdHwRYMFYwVKBSoFCG
# Tmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29mdCUy
# MFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggrBgEFBQcBAQRgMF4w
# XAYIKwYBBQUHMAKGUGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2Vy
# dHMvTWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3J0MAwG
# A1UdEwEB/wQCMAAwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZIhvcNAQELBQAD
# ggIBAN8MgE2QRRAaIK3MB7OMyO6l9stI2ygiOmYnhgCEfekYjK42b1ht/WDwPxS9
# r4RkgrTu3mt4gZcIYU8iRD3sS7oE+IweFtK5XTiz+WxHNM8MbPTbUxUvFJds2ye4
# 8+VsUp4Uh7H2lRVKe0ugdmtW4ypliKP0r3d1tVd5nCGM4W6SyFFZT9wm0yRBPnAt
# 4V/iYIJ0mERE8qPpiOx8/yjFhWkVgVGCOINAa8IldpWKisnpIzaeq4+2/JejoW4F
# /yT9G8zcb+oqNGOIjZSM8/z3SIfxNqY96Vz4kCT0ZRJDJLEXnBPFZxcqoUeH2/xe
# nOcsGOPphKbISAINmFF7MBaqmyvRb/lPGGHJWD74Sv8EWbPv+WriuBTPkE48sI9A
# ua5q/DM4qplBoALsGUGMh0QqKZ1XZWjv8cUmQn2mUe8OwdzgRJfI/laKH7NSn6vQ
# JpkAFmTo7eA5zZOTZ8U4T740FbjlP8vh0xK8Kg/8CkQpdACd1D0yfDz2Kfo2xF5C
# pqBYVOCRnq+Xmo9tp19fabozWSqqmq7eMi4zVDpKlo1ZOCh6XWERnCTFV5CpEAIp
# Y1J/XB0cDbj8/07u2Jn4EV1jeB7wnE9ptUAA4pzmT7Dub+Y/2xMcNFpha1tgrQxA
# KZwpZogCnIRa9MUihORE/gMrmy2qXoxDa/b7e0Fzaumj9V1nMIIHcTCCBVmgAwIB
# AgITMwAAABXF52ueAptJmQAAAAAAFTANBgkqhkiG9w0BAQsFADCBiDELMAkGA1UE
# BhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAc
# BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9zb2Z0
# IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTAwHhcNMjEwOTMwMTgyMjI1
# WhcNMzAwOTMwMTgzMjI1WjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGlu
# Z3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
# cmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDCC
# AiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAOThpkzntHIhC3miy9ckeb0O
# 1YLT/e6cBwfSqWxOdcjKNVf2AX9sSuDivbk+F2Az/1xPx2b3lVNxWuJ+Slr+uDZn
# hUYjDLWNE893MsAQGOhgfWpSg0S3po5GawcU88V29YZQ3MFEyHFcUTE3oAo4bo3t
# 1w/YJlN8OWECesSq/XJprx2rrPY2vjUmZNqYO7oaezOtgFt+jBAcnVL+tuhiJdxq
# D89d9P6OU8/W7IVWTe/dvI2k45GPsjksUZzpcGkNyjYtcI4xyDUoveO0hyTD4MmP
# frVUj9z6BVWYbWg7mka97aSueik3rMvrg0XnRm7KMtXAhjBcTyziYrLNueKNiOSW
# rAFKu75xqRdbZ2De+JKRHh09/SDPc31BmkZ1zcRfNN0Sidb9pSB9fvzZnkXftnIv
# 231fgLrbqn427DZM9ituqBJR6L8FA6PRc6ZNN3SUHDSCD/AQ8rdHGO2n6Jl8P0zb
# r17C89XYcz1DTsEzOUyOArxCaC4Q6oRRRuLRvWoYWmEBc8pnol7XKHYC4jMYcten
# IPDC+hIK12NvDMk2ZItboKaDIV1fMHSRlJTYuVD5C4lh8zYGNRiER9vcG9H9stQc
# xWv2XFJRXRLbJbqvUAV6bMURHXLvjflSxIUXk8A8FdsaN8cIFRg/eKtFtvUeh17a
# j54WcmnGrnu3tz5q4i6tAgMBAAGjggHdMIIB2TASBgkrBgEEAYI3FQEEBQIDAQAB
# MCMGCSsGAQQBgjcVAgQWBBQqp1L+ZMSavoKRPEY1Kc8Q/y8E7jAdBgNVHQ4EFgQU
# n6cVXQBeYl2D9OXSZacbUzUZ6XIwXAYDVR0gBFUwUzBRBgwrBgEEAYI3TIN9AQEw
# QTA/BggrBgEFBQcCARYzaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9E
# b2NzL1JlcG9zaXRvcnkuaHRtMBMGA1UdJQQMMAoGCCsGAQUFBwMIMBkGCSsGAQQB
# gjcUAgQMHgoAUwB1AGIAQwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTADAQH/
# MB8GA1UdIwQYMBaAFNX2VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0wS6BJ
# oEeGRWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01p
# Y1Jvb0NlckF1dF8yMDEwLTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYB
# BQUHMAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9v
# Q2VyQXV0XzIwMTAtMDYtMjMuY3J0MA0GCSqGSIb3DQEBCwUAA4ICAQCdVX38Kq3h
# LB9nATEkW+Geckv8qW/qXBS2Pk5HZHixBpOXPTEztTnXwnE2P9pkbHzQdTltuw8x
# 5MKP+2zRoZQYIu7pZmc6U03dmLq2HnjYNi6cqYJWAAOwBb6J6Gngugnue99qb74p
# y27YP0h1AdkY3m2CDPVtI1TkeFN1JFe53Z/zjj3G82jfZfakVqr3lbYoVSfQJL1A
# oL8ZthISEV09J+BAljis9/kpicO8F7BUhUKz/AyeixmJ5/ALaoHCgRlCGVJ1ijbC
# HcNhcy4sa3tuPywJeBTpkbKpW99Jo3QMvOyRgNI95ko+ZjtPu4b6MhrZlvSP9pEB
# 9s7GdP32THJvEKt1MMU0sHrYUP4KWN1APMdUbZ1jdEgssU5HLcEUBHG/ZPkkvnNt
# yo4JvbMBV0lUZNlz138eW0QBjloZkWsNn6Qo3GcZKCS6OEuabvshVGtqRRFHqfG3
# rsjoiV5PndLQTHa1V1QJsWkBRH58oWFsc/4Ku+xBZj1p/cvBQUl+fpO+y/g75LcV
# v7TOPqUxUYS8vwLBgqJ7Fx0ViY1w/ue10CgaiQuPNtq6TPmb/wrpNPgkNWcr4A24
# 5oyZ1uEi6vAnQj0llOZ0dFtq0Z4+7X6gMTN9vMvpe784cETRkPHIqzqKOghif9lw
# Y1NNje6CbaUFEMFxBmoQtB1VM1izoXBm8qGCAs8wggI4AgEBMIH8oYHUpIHRMIHO
# MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVk
# bW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSkwJwYDVQQLEyBN
# aWNyb3NvZnQgT3BlcmF0aW9ucyBQdWVydG8gUmljbzEmMCQGA1UECxMdVGhhbGVz
# IFRTUyBFU046Rjc3Ri1FMzU2LTVCQUUxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1l
# LVN0YW1wIFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVAOBtJtCeHgJZY3D/47zr/f6Z
# v+vGoIGDMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24x
# EDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
# bjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwDQYJKoZI
# hvcNAQEFBQACBQDmUQy4MCIYDzIwMjIwNjEzMDUwOTEyWhgPMjAyMjA2MTQwNTA5
# MTJaMHQwOgYKKwYBBAGEWQoEATEsMCowCgIFAOZRDLgCAQAwBwIBAAICHpcwBwIB
# AAICEUowCgIFAOZSXjgCAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGEWQoD
# AqAKMAgCAQACAwehIKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQB3G+Qm
# Hj0CiBpZ4webb6p9H/icff15RqHRk2KY4LVW1o0/a6WrNfR/dwp9iEqOvvEER+rO
# cXB7gnVpke63Y+mlS/5IOHDci2rG4xt76Qa04yI9y6RHCNojrmIc1dBpe3oWvnx0
# ewNFgnCo0Fy3icJkJ4ZHespMGRQHXCkG0Z8/uTGCBA0wggQJAgEBMIGTMHwxCzAJ
# BgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25k
# MR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jv
# c29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABqqUxmwvLsggOAAEAAAGqMA0G
# CWCGSAFlAwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQwLwYJ
# KoZIhvcNAQkEMSIEIBdUtsaH8S+otzUnWePIeOx6iHSYlMSxYm8MbwATcPyTMIH6
# BgsqhkiG9w0BCRACLzGB6jCB5zCB5DCBvQQgVrUCQxxavBHgc9017oAqkYUiPyQm
# WwE2BCMExvGzHsAwgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2Fz
# aGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENv
# cnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAx
# MAITMwAAAaqlMZsLy7IIDgABAAABqjAiBCBt4jQeNlIRGWRoKwCtp8RtNoKw3VFB
# l9Z3zuUqela+wjANBgkqhkiG9w0BAQsFAASCAgBokGiObrQLb3RCudBBYVB2keo6
# xkp5XS/v8B+gVWuud7FnvgDAp+FWYJ6S4f2E0xyioiGSt3DsFUHkHjXx4TOc2hwu
# kv40q7J9tACTcQ1q8qvoOZX9rdDhg6pAajNnC0+K0Lwr2yNmP91d5p2L3N7sdpmI
# dsM39EipzQ03OqvFfAWeDmnbold4g3VfImEItJapFBIwTno6ryBH/mbMVGbia987
# skoYgfAUlwiOg0RjhT/ijAziDjTZbyFXaYEduW4ybQStOzcg3Rlu2TB+C0T7rIec
# UjGh30DSLdbPw8XiOA75vEwmDVM4e0Rse37lXqXklBJQEoEHIWwG9o2rYJBnDoct
# h4/erMA8rswWmUlULaRiVAjoe2DRF7pURuc8H5MtLbu6vcUW2n5w4cS7ouQrBn7K
# 7b4bGL3b5l700OqkYGa1T+5AxPOIZ4S9TNSrUA/JgUw/AMX4ak01uAef4Kh7S7MU
# eeFNVJgueZhntmI/+tdVcDkwikpgWRBxje7leyRmjFqqgLbvk5dVKHPMHxTrAs8J
# C1oGmeGKB6cRLJ3dqDx/kAS7kfPOUQgsG3DObjcpPUh3RwQ0l1C+pfyGvt10CdpK
# s5AW4Fm/p3+6slbOKWv8SwQ9XIGmU5Xu147edGDjtjSbM6nBN0fzDMkDBFfulVxB
# uOzWEcucShgZH/HxoA==
# SIG # End signature block
