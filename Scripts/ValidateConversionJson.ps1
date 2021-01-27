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
# MIInMAYJKoZIhvcNAQcCoIInITCCJx0CAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAm5H8lix9H7Cgu
# rtdwRiBfsrysWyHeoz7gQ6z5+eaeuaCCEWkwggh7MIIHY6ADAgECAhM2AAABOgNw
# leIA4C4dAAEAAAE6MA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMDEwMjEyMDM5NTJaFw0yMTA5MTUyMTQzMDNaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQDDDS+b5DVWKtu8bpHxdAFKwLne5h8oB2TtG76NrpMklR5ovQ5DT+ZqzusC
# tVzXyNUPzMmoUn3Re5uCuIUXhxXwxdzmF0sm6xA+EuAt4RnI1ISULXFu17dftwEB
# noLnqssMBMPMosP8mILFrzWc8H/Vv5ZWRg6FUlgDVGLPI7oMGzqv5HluTJ4tNiFL
# DDyX7VhOVqN9o3DcBu5Vsyb7pKhPtEetiXw8zH91J2dbXoSgFm3+ZsMg2xGMVc4L
# vMulYjDB09T3wEwJE/UQbnX5rmdASK6OVK6YzjyMwdhDePuPc9BRHufvv4CSwXgW
# uPcLW+1741qdqc2B0bbFjCn/7Kb9AgMBAAGjggWHMIIFgzApBgkrBgEEAYI3FQoE
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
# aG9yaXR5MB0GA1UdDgQWBBRG3SojQLP8bopIpu8+hXTqgV3MLzAOBgNVHQ8BAf8E
# BAMCB4AwVAYDVR0RBE0wS6RJMEcxLTArBgNVBAsTJE1pY3Jvc29mdCBJcmVsYW5k
# IE9wZXJhdGlvbnMgTGltaXRlZDEWMBQGA1UEBRMNMjM2MTY3KzQ2MjUxNzCCAdQG
# A1UdHwSCAcswggHHMIIBw6CCAb+gggG7hjxodHRwOi8vY3JsLm1pY3Jvc29mdC5j
# b20vcGtpaW5mcmEvQ1JML0FNRSUyMENTJTIwQ0ElMjAwMS5jcmyGLmh0dHA6Ly9j
# cmwxLmFtZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMS5jcmyGLmh0dHA6Ly9j
# cmwyLmFtZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMS5jcmyGLmh0dHA6Ly9j
# cmwzLmFtZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMS5jcmyGLmh0dHA6Ly9j
# cmw0LmFtZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMS5jcmyGgbpsZGFwOi8v
# L0NOPUFNRSUyMENTJTIwQ0ElMjAwMSxDTj1CWTJQS0lDU0NBMDEsQ049Q0RQLENO
# PVB1YmxpYyUyMEtleSUyMFNlcnZpY2VzLENOPVNlcnZpY2VzLENOPUNvbmZpZ3Vy
# YXRpb24sREM9QU1FLERDPUdCTD9jZXJ0aWZpY2F0ZVJldm9jYXRpb25MaXN0P2Jh
# c2U/b2JqZWN0Q2xhc3M9Y1JMRGlzdHJpYnV0aW9uUG9pbnQwHwYDVR0jBBgwFoAU
# G2aiGfyb66XahI8YmOkQpMN7kr0wHwYDVR0lBBgwFgYKKwYBBAGCN1sBAQYIKwYB
# BQUHAwMwDQYJKoZIhvcNAQELBQADggEBAKxYk++FuWDcqQy1CRBI2tTsgkwQ/sR8
# Fi0+xziNF58QdbyUqmA4UGuHBh4T0Av2BLqfWPN8ftPb41rHP5oJCP1dypaC0jOu
# VeErfaKh8QDEDloUxNUkrSZaPV17ksbIvWuVetKFyZVkU6rS8WDkll7bIPYWPUh3
# vM+O1WDQNGA78ybQEvvOzRsSotMeCUa7AS1t1q5Dp2TUVnqjtbWTg0XB6PlhLyux
# VCz4PoX31CpF+IqwzA2w81ltwe4xzENLC24yVjHkmtmuFEAgiPATMaKqZOMeaOhm
# FMeCd0vkYbVA8OtrCHN/ij5+QXpxogz5x8znhMcUKYUU/JTEQZed3RkwggjmMIIG
# zqADAgECAhMfAAAAFLTFH8bygL5xAAAAAAAUMA0GCSqGSIb3DQEBCwUAMDwxEzAR
# BgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxEDAOBgNVBAMT
# B2FtZXJvb3QwHhcNMTYwOTE1MjEzMzAzWhcNMjEwOTE1MjE0MzAzWjBBMRMwEQYK
# CZImiZPyLGQBGRYDR0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxB
# TUUgQ1MgQ0EgMDEwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDVV4EC
# 1vn60PcbgLndN80k3GZh/OGJcq0pDNIbG5q/rrRtNLVUR4MONKcWGyaeVvoaQ8J5
# iYInBaBkaz7ehYnzJp3f/9Wg/31tcbxrPNMmZPY8UzXIrFRdQmCLsj3LcLiWX8BN
# 8HBsYZFcP7Y92R2VWnEpbN40Q9XBsK3FaNSEevoRzL1Ho7beP7b9FJlKB/Nhy0PM
# NaE1/Q+8Y9+WbfU9KTj6jNxrffv87O7T6doMqDmL/MUeF9IlmSrl088boLzAOt2L
# AeHobkgasx3ZBeea8R+O2k+oT4bwx5ZuzNpbGXESNAlALo8HCf7xC3hWqVzRqbdn
# d8HDyTNG6c6zwyf/AgMBAAGjggTaMIIE1jAQBgkrBgEEAYI3FQEEAwIBATAjBgkr
# BgEEAYI3FQIEFgQUkfwzzkKe9pPm4n1U1wgYu7jXcWUwHQYDVR0OBBYEFBtmohn8
# m+ul2oSPGJjpEKTDe5K9MIIBBAYDVR0lBIH8MIH5BgcrBgEFAgMFBggrBgEFBQcD
# AQYIKwYBBQUHAwIGCisGAQQBgjcUAgEGCSsGAQQBgjcVBgYKKwYBBAGCNwoDDAYJ
# KwYBBAGCNxUGBggrBgEFBQcDCQYIKwYBBQUIAgIGCisGAQQBgjdAAQEGCysGAQQB
# gjcKAwQBBgorBgEEAYI3CgMEBgkrBgEEAYI3FQUGCisGAQQBgjcUAgIGCisGAQQB
# gjcUAgMGCCsGAQUFBwMDBgorBgEEAYI3WwEBBgorBgEEAYI3WwIBBgorBgEEAYI3
# WwMBBgorBgEEAYI3WwUBBgorBgEEAYI3WwQBBgorBgEEAYI3WwQCMBkGCSsGAQQB
# gjcUAgQMHgoAUwB1AGIAQwBBMAsGA1UdDwQEAwIBhjASBgNVHRMBAf8ECDAGAQH/
# AgEAMB8GA1UdIwQYMBaAFCleUV5krjS566ycDaeMdQHRCQsoMIIBaAYDVR0fBIIB
# XzCCAVswggFXoIIBU6CCAU+GI2h0dHA6Ly9jcmwxLmFtZS5nYmwvY3JsL2FtZXJv
# b3QuY3JshjFodHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtpaW5mcmEvY3JsL2Ft
# ZXJvb3QuY3JshiNodHRwOi8vY3JsMi5hbWUuZ2JsL2NybC9hbWVyb290LmNybIYj
# aHR0cDovL2NybDMuYW1lLmdibC9jcmwvYW1lcm9vdC5jcmyGgapsZGFwOi8vL0NO
# PWFtZXJvb3QsQ049QU1FUk9PVCxDTj1DRFAsQ049UHVibGljJTIwS2V5JTIwU2Vy
# dmljZXMsQ049U2VydmljZXMsQ049Q29uZmlndXJhdGlvbixEQz1BTUUsREM9R0JM
# P2NlcnRpZmljYXRlUmV2b2NhdGlvbkxpc3Q/YmFzZT9vYmplY3RDbGFzcz1jUkxE
# aXN0cmlidXRpb25Qb2ludDCCAasGCCsGAQUFBwEBBIIBnTCCAZkwNwYIKwYBBQUH
# MAKGK2h0dHA6Ly9jcmwxLmFtZS5nYmwvYWlhL0FNRVJPT1RfYW1lcm9vdC5jcnQw
# RwYIKwYBBQUHMAKGO2h0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2lpbmZyYS9j
# ZXJ0cy9BTUVST09UX2FtZXJvb3QuY3J0MDcGCCsGAQUFBzAChitodHRwOi8vY3Js
# Mi5hbWUuZ2JsL2FpYS9BTUVST09UX2FtZXJvb3QuY3J0MDcGCCsGAQUFBzAChito
# dHRwOi8vY3JsMy5hbWUuZ2JsL2FpYS9BTUVST09UX2FtZXJvb3QuY3J0MIGiBggr
# BgEFBQcwAoaBlWxkYXA6Ly8vQ049YW1lcm9vdCxDTj1BSUEsQ049UHVibGljJTIw
# S2V5JTIwU2VydmljZXMsQ049U2VydmljZXMsQ049Q29uZmlndXJhdGlvbixEQz1B
# TUUsREM9R0JMP2NBQ2VydGlmaWNhdGU/YmFzZT9vYmplY3RDbGFzcz1jZXJ0aWZp
# Y2F0aW9uQXV0aG9yaXR5MA0GCSqGSIb3DQEBCwUAA4ICAQAot0qGmo8fpAFozcIA
# 6pCLygDhZB5ktbdA5c2ZabtQDTXwNARrXJOoRBu4Pk6VHVa78Xbz0OZc1N2xkzgZ
# MoRpl6EiJVoygu8Qm27mHoJPJ9ao9603I4mpHWwaqh3RfCfn8b/NxNhLGfkrc3wp
# 2VwOtkAjJ+rfJoQlgcacD14n9/VGt9smB6j9ECEgJy0443B+mwFdyCJO5OaUP+TQ
# OqiC/MmA+r0Y6QjJf93GTsiQ/Nf+fjzizTMdHggpTnxTcbWg9JCZnk4cC+AdoQBK
# R03kTbQfIm/nM3t275BjTx8j5UhyLqlqAt9cdhpNfdkn8xQz1dT6hTnLiowvNOPU
# kgbQtV+4crzKgHuHaKfJN7tufqHYbw3FnTZopnTFr6f8mehco2xpU8bVKhO4i0yx
# dXmlC0hKGwGqdeoWNjdskyUyEih8xyOK47BEJb6mtn4+hi8TY/4wvuCzcvrkZn0F
# 0oXd9JbdO+ak66M9DbevNKV71YbEUnTZ81toX0Ltsbji4PMyhlTg/669BoHsoTg4
# yoC9hh8XLW2/V2lUg3+qHHQf/2g2I4mm5lnf1mJsu30NduyrmrDIeZ0ldqKzHAHn
# fAmyFSNzWLvrGoU9Q0ZvwRlDdoUqXbD0Hju98GL6dTew3S2mcs+17DgsdargsEPm
# 6I1lUE5iixnoEqFKWTX5j/TLUjGCFR0wghUZAgEBMFgwQTETMBEGCgmSJomT8ixk
# ARkWA0dCTDETMBEGCgmSJomT8ixkARkWA0FNRTEVMBMGA1UEAxMMQU1FIENTIENB
# IDAxAhM2AAABOgNwleIA4C4dAAEAAAE6MA0GCWCGSAFlAwQCAQUAoIGuMBkGCSqG
# SIb3DQEJAzEMBgorBgEEAYI3AgEEMBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3
# AgEVMC8GCSqGSIb3DQEJBDEiBCC+d8JLzlI54sSY1UOv+/nCzfD5sDjoTtarkPdH
# KhB2SjBCBgorBgEEAYI3AgEMMTQwMqAUgBIATQBpAGMAcgBvAHMAbwBmAHShGoAY
# aHR0cDovL3d3dy5taWNyb3NvZnQuY29tMA0GCSqGSIb3DQEBAQUABIIBAFLJNXOR
# +C52AlX143O6LPw7UWZDesz0kzGSwMz03mJXHnLgC7km/VK58aj3AcNIznKr2GyW
# AtdGEf+2Kh1MF7lBF2Trl9oWSCJWHdzseWmQCxIfhnjd1LPaVQWsC5P7S6pHoZQm
# lcqhy39wiwIaBdBjkHtz9FZStMPsOIdCBHQMwodaUPpT9YvJD5igAuH91M4ud5UQ
# SQNWRERbPxUiY+e6Y7O/pe0K6EboOG7pr4fdCehPk4FagsAXcochIPsEYYZGoowU
# 3fq/eAuyTZlAYfO+SPX4Vc1bJlnDg7ZlR/vSwktR3Dj+zdagRmSC4fAJOrlfVP0T
# V9d0vG9d6DGCC5OhghLlMIIS4QYKKwYBBAGCNwMDATGCEtEwghLNBgkqhkiG9w0B
# BwKgghK+MIISugIBAzEPMA0GCWCGSAFlAwQCAQUAMIIBUQYLKoZIhvcNAQkQAQSg
# ggFABIIBPDCCATgCAQEGCisGAQQBhFkKAwEwMTANBglghkgBZQMEAgEFAAQgQS+o
# vaoc/P0No3QlgzNe/t7cC7vvEG9GIDdch1vmWn8CBmAGHJViYhgTMjAyMTAxMjcx
# ODMyNDMuNjQ3WjAEgAIB9KCB0KSBzTCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0
# aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046NDlCQy1FMzdBLTIzM0MxJTAj
# BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2Wggg48MIIE8TCCA9mg
# AwIBAgITMwAAAUmACEq7CaAzCwAAAAABSTANBgkqhkiG9w0BAQsFADB8MQswCQYD
# VQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEe
# MBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3Nv
# ZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDAeFw0yMDExMTIxODI1NTdaFw0yMjAyMTEx
# ODI1NTdaMIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUw
# IwYDVQQLExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1U
# aGFsZXMgVFNTIEVTTjo0OUJDLUUzN0EtMjMzQzElMCMGA1UEAxMcTWljcm9zb2Z0
# IFRpbWUtU3RhbXAgU2VydmljZTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoC
# ggEBAK8T+4kPhdh22no5Khk+SiUb2ncJZzAuV7VeRAyW4R6M3MhAx5Yyx3xlu6N1
# +Ml9Yb9zdI6ZkHBQBYR9VJ2UJoxrK3yBAeQoWKSgP9tjVDjLtF91ruwInDwJd/nB
# 8urOwYy8qSG0EUY6Go39ipCbmPsHRz2qrXbyLjSWuuyk6bfVV6X5QCqVW3Uvslvk
# wYoxaYRqhCmC8sZCr008EMx2BWObVfAUVVyl4FFZafdVvRQJYDq2pG4Usto6yF5g
# ID+PDrPDf5TAhObcmadBUwiv0cAuC8pja5LuWdIqIRopGNvWgHW/RTSzxY/OSBtW
# mzKuj5SaXa/tWt5qDNDPC9H51a0CAwEAAaOCARswggEXMB0GA1UdDgQWBBRWd5rx
# o++4tLw1NdZPyalz6m8kgTAfBgNVHSMEGDAWgBTVYzpcijGQ80N7fEYbxTNoWoVt
# VTBWBgNVHR8ETzBNMEugSaBHhkVodHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtp
# L2NybC9wcm9kdWN0cy9NaWNUaW1TdGFQQ0FfMjAxMC0wNy0wMS5jcmwwWgYIKwYB
# BQUHAQEETjBMMEoGCCsGAQUFBzAChj5odHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20v
# cGtpL2NlcnRzL01pY1RpbVN0YVBDQV8yMDEwLTA3LTAxLmNydDAMBgNVHRMBAf8E
# AjAAMBMGA1UdJQQMMAoGCCsGAQUFBwMIMA0GCSqGSIb3DQEBCwUAA4IBAQAQ3ngL
# PiDs4EqpzyLCovM11CPY4JPEqYf6OvcDKn4Bj/WrDh3vsD/hYI/yDw91OhiILjtR
# cLoV3Z6knTs6V6fnR/zysUDPj0fkFXteXA+OLITVlTS8soeIJwHaaJtqW6LtbR22
# DhbhsiqLtSVCSEEsRFLmef7MdhCD+sVBRC+8msqnHY0zQwWC/aGSLgHcmQNR8XCg
# fFhsAAlHVS1dof7Q8bUFbnIvUMuzjnWrsP9Gwcaa/HUzr5EpKveoJSJxt9ARry9o
# /6conRP8Nrm9KqlIqhx8Px3AzdSefS2mgL6UqapAwNuWWCEazs1WGmz73Eb3mztt
# 4IyvcC5Rkb8TpFBpMIIGcTCCBFmgAwIBAgIKYQmBKgAAAAAAAjANBgkqhkiG9w0B
# AQsFADCBiDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNV
# BAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAG
# A1UEAxMpTWljcm9zb2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTAw
# HhcNMTAwNzAxMjEzNjU1WhcNMjUwNzAxMjE0NjU1WjB8MQswCQYDVQQGEwJVUzET
# MBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMV
# TWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1T
# dGFtcCBQQ0EgMjAxMDCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKkd
# Dbx3EYo6IOz8E5f1+n9plGt0VBDVpQoAgoX77XxoSyxfxcPlYcJ2tz5mK1vwFVMn
# BDEfQRsalR3OCROOfGEwWbEwRA/xYIiEVEMM1024OAizQt2TrNZzMFcmgqNFDdDq
# 9UeBzb8kYDJYYEbyWEeGMoQedGFnkV+BVLHPk0ySwcSmXdFhE24oxhr5hoC732H8
# RsEnHSRnEnIaIYqvS2SJUGKxXf13Hz3wV3WsvYpCTUBR0Q+cBj5nf/VmwAOWRH7v
# 0Ev9buWayrGo8noqCjHw2k4GkbaICDXoeByw6ZnNPOcvRLqn9NxkvaQBwSAJk3jN
# /LzAyURdXhacAQVPIk0CAwEAAaOCAeYwggHiMBAGCSsGAQQBgjcVAQQDAgEAMB0G
# A1UdDgQWBBTVYzpcijGQ80N7fEYbxTNoWoVtVTAZBgkrBgEEAYI3FAIEDB4KAFMA
# dQBiAEMAQTALBgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAfBgNVHSMEGDAW
# gBTV9lbLj+iiXGJo0T2UkFvXzpoYxDBWBgNVHR8ETzBNMEugSaBHhkVodHRwOi8v
# Y3JsLm1pY3Jvc29mdC5jb20vcGtpL2NybC9wcm9kdWN0cy9NaWNSb29DZXJBdXRf
# MjAxMC0wNi0yMy5jcmwwWgYIKwYBBQUHAQEETjBMMEoGCCsGAQUFBzAChj5odHRw
# Oi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY1Jvb0NlckF1dF8yMDEw
# LTA2LTIzLmNydDCBoAYDVR0gAQH/BIGVMIGSMIGPBgkrBgEEAYI3LgMwgYEwPQYI
# KwYBBQUHAgEWMWh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9QS0kvZG9jcy9DUFMv
# ZGVmYXVsdC5odG0wQAYIKwYBBQUHAgIwNB4yIB0ATABlAGcAYQBsAF8AUABvAGwA
# aQBjAHkAXwBTAHQAYQB0AGUAbQBlAG4AdAAuIB0wDQYJKoZIhvcNAQELBQADggIB
# AAfmiFEN4sbgmD+BcQM9naOhIW+z66bM9TG+zwXiqf76V20ZMLPCxWbJat/15/B4
# vceoniXj+bzta1RXCCtRgkQS+7lTjMz0YBKKdsxAQEGb3FwX/1z5Xhc1mCRWS3Tv
# QhDIr79/xn/yN31aPxzymXlKkVIArzgPF/UveYFl2am1a+THzvbKegBvSzBEJCI8
# z+0DpZaPWSm8tv0E4XCfMkon/VWvL/625Y4zu2JfmttXQOnxzplmkIz/amJ/3cVK
# C5Em4jnsGUpxY517IW3DnKOiPPp/fZZqkHimbdLhnPkd/DjYlPTGpQqWhqS9nhqu
# BEKDuLWAmyI4ILUl5WTs9/S/fmNZJQ96LjlXdqJxqgaKD4kWumGnEcua2A5HmoDF
# 0M2n0O99g/DhO3EJ3110mCIIYdqwUB5vvfHhAN/nMQekkzr3ZUd46PioSKv33nJ+
# YWtvd6mBy6cJrDm77MbL2IK0cs0d9LiFAR6A+xuJKlQ5slvayA1VmXqHczsI5pgt
# 6o3gMy4SKfXAL1QnIffIrE7aKLixqduWsqdCosnPGUFN4Ib5KpqjEWYw07t0Mkvf
# Y3v1mYovG8chr1m1rtxEPJdQcdeh0sVV42neV8HR3jDA/czmTfsNv11P6Z0eGTgv
# vM9YBS7vDaBQNdrvCScc1bN+NR4Iuto229Nfj950iEkSoYICzjCCAjcCAQEwgfih
# gdCkgc0wgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYD
# VQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAj
# BgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRo
# YWxlcyBUU1MgRVNOOjQ5QkMtRTM3QS0yMzNDMSUwIwYDVQQDExxNaWNyb3NvZnQg
# VGltZS1TdGFtcCBTZXJ2aWNloiMKAQEwBwYFKw4DAhoDFQA/5bGu5y70ZIibAB0P
# nYFEa6mod6CBgzCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5n
# dG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9y
# YXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMA0G
# CSqGSIb3DQEBBQUAAgUA47vPqjAiGA8yMDIxMDEyNzE5NDAyNloYDzIwMjEwMTI4
# MTk0MDI2WjB3MD0GCisGAQQBhFkKBAExLzAtMAoCBQDju8+qAgEAMAoCAQACAgDU
# AgH/MAcCAQACAhHMMAoCBQDjvSEqAgEAMDYGCisGAQQBhFkKBAIxKDAmMAwGCisG
# AQQBhFkKAwKgCjAIAgEAAgMHoSChCjAIAgEAAgMBhqAwDQYJKoZIhvcNAQEFBQAD
# gYEAmUUyLFj97vPKmnFwc2PFvWKq/CRaM7wtVskVelLntoos7tndwlY4RbmBupvT
# jUpeFoSHF72BGqINidYOgSqqXC1lULsvr7+TWL78UWbyZEzdAh6Pb6mjpOAjO+I1
# 3k/WHDs4CO+I7In4F0XUtvpm8SG3W4QhtZhXGUaCps2vFWgxggMNMIIDCQIBATCB
# kzB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQD
# Ex1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAUmACEq7CaAzCwAA
# AAABSTANBglghkgBZQMEAgEFAKCCAUowGgYJKoZIhvcNAQkDMQ0GCyqGSIb3DQEJ
# EAEEMC8GCSqGSIb3DQEJBDEiBCDcP+e2QRl2yRvT3CfvHDKyUMteG/zKf56mhMsU
# bOaLfDCB+gYLKoZIhvcNAQkQAi8xgeowgecwgeQwgb0EICiV+vxYfXjvzN2GeBB1
# KpzsNAvSxnHQ5cb1rR+56KGyMIGYMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNV
# BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jv
# c29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAg
# UENBIDIwMTACEzMAAAFJgAhKuwmgMwsAAAAAAUkwIgQg7BD5CztnsPBpPu2fkL1a
# 2a24WbKB53G1XHNcs5qKqCowDQYJKoZIhvcNAQELBQAEggEAnUYNbmbMBGxxeQAS
# OvwrhA3pXhgENhoKfZoG2TPWJzEvMJXoJpmuP7JJSMMdOsKzKlMJH+yRbihRmatj
# XdNKacTdUwLN+lE7OrOpPQzrwKwT33OZI28VMj8xemCbTqP6V1x4W6u+EeGFk6PW
# hstRky2a7MU8SDSsp1XviF6IO1WzhPa4+T79uT43JjG+ZtRBevoSPMJBY/YyVKHM
# qECelcfQxJpRd7reVnrZCk+wxqQwg7rmq5l8FdW0lr9FwO6trKGS51H9ThpJzMlb
# rFgedjJYlxm9t/GneLoo10fNryLyKFjdXad4i2RjsMjdrOBFAy0FHS4vFc/knlyp
# tuZLkg==
# SIG # End signature block
