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
# -VmSize <size>
# -Region <region>
# -ArrAccountId
# -ArrAccountKey
# -MaxLeaseTime <MaxLeaseTime>

Param(
    [switch] $CreateSession,
    [switch] $GetSessionProperties,
    [switch] $GetSessions,
    [switch] $UpdateSession,
    [switch] $StopSession,
    [switch] $Poll,
    [string] $Id,
    [string] $ArrAccountId, #optional override for arrAccountId of accountSettings in config file
    [string] $ArrAccountKey, #optional override for arrAccountKey of accountSettings in config file
    [string] $Region, #optional override for region of accountSettings in config file
    [string] $VmSize, #optional override for vmSize of renderingSessionSettings in config file
    [string] $MaxLeaseTime, #optional override for naxLeaseTime of renderingSessionSettings in config file
    [string] $AuthenticationEndpoint,
    [string] $ServiceEndpoint,
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

$LoggedIn = CheckLogin
if (-Not $LoggedIn) {
    WriteError("User not logged in - Exiting.")
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
function GetSessions($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey) {
    try {
        $url = "$serviceEndpoint/accounts/$accountId/sessions?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
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
function UpdateSession($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $sessionId, [timespan] $maxLeaseTime) {
    try {
        $body =
        @{
            maxLeaseTimeMinutes = $maxLeaseTime.TotalMinutes -as [int];
        } | ConvertTo-Json
        $url = "$serviceEndpoint/accounts/$accountId/sessions/${sessionId}?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
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
function PollSessionStatus($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $sessionId) {
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

        $response = GetSessionProperties $authenticationEndpoint $serviceEndpoint $accountId $accountKey $sessionId
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
    -AuthenticationEndpoint $AuthenticationEndpoint `
    -ServiceEndpoint $ServiceEndpoint `
    -Region $Region `
    -VmSize $VmSize `
    -MaxLeaseTime $MaxLeaseTime

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

if (-Not ($GetSessionProperties -or $GetSessions -or $StopSession -or ($UpdateSession -and -Not $MaxLeaseTime))) {
    #to get session properties etc we do not need to have proper renderingsessionsettings
    #otherwise we need to check them
    $vmSettingsOkay = VerifyRenderingSessionSettings $config $defaultConfig
    if (-Not $vmSettingsOkay) {
        WriteError("renderSessionSettings not valid. please ensure valid renderSessionSettings in $ConfigFile or commandline parameters - Exiting.")
        exit 1
    }
}

# GetSessionProperties
if ($GetSessionProperties) {
    $sessionId = $Id
    if ([string]::IsNullOrEmpty($Id)) {
        $sessionId = Read-Host "Please enter Session Id"
    }
    if ($Poll) {
        PollSessionStatus -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId
    }
    else {
        $response = GetSessionProperties -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId

        $responseFromJson = ($response | ConvertFrom-Json)

        WriteInformation("Session Status:")
        WriteSessionProperties($responseFromJson)
    }
    exit
}

# GetSessions
if ($GetSessions) {
    GetSessions -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey
    exit
}

# StopSession
if ($StopSession) {
    $sessionId = $Id
    if ([string]::IsNullOrEmpty($Id)) {
        $sessionId = Read-Host "Please enter Session Id"
    }

    $null = StopSession -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId

    exit
}

#UpdateSession
if ($UpdateSession) {
    $sessionId = $Id
    if ([string]::IsNullOrEmpty($Id)) {
        $sessionId = Read-Host "Please enter Session Id"
    }
    $null = UpdateSession -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId -maxLeaseTime $config.renderingSessionSettings.maxLeaseTime

    exit
}

# Create a Session and Poll for Completion
$sessionId = CreateRenderingSession -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -vmSize $config.renderingSessionSettings.vmSize -maxLeaseTime $config.renderingSessionSettings.maxLeaseTime -AdditionalParameters $AdditionalParameters -sessionId $Id
if ($CreateSession -and ($false -eq $Poll)) {
    exit #do not poll if we asked to only create the session
}
PollSessionStatus -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId

# SIG # Begin signature block
# MIIrZAYJKoZIhvcNAQcCoIIrVTCCK1ECAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCD0p0jsj1n5j0gN
# bNW6Wiy8+gcD/k9jgkOCKhSlelbyb6CCEXkwggiJMIIHcaADAgECAhM2AAABqdaQ
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
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgrr5RvuPY5GJqXVz+
# WZFT4E5uVN3DMMi/ew4RAxpsAXYwQgYKKwYBBAGCNwIBDDE0MDKgFIASAE0AaQBj
# AHIAbwBzAG8AZgB0oRqAGGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbTANBgkqhkiG
# 9w0BAQEFAASCAQBXR/GCQsm3LzOC5Xbs5ihv4HAh0oVtNrsqO+qCovZ+/3C+Qo9U
# jsZEho0NQ9n1iKAoiDDQgIW3mT4PJjSzT5pazlclA0tThqMTQ690DsBa7fvwybAX
# fdDi9sFZrYnTJVOm1m9ldZd9qzCHeGIm9r3ty7V/BrEAj+JvcH6u+9InSr12i9Sw
# mDU6wCvGcWj5V4DjJoJcDY+fjSlCki8nGxeoFWutTH/w/D4xJ8pn3cylS8Cwet1V
# 76etttLMqe9QEYxgNBZ1IoGXg/mmYXuN3tuxp2zceC/JbF8IdaDbTlcsTPHhRfnX
# VHhx6FhPEmzi6ZH6IuUUBvU/q3x0xmOqaHT5oYIXCTCCFwUGCisGAQQBgjcDAwEx
# ghb1MIIW8QYJKoZIhvcNAQcCoIIW4jCCFt4CAQMxDzANBglghkgBZQMEAgEFADCC
# AVUGCyqGSIb3DQEJEAEEoIIBRASCAUAwggE8AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEINSjy+ryQVOkVCVn5uXVXbNhSXub7KeMz/b9fSTsuZ9hAgZj
# YsUW27gYEzIwMjIxMTA5MTUzMDQzLjg0NVowBIACAfSggdSkgdEwgc4xCzAJBgNV
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
# 9w0BCRABBDAvBgkqhkiG9w0BCQQxIgQg/6/kdGnKlF1UPwKawirloN6dND1r/SuU
# 1czu+gdjOsQwgfoGCyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCBWtQJDHFq8EeBz
# 3TXugCqRhSI/JCZbATYEIwTG8bMewDCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0
# YW1wIFBDQSAyMDEwAhMzAAABqqUxmwvLsggOAAEAAAGqMCIEIDq2mVUESOuFZBNl
# 3ixuczQIqJxaAcFfBATel3zqaQLVMA0GCSqGSIb3DQEBCwUABIICABn6FXZQM7hh
# h8f8JP+n1X85YxVEe55LJ1N5J5jOq0d+34wr438AgTeYXqZDIhf+dVB46/uUDpPn
# s1VHmUdc1qjS2ok93GAtpoZBWteBngwdfLa0pI1KNOCR3bwvrgKI9Kc7wfNXtLIv
# MQ6M5LoE1Fprhv3VhJQMmiKvvepljRSoAQmau6KUkr81P4M5Q5zA0mYxgB3owIQ3
# tiVUF7wmvVd8nb/2Ud8HNj8RRCTJH7oAPaNFLr00jkxKumm9UDf5RpTCVfTL7zvS
# xHsIcH1Gp5xrp0dbuFiIF5kzQMSsUN2YGPPzoxYeyUly9jKXna+C+BYkXM29efXJ
# NTERg6KTwy45u+zvOfnOir6KbBrv6+gD//LcEr2vASWonVAaFErXLjL3P4bBqMYW
# YDOZ3owQZwHwad/Qe4bJZD6T/nOx+169DhskXxssBuENlAVJ1B3016zLgq5EoD7G
# ac3Gyu2RKx7lvWzrRfSDN8aKdQIA0bsjtlRIKWJqfG1gp/HZkCxT/pPbr7xOVCIN
# lNqzw92Xq2KgjCZ8tGRq7+tevwZX5rserLGbcMZxGCzmE/jSw3zF2UR27BO5YCly
# uAX88enMkDUBpCK1I+xcYNqS/bxOtiztXWl4zodVHU7BNS5LasZwRIXjhmzz8dy/
# i4geBb8tXkoqfgZWKsaiJR4WCE9C03T4
# SIG # End signature block
