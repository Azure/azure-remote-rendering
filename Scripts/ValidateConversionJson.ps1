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
# MIIrWwYJKoZIhvcNAQcCoIIrTDCCK0gCAQExDzANBglghkgBZQMEAgEFADB5Bgor
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
# lu5jKbKdjW6f5HJ+Ir36JVMt0PWH9LHLEOlky2KZvgKAlCUxghk4MIIZNAIBATBY
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
# QjMNE4ItO8XNSepVDI/sVWML2rVKQADJSDE8oYIXADCCFvwGCisGAQQBgjcDAwEx
# ghbsMIIW6AYJKoZIhvcNAQcCoIIW2TCCFtUCAQMxDzANBglghkgBZQMEAgEFADCC
# AVEGCyqGSIb3DQEJEAEEoIIBQASCATwwggE4AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEIEeCrg17TAVJ76i7gixSdht1TIY5GTEeatB2uglHB3eRAgZi
# FmzzBP0YEzIwMjIwMzA5MTgwOTMyLjc3N1owBIACAfSggdCkgc0wgcoxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29m
# dCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOjNC
# QkQtRTMzOC1FOUExMSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2
# aWNloIIRVzCCBwwwggT0oAMCAQICEzMAAAGd/onl+Xu7TMAAAQAAAZ0wDQYJKoZI
# hvcNAQELBQAwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
# BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEm
# MCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMjExMjAy
# MTkwNTE5WhcNMjMwMjI4MTkwNTE5WjCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0
# aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046M0JCRC1FMzM4LUU5QTExJTAj
# BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggIiMA0GCSqGSIb3
# DQEBAQUAA4ICDwAwggIKAoICAQDgEWh60BxJFuR+mlFuFCtG3mR2XHNCfPMTXcp0
# 6YewAtS1bbGzK7hDC1JRMethcmiKM/ebdCcG6v6k4lQyLlSaHmHkIUC5pNEtlutz
# psVN+jo+Nbdyu9w0BMh4KzfduLdxbda1VztKDSXjE3eEl5Of+5hY3pHoJX9Nh/5r
# 4tc4Nvqt9tvVcYeIxpchZ81AK3+UzpA+hcR6HS67XA8+cQUB1fGyRoVh1sCu0+of
# dVDcWOG/tcSKtJch+eRAVDe7IRm84fPsPTFz2dIJRJA/PUaZR+3xW4Fd1ZbLNa/w
# Mbq3vaYtKogaSZiiCyUxU7mwoA32iyTcGHC7hH8MgZWVOEBu7CfNvMyrsR8Quvu3
# m91Dqsc5gZHMxvgeAO9LLiaaU+klYmFWQvLXpilS1iDXb/82+TjwGtxEnc8x/EvL
# kk7Ukj4uKZ6J8ynlgPhPRqejcoKlHsKgxWmD3wzEXW1a09d1L2Io004w01i31QAM
# B/GLhgmmMIE5Z4VI2Jlh9sX2nkyh5QOnYOznECk4za9cIdMKP+sde2nhvvcSdrGX
# Q8fWO/+N1mjT0SIkX41XZjm+QMGR03ta63pfsj3g3E5a1r0o9aHgcuphW0lwrbBA
# /TGMo5zC8Z5WI+Rwpr0MAiDZGy5h2+uMx/2+/F4ZiyKauKXqd7rIl1seAYQYxKQ4
# SemB0QIDAQABo4IBNjCCATIwHQYDVR0OBBYEFNbfEI3hKujMnF4Rgdvay4rZG1Xk
# MB8GA1UdIwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8GA1UdHwRYMFYwVKBS
# oFCGTmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29m
# dCUyMFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggrBgEFBQcBAQRg
# MF4wXAYIKwYBBQUHMAKGUGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMv
# Y2VydHMvTWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3J0
# MAwGA1UdEwEB/wQCMAAwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZIhvcNAQEL
# BQADggIBAIbHcpxLt2h0LNJ334iCNZYsta2Eant9JUeipwebFIwQMij7SIQ83iJ4
# Y4OL5YwlppwvF516AhcHevYMScY6NAXSAGhp5xYtkEckeV6gNbcp3C4I3yotWvDd
# 9KQCh7LdIhpiYCde0SF4N5JRZUHXIMczvNhe8+dEuiCnS1sWiGPUFzNJfsAcNs1a
# BkHItaSxM0AVHgZfgK8R2ihVktirxwYG0T9o1h0BkRJ3PfuJF+nOjt1+eFYYgq+b
# OLQs/SdgY4DbUVfrtLdEg2TbS+siZw4dqzM+tLdye5XGyJlKBX7aIs4xf1Hh1ymM
# X24YJlm8vyX+W4x8yytPmziNHtshxf7lKd1Pm7t+7UUzi8QBhby0vYrfrnoW1Kws
# +z34uoc2+D2VFxrH39xq/8KbeeBpuL5++CipoZQsd5QO5Ni81nBlwi/71JsZDEom
# so/k4JioyvVAM2818CgnsNJnMZZSxM5kyeRdYh9IbjGdPddPVcv0kPKrNalPtRO4
# ih0GVkL/a4BfEBtXDeEUIsM4A00QehD+ESV3I0UbW+b4NTmbRcjnVFk5t6nuK/Fo
# FQc5N4XueYAOw2mMDhAoFE+2xtTHk2ewd9xGkbFDl2b6u/FbhsUb5+XoP0PdJ3FT
# NP6G/7Vr4sIOxar4PpY674aQCiMSywwtIWOoqRS/OP/rSjF9E/xfMIIHcTCCBVmg
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
# VFNTIEVTTjozQkJELUUzMzgtRTlBMTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIaAxUAt+lDSRX92KFyij71Jn20CoSy
# yuCggYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDANBgkqhkiG
# 9w0BAQUFAAIFAOXTX8EwIhgPMjAyMjAzMTAwMTE3NTNaGA8yMDIyMDMxMTAxMTc1
# M1owdzA9BgorBgEEAYRZCgQBMS8wLTAKAgUA5dNfwQIBADAKAgEAAgImIQIB/zAH
# AgEAAgIRpTAKAgUA5dSxQQIBADA2BgorBgEEAYRZCgQCMSgwJjAMBgorBgEEAYRZ
# CgMCoAowCAIBAAIDB6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEBBQUAA4GBADeO
# SYAz8lOIK4WHNBDthmnJKBHFNvdYQzfTjs1Iw0jtzV80b1iDYTtCPMZr5fm0/DBx
# 6GVg+NsdBUjZ5eNQHBhcYrytQiXh9nivSmRSeG0BcP1vwUqynV02WHdNKIc54f5C
# rdNj4zW6DfIVYYinQTTtQ3buY8r8isG3kEmPrAvyMYIEDTCCBAkCAQEwgZMwfDEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWlj
# cm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTACEzMAAAGd/onl+Xu7TMAAAQAAAZ0w
# DQYJYIZIAWUDBAIBBQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRABBDAv
# BgkqhkiG9w0BCQQxIgQgw8Y2p9ayqzeNcLP/LotgWfVTmTfgiaTCLUX7hZ2mJQAw
# gfoGCyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCD1HmOt4IqgT4A0n4JblX/fzFLy
# Eu4OBDOb+mpMlYdFoTCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpX
# YXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQg
# Q29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAy
# MDEwAhMzAAABnf6J5fl7u0zAAAEAAAGdMCIEIJl8MkTWTEoWj3w8pqMGRe+KEMfO
# DrFflXk4watC6lGQMA0GCSqGSIb3DQEBCwUABIICAIj2m9SvofChS5C/QZnVTlf3
# 6wHWhuYOGGNRPV7TJDfpaMQUI6RWBACQAE6+7AFNuV6UfiJsnm4v5fjecxi+hnL/
# TYnaa+VHUW9Ijk1SjIdKCp+98BnVdORrkOAwB3F32+SZiqrjUoWXtX268wyjgniD
# w6s52iZEKQToWYpLdR+yhaLGdNowQ4IXwwX7KhrryRlYogZcpaHD23D5pOD18NzR
# azvEaAPFAAN6yYaFntxcN7YB0M4RP/h4C/CdUG2lO//DVg5IshtpkDybUEddUCY4
# Wh7+WJ3aimOUE9Tpfi8SNyBr2+8xCe488C5BCDt0oIwmK+SiOfWQ4JjwkDzeAiP1
# wiTz7oskb0CbFx/tYF2DpmBYI2jic1F0N28IQqcw5uJ7Yq3MfhjMNQImqO3T1ZUf
# f0ZDyL+yjwVSFNT70ZRRFo4jnfnijJiBYkFf3C2c1lWirAx7yNH8nm/Ra3ME/KmY
# 9H9f0TfBBxr51tbhDL60VhtMQry64hRkrY7vJvdNQ8Nbk1s8VMUoqJj+Gb/00bzd
# oxiM+UYs6FTvw4qJ075htRq6duSL3a+ga9fYZzQVDCHOZbe4ly+rWaunIiwQheC2
# CwE0gHjpftjIxiPMsafs7ZT2au+Mb9FFvwOlvGzjxJ/OizfdctodTBjjXfmYLGxX
# /Z6OFNCAzL8KdDNuAbqB
# SIG # End signature block
