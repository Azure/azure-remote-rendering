# This Powershell script is an example for the usage of the Azure Remote Rendering service
# Documentation: https://docs.microsoft.com/en-us/azure/remote-rendering/samples/powershell-example-scripts
#
# Usage:
# Fill out the accountSettings and renderingSessionSettings in arrconfig.json next to this file!
# This script is using the ARR REST API to start a rendering session

# RenderingSession.ps1
#   Will call the ARR REST interface to create a rendering session and poll its status until the rendering session is ready or an error occurs
#   Will print the hostname of the spun up rendering session on completion

# RenderingSession.ps1 -CreateSession
#   Will call the ARR REST interface to create a rendering session. Will print a session id which can be used to poll for its status

# RenderingSession.ps1 -GetSessionProperties [sessionID] [-Poll]
#   Will call the session properties REST API to retrieve the status of the rendering session with the given sessionID
#   If no sessionID is provided will prompt the user to enter a sessionID
#   Prints the current status of the session
#   If -Poll is specified will poll until the session is ready or an error occurs

# RenderingSession.ps1 -UpdateSession -MaxLeaseTime <hh:mm:ss> -Id [sessionID]
#   Updates the MaxLeaseTime of an already running session.
#   Note this sets the maxLeaseTime and does not extend it by the given duration
#   If no sessionID is provided the user will be asked to ender a sessionID

# RenderingSession.ps1 -StopSession -Id [sessionID]
#   Will call the stop session REST API to terminate an ongoing rendering session
#   If no sessionID is provided the user will be asked to ender a sessionID

# RenderingSession.ps1 -GetSessions -Id [sessionID]
#   Will list all currently running sessions and their properties

#The following individual parameters can be used to override values in the config file to create a session
# -VmSize <standard/premium>
# -RemoteRenderingDomain <remoteRenderingDomain e.g. westeurope or https://remoterendering.japaneast.mixedreality.azure.com>
# -ArrAccountId <id as guid>
# -ArrAccountKey <key in plain text>
# -ArrAccountDomain <arrAccountDomain e.g. australiaeast or https://sts.eastus.mixedreality.azure.com>
# -MaxLeaseTime <MaxLeaseTime as hh:mm:ss>

Param(
    [switch] $CreateSession,
    [switch] $GetSessionProperties,
    [switch] $GetSessions,
    [switch] $UpdateSession,
    [switch] $StopSession,
    [switch] $Poll,
    [string] $Id,
    [string] $ArrAccountId, # optional override for arrAccountId of accountSettings in config file
    [string] $ArrAccountKey, # optional override for arrAccountKey of accountSettings in config file
    [string] $ArrAccountDomain, # optional override for region where the ARR account is located
    [string] $VmSize, # optional override for vmSize of renderingSessionSettings in config file
    [string] $MaxLeaseTime, # optional override for maxLeaseTime of renderingSessionSettings in config file
    [string] $RemoteRenderingDomain, # optional override for region where rendering session is or should be located
    [string] $Region, # deprecated, please use explicit RemoteRenderingDomain and ArrAccountDomain
    [string] $ConfigFile,
    [hashtable] $AdditionalParameters
)

. "$PSScriptRoot\ARRUtils.ps1" #include ARRUtils for Logging, Config parsing

Set-StrictMode -Version Latest
$PrerequisitesInstalled = CheckPrerequisites
if (-Not $PrerequisitesInstalled) {
    WriteError("Prerequisites not installed - Exiting.")
    exit 1
}

function WriteSessionProperties($session) {
    WriteInformation("    sessionId:                  $($session.id)")
    WriteInformation("    elapsedTimeMinutes:         $($session.elapsedTimeMinutes)")
    if ([bool]($session | get-member -name "hostname")) {
        WriteInformation("    sessionHostname:            $($session.hostname)")
    }
    if ([bool]($session | get-member -name "arrInspectorPort")) {
        WriteInformation("    arrInspectorPort:           $($session.arrInspectorPort)")
    }
    if ([bool]($session | get-member -name "handshakePort")) {
        WriteInformation("    handshakePort:              $($session.handshakePort)")
    }
    WriteInformation("    sessionMaxLeaseTimeMinutes: $($session.maxLeaseTimeMinutes)")
    WriteInformation("    sessionSize:                $($session.size)")
    WriteInformation("    sessionStatus:              $($session.status)")
    if ([bool]($session | get-member -name "error")) {
        WriteInformation("    error:                      $($session.error)")
    }
}

