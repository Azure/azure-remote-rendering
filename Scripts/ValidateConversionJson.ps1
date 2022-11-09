#.SYNOPSIS
# This Powershell script demonstrates how to validate the ConversionSettings and MaterialOverrides JSON files in advance of triggering ARR conversion.

#.EXAMPLE
# ValidateConversionJson.ps1
#   This will check the two types of conversion JSON files in the input container, corresponding to the settings in arrconfig.json.

#.EXAMPLE
# ValidateConversionJson.ps1 -Local
#   Checks the files in the specified LocalAssetsDirectory instead of checking the files in blob storage.

#.OUTPUTS
# $True if all of the schemas it found were valid

#The Test-Json cmdlet is in 6.1.
#Requires -Version 6.1

Param(
    [switch] $Local, # the local assets in the LocalAssetDirectoryPath should be checked instead of the remote ones
    [string] $ConfigFile, # Use the specified config file instead of arrconfig.json.
    [string] $ResourceGroup, # optional override for resourceGroup of assetConversionSettings in config file
    [string] $StorageAccountName, # optional override for storageAccountName of assetConversionSettings in config file
    [string] $BlobInputContainerName, # optional override for blobInputContainer of assetConversionSettings in config file
    [string] $InputAssetPath, # path under inputcontainer/InputFolderPath pointing to the asset to be converted e.g model\box.fbx 
    [string] $InputFolderPath, # optional path in input container
    [string] $LocalAssetDirectoryPath # Path to directory containing all input asset data (e.g. fbx and textures referenced by it)
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

# Contains the Test-Json cmdlet
Import-Module Microsoft.PowerShell.Utility

$SchemaRelativeDirectory = Join-Path ".." "JsonSchemas"
$SchemaDirectory = Join-Path $PSScriptRoot $SchemaRelativeDirectory
$ConversionSettingsSchemaFilename = "ConversionSettingsSchema.json"
$MaterialOverridesSchemaFilename = "MaterialOverridesSchema.json"
$ConversionSettingsSchemaPath = Join-Path $SchemaDirectory $ConversionSettingsSchemaFilename
$MaterialOverridesSchemaPath = Join-Path $SchemaDirectory $MaterialOverridesSchemaFilename

if (-Not (Test-Path -Path $ConversionSettingsSchemaPath))
{
    WriteError "The required schema files $($ConversionSettingsSchemaFilename) was not found."
    WriteError "This script expects the schemas to be located in the relative location $($SchemaRelativeDirectory)"
    exit 1
}
if (-Not (Test-Path -Path $MaterialOverridesSchemaPath))
{
    WriteError "The required schema files $($MaterialOverridesSchemaFilename) was not found."
    WriteError "This script expects the schemas to be located in the relative location $($SchemaRelativeDirectory)"
    exit 1
}

$ConversionSettingsSchema = Get-Content $ConversionSettingsSchemaPath -Raw
$MaterialOverridesSchema = Get-Content $MaterialOverridesSchemaPath -Raw

# Just in case the schemas got corrupted somehow
if (-Not (Test-Json -Json $ConversionSettingsSchema))
{
    WriteError "The schema $($ConversionSettingsSchemaPath) is not valid JSON."
    exit 1
}
if (-Not (Test-Json -Json $MaterialOverridesSchema))
{
    WriteError "The schema $($MaterialOverridesSchemaPath) is not valid JSON."
    exit 1
}

# Call Test-Json with a work-around and some additional output
function IsValidAgainstSchema($json, $schema, $pathForMessage)
{
    try
    {
        # Work around a limitation of Test-Json, which can't parse top-level arrays:
        # Wrap the actual json as the value of a single property "_" in an object.
        # This is not intended to be completely general, but should be sufficient for the
        # JSON files used by the ARR Conversion service.
        $schemaPrefix = "{`"type`":`"object`",`"properties`":{`"_`":"
        $schemaBody = $schema -replace "`"\`$ref`"\s*:\s*`"#/", "`"`$ref`":`"#/properties/_/"
        $schemaSuffix = "}}"
        $wrappedSchema = $schemaPrefix + $schemaBody + $schemaSuffix
        $wrappedJson = "{`"_`":" + $json +"}"
        Test-Json -Json $wrappedJson -Schema $wrappedSchema -ErrorAction Stop
        WriteSuccess "$($pathForMessage) validates against its schema"
        return $True
    }
    catch
    {
        # Have to unwrap the path in any schema error
        $schemaError = ([string]$_).Replace("#/_.", "#/").Replace("#/_[", "#/[")
        WriteError "$($pathForMessage): $($_.Exception.Message) $($schemaError)"
        return $False
    }
}

