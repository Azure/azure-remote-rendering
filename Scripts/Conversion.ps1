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
            $conversionSucceeded = false
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
# MIInKwYJKoZIhvcNAQcCoIInHDCCJxgCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBfmBcjSMtn6PcK
# GATx1yyChPUHBC5xUVtEwi1BGixwnKCCEWUwggh3MIIHX6ADAgECAhM2AAABOXjG
# OfXldyfqAAEAAAE5MA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMDEwMjEyMDM5MDZaFw0yMTA5MTUyMTQzMDNaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQCvtf6RG9X1bFXLQOzLuA06k5gBhizLWQ3/m6nIKOwoNsu9N+s9yt+ZGRpb
# ZbDtBmtAeoi3c2XK9vf0x3sq32GWPPv+px6a7u55tQ9lq4evX6QNxPhrH++ltlUt
# siiVmV934/+F5B/71sJ1Nxr89OsExV1b5Ey7LiKkEwxpTRxlOyUXf4OiQvTDzG0I
# 7AseJ4RxOy23tLnh8268pkucY2PbSLFYoRIG1ZGNgchcprL+uiRLuCz4vZXfidQo
# Wus3ThY8+mYulD8AaQ5ZtnuwzSHtzxYm/g6OeSDsf4xFep0DYLA3zNiKO4CvmzNR
# jJbcg1Bm7OpDe/CSLSWG5aoqW+X5AgMBAAGjggWDMIIFfzApBgkrBgEEAYI3FQoE
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
# aG9yaXR5MB0GA1UdDgQWBBRQasfWFuGWZ4TjHj7E0G+JYLldgzAOBgNVHQ8BAf8E
# BAMCB4AwUAYDVR0RBEkwR6RFMEMxKTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRp
# b25zIFB1ZXJ0byBSaWNvMRYwFAYDVQQFEw0yMzYxNjcrNDYyNTE2MIIB1AYDVR0f
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
# AzANBgkqhkiG9w0BAQsFAAOCAQEArFNMfAJStrd/3V4hInTdjEo/CLYAY8YX/foG
# Amyk6NrjEx3uFN0sJmR3qR0iBggS3SBiUi4oZ+Xk8+DjVnnJFn9Fhmu/kB2wT4ZK
# jjjZeWROPcTsUnRgs1+OhKTWbX2Eng8oH3Cq0qR9LaOT/ES5Ejd98S1jq6WZ8B8K
# dNHg0d+VGAtwts+E3uu8MkUM5rUukmPHW7BC8ttmgKeXZiIiLV4T1KzxBMMNg0lY
# 7iFbQ5fkj5hLa1E0WvsGMcMGOMwRUVwVwl6F8OL8aUY5i7tpAuz54XVS4W1grPyT
# JDae1qB19H5JvqTwPPNm30JrFGpR/X/SGQhROsoD4V1tvCJ8tDCCCOYwggbOoAMC
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
# TmKLGegSoUpZNfmP9MtSMYIVHDCCFRgCAQEwWDBBMRMwEQYKCZImiZPyLGQBGRYD
# R0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxBTUUgQ1MgQ0EgMDEC
# EzYAAAE5eMY59eV3J+oAAQAAATkwDQYJYIZIAWUDBAIBBQCgga4wGQYJKoZIhvcN
# AQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisGAQQBgjcCARUw
# LwYJKoZIhvcNAQkEMSIEIHxiDvLOJ6hW1K7mL0eLRGlQuuAZaosNP5jcYKoC8nUN
# MEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8AcwBvAGYAdKEagBhodHRw
# Oi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEAKrtXsKuzfM/Q
# MzhUOrp0CxZtSjj2us4VdGNaTpX4bHDmgSUo41CvuHcMdtSffxotTPDOYfsBlF3D
# NAidey95zuzcccavkJn75GfWKmLGVCRawdGQmGtrkolpAuVBtdMj4JFvBiDVcBGD
# xvqAdQXDA1hZamdnF5JFiyLGWe4wnfNdLHsrBlKcUbUw+rDLqz1HrJS+vhrypRTe
# e3B9i8wL0qyMZCSEhCdoa7Cd3/5g//Fq+T42c+YetTIzHGjiRUBpBocuMvXqRCLZ
# 4Cm3K78JD+TxAHgpLCAq42aFK7bWbjOOdwYT3IWlt2flgxdV8mGTeyT74rsE45vE
# XypZPGydcKGCEuQwghLgBgorBgEEAYI3AwMBMYIS0DCCEswGCSqGSIb3DQEHAqCC
# Er0wghK5AgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFQBgsqhkiG9w0BCRABBKCCAT8E
# ggE7MIIBNwIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFlAwQCAQUABCCjtMbQQsON
# 8l2DUKMFRWLlbYMHguWapVPujEUGf5R2iAIGYD0KtyJ1GBIyMDIxMDMxNjEyMTQy
# OS40MVowBIACAfSggdCkgc0wgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMx
# JjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOkU1QTYtRTI3Qy01OTJFMSUwIwYDVQQD
# ExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNloIIOPDCCBPEwggPZoAMCAQIC
# EzMAAAFHnY/x5t4xg1kAAAAAAUcwDQYJKoZIhvcNAQELBQAwfDELMAkGA1UEBhMC
# VVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNV
# BAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRp
# bWUtU3RhbXAgUENBIDIwMTAwHhcNMjAxMTEyMTgyNTU1WhcNMjIwMjExMTgyNTU1
# WjCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcT
# B1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UE
# CxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0aW9uczEmMCQGA1UECxMdVGhhbGVz
# IFRTUyBFU046RTVBNi1FMjdDLTU5MkUxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1l
# LVN0YW1wIFNlcnZpY2UwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCt
# BQNM6X32KFk/BJ8YaprfzEt6Lj34G+VLjzgfEgOGSVd1Mu7nCphK0K4oyPrzItgN
# RjB4gUiKq6GzgxdDHgZPgTEvm57zsascyGrybWkf3VVr8bqf2PIgGvwKDNEgVcyg
# sEbuWwXz9Li6M7AOoD4TB8fl4ATm+L7b4+lYDUMJYMLzpiJzM745a0XHiriUaOpY
# WfkwO9Hz6uf+k2Hq7yGyguH8naPLMnYfmYIt2PXAwWVvG4MD4YbjXBVZ14ueh7Yl
# qZTMua3n9kT1CZDsHvz+o58nsoamXRwRFOb7LDjVV++cZIZLO29usiI0H79tb3fS
# vh9tU7QC7CirNCBYagNJAgMBAAGjggEbMIIBFzAdBgNVHQ4EFgQUtPjcb95koYZX
# Gy9DPxN49dSCsLowHwYDVR0jBBgwFoAU1WM6XIoxkPNDe3xGG8UzaFqFbVUwVgYD
# VR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwv
# cHJvZHVjdHMvTWljVGltU3RhUENBXzIwMTAtMDctMDEuY3JsMFoGCCsGAQUFBwEB
# BE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9j
# ZXJ0cy9NaWNUaW1TdGFQQ0FfMjAxMC0wNy0wMS5jcnQwDAYDVR0TAQH/BAIwADAT
# BgNVHSUEDDAKBggrBgEFBQcDCDANBgkqhkiG9w0BAQsFAAOCAQEAUMQOyjV+ea2k
# EtXqD0cOfD2Z2PFUIy5kLkGU53RDGcfhlzIR9QlTgZLqTEhgLLuCSy6jcma+nPg7
# e5Xg1oqCZcZJRwtRPzS1F6/M6YR35H3brN0maVnPrmrQ91kkfsNqDTtuWDiAIBfk
# NEgCpQZCb4OV3HMu5L8eZzg5dUaJ7XE+LBuphJSLFJtabxYt4fkCQxnTD2z50Y32
# ZuXiNmFFia7qVq+3Yc3mmW02+/KWH8P1HPiobJG8crGYgSEkxtkUXGdoutwGWW88
# KR9RRcM/4GKLqt2OQ8AWEQb7shgM8pxNvu30TxejRApa4WAfOAejTG4+KzBm67Xj
# VZ2IlXAPkjCCBnEwggRZoAMCAQICCmEJgSoAAAAAAAIwDQYJKoZIhvcNAQELBQAw
# gYgxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdS
# ZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xMjAwBgNVBAMT
# KU1pY3Jvc29mdCBSb290IENlcnRpZmljYXRlIEF1dGhvcml0eSAyMDEwMB4XDTEw
# MDcwMTIxMzY1NVoXDTI1MDcwMTIxNDY1NVowfDELMAkGA1UEBhMCVVMxEzARBgNV
# BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jv
# c29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAg
# UENBIDIwMTAwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCpHQ28dxGK
# OiDs/BOX9fp/aZRrdFQQ1aUKAIKF++18aEssX8XD5WHCdrc+Zitb8BVTJwQxH0Eb
# GpUdzgkTjnxhMFmxMEQP8WCIhFRDDNdNuDgIs0Ldk6zWczBXJoKjRQ3Q6vVHgc2/
# JGAyWGBG8lhHhjKEHnRhZ5FfgVSxz5NMksHEpl3RYRNuKMYa+YaAu99h/EbBJx0k
# ZxJyGiGKr0tkiVBisV39dx898Fd1rL2KQk1AUdEPnAY+Z3/1ZsADlkR+79BL/W7l
# msqxqPJ6Kgox8NpOBpG2iAg16HgcsOmZzTznL0S6p/TcZL2kAcEgCZN4zfy8wMlE
# XV4WnAEFTyJNAgMBAAGjggHmMIIB4jAQBgkrBgEEAYI3FQEEAwIBADAdBgNVHQ4E
# FgQU1WM6XIoxkPNDe3xGG8UzaFqFbVUwGQYJKwYBBAGCNxQCBAweCgBTAHUAYgBD
# AEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMBAf8wHwYDVR0jBBgwFoAU1fZW
# y4/oolxiaNE9lJBb186aGMQwVgYDVR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5t
# aWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWljUm9vQ2VyQXV0XzIwMTAt
# MDYtMjMuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3
# dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNSb29DZXJBdXRfMjAxMC0wNi0y
# My5jcnQwgaAGA1UdIAEB/wSBlTCBkjCBjwYJKwYBBAGCNy4DMIGBMD0GCCsGAQUF
# BwIBFjFodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vUEtJL2RvY3MvQ1BTL2RlZmF1
# bHQuaHRtMEAGCCsGAQUFBwICMDQeMiAdAEwAZQBnAGEAbABfAFAAbwBsAGkAYwB5
# AF8AUwB0AGEAdABlAG0AZQBuAHQALiAdMA0GCSqGSIb3DQEBCwUAA4ICAQAH5ohR
# DeLG4Jg/gXEDPZ2joSFvs+umzPUxvs8F4qn++ldtGTCzwsVmyWrf9efweL3HqJ4l
# 4/m87WtUVwgrUYJEEvu5U4zM9GASinbMQEBBm9xcF/9c+V4XNZgkVkt070IQyK+/
# f8Z/8jd9Wj8c8pl5SpFSAK84Dxf1L3mBZdmptWvkx872ynoAb0swRCQiPM/tA6WW
# j1kpvLb9BOFwnzJKJ/1Vry/+tuWOM7tiX5rbV0Dp8c6ZZpCM/2pif93FSguRJuI5
# 7BlKcWOdeyFtw5yjojz6f32WapB4pm3S4Zz5Hfw42JT0xqUKloakvZ4argRCg7i1
# gJsiOCC1JeVk7Pf0v35jWSUPei45V3aicaoGig+JFrphpxHLmtgOR5qAxdDNp9Dv
# fYPw4TtxCd9ddJgiCGHasFAeb73x4QDf5zEHpJM692VHeOj4qEir995yfmFrb3ep
# gcunCaw5u+zGy9iCtHLNHfS4hQEegPsbiSpUObJb2sgNVZl6h3M7COaYLeqN4DMu
# Ein1wC9UJyH3yKxO2ii4sanblrKnQqLJzxlBTeCG+SqaoxFmMNO7dDJL32N79ZmK
# LxvHIa9Zta7cRDyXUHHXodLFVeNp3lfB0d4wwP3M5k37Db9dT+mdHhk4L7zPWAUu
# 7w2gUDXa7wknHNWzfjUeCLraNtvTX4/edIhJEqGCAs4wggI3AgEBMIH4oYHQpIHN
# MIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQL
# ExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMg
# VFNTIEVTTjpFNUE2LUUyN0MtNTkyRTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIaAxUAq6fBtEENocNASMqL03zGJS0w
# Zd2ggYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDANBgkqhkiG
# 9w0BAQUFAAIFAOP6pk4wIhgPMjAyMTAzMTYxMTM2NDZaGA8yMDIxMDMxNzExMzY0
# NlowdzA9BgorBgEEAYRZCgQBMS8wLTAKAgUA4/qmTgIBADAKAgEAAgIXSwIB/zAH
# AgEAAgIQsTAKAgUA4/v3zgIBADA2BgorBgEEAYRZCgQCMSgwJjAMBgorBgEEAYRZ
# CgMCoAowCAIBAAIDB6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEBBQUAA4GBAKvn
# H431ZkyZ/nEahkxd+yyJZNgDA+4cUTZrhS/UatxqNh27FHz2PMiqVuNwHIHxyuHe
# eXSJbBhN8fe/pl5+xfiBGeNkapHvpnnsMiATo+Nr3R0XYT4rik78IxHEceBGZtf7
# N/trYXcGOvALw9GQA51a+w5zCvmLc/Ga6R49JAZqMYIDDTCCAwkCAQEwgZMwfDEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWlj
# cm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTACEzMAAAFHnY/x5t4xg1kAAAAAAUcw
# DQYJYIZIAWUDBAIBBQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRABBDAv
# BgkqhkiG9w0BCQQxIgQgaQHM2rkSyVwyzhppJzhtmvw6NLqH01a2kPZGShND1ZUw
# gfoGCyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCB72zwSA5TPugbIiZO/2H1hrisA
# VItwzDscb0WqihjphTCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpX
# YXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQg
# Q29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAy
# MDEwAhMzAAABR52P8ebeMYNZAAAAAAFHMCIEIGenSjp4xRDd9BvrOT5uS2mh1eE/
# UEmPAVLN/PmHiYsxMA0GCSqGSIb3DQEBCwUABIIBAI0yDhQ2+qRcpX3VfrjibtYj
# jykzAGdgiks0lKsLn7nYzmp0dxsynzYOpfWBTMrkKGD+mZVJyeiqznrVgxsgZXOZ
# Qo4fI7/sE1DqAx/aULKuDm063qcIxHALEO0vp564raePLateosm3q6FntpcWZMr7
# pa5AxBueBD7HUC0CknUj4hKWyMpWclII4u3R4CRyplTbMIyD8wPMoo8Emf16BF/8
# DcuRizmUJ78OJTK+7xwd4TbMs9MKkqCMqxG0hW24kZeBBIUQzx3lbrr9sOQ4XspH
# LMUgV+t9XfnWZBm0umbpGnVTfKPYA50GsB37iGhyOMgIAkpUXX797nq5MTB0nHw=
# SIG # End signature block