#call REST API <endpoint>/accounts/<accountId>/sessions/
function GetSessions($arrAccountDomain, $remoteRenderingDomain, $accountId, $accountKey) {
    try {
        $url = "$remoteRenderingDomain/accounts/$accountId/sessions?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -arrAccountDomain $arrAccountDomain -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method GET -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

        if ($response.StatusCode -eq 200) {
            Write-Host -ForegroundColor Green "********************************************************************************************************************";

            $responseFromJson = ($response | ConvertFrom-Json)

            WriteSuccessResponse("Currently there are $($responseFromJson.sessions.Length) sessions:")

            foreach ($session in $responseFromJson.sessions) {
                WriteSessionProperties($session)
                WriteInformation("")
            }

            Write-Host -ForegroundColor Green "********************************************************************************************************************";
        }
    }
    catch {
        WriteError("Unable to get the status of sessions")
        HandleException($_.Exception)
        throw
    }
}

#call REST API <endpoint>/accounts/<accountId>/sessions/<SessionId> with PATCH to update a session
# currently only updates the leaseTime
# $MaxLeaseTime has to be strictly larger than the existing lease time of the session
function UpdateSession($arrAccountDomain, $remoteRenderingDomain, $accountId, $accountKey, $sessionId, [timespan] $maxLeaseTime) {
    try {
        $body =
        @{
            maxLeaseTimeMinutes = $maxLeaseTime.TotalMinutes -as [int];
        } | ConvertTo-Json
        $url = "$remoteRenderingDomain/accounts/$accountId/sessions/${sessionId}?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -ArrAccountDomain $arrAccountDomain -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method PATCH -ContentType "application/json" -Body $body -Headers @{ Authorization = "Bearer $token" }

        WriteSuccessResponse($response.RawContent)

        $responseFromJson = ($response | ConvertFrom-Json)

        WriteInformation("Successfully updated session. Session properties:")
        WriteSessionProperties($responseFromJson)

        return $response
    }
    catch {
        WriteError("Unable to get the status of the session with Id: $sessionId")
        HandleException($_.Exception)
        throw
    }
}

# retrieves GetSessionProperties until error or ready status of rendering session are achieved
function PollSessionStatus($arrAccountDomain, $remoteRenderingDomain, $accountId, $accountKey, $sessionId) {
    $sessionStatus = "Starting"

    WriteInformation("Provisioning a VM for rendering session '$sessionId' ...")

    $pollIntervalSec = 10

    if ($VmSize -eq "standard") {
        $pollIntervalSec = 2
    }

    $stopwatch = [system.diagnostics.stopwatch]::StartNew()

    while ($true) {
        $duration = FormatMilliseconds $stopwatch.ElapsedMilliseconds

        WriteProgress -Activity "Preparing VM for rendering session '$sessionId' ..." -Status: "Preparing for $duration"

        $response = GetSessionProperties $arrAccountDomain $remoteRenderingDomain $accountId $accountKey $sessionId
        $responseContent = $response.Content | ConvertFrom-Json

        $sessionStatus = $responseContent.status
        if ($sessionStatus -iin "ready", "error", "expired") {
            break
        }
        Start-Sleep -Seconds $pollIntervalSec
    }

    $duration = FormatMilliseconds $stopwatch.ElapsedMilliseconds

    if ("ready" -ieq $sessionStatus) {
        WriteInformation ("")
        Write-Host -ForegroundColor Green "Session is ready.";
        WriteInformation ("Time elapsed: $duration")
        WriteInformation ("")
        WriteSessionProperties($responseContent)
        WriteInformation ("")
        WriteInformation ("Response details:")
        WriteInformation($response)
    }

    if ("error" -ieq $sessionStatus) {
        WriteInformation ("The attempt to create a new session resulted in an error.")
        WriteInformation ("SessionId: $sessionId")
        WriteInformation ("Time elapsed: $duration")
        WriteInformation($response)
        exit 1
    }

    if ("expired" -ieq $sessionStatus) {
        WriteInformation ("The attempt to create a new session expired before it became ready. Check the settings in your configuration (arrconfig.json).")
        WriteInformation ("SessionId: $sessionId")
        WriteInformation ("Time elapsed: $duration")
        WriteInformation($response)
        exit 1
    }
}

