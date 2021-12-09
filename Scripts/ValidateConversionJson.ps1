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
# MIInPQYJKoZIhvcNAQcCoIInLjCCJyoCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAm5H8lix9H7Cgu
# rtdwRiBfsrysWyHeoz7gQ6z5+eaeuaCCEXkwggiJMIIHcaADAgECAhM2AAABfv9v
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
# lu5jKbKdjW6f5HJ+Ir36JVMt0PWH9LHLEOlky2KZvgKAlCUxghUaMIIVFgIBATBY
# MEExEzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTAT
# BgNVBAMTDEFNRSBDUyBDQSAwMQITNgAAAX7/b/0EpCVYEgACAAABfjANBglghkgB
# ZQMEAgEFAKCBrjAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgvnfCS85SOeLEmNVD
# r/v5ws3w+bA46E7Wq5D3RyoQdkowQgYKKwYBBAGCNwIBDDE0MDKgFIASAE0AaQBj
# AHIAbwBzAG8AZgB0oRqAGGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbTANBgkqhkiG
# 9w0BAQEFAASCAQBhJCj9ej27Ux5+GbV9SEW6XHLJuC7B4ThwRsW1BYxmarPh/thc
# z16GQCFNUpGqKXQndUU4oVjgDpjhRTe+QIlUxzIgrBEwKJzqNCxRBLKRcxgVu/oo
# ldX0ggHrQRZnM0g3ppgasx52oRgb12SMjSkW0wE+NUWEsZkPEA2vbXXtLNVic53G
# TrXu9O92BEiGYi8GE/VpcKueX9omfjtuLNtV4KjPBVGEPMtiygvGV6QUbsdWXQ/h
# L53pmcLJH1a8XfFCcBMv62jf7CO9iofsKudXra9md/mokZytZUjSvI9lfeEigMC2
# QjMNE4ItO8XNSepVDI/sVWML2rVKQADJSDE8oYIS4jCCEt4GCisGAQQBgjcDAwEx
# ghLOMIISygYJKoZIhvcNAQcCoIISuzCCErcCAQMxDzANBglghkgBZQMEAgEFADCC
# AVEGCyqGSIb3DQEJEAEEoIIBQASCATwwggE4AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEIEeCrg17TAVJ76i7gixSdht1TIY5GTEeatB2uglHB3eRAgZh
# kuGI0f0YEzIwMjExMjA4MTgwMzM4LjA3OFowBIACAfSggdCkgc0wgcoxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29m
# dCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOkQ2
# QkQtRTNFNy0xNjg1MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2
# aWNloIIOOTCCBPEwggPZoAMCAQICEzMAAAFQWKLUp5sLMOsAAAAAAVAwDQYJKoZI
# hvcNAQELBQAwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
# BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEm
# MCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMjAxMTEy
# MTgyNjAzWhcNMjIwMjExMTgyNjAzWjCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0
# aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046RDZCRC1FM0U3LTE2ODUxJTAj
# BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggEiMA0GCSqGSIb3
# DQEBAQUAA4IBDwAwggEKAoIBAQDnen+UeypZwycbVpoN8zNSAqnZl40+RjRTx17g
# sPvVYNxvPe6PzruS/J5X2mON6BRt+XaJATJJvkCgHvViJqrU7Q39T0qTf02fOTTz
# kBR1zhB2ihL3XSaEpRE/L2wSa7vgL8jhPFi0dZ8nnqcj96bVLaRvPs7ANXeDF3xp
# ZNgUSKL2EegBcmRUse+92uWk/NYsj8Y3ECv2qPnSCNESqdQ97JS4K3R5PzHSCG2x
# YvRRLp+b90FVI2JCQr1IAj92UNke2wKHbQs5VdyJE+/vgg6tyZdaxW7AVojIq5Kc
# fM3+QahNKpsdOHm37IwYmD1LfTsb0tVhXLjbh7o4T6cCKiWbAgMBAAGjggEbMIIB
# FzAdBgNVHQ4EFgQUglUZHxlF261kL0PBAEM7t+ufRX4wHwYDVR0jBBgwFoAU1WM6
# XIoxkPNDe3xGG8UzaFqFbVUwVgYDVR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5t
# aWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWljVGltU3RhUENBXzIwMTAt
# MDctMDEuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3
# dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNUaW1TdGFQQ0FfMjAxMC0wNy0w
# MS5jcnQwDAYDVR0TAQH/BAIwADATBgNVHSUEDDAKBggrBgEFBQcDCDANBgkqhkiG
# 9w0BAQsFAAOCAQEAUT9odHKO/uPj08AeL5P2HixMOqHK3oPk9JAdmlgf2Xt8xF7Y
# 9BHiFQNWYMKd/HI2ryYOu3SAAs3txZaRpalvY0R16WWIQzC9G9oqSD7QNN0RMxsi
# iCMM65/nq9xSPIrmYh6aTXFgIMuh4GLNk7gMQFybUbg2ZlLZsn9r5RzxX/x8aK17
# ggEWKmiij1lgb/6AE+bAPUuEyy50ua6U9Zs0+bi8/HvnZs6PiMwGhtXz/sRrZaAY
# jbLvaCXOk+DbRvHBoYHQQm35QrPUIfiNcw30giIMRy7xYHjiml/IxakMFUJ56mLE
# 3SvnbSGxaKwppPlkIsw5HhemdSGHs5SlrQTbXjCCBnEwggRZoAMCAQICCmEJgSoA
# AAAAAAIwDQYJKoZIhvcNAQELBQAwgYgxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpX
# YXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQg
# Q29ycG9yYXRpb24xMjAwBgNVBAMTKU1pY3Jvc29mdCBSb290IENlcnRpZmljYXRl
# IEF1dGhvcml0eSAyMDEwMB4XDTEwMDcwMTIxMzY1NVoXDTI1MDcwMTIxNDY1NVow
# fDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1Jl
# ZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMd
# TWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwggEiMA0GCSqGSIb3DQEBAQUA
# A4IBDwAwggEKAoIBAQCpHQ28dxGKOiDs/BOX9fp/aZRrdFQQ1aUKAIKF++18aEss
# X8XD5WHCdrc+Zitb8BVTJwQxH0EbGpUdzgkTjnxhMFmxMEQP8WCIhFRDDNdNuDgI
# s0Ldk6zWczBXJoKjRQ3Q6vVHgc2/JGAyWGBG8lhHhjKEHnRhZ5FfgVSxz5NMksHE
# pl3RYRNuKMYa+YaAu99h/EbBJx0kZxJyGiGKr0tkiVBisV39dx898Fd1rL2KQk1A
# UdEPnAY+Z3/1ZsADlkR+79BL/W7lmsqxqPJ6Kgox8NpOBpG2iAg16HgcsOmZzTzn
# L0S6p/TcZL2kAcEgCZN4zfy8wMlEXV4WnAEFTyJNAgMBAAGjggHmMIIB4jAQBgkr
# BgEEAYI3FQEEAwIBADAdBgNVHQ4EFgQU1WM6XIoxkPNDe3xGG8UzaFqFbVUwGQYJ
# KwYBBAGCNxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQF
# MAMBAf8wHwYDVR0jBBgwFoAU1fZWy4/oolxiaNE9lJBb186aGMQwVgYDVR0fBE8w
# TTBLoEmgR4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVj
# dHMvTWljUm9vQ2VyQXV0XzIwMTAtMDYtMjMuY3JsMFoGCCsGAQUFBwEBBE4wTDBK
# BggrBgEFBQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9N
# aWNSb29DZXJBdXRfMjAxMC0wNi0yMy5jcnQwgaAGA1UdIAEB/wSBlTCBkjCBjwYJ
# KwYBBAGCNy4DMIGBMD0GCCsGAQUFBwIBFjFodHRwOi8vd3d3Lm1pY3Jvc29mdC5j
# b20vUEtJL2RvY3MvQ1BTL2RlZmF1bHQuaHRtMEAGCCsGAQUFBwICMDQeMiAdAEwA
# ZQBnAGEAbABfAFAAbwBsAGkAYwB5AF8AUwB0AGEAdABlAG0AZQBuAHQALiAdMA0G
# CSqGSIb3DQEBCwUAA4ICAQAH5ohRDeLG4Jg/gXEDPZ2joSFvs+umzPUxvs8F4qn+
# +ldtGTCzwsVmyWrf9efweL3HqJ4l4/m87WtUVwgrUYJEEvu5U4zM9GASinbMQEBB
# m9xcF/9c+V4XNZgkVkt070IQyK+/f8Z/8jd9Wj8c8pl5SpFSAK84Dxf1L3mBZdmp
# tWvkx872ynoAb0swRCQiPM/tA6WWj1kpvLb9BOFwnzJKJ/1Vry/+tuWOM7tiX5rb
# V0Dp8c6ZZpCM/2pif93FSguRJuI57BlKcWOdeyFtw5yjojz6f32WapB4pm3S4Zz5
# Hfw42JT0xqUKloakvZ4argRCg7i1gJsiOCC1JeVk7Pf0v35jWSUPei45V3aicaoG
# ig+JFrphpxHLmtgOR5qAxdDNp9DvfYPw4TtxCd9ddJgiCGHasFAeb73x4QDf5zEH
# pJM692VHeOj4qEir995yfmFrb3epgcunCaw5u+zGy9iCtHLNHfS4hQEegPsbiSpU
# ObJb2sgNVZl6h3M7COaYLeqN4DMuEin1wC9UJyH3yKxO2ii4sanblrKnQqLJzxlB
# TeCG+SqaoxFmMNO7dDJL32N79ZmKLxvHIa9Zta7cRDyXUHHXodLFVeNp3lfB0d4w
# wP3M5k37Db9dT+mdHhk4L7zPWAUu7w2gUDXa7wknHNWzfjUeCLraNtvTX4/edIhJ
# EqGCAsswggI0AgEBMIH4oYHQpIHNMIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRp
# b25zMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjpENkJELUUzRTctMTY4NTElMCMG
# A1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIa
# AxUAIw17n3LxNWtGEZtallmkMZYeoBKggYMwgYCkfjB8MQswCQYDVQQGEwJVUzET
# MBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMV
# TWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1T
# dGFtcCBQQ0EgMjAxMDANBgkqhkiG9w0BAQUFAAIFAOVbCT0wIhgPMjAyMTEyMDgx
# ODM2NDVaGA8yMDIxMTIwOTE4MzY0NVowdDA6BgorBgEEAYRZCgQBMSwwKjAKAgUA
# 5VsJPQIBADAHAgEAAgIRdzAHAgEAAgIRNjAKAgUA5VxavQIBADA2BgorBgEEAYRZ
# CgQCMSgwJjAMBgorBgEEAYRZCgMCoAowCAIBAAIDB6EgoQowCAIBAAIDAYagMA0G
# CSqGSIb3DQEBBQUAA4GBACOXVYZKvWPRL4ajr+iha9iLWj6uc225J0U27PG2xlSj
# A6o+w6Lv5T/mdKgRkusKqcQT6HK58/b7QHpVOxOYT/QaIrRGRObYoNZ/k+aXCxip
# gIN5UYB95+n2dUZPPSHBb/a1B98TMEaF/BgZPjq3IOi9FL/NHxYbRNr5b4QYU23z
# MYIDDTCCAwkCAQEwgZMwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0
# b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3Jh
# dGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTACEzMA
# AAFQWKLUp5sLMOsAAAAAAVAwDQYJYIZIAWUDBAIBBQCgggFKMBoGCSqGSIb3DQEJ
# AzENBgsqhkiG9w0BCRABBDAvBgkqhkiG9w0BCQQxIgQgEvCfjTbcZ4D3Gy+9DRN5
# LhwRjkGMZ05F68/6qOofgyAwgfoGCyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCBs
# 9D6fL5rCThgXJmGIhdXS6IY1Zg6op47dkKJ8L/Kj9jCBmDCBgKR+MHwxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29m
# dCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABUFii1KebCzDrAAAAAAFQMCIEIN7p
# PsDZgFqiQtFpN/S8flx+0h+FpVoSy9UNuN4Q5NaCMA0GCSqGSIb3DQEBCwUABIIB
# ABQdAR24KXvcFSdyXp9+94fXmbLXNKDfdFlFitK/TUz8BeAQ/LL6R8V9oNQ/0cSz
# ni8a1vFopHJJBqUE8kJkmMjaDItcok0k6hahe9kHquvgbbSqPzwu5yNTaO2k3W+d
# OEAwlWl7BLMfwzRxZn1fxdeIV3XxnHTfFyICeBGYkQCX6QF/eMD0nAQQXIBbpJyR
# 6KK1Th163+/+lZH+NuFPNaybPc9OuNPKzPgujgkR0+pS7jK4IXx7LxHVghptWuuh
# me0mG++mgSnAFJ18UAuAeN+FrzNMyPhPZsy4jqOFVkrjx92ONJZWcFqEZpG2Pzxa
# PL77ax+06TKMjHw3aAF6STQ=
# SIG # End signature block