# Validate the file corresponding to the conversionSettings file, except with its extension replaced.
# If the file is not present, a message is written, but $true is still returned.
function ValidateConversionFile($assetConversionSettings, $extension, $schema)
{
    $isValid = $True
    $pathSplit = $assetConversionSettings.inputAssetPath -split "\."
    if ($pathSplit.Count -lt 2)
    {
        WriteError "The inputAssetPath is expected to have an extension"
        exit 1
    }
    $inputAssetPathWithoutExtension = $pathSplit[0..($pathSplit.Count - 2)] -join "."
    $filePath = $inputAssetPathWithoutExtension + $extension

    # If local storage has been specified, validate the file there.
    if ($Local)
    {
        $filePathInLocalDir = if ($assetConversionSettings.localAssetDirectoryPath) { Join-Path $assetConversionSettings.localAssetDirectoryPath $filePath } Else { $filePath }
        if (Test-Path -Path $filePathInLocalDir)
        {
            $json = Get-Content $filePathInLocalDir -Raw
            if (-Not (IsValidAgainstSchema $json $schema $filePathInLocalDir))
            {
                $isValid = $False
            }
        }
        else
        {
            Write-Information "No file $($filePathInLocalDir) found in the local asset directory"
        }
    }
    else
    {
        # Test in the input storage
        $filePathInInputDir = if ($assetConversionSettings.inputFolderPath) { Join-Path $assetConversionSettings.inputFolderPath $filePath } Else { $filePath }
        $remoteBlobpath = $filePathInInputDir.Replace("\", "/")

        try
        {
            # Copy the json to a temporary file and check it there.
            $fileCopy = New-TemporaryFile
            $blob = Get-AzStorageBlobContent -Container $assetConversionSettings.blobInputContainerName -Context $assetConversionSettings.storageContext -Blob $remoteBlobpath -Destination $fileCopy -ErrorAction Stop -Force
            $json = Get-Content $fileCopy -Raw
            if (-Not (IsValidAgainstSchema $json $schema $remoteBlobpath))
            {
                $isValid = $False
            }
            Remove-Item $fileCopy
        }
        catch [Microsoft.WindowsAzure.Commands.Storage.Common.ResourceNotFoundException]
        {
            Write-Information "No file $($remoteBlobpath) found in the input container" 
        }
    }
    return $isValid
}

if ([string]::IsNullOrEmpty($ConfigFile)) {
    $ConfigFile = "$PSScriptRoot\arrconfig.json"
}

$config = LoadConfig `
    -fileLocation $ConfigFile `
    -StorageAccountName $StorageAccountName `
    -ResourceGroup $ResourceGroup `
    -BlobInputContainerName $BlobInputContainerName `
    -LocalAssetDirectoryPath $LocalAssetDirectoryPath `
    -InputAssetPath $InputAssetPath `
    -InputFolderPath $InputFolderPath `

if ($null -eq $config) {
    WriteError("Error reading config file - Exiting.")
    exit 1
}

$defaultConfig = GetDefaultConfig

$storageSettingsOkay = VerifyStorageSettings $config $defaultConfig
if ($false -eq $storageSettingsOkay) {
    WriteError("Error reading assetConversionSettings in $ConfigFile - Exiting.")
    exit 1
}

# if we do any conversion related things we need to validate storage settings
$isValid = ValidateConversionSettings $config $defaultConfig (-not $Local)
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

$conversionSettingsWasValid = ValidateConversionFile $config.assetConversionSettings ".ConversionSettings.json" $ConversionSettingsSchema
$materialOverridesWasValid = ValidateConversionFile $config.assetConversionSettings ".MaterialOverrides.json" $MaterialOverridesSchema

return $conversionSettingsWasValid -and $materialOverridesWasValid

# SIG # Begin signature block
# MIIrZAYJKoZIhvcNAQcCoIIrVTCCK1ECAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAm5H8lix9H7Cgu
# rtdwRiBfsrysWyHeoz7gQ6z5+eaeuaCCEXkwggiJMIIHcaADAgECAhM2AAABqdaQ
# MGZD2x+CAAIAAAGpMA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMjA2MTAxODI3MDRaFw0yMzA2MTAxODI3MDRaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQC4u9Lcerpczo3llU92plBBtOjhYWj0CvpOrIulkipCk2hb1kbnx15rINdV
# XvAqCfQgCN7AzdV88a2JfyOM7PhW16VsJidtX3OuqpSu1OWpNsUHUv5RZA7YMuHE
# HxDJsvGLfwpqJjUMLoMvnEq4CcgZadU1LXrwWKFLEg+d4Yp8beckfUKBID+snvDu
# 2djyEeWk+kyJrqgpUBlK+iz398OkGZf5yu7exd8S/X2z7g4koug+UmI1HQ+Gypbm
# EKFOf62NU4G7xN3u1xv6N/1BCzXYc8G3Hecw2E2VhlCupckxTLrlEfbMBgB30321
# 2jpVFT/y9FjNg6tYdK6UNW0yfZyPAgMBAAGjggWVMIIFkTApBgkrBgEEAYI3FQoE
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
# aG9yaXR5MB0GA1UdDgQWBBSPmAlYWIPObTrIuddZ/ZX08zr7VzAOBgNVHQ8BAf8E
# BAMCB4AwUAYDVR0RBEkwR6RFMEMxKTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRp
# b25zIFB1ZXJ0byBSaWNvMRYwFAYDVQQFEw0yMzYxNjcrNDcwODYxMIIB5gYDVR0f
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
# BgEEAYI3WwEBBggrBgEFBQcDAzANBgkqhkiG9w0BAQsFAAOCAQEAcPU4lsVn+0hr
# lmkPN5T6apYc50XaG0BkqJxF81iFpqOPJhG8JNQqf/lJkZQop1WazGIW0I6naUnb
# 4Ldvsgm6SSoL1KakiRAuEG7Pu4rg2xcHpefci/fZi4p4fiyp1GokwJ7OGxqV79KH
# p95yxVakmey99fF1cELKhVsBkJkJA3d05dTPgO0R9XZ/GFHNN9JSEqyvVVJj0cL+
# bJ52JKq+p3fN+Ar2PohHQNwvdaQqJXQH92djCe2ee2uEXEZhC489cEDvFfXRIH/w
# JUDxXU2i86S0Y7lyC+ZUx7mkDab0zuw4GAWSNeA8PuLg+gvlfSYr7pudyGIRmPUL
# mXVfovMkfjCCCOgwggbQoAMCAQICEx8AAABR6o/2nHMMqDsAAAAAAFEwDQYJKoZI
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
# lu5jKbKdjW6f5HJ+Ir36JVMt0PWH9LHLEOlky2KZvgKAlCUxghlBMIIZPQIBATBY
# MEExEzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTAT
# BgNVBAMTDEFNRSBDUyBDQSAwMQITNgAAAanWkDBmQ9sfggACAAABqTANBglghkgB
# ZQMEAgEFAKCBrjAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgvnfCS85SOeLEmNVD
# r/v5ws3w+bA46E7Wq5D3RyoQdkowQgYKKwYBBAGCNwIBDDE0MDKgFIASAE0AaQBj
# AHIAbwBzAG8AZgB0oRqAGGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbTANBgkqhkiG
# 9w0BAQEFAASCAQBXsVIJCA7TRsYJOr4SrncnpNQkLPpIPlTkFoYJ/vZtM/KOPR82
# e1q30I8KDeEqxpYXmBPq1WVbda1h8c+vkKspYYnCz6C91pMtPDUcwRTmf+hg3/O4
# FqaGJxFUAXGSHRANxtvuFF3Xqr+Tm7JOP7xS0DYLkFejkbIf5sF5B3JJ8MO8W9Qc
# lF+Vz2rJ+dEVhuW/UfRdPrKRNxs2WnSSKh6bTsUvmT9gJHFL3h3iGCjWJ9CBgXIA
# 3XnLyLBHBqHwew8pb2E8dVLZe+/Gfk1WDv39S0QIYj2ufuBGpBMiYOIpPxnciuic
# x9UvxQu/r9XPqSYSwybQm2JuRMge1sa/M/floYIXCTCCFwUGCisGAQQBgjcDAwEx
# ghb1MIIW8QYJKoZIhvcNAQcCoIIW4jCCFt4CAQMxDzANBglghkgBZQMEAgEFADCC
# AVUGCyqGSIb3DQEJEAEEoIIBRASCAUAwggE8AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEIIU7TkEfDvZn2piv1QTXLxPSmr/Rb5fqw5+A9NeFwmDtAgZj
# YsUW21QYEzIwMjIxMTA5MTUzMDQxLjU2OFowBIACAfSggdSkgdEwgc4xCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xKTAnBgNVBAsTIE1pY3Jvc29m
# dCBPcGVyYXRpb25zIFB1ZXJ0byBSaWNvMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVT
# TjpGNzdGLUUzNTYtNUJBRTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAg
# U2VydmljZaCCEVwwggcQMIIE+KADAgECAhMzAAABqqUxmwvLsggOAAEAAAGqMA0G
# CSqGSIb3DQEBCwUAMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9u
# MRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRp
# b24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIy
# MDMwMjE4NTEyNloXDTIzMDUxMTE4NTEyNlowgc4xCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xKTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRpb25z
# IFB1ZXJ0byBSaWNvMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjpGNzdGLUUzNTYt
# NUJBRTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZTCCAiIw
# DQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAKBP7HK51bWHf+FDSh9O7YyrQtkN
# MvdHzHiazvOdI9POGjyJIYrs1WOMmSCp3o/mvsuPnFSP5c0dCeBuUq6u6J30M81Z
# aNOP/abZrTwYrYN+N5nStrOGdCtRBum76hy7Tr3AZDUArLwvhsGlXhLlDU1wioax
# M+BVwCNI7LmTaYKqjm58hEgsYtKIHk59LzOnI4aenbPLBP/VYYjI6a4KIcun0EZE
# rAukt5PC/mKUaOphUMGYm0PxfpY9BkG5sPfczFyIfA13LLRS4sGhbUrcM54EvE2F
# lWBQaJo7frKW7CVjITLEX4E2lxwQG/MuZ+1wDYg9OOErT5h+6zecj67eenwxeUoa
# OEbKtiUxaJUYnyQKxCWTkNdWRXTKSmIxx0tbsP5irWjqXvT6t/zeJKw05NY8hPT5
# 6vW20q0DYK2NteOCDD0UD6ZNAFLV87GOkl0eBqXcToFVdeJwwOTE6aA4RqYoNr2Q
# UPBIU6JEiUGBs9c4qC5mBHTY46VaR/odaFDLcxQI4OPkn5al/IPsd8/raDmMfKik
# 66xcNh2qN4yytYM3uiDenX5qeFdx3pdi43pYAFN/S1/3VRNk+/GRVUUYWYBjDZSq
# xslidE8hsxC7K8qLfmNoaQ2aAsu13h1faTMSZIEVxosz1b9yIeXmtM6NlrjV3etw
# S7JXYwGhHMdVYEL1AgMBAAGjggE2MIIBMjAdBgNVHQ4EFgQUP5oUvFOHLthfd0Wz
# 3hGtnQVGpJ4wHwYDVR0jBBgwFoAUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXwYDVR0f
# BFgwVjBUoFKgUIZOaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwv
# TWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3JsMGwGCCsG
# AQUFBwEBBGAwXjBcBggrBgEFBQcwAoZQaHR0cDovL3d3dy5taWNyb3NvZnQuY29t
# L3BraW9wcy9jZXJ0cy9NaWNyb3NvZnQlMjBUaW1lLVN0YW1wJTIwUENBJTIwMjAx
# MCgxKS5jcnQwDAYDVR0TAQH/BAIwADATBgNVHSUEDDAKBggrBgEFBQcDCDANBgkq
# hkiG9w0BAQsFAAOCAgEA3wyATZBFEBogrcwHs4zI7qX2y0jbKCI6ZieGAIR96RiM
# rjZvWG39YPA/FL2vhGSCtO7ea3iBlwhhTyJEPexLugT4jB4W0rldOLP5bEc0zwxs
# 9NtTFS8Ul2zbJ7jz5WxSnhSHsfaVFUp7S6B2a1bjKmWIo/Svd3W1V3mcIYzhbpLI
# UVlP3CbTJEE+cC3hX+JggnSYRETyo+mI7Hz/KMWFaRWBUYI4g0BrwiV2lYqKyekj
# Np6rj7b8l6OhbgX/JP0bzNxv6io0Y4iNlIzz/PdIh/E2pj3pXPiQJPRlEkMksRec
# E8VnFyqhR4fb/F6c5ywY4+mEpshIAg2YUXswFqqbK9Fv+U8YYclYPvhK/wRZs+/5
# auK4FM+QTjywj0C5rmr8MziqmUGgAuwZQYyHRCopnVdlaO/xxSZCfaZR7w7B3OBE
# l8j+Voofs1Kfq9AmmQAWZOjt4DnNk5NnxThPvjQVuOU/y+HTErwqD/wKRCl0AJ3U
# PTJ8PPYp+jbEXkKmoFhU4JGer5eaj22nX19pujNZKqqart4yLjNUOkqWjVk4KHpd
# YRGcJMVXkKkQAiljUn9cHRwNuPz/Tu7YmfgRXWN4HvCcT2m1QADinOZPsO5v5j/b
# Exw0WmFrW2CtDEApnClmiAKchFr0xSKE5ET+AyubLapejENr9vt7QXNq6aP1XWcw
# ggdxMIIFWaADAgECAhMzAAAAFcXna54Cm0mZAAAAAAAVMA0GCSqGSIb3DQEBCwUA
# MIGIMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMTIwMAYDVQQD
# EylNaWNyb3NvZnQgUm9vdCBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkgMjAxMDAeFw0y
# MTA5MzAxODIyMjVaFw0zMDA5MzAxODMyMjVaMHwxCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1w
# IFBDQSAyMDEwMIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA5OGmTOe0
# ciELeaLL1yR5vQ7VgtP97pwHB9KpbE51yMo1V/YBf2xK4OK9uT4XYDP/XE/HZveV
# U3Fa4n5KWv64NmeFRiMMtY0Tz3cywBAY6GB9alKDRLemjkZrBxTzxXb1hlDcwUTI
# cVxRMTegCjhuje3XD9gmU3w5YQJ6xKr9cmmvHaus9ja+NSZk2pg7uhp7M62AW36M
# EBydUv626GIl3GoPz130/o5Tz9bshVZN7928jaTjkY+yOSxRnOlwaQ3KNi1wjjHI
# NSi947SHJMPgyY9+tVSP3PoFVZhtaDuaRr3tpK56KTesy+uDRedGbsoy1cCGMFxP
# LOJiss254o2I5JasAUq7vnGpF1tnYN74kpEeHT39IM9zfUGaRnXNxF803RKJ1v2l
# IH1+/NmeRd+2ci/bfV+AutuqfjbsNkz2K26oElHovwUDo9Fzpk03dJQcNIIP8BDy
# t0cY7afomXw/TNuvXsLz1dhzPUNOwTM5TI4CvEJoLhDqhFFG4tG9ahhaYQFzymei
# XtcodgLiMxhy16cg8ML6EgrXY28MyTZki1ugpoMhXV8wdJGUlNi5UPkLiWHzNgY1
# GIRH29wb0f2y1BzFa/ZcUlFdEtsluq9QBXpsxREdcu+N+VLEhReTwDwV2xo3xwgV
# GD94q0W29R6HXtqPnhZyacaue7e3PmriLq0CAwEAAaOCAd0wggHZMBIGCSsGAQQB
# gjcVAQQFAgMBAAEwIwYJKwYBBAGCNxUCBBYEFCqnUv5kxJq+gpE8RjUpzxD/LwTu
# MB0GA1UdDgQWBBSfpxVdAF5iXYP05dJlpxtTNRnpcjBcBgNVHSAEVTBTMFEGDCsG
# AQQBgjdMg30BATBBMD8GCCsGAQUFBwIBFjNodHRwOi8vd3d3Lm1pY3Jvc29mdC5j
# b20vcGtpb3BzL0RvY3MvUmVwb3NpdG9yeS5odG0wEwYDVR0lBAwwCgYIKwYBBQUH
# AwgwGQYJKwYBBAGCNxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8GA1Ud
# EwEB/wQFMAMBAf8wHwYDVR0jBBgwFoAU1fZWy4/oolxiaNE9lJBb186aGMQwVgYD
# VR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwv
# cHJvZHVjdHMvTWljUm9vQ2VyQXV0XzIwMTAtMDYtMjMuY3JsMFoGCCsGAQUFBwEB
# BE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9j
# ZXJ0cy9NaWNSb29DZXJBdXRfMjAxMC0wNi0yMy5jcnQwDQYJKoZIhvcNAQELBQAD
# ggIBAJ1VffwqreEsH2cBMSRb4Z5yS/ypb+pcFLY+TkdkeLEGk5c9MTO1OdfCcTY/
# 2mRsfNB1OW27DzHkwo/7bNGhlBgi7ulmZzpTTd2YurYeeNg2LpypglYAA7AFvono
# aeC6Ce5732pvvinLbtg/SHUB2RjebYIM9W0jVOR4U3UkV7ndn/OOPcbzaN9l9qRW
# qveVtihVJ9AkvUCgvxm2EhIRXT0n4ECWOKz3+SmJw7wXsFSFQrP8DJ6LGYnn8Atq
# gcKBGUIZUnWKNsIdw2FzLixre24/LAl4FOmRsqlb30mjdAy87JGA0j3mSj5mO0+7
# hvoyGtmW9I/2kQH2zsZ0/fZMcm8Qq3UwxTSwethQ/gpY3UA8x1RtnWN0SCyxTkct
# wRQEcb9k+SS+c23Kjgm9swFXSVRk2XPXfx5bRAGOWhmRaw2fpCjcZxkoJLo4S5pu
# +yFUa2pFEUep8beuyOiJXk+d0tBMdrVXVAmxaQFEfnyhYWxz/gq77EFmPWn9y8FB
# SX5+k77L+DvktxW/tM4+pTFRhLy/AsGConsXHRWJjXD+57XQKBqJC4822rpM+Zv/
# Cuk0+CQ1ZyvgDbjmjJnW4SLq8CdCPSWU5nR0W2rRnj7tfqAxM328y+l7vzhwRNGQ
# 8cirOoo6CGJ/2XBjU02N7oJtpQUQwXEGahC0HVUzWLOhcGbyoYICzzCCAjgCAQEw
# gfyhgdSkgdEwgc4xCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# KTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRpb25zIFB1ZXJ0byBSaWNvMSYwJAYD
# VQQLEx1UaGFsZXMgVFNTIEVTTjpGNzdGLUUzNTYtNUJBRTElMCMGA1UEAxMcTWlj
# cm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIaAxUA4G0m0J4e
# AlljcP/jvOv9/pm/68aggYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0Eg
# MjAxMDANBgkqhkiG9w0BAQUFAAIFAOcV1MwwIhgPMjAyMjExMDkxMTI3MDhaGA8y
# MDIyMTExMDExMjcwOFowdDA6BgorBgEEAYRZCgQBMSwwKjAKAgUA5xXUzAIBADAH
# AgEAAgIFLTAHAgEAAgIRbDAKAgUA5xcmTAIBADA2BgorBgEEAYRZCgQCMSgwJjAM
# BgorBgEEAYRZCgMCoAowCAIBAAIDB6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEB
# BQUAA4GBAE31CJpVQ2jFWTv92pqJDfThvRe/5HatmmZnKw9v6wUm7jIStZTXtwSE
# iyawVE9TGXl0kvTgImP/Y9RovdfqDVURHuAvJqAZ29NbLg7Y/GQbZBTOqHYvrESF
# 9UkmN0wC5jZf/1Q8My6LVoKIaxuqx9ZIWc/Miz4+Dao2sAjoowjgMYIEDTCCBAkC
# AQEwgZMwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNV
# BAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQG
# A1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTACEzMAAAGqpTGbC8uy
# CA4AAQAAAaowDQYJYIZIAWUDBAIBBQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG
# 9w0BCRABBDAvBgkqhkiG9w0BCQQxIgQg7fsItHzT9WLOwVMbcRXxbcdDyrX9ydOz
# 9VuRu8V81zUwgfoGCyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCBWtQJDHFq8EeBz
# 3TXugCqRhSI/JCZbATYEIwTG8bMewDCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0
# YW1wIFBDQSAyMDEwAhMzAAABqqUxmwvLsggOAAEAAAGqMCIEIDq2mVUESOuFZBNl
# 3ixuczQIqJxaAcFfBATel3zqaQLVMA0GCSqGSIb3DQEBCwUABIICABt8N816kxkN
# YVHfhtDp59AgLM1aXOtohTWugkW4o0YzCoTG0BjxWfPRUaAk0HzEzHSSVFtPOMiq
# HYVKIz9OIONOxBSeokH1rszs4xH3unGep5xELekzJFEF93PogbTzU++OohOL8+mD
# RioYhURmoUQaAFE418LiFEGcgzgT7h7ITpIi6Jxx+65ZzBbR/bkweOHgUs94ANwP
# spuu4KA50xgBbUzNEHCF0i0qY3htRcvcUVSrOJl6k00wzCkWHNCDqdG84r/s4/Jl
# yDscQj9xfOllxXaWaNUhVsqheEbGDHMv4GM2hejV+anJNPtXytSCZEhLhvLF70WA
# MCKgVmhi0spGNJCMWTrEqtXzxUAmgbCdMCGGQvSve6c+O9PR91EI5cQeLzc0C4D3
# 7yVuUU7GyYL1A3zdZgtydo9X3FztPr9BH1+5t5mTWKmb7EEsHG/WNeS5I+/s60ST
# cZsuOscplmbEnqdSnuMNjc/yCY6xbiK47SkpJCoQOZxmtipaKE8oIGrS9hrvZ3yv
# PcqGrWlNLtpy1GQWA2DcsPoH0l1dVPrw0QCqn3vwM/1Eq21vZv9oNp2R1dl25Dsv
# y/jD3gLN3aMh0tpPe8JSV0mRbVZVfIqcHrGWP5ljBF50kiocCCRjDJZhC/Spkvu+
# xYJQsv9yre0hPExQD/ypOVejMdDor3bn
# SIG # End signature block
