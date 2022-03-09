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
# MIIrWwYJKoZIhvcNAQcCoIIrTDCCK0gCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCC1ZyZYo1o4XWd0
# fxOQhu8EE5FfF4oC8GXpp6d0/4BHTKCCEXkwggiJMIIHcaADAgECAhM2AAABfv9v
# /QSkJVgSAAIAAAF+MA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMTA5MDkwMTI2MjZaFw0yMjA5MDkwMTI2MjZaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQCQh1zMc6GVq9fygCskp/O9g6jS0ilJ3idmz+2JkE+9AarM0AiJ1/CDQETS
# X56JOh9Vm8kdffjdqJfD2NoSV2lO1eKAFKETKyiJKvbcW38H7JhH1h+yCBjajiWy
# wcAZ/ipRX3sMYM5nXl5+GxEZpGQbLIsrLj24Zi9dj2kdHc0DxqbemzlCySiB+n9r
# HFdi9zEn6XzuTf/3i6XM36lUPZ+xt6Zckupu0CAnu4dZr1XiwHvbJvqq3RcXOU5j
# p1m/AKk4Ov+9jaEKOnYiHJbnpC+vKx/Zv8aZajhPyVY3fXb/tygGOyb607EYn7F2
# v4AcJL5ocPTT3BGWtve1KuOwRRs3AgMBAAGjggWVMIIFkTApBgkrBgEEAYI3FQoE
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
# aG9yaXR5MB0GA1UdDgQWBBRufMhNVeWweAyGzdFbxkxa8y1WjDAOBgNVHQ8BAf8E
# BAMCB4AwUAYDVR0RBEkwR6RFMEMxKTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRp
# b25zIFB1ZXJ0byBSaWNvMRYwFAYDVQQFEw0yMzYxNjcrNDY3OTc0MIIB5gYDVR0f
# BIIB3TCCAdkwggHVoIIB0aCCAc2GP2h0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9w
# a2lpbmZyYS9DUkwvQU1FJTIwQ1MlMjBDQSUyMDAxKDIpLmNybIYxaHR0cDovL2Ny
# bDEuYW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxKDIpLmNybIYxaHR0cDov
# L2NybDIuYW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxKDIpLmNybIYxaHR0
# cDovL2NybDMuYW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxKDIpLmNybIYx
# aHR0cDovL2NybDQuYW1lLmdibC9jcmwvQU1FJTIwQ1MlMjBDQSUyMDAxKDIpLmNy
# bIaBvWxkYXA6Ly8vQ049QU1FJTIwQ1MlMjBDQSUyMDAxKDIpLENOPUJZMlBLSUNT
# Q0EwMSxDTj1DRFAsQ049UHVibGljJTIwS2V5JTIwU2VydmljZXMsQ049U2Vydmlj
# ZXMsQ049Q29uZmlndXJhdGlvbixEQz1BTUUsREM9R0JMP2NlcnRpZmljYXRlUmV2
# b2NhdGlvbkxpc3Q/YmFzZT9vYmplY3RDbGFzcz1jUkxEaXN0cmlidXRpb25Qb2lu
# dDAfBgNVHSMEGDAWgBSWUYTga297/tgGq8PyheYprmr51DAfBgNVHSUEGDAWBgor
# BgEEAYI3WwEBBggrBgEFBQcDAzANBgkqhkiG9w0BAQsFAAOCAQEAU1RmrZsQtaYx
# 8dBu9zC6w4TXEtumd3O0ArP7W0Co7nNFCDTv8pxqOM2bz/pH49DXdnzcXCTjUjci
# o03V+QPO3Ql8xOMqm8bE9Kcof+fPk4DyDY5y+YzxQyk49URn4ea3WhihAJkg/xnF
# LiKnbWW8iyqxie+B44u9dPfbsWrxcgedzSnH0aXwfIt29IKCpGHL74rBDbKHXdL0
# pEjf9c2YA6OiS1IH7X/suBjEFa4LEYPTSFK2AJXpgM7q9dmSvta4CyudRoYf1BXP
# KR+CzNT9XL5ZJX8LUuC5LrZgbt7LzjlW+1Umo2OsmUO3YA7/s5vH6Tqc6uZ9isIw
# sit0XfouHTCCCOgwggbQoAMCAQICEx8AAABR6o/2nHMMqDsAAAAAAFEwDQYJKoZI
# hvcNAQELBQAwPDETMBEGCgmSJomT8ixkARkWA0dCTDETMBEGCgmSJomT8ixkARkW
# A0FNRTEQMA4GA1UEAxMHYW1lcm9vdDAeFw0yMTA1MjExODQ0MTRaFw0yNjA1MjEx
# ODU0MTRaMEExEzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNB
# TUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTCCASIwDQYJKoZIhvcNAQEBBQADggEP
# ADCCAQoCggEBAMmaUgl9AZ6NVtcqlzIU+gVJSWVqWuKd8RXokxzuL5tkOgv2s0ec
# cMZ8mB65Ehg7Utj/V/igxOuFdtJphEJLm8ZzzXjlZxNkb3TxsYMJavgYUtzjXVbE
# D4+/au14BzPR4cwffqpNDwvSjdc5vaf7HsokUuiRdXWzqkX9aVJexQFcZoIghYFf
# IRyG/6wz14oOxQ4t0tMhMdglA1aSKvIxIRvGp1BRNVmMTPp4tEuSh8MCjyleKshg
# 6AzvvQJg6JmtwocruVg5VuXHbal01rBjxN7prZ1+gJpZXVBS5rODlUeILin/p+Sy
# AQgum04qHH1z6JqmI2EysewBjH2lS2ml5oUCAwEAAaOCBNwwggTYMBIGCSsGAQQB
# gjcVAQQFAgMCAAIwIwYJKwYBBAGCNxUCBBYEFBJoJEIhR8vUa74xzyCkwAsjfz9H
# MB0GA1UdDgQWBBSWUYTga297/tgGq8PyheYprmr51DCCAQQGA1UdJQSB/DCB+QYH
# KwYBBQIDBQYIKwYBBQUHAwEGCCsGAQUFBwMCBgorBgEEAYI3FAIBBgkrBgEEAYI3
# FQYGCisGAQQBgjcKAwwGCSsGAQQBgjcVBgYIKwYBBQUHAwkGCCsGAQUFCAICBgor
# BgEEAYI3QAEBBgsrBgEEAYI3CgMEAQYKKwYBBAGCNwoDBAYJKwYBBAGCNxUFBgor
# BgEEAYI3FAICBgorBgEEAYI3FAIDBggrBgEFBQcDAwYKKwYBBAGCN1sBAQYKKwYB
# BAGCN1sCAQYKKwYBBAGCN1sDAQYKKwYBBAGCN1sFAQYKKwYBBAGCN1sEAQYKKwYB
# BAGCN1sEAjAZBgkrBgEEAYI3FAIEDB4KAFMAdQBiAEMAQTALBgNVHQ8EBAMCAYYw
# EgYDVR0TAQH/BAgwBgEB/wIBADAfBgNVHSMEGDAWgBQpXlFeZK40ueusnA2njHUB
# 0QkLKDCCAWgGA1UdHwSCAV8wggFbMIIBV6CCAVOgggFPhjFodHRwOi8vY3JsLm1p
# Y3Jvc29mdC5jb20vcGtpaW5mcmEvY3JsL2FtZXJvb3QuY3JshiNodHRwOi8vY3Js
# Mi5hbWUuZ2JsL2NybC9hbWVyb290LmNybIYjaHR0cDovL2NybDMuYW1lLmdibC9j
# cmwvYW1lcm9vdC5jcmyGI2h0dHA6Ly9jcmwxLmFtZS5nYmwvY3JsL2FtZXJvb3Qu
# Y3JshoGqbGRhcDovLy9DTj1hbWVyb290LENOPUFNRVJvb3QsQ049Q0RQLENOPVB1
# YmxpYyUyMEtleSUyMFNlcnZpY2VzLENOPVNlcnZpY2VzLENOPUNvbmZpZ3VyYXRp
# b24sREM9QU1FLERDPUdCTD9jZXJ0aWZpY2F0ZVJldm9jYXRpb25MaXN0P2Jhc2U/
# b2JqZWN0Q2xhc3M9Y1JMRGlzdHJpYnV0aW9uUG9pbnQwggGrBggrBgEFBQcBAQSC
# AZ0wggGZMEcGCCsGAQUFBzAChjtodHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtp
# aW5mcmEvY2VydHMvQU1FUm9vdF9hbWVyb290LmNydDA3BggrBgEFBQcwAoYraHR0
# cDovL2NybDIuYW1lLmdibC9haWEvQU1FUm9vdF9hbWVyb290LmNydDA3BggrBgEF
# BQcwAoYraHR0cDovL2NybDMuYW1lLmdibC9haWEvQU1FUm9vdF9hbWVyb290LmNy
# dDA3BggrBgEFBQcwAoYraHR0cDovL2NybDEuYW1lLmdibC9haWEvQU1FUm9vdF9h
# bWVyb290LmNydDCBogYIKwYBBQUHMAKGgZVsZGFwOi8vL0NOPWFtZXJvb3QsQ049
# QUlBLENOPVB1YmxpYyUyMEtleSUyMFNlcnZpY2VzLENOPVNlcnZpY2VzLENOPUNv
# bmZpZ3VyYXRpb24sREM9QU1FLERDPUdCTD9jQUNlcnRpZmljYXRlP2Jhc2U/b2Jq
# ZWN0Q2xhc3M9Y2VydGlmaWNhdGlvbkF1dGhvcml0eTANBgkqhkiG9w0BAQsFAAOC
# AgEAUBAjt08P6N9e0a3e8mnanLMD8dS7yGMppGkzeinJrkbehymtF3u91MdvwEN9
# E34APRgSZ4MHkcpCgbrEc8jlNe4iLmyb8t4ANtXcLarQdA7KBL9VP6bVbtr/vnaE
# wif4vhm7LFV5IGl/B/uhDhhJk+Hr6eBm8EeB8FpXPg73/Bx/D3VANmdOAr3MCH3J
# EoqWzZvOI8SfF45kxU1rHJXS/XnY9jbGOohp8iRSMrq9j0u1UWMld6dVQCafdYI9
# Y0ULVhMggfD+YPZxN8/LtADWlP4Y8BEAq3Rsq2r1oJ39ibRvm09umAKJG3PJvt9s
# 1LV0TvjSt7QI4TrthXbBt6jaxeLHO8t+0fwvuz3G/3BX4bbarIq3qWYouMUrXIzD
# g2Ll8xptyCbNG9KMBxuqCne2Thrx6ZpofSvPwy64g/7KvG1EQ9dKov8LlvMzOyKS
# 4Nb3EfXSCtpnNKY+OKXOlF9F27bT/1RCYLt5U9niPVY1rWio8d/MRPcKEjMnpD0b
# c08IH7srBfQ5CYrK/sgOKaPxT8aWwcPXP4QX99gx/xhcbXktqZo4CiGzD/LA7pJh
# Kt5Vb7ljSbMm62cEL0Kb2jOPX7/iSqSyuWFmBH8JLGEUfcFPB4fyA/YUQhJG1KEN
# lu5jKbKdjW6f5HJ+Ir36JVMt0PWH9LHLEOlky2KZvgKAlCUxghk4MIIZNAIBATBY
# MEExEzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTAT
# BgNVBAMTDEFNRSBDUyBDQSAwMQITNgAAAX7/b/0EpCVYEgACAAABfjANBglghkgB
# ZQMEAgEFAKCBrjAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgCmkg7fufFinmWD5d
# xpfon+i7SbRoY/Wo41KAKOvO+OQwQgYKKwYBBAGCNwIBDDE0MDKgFIASAE0AaQBj
# AHIAbwBzAG8AZgB0oRqAGGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbTANBgkqhkiG
# 9w0BAQEFAASCAQBBRiMInAE9Cxy1N6vuTC9NUe+8CTaM1M0oDdohzCH4EwXlxcmj
# 0gJzLM+1IaiLF+Hg1KL7HWdWeExkMGHI50Two0siwMjrVT4h5SN4KOZUCkSUL/7i
# 3Ad+t34WHeNC1KFdxHxkPnEaDfLNUjb9NItCMvoP5C/QGv4MAJ2+I+pnFPyrk7gi
# g20vAI0aHcvV6IOJamKYZH2hoM2RPKFXn0v0aXueh0fMDr1+f2eu92Ud3EX6vS0F
# 4kuTYGY0eKUdvN0a+ocKlWZw3ZafoCWJG3D9y7qFJ6tAsAQawMwD8kP4i3PpDi0e
# 490e25zRd3flz++rChpbrIoyNdRAipQfxmtUoYIXADCCFvwGCisGAQQBgjcDAwEx
# ghbsMIIW6AYJKoZIhvcNAQcCoIIW2TCCFtUCAQMxDzANBglghkgBZQMEAgEFADCC
# AVEGCyqGSIb3DQEJEAEEoIIBQASCATwwggE4AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEIK4UnCQ1TXRtKUmPgXIuSmdiEi+oA7vxxierPaQOVpplAgZi
# Fl9fFv0YEzIwMjIwMzA5MTgwOTIzLjIwM1owBIACAfSggdCkgc0wgcoxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29m
# dCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOkFF
# MkMtRTMyQi0xQUZDMSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2
# aWNloIIRVzCCBwwwggT0oAMCAQICEzMAAAGWSVti4S/d908AAQAAAZYwDQYJKoZI
# hvcNAQELBQAwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
# BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEm
# MCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMjExMjAy
# MTkwNTEzWhcNMjMwMjI4MTkwNTEzWjCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0
# aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046QUUyQy1FMzJCLTFBRkMxJTAj
# BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggIiMA0GCSqGSIb3
# DQEBAQUAA4ICDwAwggIKAoICAQDSH2wQC2+t/jzA6jL6LZMhDNJG0nv1cUqe+H4M
# GKyEgRZUwp1YsHl1ITGyi8K9rkPRKKKAi0lT8g0b1GIipkWc8qCtE3wibxoNR4mC
# yrvgEsXutnbxI1obx8cMfa2XgchG/XBGZcFtGd0UQvXkxUYvokfG1TyBMqnIZvQ2
# LtcmGj86laPRNuRodkEM7VVUO2oMSHJbaTNj1b2kAC8sqlytH1zmfrQpTA3rZOyE
# mywT43DRfsNlXmkNKMiW7BafNnHZLGHGacpimE4doDMur3yiH/qCCx2PO4pIqkA6
# WLGSN8yhYavcQZRFVtsl/x/IiuL0fxPGpQmRc84m41yauncveNh/5/14MqsZ7ugY
# 1ix8fkOYgJBlLss8myPhaMA6qcEB/RWWqcCfhyARNjCcmBNGNXeMgKyZ/+e3bCOl
# XmWeDtVJDLmOtzEDBLmkg2/etp3T9hOX+LodYwdBkY2noCDEzPWVa834AmkJvR6y
# nEeBGj6ouWifpXxaobBdasb0+r/9eYr+T00yrLFn16rrTULnVzkW7lLyXWEousvz
# Ynul3HPCQooQS4LY1HBKTyTSftGX56ZgOz7Rk+esvbcr+NjLvBBy7Xeomgkuw1F/
# Uru7lZ9AR+EQbpg2pvCHSarMQQHbf1GXPhlDTHwkeskRiz5jPjTr1Wz/f+9CZx5o
# vtTF0QIDAQABo4IBNjCCATIwHQYDVR0OBBYEFNLfCNksLmWtIGEsiYuEKprRzXSy
# MB8GA1UdIwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8GA1UdHwRYMFYwVKBS
# oFCGTmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29m
# dCUyMFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggrBgEFBQcBAQRg
# MF4wXAYIKwYBBQUHMAKGUGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMv
# Y2VydHMvTWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3J0
# MAwGA1UdEwEB/wQCMAAwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZIhvcNAQEL
# BQADggIBAK9gCxC4IVbYKVQBHP5ztJc/kfgSubcL5hTReVE1uwSVKp92Sfd/IIvF
# gGQcwVlAZc8DubOhTshlR2fSFfK6+sUzoMOuf9ItKF7m348+SpZ455iITDyTgEjq
# XhTmTTvBfyEHA6gxHGzVo578k2Qsc7qSuXmPr8ZkeuRNHNOxFRQmnUWmdTOLGJlb
# Jq9zTH+KYbnJZ2tK5xwT2d2irtBu7U/FruzCxSbnM00y6dpYZcMUCdLuzxHEnX8/
# epO1nQlrpUTpJ6gel2Pv+E+4oktdX8zz0Y0WfwdQOZVbn5gr/wPLvIoceKJJ366A
# A36lbc8Do5h6TSvJbVArNutbg/1JcCT5Tl9peMEmiK1b3z5kRFZffztUe9pNYnhi
# jkGaQnRTbsBqXaCCLmPU9i4PEHcOyh8z7t5tzjOAnQYXi7oNBbRXitz8XbPK2Xas
# NB9QaU+01TKZRlVtYlsWrDriN7xCwCcx4bUnyiHGNiV5reIsDMbCKZ7h1sxLIQeg
# 5tW/Mg3R30EnzjFV5cq8RPXvoaFj89LpFMlmJbk8+KFmHzwXcl5wS+GVy38VulA+
# 36aEM4FADKqMjW10FCUEVVfznFZ3UlGdSS7GqyFeoXBzEqvwaIWxv0BXvLtNPfR+
# YxOzeCaeiMVC3cx0PlDcz+AF/VN2WHKI81dOAmE/qLJkd/EpmLZzMIIHcTCCBVmg
# AwIBAgITMwAAABXF52ueAptJmQAAAAAAFTANBgkqhkiG9w0BAQsFADCBiDELMAkG
# A1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQx
# HjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9z
# b2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTAwHhcNMjEwOTMwMTgy
# MjI1WhcNMzAwOTMwMTgzMjI1WjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2Fz
# aGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENv
# cnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAx
# MDCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAOThpkzntHIhC3miy9ck
# eb0O1YLT/e6cBwfSqWxOdcjKNVf2AX9sSuDivbk+F2Az/1xPx2b3lVNxWuJ+Slr+
# uDZnhUYjDLWNE893MsAQGOhgfWpSg0S3po5GawcU88V29YZQ3MFEyHFcUTE3oAo4
# bo3t1w/YJlN8OWECesSq/XJprx2rrPY2vjUmZNqYO7oaezOtgFt+jBAcnVL+tuhi
# JdxqD89d9P6OU8/W7IVWTe/dvI2k45GPsjksUZzpcGkNyjYtcI4xyDUoveO0hyTD
# 4MmPfrVUj9z6BVWYbWg7mka97aSueik3rMvrg0XnRm7KMtXAhjBcTyziYrLNueKN
# iOSWrAFKu75xqRdbZ2De+JKRHh09/SDPc31BmkZ1zcRfNN0Sidb9pSB9fvzZnkXf
# tnIv231fgLrbqn427DZM9ituqBJR6L8FA6PRc6ZNN3SUHDSCD/AQ8rdHGO2n6Jl8
# P0zbr17C89XYcz1DTsEzOUyOArxCaC4Q6oRRRuLRvWoYWmEBc8pnol7XKHYC4jMY
# ctenIPDC+hIK12NvDMk2ZItboKaDIV1fMHSRlJTYuVD5C4lh8zYGNRiER9vcG9H9
# stQcxWv2XFJRXRLbJbqvUAV6bMURHXLvjflSxIUXk8A8FdsaN8cIFRg/eKtFtvUe
# h17aj54WcmnGrnu3tz5q4i6tAgMBAAGjggHdMIIB2TASBgkrBgEEAYI3FQEEBQID
# AQABMCMGCSsGAQQBgjcVAgQWBBQqp1L+ZMSavoKRPEY1Kc8Q/y8E7jAdBgNVHQ4E
# FgQUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXAYDVR0gBFUwUzBRBgwrBgEEAYI3TIN9
# AQEwQTA/BggrBgEFBQcCARYzaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9w
# cy9Eb2NzL1JlcG9zaXRvcnkuaHRtMBMGA1UdJQQMMAoGCCsGAQUFBwMIMBkGCSsG
# AQQBgjcUAgQMHgoAUwB1AGIAQwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTAD
# AQH/MB8GA1UdIwQYMBaAFNX2VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0w
# S6BJoEeGRWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3Rz
# L01pY1Jvb0NlckF1dF8yMDEwLTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYI
# KwYBBQUHMAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWlj
# Um9vQ2VyQXV0XzIwMTAtMDYtMjMuY3J0MA0GCSqGSIb3DQEBCwUAA4ICAQCdVX38
# Kq3hLB9nATEkW+Geckv8qW/qXBS2Pk5HZHixBpOXPTEztTnXwnE2P9pkbHzQdTlt
# uw8x5MKP+2zRoZQYIu7pZmc6U03dmLq2HnjYNi6cqYJWAAOwBb6J6Gngugnue99q
# b74py27YP0h1AdkY3m2CDPVtI1TkeFN1JFe53Z/zjj3G82jfZfakVqr3lbYoVSfQ
# JL1AoL8ZthISEV09J+BAljis9/kpicO8F7BUhUKz/AyeixmJ5/ALaoHCgRlCGVJ1
# ijbCHcNhcy4sa3tuPywJeBTpkbKpW99Jo3QMvOyRgNI95ko+ZjtPu4b6MhrZlvSP
# 9pEB9s7GdP32THJvEKt1MMU0sHrYUP4KWN1APMdUbZ1jdEgssU5HLcEUBHG/ZPkk
# vnNtyo4JvbMBV0lUZNlz138eW0QBjloZkWsNn6Qo3GcZKCS6OEuabvshVGtqRRFH
# qfG3rsjoiV5PndLQTHa1V1QJsWkBRH58oWFsc/4Ku+xBZj1p/cvBQUl+fpO+y/g7
# 5LcVv7TOPqUxUYS8vwLBgqJ7Fx0ViY1w/ue10CgaiQuPNtq6TPmb/wrpNPgkNWcr
# 4A245oyZ1uEi6vAnQj0llOZ0dFtq0Z4+7X6gMTN9vMvpe784cETRkPHIqzqKOghi
# f9lwY1NNje6CbaUFEMFxBmoQtB1VM1izoXBm8qGCAs4wggI3AgEBMIH4oYHQpIHN
# MIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQL
# ExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMg
# VFNTIEVTTjpBRTJDLUUzMkItMUFGQzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIaAxUA0PommlVZaduKtDHghztBZDfm
# Vv6ggYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDANBgkqhkiG
# 9w0BAQUFAAIFAOXTUkQwIhgPMjAyMjAzMTAwMDIwMjBaGA8yMDIyMDMxMTAwMjAy
# MFowdzA9BgorBgEEAYRZCgQBMS8wLTAKAgUA5dNSRAIBADAKAgEAAgIWzwIB/zAH
# AgEAAgIRsDAKAgUA5dSjxAIBADA2BgorBgEEAYRZCgQCMSgwJjAMBgorBgEEAYRZ
# CgMCoAowCAIBAAIDB6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEBBQUAA4GBABoY
# LaqPZdtoQR1UA6Mzs002V67YxuzjLCVDnCJiu8B8dMZ63qdj3OrDZ6GW7jgxG9vF
# B+t3dF5ZCfdWudTfcmmyHBDiZALa63G/iunSUKCZWeRWAc1obiKVGdYbsL982GkG
# n3fMZbhYmDrKAWty8SNCSjuFsq6Cw1VAsO5jVWskMYIEDTCCBAkCAQEwgZMwfDEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWlj
# cm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTACEzMAAAGWSVti4S/d908AAQAAAZYw
# DQYJYIZIAWUDBAIBBQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRABBDAv
# BgkqhkiG9w0BCQQxIgQgVWLVoQ5rORJcjIckg0KE4ZJpDgoy+RvVvrXZJcU72u4w
# gfoGCyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCB2BNYC+B0105J2Ry6CfnZ0JA8J
# flZQQ6sLpHI3LbK9kDCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpX
# YXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQg
# Q29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAy
# MDEwAhMzAAABlklbYuEv3fdPAAEAAAGWMCIEIOgXFASx/7HSMUMrpfSYvl0Xi75b
# jRtgeyql6FkLNBrTMA0GCSqGSIb3DQEBCwUABIICAMP5QUBYXJ4LyV5IgZcnONFo
# 2lbt3yw5+k/+vmN4W43IEeqnbD//609lRYPzUYLR4jG29BOcbPqp6iXnhWQCLv0F
# Z410pbQLWsDPwqlhHtbxrYV9tzp8u6Iafv3PrmFhbK+UjtZ4Jdb8zsaorlLyIRwB
# up04q0VkaJ5W2rCGYxHU5+2p9n3mO55CAI7idcPEmcy4pzCXvr6CEirTFyrXkMOu
# 5PVYOUCA7W+i5ew84SpJci1PuCH9QAhkc0BGwAlDNFCXi728FkvhufxzVvQluIk/
# 6c8mWy0QxY3BUtSlQb/Vmk6mgd3R3K2Bqa5xoTdM7VvDB8a04b/Nd6NeBuIudi2E
# TZM+frB+zmC2gAxsYliTp2UXLFpQRr7IOBjFEVLBvlIY8FCwpE0VPf63ZpbZBYbx
# x3TMBMdxshmEG/Nck/HSHP4PkR6olSKo5+4gEYaCCp3t614/snsKTD1v7MTbHdcj
# 2nzNdwCJOVq+TCXvC3QuZuv1Jjd0oSeOsj4a62R7i25Nz1agOQxHoRuHbh8m0196
# DzWDdEclxKdZwEmjLgTINIBwmpDqQESI0WEaR5zHo+IA9rKL/ruAkwwBRQV9TWSb
# PhgX5P4cHvJ/0cm52OzrECgOZWstRB87CNNA+aVywEcX66LCII1HN2M1faEXkpLs
# /tPqIwypBHekqHd9Mzgb
# SIG # End signature block
