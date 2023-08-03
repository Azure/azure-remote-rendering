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
# MIInkwYJKoZIhvcNAQcCoIInhDCCJ4ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAm5H8lix9H7Cgu
# rtdwRiBfsrysWyHeoz7gQ6z5+eaeuaCCDXYwggX0MIID3KADAgECAhMzAAADTrU8
# esGEb+srAAAAAANOMA0GCSqGSIb3DQEBCwUAMH4xCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNpZ25p
# bmcgUENBIDIwMTEwHhcNMjMwMzE2MTg0MzI5WhcNMjQwMzE0MTg0MzI5WjB0MQsw
# CQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9u
# ZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMR4wHAYDVQQDExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIB
# AQDdCKiNI6IBFWuvJUmf6WdOJqZmIwYs5G7AJD5UbcL6tsC+EBPDbr36pFGo1bsU
# p53nRyFYnncoMg8FK0d8jLlw0lgexDDr7gicf2zOBFWqfv/nSLwzJFNP5W03DF/1
# 1oZ12rSFqGlm+O46cRjTDFBpMRCZZGddZlRBjivby0eI1VgTD1TvAdfBYQe82fhm
# WQkYR/lWmAK+vW/1+bO7jHaxXTNCxLIBW07F8PBjUcwFxxyfbe2mHB4h1L4U0Ofa
# +HX/aREQ7SqYZz59sXM2ySOfvYyIjnqSO80NGBaz5DvzIG88J0+BNhOu2jl6Dfcq
# jYQs1H/PMSQIK6E7lXDXSpXzAgMBAAGjggFzMIIBbzAfBgNVHSUEGDAWBgorBgEE
# AYI3TAgBBggrBgEFBQcDAzAdBgNVHQ4EFgQUnMc7Zn/ukKBsBiWkwdNfsN5pdwAw
# RQYDVR0RBD4wPKQ6MDgxHjAcBgNVBAsTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEW
# MBQGA1UEBRMNMjMwMDEyKzUwMDUxNjAfBgNVHSMEGDAWgBRIbmTlUAXTgqoXNzci
# tW2oynUClTBUBgNVHR8ETTBLMEmgR6BFhkNodHRwOi8vd3d3Lm1pY3Jvc29mdC5j
# b20vcGtpb3BzL2NybC9NaWNDb2RTaWdQQ0EyMDExXzIwMTEtMDctMDguY3JsMGEG
# CCsGAQUFBwEBBFUwUzBRBggrBgEFBQcwAoZFaHR0cDovL3d3dy5taWNyb3NvZnQu
# Y29tL3BraW9wcy9jZXJ0cy9NaWNDb2RTaWdQQ0EyMDExXzIwMTEtMDctMDguY3J0
# MAwGA1UdEwEB/wQCMAAwDQYJKoZIhvcNAQELBQADggIBAD21v9pHoLdBSNlFAjmk
# mx4XxOZAPsVxxXbDyQv1+kGDe9XpgBnT1lXnx7JDpFMKBwAyIwdInmvhK9pGBa31
# TyeL3p7R2s0L8SABPPRJHAEk4NHpBXxHjm4TKjezAbSqqbgsy10Y7KApy+9UrKa2
# kGmsuASsk95PVm5vem7OmTs42vm0BJUU+JPQLg8Y/sdj3TtSfLYYZAaJwTAIgi7d
# hzn5hatLo7Dhz+4T+MrFd+6LUa2U3zr97QwzDthx+RP9/RZnur4inzSQsG5DCVIM
# pA1l2NWEA3KAca0tI2l6hQNYsaKL1kefdfHCrPxEry8onJjyGGv9YKoLv6AOO7Oh
# JEmbQlz/xksYG2N/JSOJ+QqYpGTEuYFYVWain7He6jgb41JbpOGKDdE/b+V2q/gX
# UgFe2gdwTpCDsvh8SMRoq1/BNXcr7iTAU38Vgr83iVtPYmFhZOVM0ULp/kKTVoir
# IpP2KCxT4OekOctt8grYnhJ16QMjmMv5o53hjNFXOxigkQWYzUO+6w50g0FAeFa8
# 5ugCCB6lXEk21FFB1FdIHpjSQf+LP/W2OV/HfhC3uTPgKbRtXo83TZYEudooyZ/A
# Vu08sibZ3MkGOJORLERNwKm2G7oqdOv4Qj8Z0JrGgMzj46NFKAxkLSpE5oHQYP1H
# tPx1lPfD7iNSbJsP6LiUHXH1MIIHejCCBWKgAwIBAgIKYQ6Q0gAAAAAAAzANBgkq
# hkiG9w0BAQsFADCBiDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24x
# EDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
# bjEyMDAGA1UEAxMpTWljcm9zb2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5
# IDIwMTEwHhcNMTEwNzA4MjA1OTA5WhcNMjYwNzA4MjEwOTA5WjB+MQswCQYDVQQG
# EwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwG
# A1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSgwJgYDVQQDEx9NaWNyb3NvZnQg
# Q29kZSBTaWduaW5nIFBDQSAyMDExMIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIIC
# CgKCAgEAq/D6chAcLq3YbqqCEE00uvK2WCGfQhsqa+laUKq4BjgaBEm6f8MMHt03
# a8YS2AvwOMKZBrDIOdUBFDFC04kNeWSHfpRgJGyvnkmc6Whe0t+bU7IKLMOv2akr
# rnoJr9eWWcpgGgXpZnboMlImEi/nqwhQz7NEt13YxC4Ddato88tt8zpcoRb0Rrrg
# OGSsbmQ1eKagYw8t00CT+OPeBw3VXHmlSSnnDb6gE3e+lD3v++MrWhAfTVYoonpy
# 4BI6t0le2O3tQ5GD2Xuye4Yb2T6xjF3oiU+EGvKhL1nkkDstrjNYxbc+/jLTswM9
# sbKvkjh+0p2ALPVOVpEhNSXDOW5kf1O6nA+tGSOEy/S6A4aN91/w0FK/jJSHvMAh
# dCVfGCi2zCcoOCWYOUo2z3yxkq4cI6epZuxhH2rhKEmdX4jiJV3TIUs+UsS1Vz8k
# A/DRelsv1SPjcF0PUUZ3s/gA4bysAoJf28AVs70b1FVL5zmhD+kjSbwYuER8ReTB
# w3J64HLnJN+/RpnF78IcV9uDjexNSTCnq47f7Fufr/zdsGbiwZeBe+3W7UvnSSmn
# Eyimp31ngOaKYnhfsi+E11ecXL93KCjx7W3DKI8sj0A3T8HhhUSJxAlMxdSlQy90
# lfdu+HggWCwTXWCVmj5PM4TasIgX3p5O9JawvEagbJjS4NaIjAsCAwEAAaOCAe0w
# ggHpMBAGCSsGAQQBgjcVAQQDAgEAMB0GA1UdDgQWBBRIbmTlUAXTgqoXNzcitW2o
# ynUClTAZBgkrBgEEAYI3FAIEDB4KAFMAdQBiAEMAQTALBgNVHQ8EBAMCAYYwDwYD
# VR0TAQH/BAUwAwEB/zAfBgNVHSMEGDAWgBRyLToCMZBDuRQFTuHqp8cx0SOJNDBa
# BgNVHR8EUzBRME+gTaBLhklodHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtpL2Ny
# bC9wcm9kdWN0cy9NaWNSb29DZXJBdXQyMDExXzIwMTFfMDNfMjIuY3JsMF4GCCsG
# AQUFBwEBBFIwUDBOBggrBgEFBQcwAoZCaHR0cDovL3d3dy5taWNyb3NvZnQuY29t
# L3BraS9jZXJ0cy9NaWNSb29DZXJBdXQyMDExXzIwMTFfMDNfMjIuY3J0MIGfBgNV
# HSAEgZcwgZQwgZEGCSsGAQQBgjcuAzCBgzA/BggrBgEFBQcCARYzaHR0cDovL3d3
# dy5taWNyb3NvZnQuY29tL3BraW9wcy9kb2NzL3ByaW1hcnljcHMuaHRtMEAGCCsG
# AQUFBwICMDQeMiAdAEwAZQBnAGEAbABfAHAAbwBsAGkAYwB5AF8AcwB0AGEAdABl
# AG0AZQBuAHQALiAdMA0GCSqGSIb3DQEBCwUAA4ICAQBn8oalmOBUeRou09h0ZyKb
# C5YR4WOSmUKWfdJ5DJDBZV8uLD74w3LRbYP+vj/oCso7v0epo/Np22O/IjWll11l
# hJB9i0ZQVdgMknzSGksc8zxCi1LQsP1r4z4HLimb5j0bpdS1HXeUOeLpZMlEPXh6
# I/MTfaaQdION9MsmAkYqwooQu6SpBQyb7Wj6aC6VoCo/KmtYSWMfCWluWpiW5IP0
# wI/zRive/DvQvTXvbiWu5a8n7dDd8w6vmSiXmE0OPQvyCInWH8MyGOLwxS3OW560
# STkKxgrCxq2u5bLZ2xWIUUVYODJxJxp/sfQn+N4sOiBpmLJZiWhub6e3dMNABQam
# ASooPoI/E01mC8CzTfXhj38cbxV9Rad25UAqZaPDXVJihsMdYzaXht/a8/jyFqGa
# J+HNpZfQ7l1jQeNbB5yHPgZ3BtEGsXUfFL5hYbXw3MYbBL7fQccOKO7eZS/sl/ah
# XJbYANahRr1Z85elCUtIEJmAH9AAKcWxm6U/RXceNcbSoqKfenoi+kiVH6v7RyOA
# 9Z74v2u3S5fi63V4GuzqN5l5GEv/1rMjaHXmr/r8i+sLgOppO6/8MO0ETI7f33Vt
# Y5E90Z1WTk+/gFcioXgRMiF670EKsT/7qMykXcGhiJtXcVZOSEXAQsmbdlsKgEhr
# /Xmfwb1tbWrJUnMTDXpQzTGCGXMwghlvAgEBMIGVMH4xCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNp
# Z25pbmcgUENBIDIwMTECEzMAAANOtTx6wYRv6ysAAAAAA04wDQYJYIZIAWUDBAIB
# BQCgga4wGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEO
# MAwGCisGAQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEIL53wkvOUjnixJjVQ6/7+cLN
# 8PmwOOhO1quQ90cqEHZKMEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8A
# cwBvAGYAdKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEB
# BQAEggEAJxRVXuSyf3LMx5Uap4hYYNrtBnuwD5e1KRK6voyyHNWSyKKinJF56uBL
# c1GCMzK/JyTGEtWEFxBa7h7Nr8m2PinSbh+3Nt/h79RBQazSy1wIZujpmoj6XTG2
# 7oIDyPtmSrofGCkugQP2uCHIZmrrnFeHExim8Lu+No1seCGKlRfh1d+AeYolB3HL
# U+RQNCTCkpBuS3uW+kjUt9f/ml3a1SrJ8DHxf76onFb19kVkUVYb/FDH83POv5zY
# e2muyaYPFyuk6Oq3jeXMx4pcqbuQoKQUyFlVvc6VqRgpTkekzuqQAmL6qKP3Srth
# C1skIRj/Zoika/1P2b4Nwps1tDAKKKGCFv0wghb5BgorBgEEAYI3AwMBMYIW6TCC
# FuUGCSqGSIb3DQEHAqCCFtYwghbSAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFRBgsq
# hkiG9w0BCRABBKCCAUAEggE8MIIBOAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFl
# AwQCAQUABCAAUf+IQmwCdLWAM0krcfd8mWt+DFl+v7/MNLE9AI1t7AIGZLBK97sH
# GBMyMDIzMDgwMTE1MzcwMy4yNDhaMASAAgH0oIHQpIHNMIHKMQswCQYDVQQGEwJV
# UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UE
# ChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1l
# cmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjo0OUJDLUUz
# N0EtMjMzQzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaCC
# EVQwggcMMIIE9KADAgECAhMzAAABwFWkjcNkFcVLAAEAAAHAMA0GCSqGSIb3DQEB
# CwUAMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
# EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNV
# BAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIyMTEwNDE5MDEy
# NVoXDTI0MDIwMjE5MDEyNVowgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMx
# JjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOjQ5QkMtRTM3QS0yMzNDMSUwIwYDVQQD
# ExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNlMIICIjANBgkqhkiG9w0BAQEF
# AAOCAg8AMIICCgKCAgEAvO1g+2NhhmBQvlGlCTOMaFw3jbIhUdDTqkaQhRpdHVb+
# huU/0HNhLmoRYvrp7z5vIoL1MPAkVBFWJIkrcG7sSrednyZwreY207C9n8XivL9Z
# BOQeiUeL/TMlJ6VinrcafbhdnkNO5JDlPozC9dGySiubryds5GKtu69D1wNat9DI
# Ql6alFO6pncZK4RIzfv+KzkM7RkY3vHphV0C8EFUpF+lysaGJXFf9QsUUHwj9XKW
# Hfc9BfhLoCReXUzvgrspdFmVnA9ATYXmidSjrshf8A+E0/FpTdhXPI9XXqsZDHBq
# r7DlYoSCU3lvrVDRu1p5pHHf7s3kM16HpK6arDtY3ai1soASmEpv3C2N/y5MDBAp
# Dd4SpSkLMa7+6es/daeS7zdH1qdCa2RoJPM6Eh/6YmBfofhfLQofKPJl34ALlZWK
# 5AzVtFRNOXacoj6MAG2dT8Rc5fpKCH1E3n7Zje0dK24QVfSv/YOxw52ECaMLlW5P
# hHT3ZINNaCmRgcHCTClOKzC2FOr03YBc2zPOW6bIVdXloPmBMVaE+thXqPmANBw0
# YsncaOkVggjDb5O5VqOp98MklHpJoJI6pk5zAlx8/OtC7FutrdtYNUC6ykXzMAPF
# uYkWGgx/W7A0itKW8WzYzwO3bAhprwznouGZmRiw2k8pen80BzqzdyPvbzTxQsMC
# AwEAAaOCATYwggEyMB0GA1UdDgQWBBQARMZ480jwpK3P6quVWUEJ0c30hTAfBgNV
# HSMEGDAWgBSfpxVdAF5iXYP05dJlpxtTNRnpcjBfBgNVHR8EWDBWMFSgUqBQhk5o
# dHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NybC9NaWNyb3NvZnQlMjBU
# aW1lLVN0YW1wJTIwUENBJTIwMjAxMCgxKS5jcmwwbAYIKwYBBQUHAQEEYDBeMFwG
# CCsGAQUFBzAChlBodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NlcnRz
# L01pY3Jvc29mdCUyMFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNydDAMBgNV
# HRMBAf8EAjAAMBMGA1UdJQQMMAoGCCsGAQUFBwMIMA0GCSqGSIb3DQEBCwUAA4IC
# AQCtTh0EQn16kKQyCeVk9Vc10m6L0EwLRo3ATRouP7Yd2hWeEB2Y4ZF4CJKe9qfX
# WGJKzV7tMUm6DAsBKYH/nT+8ybI8uJiHGnfnVi6Sh7gFjnTpfh1j1T90H/uLeoFj
# pOn/+eoCoJmorW5Gb2ezlTlo5I0kNAubxtCxqbLizuPNPob8kRAKQgv+4/CC1Jmi
# UFG0uKINlKj9SsHcrWeBBQHX62nNgziIwT44JqHrA02I6cmQAi9BZcsf57OOLpRY
# lzoPH3x/+ldSySXAmyLq2uSbWtQuD84I/0ZgS/B5L3ewqTdiE1KbKX89MW5JqCK/
# yI/mAIQammAlHPqU9eZZTMPOHQs0XrpCijlk+qyo2JaHiySww6nuPqXzU3sEj3VW
# 00YiVSayKEu1IrRzzX3La8qe6OqLTvK/6gu5XdKq7TT852nB6IP0QM+Budtr4Fbx
# 4/svpKHGpK9/zBuaHHDXX5AoSksh/kSDYKfefQIhIfQJJzoE3X+MimMJrgrwZXlt
# b6j1IL0HY3qCpa03Ghgi0ITzqfkw3Man3G8kB1Ql+SeNciPUj73Kn2veJenGLtT8
# JkUM9RUi0woO0iuY4tJnYuS+SeqavXUOWqUYVY19FIr1PLqpmWkbrO5xKjkyOHoA
# mLxjNbKjOnkAwft+1G00kulKqzqPbm+Sn+47JsGQFhNGbTCCB3EwggVZoAMCAQIC
# EzMAAAAVxedrngKbSZkAAAAAABUwDQYJKoZIhvcNAQELBQAwgYgxCzAJBgNVBAYT
# AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYD
# VQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xMjAwBgNVBAMTKU1pY3Jvc29mdCBS
# b290IENlcnRpZmljYXRlIEF1dGhvcml0eSAyMDEwMB4XDTIxMDkzMDE4MjIyNVoX
# DTMwMDkzMDE4MzIyNVowfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0
# b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3Jh
# dGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwggIi
# MA0GCSqGSIb3DQEBAQUAA4ICDwAwggIKAoICAQDk4aZM57RyIQt5osvXJHm9DtWC
# 0/3unAcH0qlsTnXIyjVX9gF/bErg4r25PhdgM/9cT8dm95VTcVrifkpa/rg2Z4VG
# Iwy1jRPPdzLAEBjoYH1qUoNEt6aORmsHFPPFdvWGUNzBRMhxXFExN6AKOG6N7dcP
# 2CZTfDlhAnrEqv1yaa8dq6z2Nr41JmTamDu6GnszrYBbfowQHJ1S/rboYiXcag/P
# XfT+jlPP1uyFVk3v3byNpOORj7I5LFGc6XBpDco2LXCOMcg1KL3jtIckw+DJj361
# VI/c+gVVmG1oO5pGve2krnopN6zL64NF50ZuyjLVwIYwXE8s4mKyzbnijYjklqwB
# Sru+cakXW2dg3viSkR4dPf0gz3N9QZpGdc3EXzTdEonW/aUgfX782Z5F37ZyL9t9
# X4C626p+Nuw2TPYrbqgSUei/BQOj0XOmTTd0lBw0gg/wEPK3Rxjtp+iZfD9M269e
# wvPV2HM9Q07BMzlMjgK8QmguEOqEUUbi0b1qGFphAXPKZ6Je1yh2AuIzGHLXpyDw
# wvoSCtdjbwzJNmSLW6CmgyFdXzB0kZSU2LlQ+QuJYfM2BjUYhEfb3BvR/bLUHMVr
# 9lxSUV0S2yW6r1AFemzFER1y7435UsSFF5PAPBXbGjfHCBUYP3irRbb1Hode2o+e
# FnJpxq57t7c+auIurQIDAQABo4IB3TCCAdkwEgYJKwYBBAGCNxUBBAUCAwEAATAj
# BgkrBgEEAYI3FQIEFgQUKqdS/mTEmr6CkTxGNSnPEP8vBO4wHQYDVR0OBBYEFJ+n
# FV0AXmJdg/Tl0mWnG1M1GelyMFwGA1UdIARVMFMwUQYMKwYBBAGCN0yDfQEBMEEw
# PwYIKwYBBQUHAgEWM2h0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvRG9j
# cy9SZXBvc2l0b3J5Lmh0bTATBgNVHSUEDDAKBggrBgEFBQcDCDAZBgkrBgEEAYI3
# FAIEDB4KAFMAdQBiAEMAQTALBgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAf
# BgNVHSMEGDAWgBTV9lbLj+iiXGJo0T2UkFvXzpoYxDBWBgNVHR8ETzBNMEugSaBH
# hkVodHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtpL2NybC9wcm9kdWN0cy9NaWNS
# b29DZXJBdXRfMjAxMC0wNi0yMy5jcmwwWgYIKwYBBQUHAQEETjBMMEoGCCsGAQUF
# BzAChj5odHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY1Jvb0Nl
# ckF1dF8yMDEwLTA2LTIzLmNydDANBgkqhkiG9w0BAQsFAAOCAgEAnVV9/Cqt4Swf
# ZwExJFvhnnJL/Klv6lwUtj5OR2R4sQaTlz0xM7U518JxNj/aZGx80HU5bbsPMeTC
# j/ts0aGUGCLu6WZnOlNN3Zi6th542DYunKmCVgADsAW+iehp4LoJ7nvfam++Kctu
# 2D9IdQHZGN5tggz1bSNU5HhTdSRXud2f8449xvNo32X2pFaq95W2KFUn0CS9QKC/
# GbYSEhFdPSfgQJY4rPf5KYnDvBewVIVCs/wMnosZiefwC2qBwoEZQhlSdYo2wh3D
# YXMuLGt7bj8sCXgU6ZGyqVvfSaN0DLzskYDSPeZKPmY7T7uG+jIa2Zb0j/aRAfbO
# xnT99kxybxCrdTDFNLB62FD+CljdQDzHVG2dY3RILLFORy3BFARxv2T5JL5zbcqO
# Cb2zAVdJVGTZc9d/HltEAY5aGZFrDZ+kKNxnGSgkujhLmm77IVRrakURR6nxt67I
# 6IleT53S0Ex2tVdUCbFpAUR+fKFhbHP+CrvsQWY9af3LwUFJfn6Tvsv4O+S3Fb+0
# zj6lMVGEvL8CwYKiexcdFYmNcP7ntdAoGokLjzbaukz5m/8K6TT4JDVnK+ANuOaM
# mdbhIurwJ0I9JZTmdHRbatGePu1+oDEzfbzL6Xu/OHBE0ZDxyKs6ijoIYn/ZcGNT
# TY3ugm2lBRDBcQZqELQdVTNYs6FwZvKhggLLMIICNAIBATCB+KGB0KSBzTCByjEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWlj
# cm9zb2Z0IEFtZXJpY2EgT3BlcmF0aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBF
# U046NDlCQy1FMzdBLTIzM0MxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1w
# IFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVABAQ7ExF19KkwVL1E3Ad8k0Peb6doIGD
# MIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNV
# BAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQG
# A1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwDQYJKoZIhvcNAQEF
# BQACBQDocyyvMCIYDzIwMjMwODAxMTUwMzExWhgPMjAyMzA4MDIxNTAzMTFaMHQw
# OgYKKwYBBAGEWQoEATEsMCowCgIFAOhzLK8CAQAwBwIBAAICAb8wBwIBAAICEaow
# CgIFAOh0fi8CAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGEWQoDAqAKMAgC
# AQACAwehIKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQCTTIjqqwmoKaKx
# r5kJCg0kcBuI8r40fYN/J4NExg1+fAAsPCyoGfxJBQSwBr655J5gN6UMPp7hj+2C
# nEs09ibl+xLEuoAP0zBlGcwDYqhafOlulcC73SLY/hWytGAibcDgnTSZWwLSzxXL
# 2H+oVvBQZF6dUYRLfhHq9QlxkTCNrDGCBA0wggQJAgEBMIGTMHwxCzAJBgNVBAYT
# AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYD
# VQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBU
# aW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABwFWkjcNkFcVLAAEAAAHAMA0GCWCGSAFl
# AwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQwLwYJKoZIhvcN
# AQkEMSIEIBGQBwdAEFQcmxTrnjkfDOQoyhYVpU6X/vg9kt/fiWGfMIH6BgsqhkiG
# 9w0BCRACLzGB6jCB5zCB5DCBvQQgWvFYolIIXME0zK/W6XsCkkYX7lYNb9yA8Jxw
# Y04Pk08wgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3Rv
# bjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0
# aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAA
# AcBVpI3DZBXFSwABAAABwDAiBCC1TDcRYDCURaMyG+qNKvC1dGh86U9iaPk9jr4H
# zKCPzDANBgkqhkiG9w0BAQsFAASCAgB32T8+f3i7VJ2MqZi32jU5pncuZztlvZii
# Wq6ssV1hNoa3aIHlaoV3ryBRWho1k9PgkNkiqagxnfb19LjFY+EG9o35AuwuncEw
# WP8980t1UiARq9/8OgK1l04VsM441krfb6pW8eeWGg9aYyEz76D9dL8VnN9KqgLS
# U8ZXuIPywNF3epGAKsnRYbhzqi1t57svpD1KNAYGSQaxT6j1aj+eN9VmJdzW17MX
# o2bz29fspBZn8nW26eBHZF3vO7GPH6Q00F32IGITIi73taC5JrzHKOyazhhjgilK
# pHX8Lm0ZKQQbtNrM5ycjjcMLlp0tbiaGYmcyk9F2xMPkcIUqiaK+21RoHN/pf8oN
# kGyjfTsFCi+NHW/1A3jejz9M4/qXk55528BkKBfrbe/am5OnXfonpM/jNNjiuW+L
# lRosJbgnedj1wEmTR+DRQArvJIwbEMcwiqhx03sxznCfXxP5eOm0gmb6XxggH6xR
# GPBf0uy1VoBY26p+ooTTIwZLEmZgrt5n7tSXCNnaYIXDPDixtRqxe9Coueq3hret
# STnfdSkvUs4KtsNzFWZudZzYvA/n0hwYgm7aAvZOpy1I+iW+c2gPqnA5uPCkkwTk
# CrzG2EIIt+nYmEG5J6sxa3wR+tzp9tyBt9K7ludNDaD8LFDFsNepdT0SNigCmhVe
# ZoIyp1m26g==
# SIG # End signature block