# Execution of script starts here

if ([string]::IsNullOrEmpty($ConfigFile)) {
    $ConfigFile = "$PSScriptRoot\arrconfig.json"
}

$config = LoadConfig `
    -fileLocation $ConfigFile `
    -ArrAccountId $ArrAccountId `
    -ArrAccountKey $ArrAccountKey `
    -ArrAccountDomain $ArrAccountDomain `
    -RemoteRenderingDomain $RemoteRenderingDomain `
    -Region $Region `
    -VmSize $VmSize `
    -MaxLeaseTime $MaxLeaseTime

if ($null -eq $config) {
    WriteError("Error reading config file - Exiting.")
    exit 1
}

$defaultConfig = GetDefaultConfig

#to get session properties etc we do not need to have full renderingsessionsettings
$skipVmSettingsCheck = $GetSessionProperties -or $GetSessions -or $StopSession -or ($UpdateSession -and -Not $MaxLeaseTime)
$vmSettingsOkay = VerifyRenderingSessionSettings $config $defaultConfig $skipVmSettingsCheck
if (-Not $vmSettingsOkay) {
    WriteError("renderSessionSettings not valid. please ensure valid renderSessionSettings in $ConfigFile or commandline parameters - Exiting.")
    exit 1
}

# GetSessionProperties
if ($GetSessionProperties) {
    $sessionId = $Id
    if ([string]::IsNullOrEmpty($Id)) {
        $sessionId = Read-Host "Please enter Session Id"
    }
    if ($Poll) {
        PollSessionStatus -arrAccountDomain $config.accountSettings.arrAccountDomain -remoteRenderingDomain $config.renderingSessionSettings.remoteRenderingDomain -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId
    }
    else {
        $response = GetSessionProperties -arrAccountDomain $config.accountSettings.arrAccountDomain -remoteRenderingDomain $config.renderingSessionSettings.remoteRenderingDomain -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId

        $responseFromJson = ($response | ConvertFrom-Json)

        WriteInformation("Session Status:")
        WriteSessionProperties($responseFromJson)
    }
    exit
}

# GetSessions
if ($GetSessions) {
    GetSessions -arrAccountDomain $config.accountSettings.arrAccountDomain -remoteRenderingDomain $config.renderingSessionSettings.remoteRenderingDomain -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey
    exit
}

# StopSession
if ($StopSession) {
    $sessionId = $Id
    if ([string]::IsNullOrEmpty($Id)) {
        $sessionId = Read-Host "Please enter Session Id"
    }

    $null = StopSession -arrAccountDomain $config.accountSettings.arrAccountDomain -remoteRenderingDomain $config.renderingSessionSettings.remoteRenderingDomain -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId

    exit
}

#UpdateSession
if ($UpdateSession) {
    $sessionId = $Id
    if ([string]::IsNullOrEmpty($Id)) {
        $sessionId = Read-Host "Please enter Session Id"
    }
    $null = UpdateSession -arrAccountDomain $config.accountSettings.arrAccountDomain -remoteRenderingDomain $config.renderingSessionSettings.remoteRenderingDomain -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId -maxLeaseTime $config.renderingSessionSettings.maxLeaseTime

    exit
}

# Create a Session and Poll for Completion
$sessionId = CreateRenderingSession -arrAccountDomain $config.accountSettings.arrAccountDomain -remoteRenderingDomain $config.renderingSessionSettings.remoteRenderingDomain -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -vmSize $config.renderingSessionSettings.vmSize -maxLeaseTime $config.renderingSessionSettings.maxLeaseTime -AdditionalParameters $AdditionalParameters -sessionId $Id
if ($CreateSession -and ($false -eq $Poll)) {
    exit #do not poll if we asked to only create the session
}
PollSessionStatus -arrAccountDomain $config.accountSettings.arrAccountDomain -remoteRenderingDomain $config.renderingSessionSettings.remoteRenderingDomain -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId

# SIG # Begin signature block
# MIInvwYJKoZIhvcNAQcCoIInsDCCJ6wCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAw6xhx36gxyNpk
# QsF2R9nEXonrkLpWgXK1CaDnrMbITqCCDXYwggX0MIID3KADAgECAhMzAAADTrU8
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
# /Xmfwb1tbWrJUnMTDXpQzTGCGZ8wghmbAgEBMIGVMH4xCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNp
# Z25pbmcgUENBIDIwMTECEzMAAANOtTx6wYRv6ysAAAAAA04wDQYJYIZIAWUDBAIB
# BQCgga4wGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEO
# MAwGCisGAQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEIC3OuUr8QB3wad4xWpdsoPYC
# uhyP1F+gN+OliCVUsHDEMEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8A
# cwBvAGYAdKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEB
# BQAEggEAQS7yfBxyghDlxsM8M10+tRCjcB/8gkZNttc3vvACEo2ci3hFCBED1nk2
# aYIa8IKkkI8V5jD5J7x5djtToH/9Uk8qWm4Eu/bXEobhkCzD0iGgoUSH8QIaZiRH
# W+UWKQvedKbDLasHx4SDMnH+NDasO8Q6YmdJVz1STusDNX95MgdKVwq4gpUCra01
# /lLd3Y/H2lGjK5JPjlCpyjnlY1JQEcCWkpTYB0XkwPQNrwdBCMNN2BGLsPeoti3I
# OVfCfNbvyQdKfv/6FbIdd/udITnW6LNs+47M0p7wPv5kq0/LD5BO/9yWhrLPXSpM
# VMNVd36pDNXZnQ4UeySIbX9dKRBGhKGCFykwghclBgorBgEEAYI3AwMBMYIXFTCC
# FxEGCSqGSIb3DQEHAqCCFwIwghb+AgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFZBgsq
# hkiG9w0BCRABBKCCAUgEggFEMIIBQAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFl
# AwQCAQUABCDO02LNb21bq1cKQu6d9yM0fzRIxoMHTvbJgrD6Ka7ZvAIGZN/N9Sou
# GBMyMDIzMDkxODA5MzkzNS4zMTFaMASAAgH0oIHYpIHVMIHSMQswCQYDVQQGEwJV
# UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UE
# ChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMS0wKwYDVQQLEyRNaWNyb3NvZnQgSXJl
# bGFuZCBPcGVyYXRpb25zIExpbWl0ZWQxJjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNO
# OkQwODItNEJGRC1FRUJBMSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBT
# ZXJ2aWNloIIReDCCBycwggUPoAMCAQICEzMAAAG6Hz8Z98F1vXwAAQAAAbowDQYJ
# KoZIhvcNAQELBQAwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24x
# EDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
# bjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMjIw
# OTIwMjAyMjE5WhcNMjMxMjE0MjAyMjE5WjCB0jELMAkGA1UEBhMCVVMxEzARBgNV
# BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jv
# c29mdCBDb3Jwb3JhdGlvbjEtMCsGA1UECxMkTWljcm9zb2Z0IElyZWxhbmQgT3Bl
# cmF0aW9ucyBMaW1pdGVkMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjpEMDgyLTRC
# RkQtRUVCQTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZTCC
# AiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAIhOFYMzkjWAE9UVnXF9hRGv
# 0xBRxc+I5Hu3hxVFXyK3u38xusEb0pLkwjgGtDsaLLbrlMxqX3tFb/3BgEPEC3L0
# wX76gD8zHt+wiBV5mq5BWop29qRrgMJKKCPcpQnSjs9B/4XMFFvrpdPicZDv43FL
# gz9fHqMq0LJDw5JAHGDS30TCY9OF43P4d44Z9lE7CaVS2pJMF3L453MXB5yYK/KD
# bilhERP1jxn2yl+tGCRguIAsMG0oeOhXaw8uSGOhS6ACSHb+ebi0038MFHyoTNhK
# f+SYo4OpSY3xP4+swBBTKDoYP1wH+CfxG6h9fymBJQPQZaqfl0riiDLjmDunQtH1
# GD64Air5k9Jdwhq5wLmSWXjyFVL+IDfOpdixJ6f5o+MhE6H4t31w+prygHmd2UHQ
# 657UGx6FNuzwC+SpAHmV76MZYac4uAhTgaP47P2eeS1ockvyhl9ya+9JzPfMkug3
# xevzFADWiLRMr066EMV7q3JSRAsnCS9GQ08C4FKPbSh8OPM33Lng0ffxANnHAAX/
# DE7cHcx7l9jaV3Acmkj7oqir4Eh2u5YxwiaTE37XaMumX2ES3PJ5NBaXq7YdLJwy
# SD+U9pk/tl4dQ1t/Eeo7uDTliOyQkD8I74xpVB0T31/67KHfkBkFVvy6wye21V+9
# IC8uSD++RgD3RwtN2kE/AgMBAAGjggFJMIIBRTAdBgNVHQ4EFgQUimLm8QMeJa25
# j9MWeabI2HSvZOUwHwYDVR0jBBgwFoAUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXwYD
# VR0fBFgwVjBUoFKgUIZOaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9j
# cmwvTWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3JsMGwG
# CCsGAQUFBwEBBGAwXjBcBggrBgEFBQcwAoZQaHR0cDovL3d3dy5taWNyb3NvZnQu
# Y29tL3BraW9wcy9jZXJ0cy9NaWNyb3NvZnQlMjBUaW1lLVN0YW1wJTIwUENBJTIw
# MjAxMCgxKS5jcnQwDAYDVR0TAQH/BAIwADAWBgNVHSUBAf8EDDAKBggrBgEFBQcD
# CDAOBgNVHQ8BAf8EBAMCB4AwDQYJKoZIhvcNAQELBQADggIBAF/I8U6hbZhvDcn9
# 6nZ6tkbSEjXPvKZ6wroaXcgstEhpgaeEwleLuPXHLzEWtuJuYz4eshmhXqFr49lb
# AcX5SN5/cEsP0xdFayb7U5P94JZd3HjFvpWRNoNBhF3SDM0A38sI2H+hjhB/VfX1
# XcZiei1ROPAyCHcBgHLyQrEu6mnb3HhbIdr8h0Ta7WFylGhLSFW6wmzKusP6aOlm
# nGSac5NMfla6lRvTYHd28rbbCgfSm1RhTgoZj+W8DTKtiEMwubHJ3mIPKmo8xtJI
# WXPnXq6XKgldrL5cynLMX/0WX65OuWbHV5GTELdfWvGV3DaZrHPUQ/UP31Keqb2x
# jVCb30LVwgbjIvYS77N1dARkN8F/9pJ1gO4IvZWMwyMlKKFGojO1f1wbjSWcA/57
# tsc+t2blrMWgSNHgzDr01jbPSupRjy3Ht9ZZs4xN02eiX3eG297NrtC6l4c/gzn2
# 0eqoqWx/uHWxmTgB0F5osBuTHOe77DyEA0uhArGlgKP91jghgt/OVHoH65g0QqCt
# gZ+36mnCEg6IOhFoFrCc0fJFGVmb1+17gEe+HRMM7jBk4O06J+IooFrI3e3PJjPr
# Qano/MyE3h+zAuBWGMDRcUlNKCDU7dGnWvH3XWwLrCCIcz+3GwRUMsLsDdPW2OVv
# 7v1eEJiMSIZ2P+M7L20Q8aznU4OAMIIHcTCCBVmgAwIBAgITMwAAABXF52ueAptJ
# mQAAAAAAFTANBgkqhkiG9w0BAQsFADCBiDELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9zb2Z0IFJvb3QgQ2VydGlmaWNh
# dGUgQXV0aG9yaXR5IDIwMTAwHhcNMjEwOTMwMTgyMjI1WhcNMzAwOTMwMTgzMjI1
# WjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQD
# Ex1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDCCAiIwDQYJKoZIhvcNAQEB
# BQADggIPADCCAgoCggIBAOThpkzntHIhC3miy9ckeb0O1YLT/e6cBwfSqWxOdcjK
# NVf2AX9sSuDivbk+F2Az/1xPx2b3lVNxWuJ+Slr+uDZnhUYjDLWNE893MsAQGOhg
# fWpSg0S3po5GawcU88V29YZQ3MFEyHFcUTE3oAo4bo3t1w/YJlN8OWECesSq/XJp
# rx2rrPY2vjUmZNqYO7oaezOtgFt+jBAcnVL+tuhiJdxqD89d9P6OU8/W7IVWTe/d
# vI2k45GPsjksUZzpcGkNyjYtcI4xyDUoveO0hyTD4MmPfrVUj9z6BVWYbWg7mka9
# 7aSueik3rMvrg0XnRm7KMtXAhjBcTyziYrLNueKNiOSWrAFKu75xqRdbZ2De+JKR
# Hh09/SDPc31BmkZ1zcRfNN0Sidb9pSB9fvzZnkXftnIv231fgLrbqn427DZM9itu
# qBJR6L8FA6PRc6ZNN3SUHDSCD/AQ8rdHGO2n6Jl8P0zbr17C89XYcz1DTsEzOUyO
# ArxCaC4Q6oRRRuLRvWoYWmEBc8pnol7XKHYC4jMYctenIPDC+hIK12NvDMk2ZItb
# oKaDIV1fMHSRlJTYuVD5C4lh8zYGNRiER9vcG9H9stQcxWv2XFJRXRLbJbqvUAV6
# bMURHXLvjflSxIUXk8A8FdsaN8cIFRg/eKtFtvUeh17aj54WcmnGrnu3tz5q4i6t
# AgMBAAGjggHdMIIB2TASBgkrBgEEAYI3FQEEBQIDAQABMCMGCSsGAQQBgjcVAgQW
# BBQqp1L+ZMSavoKRPEY1Kc8Q/y8E7jAdBgNVHQ4EFgQUn6cVXQBeYl2D9OXSZacb
# UzUZ6XIwXAYDVR0gBFUwUzBRBgwrBgEEAYI3TIN9AQEwQTA/BggrBgEFBQcCARYz
# aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9Eb2NzL1JlcG9zaXRvcnku
# aHRtMBMGA1UdJQQMMAoGCCsGAQUFBwMIMBkGCSsGAQQBgjcUAgQMHgoAUwB1AGIA
# QwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTADAQH/MB8GA1UdIwQYMBaAFNX2
# VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0wS6BJoEeGRWh0dHA6Ly9jcmwu
# bWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01pY1Jvb0NlckF1dF8yMDEw
# LTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYBBQUHMAKGPmh0dHA6Ly93
# d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9vQ2VyQXV0XzIwMTAtMDYt
# MjMuY3J0MA0GCSqGSIb3DQEBCwUAA4ICAQCdVX38Kq3hLB9nATEkW+Geckv8qW/q
# XBS2Pk5HZHixBpOXPTEztTnXwnE2P9pkbHzQdTltuw8x5MKP+2zRoZQYIu7pZmc6
# U03dmLq2HnjYNi6cqYJWAAOwBb6J6Gngugnue99qb74py27YP0h1AdkY3m2CDPVt
# I1TkeFN1JFe53Z/zjj3G82jfZfakVqr3lbYoVSfQJL1AoL8ZthISEV09J+BAljis
# 9/kpicO8F7BUhUKz/AyeixmJ5/ALaoHCgRlCGVJ1ijbCHcNhcy4sa3tuPywJeBTp
# kbKpW99Jo3QMvOyRgNI95ko+ZjtPu4b6MhrZlvSP9pEB9s7GdP32THJvEKt1MMU0
# sHrYUP4KWN1APMdUbZ1jdEgssU5HLcEUBHG/ZPkkvnNtyo4JvbMBV0lUZNlz138e
# W0QBjloZkWsNn6Qo3GcZKCS6OEuabvshVGtqRRFHqfG3rsjoiV5PndLQTHa1V1QJ
# sWkBRH58oWFsc/4Ku+xBZj1p/cvBQUl+fpO+y/g75LcVv7TOPqUxUYS8vwLBgqJ7
# Fx0ViY1w/ue10CgaiQuPNtq6TPmb/wrpNPgkNWcr4A245oyZ1uEi6vAnQj0llOZ0
# dFtq0Z4+7X6gMTN9vMvpe784cETRkPHIqzqKOghif9lwY1NNje6CbaUFEMFxBmoQ
# tB1VM1izoXBm8qGCAtQwggI9AgEBMIIBAKGB2KSB1TCB0jELMAkGA1UEBhMCVVMx
# EzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoT
# FU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEtMCsGA1UECxMkTWljcm9zb2Z0IElyZWxh
# bmQgT3BlcmF0aW9ucyBMaW1pdGVkMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjpE
# MDgyLTRCRkQtRUVCQTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2Vy
# dmljZaIjCgEBMAcGBSsOAwIaAxUAdqNHe113gCJ87aZIGa5QBUqIwvKggYMwgYCk
# fjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQD
# Ex1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDANBgkqhkiG9w0BAQUFAAIF
# AOiyf1swIhgPMjAyMzA5MTgxNTQ4NDNaGA8yMDIzMDkxOTE1NDg0M1owdDA6Bgor
# BgEEAYRZCgQBMSwwKjAKAgUA6LJ/WwIBADAHAgEAAgIJSjAHAgEAAgISJDAKAgUA
# 6LPQ2wIBADA2BgorBgEEAYRZCgQCMSgwJjAMBgorBgEEAYRZCgMCoAowCAIBAAID
# B6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEBBQUAA4GBAGK+6UVMbVgt4qWdPk/1
# tYxGjavQWgZ3LPfp9l3mh/tQK2RhpjsBgKJO+VVBXcUW3YQb5qP9g40+jrcIFlfy
# vrAK3UpbfuIZ6DJ6AayEF30fseVPvwaqjl/BJlKUL3ofsjEMcZPdpfHQv4Zdj3rr
# cWGEIG68RqDIePRRKRZEJtI0MYIEDTCCBAkCAQEwgZMwfDELMAkGA1UEBhMCVVMx
# EzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoT
# FU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgUENBIDIwMTACEzMAAAG6Hz8Z98F1vXwAAQAAAbowDQYJYIZIAWUDBAIB
# BQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRABBDAvBgkqhkiG9w0BCQQx
# IgQgdLO9Gy2hL8lnrcjrboiiANgOTeqQpJwFgd2TBDgPg6EwgfoGCyqGSIb3DQEJ
# EAIvMYHqMIHnMIHkMIG9BCApVb08M25w+tYGWsmlGtp1gy1nPcqWfqgMF3nlWYVz
# BTCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# JjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABuh8/
# GffBdb18AAEAAAG6MCIEIMHkOGC427PqmUI7Oe7xVuezks+e+hMM+17Nfgn9Gbmw
# MA0GCSqGSIb3DQEBCwUABIICAIFN7B8ODjon7gHEAxYHHBVUBVg0Y9tPv3vWBrsC
# za9WVBxux/YF+0zPagWvfNAWQZBAEYijOjuiAeLoiZGIpOcgLchtYchNKTAsF2oC
# nXAy3YcTQNbwF5QwLNNoxHCbtQHx9/UHglfyvNEQP1d7UfpqoZo992OK75cP2hB2
# i0onLs9Btak3U4BOWPcqtPKnxS3vzyq8nAIHx3q6EuqjGuvKsrHm+HZeC2P7TSQR
# A9PIxQnglYe1D0RV0WkuCI31KbnlHJLfhxbUus4dR0JDwtMXXLIRdOEyLxOTiq+G
# iMarPvK2drAKiKfcYZwJCrcCm5QdYCmaqtY1sqxlS9hzTplSpifDKsnYOlfBArr3
# sKjQ2t4duJzGp6lay+HKe1TORfg0ZnUm9HFeh5APN971GBmUAWJ/R3jaZYA51LHs
# N10UtBLnF6wfSgUEoUApw44vliJijoTe74iEUSO5otDLNSuNdTcBhbDyp81AtXfO
# 11CGvpvqu1Rx4SKWNausXCJ2hqbu2m8M/rX4/ikvahCfDJ2KO6WlBI3JsY+aV7wB
# SpRlt4w2Pa9k8Xuws4RkRZ9W9k7s0D2xbq9cK2w6Lwby3gpUXboR10OEXqYdH01w
# v5oTIlfgJjqroKwWRKEYilZsiZV/K3X0Z9vIJDcT894tPOlyhLT6cBz6rcveNCvv
# QT3Q
# SIG # End signature block
