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
# Create a Session by calling REST API <endpoint>/v1/accounts/<accountId>/sessions/create/
# returns a session GUID which can be used to retrieve session status
function CreateRenderingSession($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $vmSize = "standard", $directConnectionSession = "false", $maxLeaseTime = "4:0:0", $additionalParameters) {
    try {
        $body =
        @{
            # defaults to 4 Hours
            maxLeaseTime = $maxLeaseTime;
            # defaults to "standard"
            size         = $vmSize;
            # defaults to "false"
            directConnectionSession = $directConnectionSession;
        }

        if ($additionalParameters) {
            $additionalParameters.Keys | % { $body += @{ $_ = $additionalParameters.Item($_) } }
        }

        $url = "$serviceEndpoint/v1/accounts/$accountId/sessions/create/"

        WriteInformation("Creating Rendering Session ...")
        WriteInformation("  Authentication endpoint: $authenticationEndpoint")
        WriteInformation("  Service endpoint: $serviceEndpoint")
        WriteInformation("  maxLeaseTime: $maxLeaseTime")
        WriteInformation("  size: $vmSize")
        WriteInformation("  directConnectionSession: $directConnectionSession")
        WriteInformation("  additionalParameters: $($additionalParameters | ConvertTo-Json)")

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey

        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method POST -ContentType "application/json" -Body ($body | ConvertTo-Json) -Headers @{ Authorization = "Bearer $token" }

        $sessionId = (GetResponseBody($response)).SessionId
        WriteSuccess("Successfully created the session with Id: $sessionId")
        WriteSuccessResponse($response.RawContent)

        return $sessionId
    }
    catch {
        WriteError("Unable to start the rendering session ...")
        HandleException($_.Exception)
        throw
    }
}

#call REST API <endpoint>/v1/accounts/<accountId>/sessions/<SessionId>/properties/ 
function GetSessionProperties($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $SessionId) {
    try {
        $url = "$serviceEndpoint/v1/accounts/$accountId/sessions/${SessionId}/properties/"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method GET -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

        WriteSuccessResponse($response.RawContent)

        return $response
    }
    catch {
        WriteError("Unable to get the status of the session with Id: $sessionId")
        HandleException($_.Exception)
        throw
    }
}

#call REST API <endpoint>/v1/accounts/<accountId>/sessions/
function GetSessions($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $SessionId) {
    try {
        $url = "$serviceEndpoint/v1/accounts/$accountId/sessions/"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method GET -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

        if ($response.StatusCode -eq 200) {
            Write-Host -ForegroundColor Green "********************************************************************************************************************";

            $responseFromJson = ($response | ConvertFrom-Json)

            WriteSuccessResponse("Currently there are $($responseFromJson.sessions.Length) sessions:")

            foreach ($session in $responseFromJson.sessions) {
                WriteInformation("    sessionId:           $($session.sessionId)")
                WriteInformation("    message:             $($session.message)")
                WriteInformation("    sessionElapsedTime:  $($session.sessionElapsedTime)")
                WriteInformation("    sessionHostname:     $($session.sessionHostname)")
                WriteInformation("    arrInspectorPort:    $($session.arrInspectorPort)")
                WriteInformation("    handshakePort:       $($session.handshakePort)")
                WriteInformation("    sessionMaxLeaseTime: $($session.sessionMaxLeaseTime)")
                WriteInformation("    sessionSize:         $($session.sessionSize)")
                WriteInformation("    sessionStatus:       $($session.sessionStatus)")          
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

#call REST API <endpoint>/v1/accounts/<accountId>/sessions/<SessionId>/ with PATCH to updat a session
# currently only updates the leaseTime
# $MaxLeaseTime has to be strictly larger than the existing lease time of the session
function UpdateSession($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $SessionId, $MaxLeaseTime) {
    try {
        $body =
        @{
            maxLeaseTime = $MaxLeaseTime;
        } | ConvertTo-Json
        $url = "$serviceEndpoint/v1/accounts/$accountId/sessions/${SessionId}" 

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method PATCH -ContentType "application/json" -Body $body -Headers @{ Authorization = "Bearer $token" }

        WriteSuccessResponse($response.RawContent)

        return $response
    }
    catch {
        WriteError("Unable to get the status of the session with Id: $sessionId")
        HandleException($_.Exception)
        throw
    }
}


# call "<endPoint>/v1/accounts/<accountId>/sessions/<SessionId>" with Method DELETE to stop a session
function StopSession($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $SessionId) {
    try {
        $url = "$serviceEndpoint/v1/accounts/$accountId/sessions/${SessionId}"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method DELETE -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

        WriteSuccessResponse($response.RawContent)

        return $response
    }
    catch {
        WriteError("Unable to stop session with Id: $sessionId")
        HandleException($_.Exception)
        throw
    }
}

# retrieves GetSessionProperties until error or ready status of rendering session are achieved
function PollSessionStatus($authenticationEndpoint, $serviceEndpoint, $accountId, $accountKey, $SessionId) {
    $sessionStatus = "Starting"
    $sessionProperties = $null
    $sessionProgress = 0
    $startTime = $(Get-Date)

    WriteInformation("Provisioning a VM for rendering session '$SessionId' ...")

    while ($true) {
        WriteProgress -Activity "Preparing VM for rendering session '$SessionId' ..." -Status: "Preparing for $($sessionProgress * 10) Seconds"

        $response = GetSessionProperties $authenticationEndpoint $serviceEndpoint $accountId $accountKey $SessionId
        $responseContent = $response.Content | ConvertFrom-Json
        $sessionProperties =
        @{
            Message         = $responseContent.message;
            SessionHostname = $responseContent.sessionHostname;
            SessionStatus   = $responseContent.sessionStatus;
        }

        $sessionStatus = $sessionProperties.SessionStatus
        if ("ready" -eq $sessionStatus.ToLower() -or "error" -eq $sessionStatus.ToLower()) {
            break
        }
        Start-Sleep -Seconds 10
        $sessionProgress++
    }

    $totalTimeElapsed = $(New-TimeSpan $startTime $(get-date)).TotalSeconds
    if ("ready" -eq $sessionStatus.ToLower()) {
		WriteInformation ("")
		Write-Host -ForegroundColor Green "Session is ready.";
		WriteInformation ("")
        WriteInformation ("SessionId: $SessionId")
		WriteInformation ("Time elapsed: $totalTimeElapsed (sec)")
		WriteInformation ("")
		WriteInformation ("Response details:")
        WriteInformation($response)
        return $sessionProperties
    }

    if ("error" -eq $sessionStatus.ToLower()) {
        WriteInformation ("The attempt to create a new session resulted in an error.")
        WriteInformation ("SessionId: $SessionId")
        WriteInformation ("Time elapsed: $totalTimeElapsed (sec)")
        WriteInformation($response)
        exit 1
    }

    if ("expired" -eq $sessionStatus.ToLower()) {
        WriteInformation ("The attempt to create a new session expired before it became ready. Check the settings in your configuration (arrconfig.json).")
        WriteInformation ("SessionId: $SessionId")
        WriteInformation ("Time elapsed: $totalTimeElapsed (sec)")
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
        GetSessionProperties -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId
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
    StopSession -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId
    
    exit
}

#UpdateSession
if ($UpdateSession) {
    $sessionId = $Id
    if ([string]::IsNullOrEmpty($Id)) {
        $sessionId = Read-Host "Please enter Session Id"
    }
    UpdateSession -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId -maxLeaseTime $config.renderingSessionSettings.maxLeaseTime
    
    exit
}

# Create a Session and Poll for Completion
$sessionId = $sessionId = CreateRenderingSession -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -vmSize $config.renderingSessionSettings.vmSize -maxLeaseTime $config.renderingSessionSettings.maxLeaseTime -AdditionalParameters $AdditionalParameters
if ($CreateSession -and ($false -eq $Poll)) {
    exit #do not poll if we asked to only create the session 
}
PollSessionStatus -authenticationEndpoint $config.accountSettings.authenticationEndpoint -serviceEndpoint $config.accountSettings.serviceEndpoint -accountId $config.accountSettings.arrAccountId -accountKey $config.accountSettings.arrAccountKey -SessionId $sessionId

# SIG # Begin signature block
# MIInLAYJKoZIhvcNAQcCoIInHTCCJxkCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCDcz8g4gElADSs3
# wrkXbsA0+EiHRBFOlsHsMlv79Pi/KaCCEWUwggh3MIIHX6ADAgECAhM2AAABOXjG
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
# TmKLGegSoUpZNfmP9MtSMYIVHTCCFRkCAQEwWDBBMRMwEQYKCZImiZPyLGQBGRYD
# R0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxBTUUgQ1MgQ0EgMDEC
# EzYAAAE5eMY59eV3J+oAAQAAATkwDQYJYIZIAWUDBAIBBQCgga4wGQYJKoZIhvcN
# AQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisGAQQBgjcCARUw
# LwYJKoZIhvcNAQkEMSIEILzsYb7XdDdws2RDyZjNooDjFSOqcbvtwiMRTkSKvB6P
# MEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8AcwBvAGYAdKEagBhodHRw
# Oi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEABmcaIikXX8X/
# PpmD5cMfCd0vkYpah2Em2zKOj7VQvnmrQr2Zpe+9ZHTdi+4D6rHPDm5VYQ/IeDRA
# WzVnjKZQuia5E18sEnQL2meSLwMm2jY4sP+b11hAivfCzNcmiaYwv6OvapIf34b/
# IFo5kdezsUyyo7KuZhoatFaYl6JDbHi3YOMOLYYOIeg9vRmX2MwYoFubVgawOQyP
# mf1/D/SCdc9rykINoa/CEuV//AN2jM3+ruzojwsS2AnxfvL2OosYRpDasEEf+CdV
# 4HeKAGVG0iTiB2rtVPxTg8BErgXZIff8yLfLFmMOPcsmaGA/LFlnAq5HBfwzCe+4
# M0d9Drx1faGCEuUwghLhBgorBgEEAYI3AwMBMYIS0TCCEs0GCSqGSIb3DQEHAqCC
# Er4wghK6AgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFRBgsqhkiG9w0BCRABBKCCAUAE
# ggE8MIIBOAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFlAwQCAQUABCDEv0EOzc08
# k8/LgiXH1J65VqEf+g+x3uPF1XizPEdw3wIGYBiDUwFzGBMyMDIxMDIxMDE0NDAw
# My44NTlaMASAAgH0oIHQpIHNMIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2Fz
# aGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENv
# cnBvcmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25z
# MSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjo3QkYxLUUzRUEtQjgwODElMCMGA1UE
# AxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaCCDjwwggTxMIID2aADAgEC
# AhMzAAABUcNQ51lsqsanAAAAAAFRMA0GCSqGSIb3DQEBCwUAMHwxCzAJBgNVBAYT
# AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYD
# VQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBU
# aW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIwMTExMjE4MjYwNFoXDTIyMDIxMTE4MjYw
# NFowgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
# EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNV
# BAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRoYWxl
# cyBUU1MgRVNOOjdCRjEtRTNFQS1CODA4MSUwIwYDVQQDExxNaWNyb3NvZnQgVGlt
# ZS1TdGFtcCBTZXJ2aWNlMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA
# n9KH76qErjvvOIkjWbHptMkYDjmG+JEmzguyr/VxjZgZ/ig8Mk47jqSJP5RxH/sD
# yqhYu7jPSO86siZh8u7DBX9L8I+AB+8fPPvD4uoLKD22BpoFl4B8Fw5K7Suibvbx
# GN7adL1/zW+sWXlVvpDhEPIKDICvEdNjGTLhktfftjefg9lumBMUBJ2G4/g4ad0d
# DvRNmKiMZXXe/Ll4Qg/oPSzXCUEYoSSqa5D+5MRimVe5/YTLj0jVr8iF45V0hT7V
# H8OJO4YImcnZhq6Dw1G+w6ACRGePFmOWqW8tEZ13SMmOquJrTkwyy8zyNtVttJAX
# 7diFLbR0SvMlbJZWK0KHdwIDAQABo4IBGzCCARcwHQYDVR0OBBYEFMV3/+NoUGKT
# NGg6OMyE6fN1ROptMB8GA1UdIwQYMBaAFNVjOlyKMZDzQ3t8RhvFM2hahW1VMFYG
# A1UdHwRPME0wS6BJoEeGRWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3Js
# L3Byb2R1Y3RzL01pY1RpbVN0YVBDQV8yMDEwLTA3LTAxLmNybDBaBggrBgEFBQcB
# AQROMEwwSgYIKwYBBQUHMAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kv
# Y2VydHMvTWljVGltU3RhUENBXzIwMTAtMDctMDEuY3J0MAwGA1UdEwEB/wQCMAAw
# EwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZIhvcNAQELBQADggEBACv99cAVg5nx
# 0SqjvLfQzmugMj5cJ9NE60duSH1LpxHYim9Ls3UfiYd7t0JvyEw/rRTEKHbznV6L
# FLlX++lHJMGKzZnHtTe2OI6ZHFnNiFhtgyWuYDJrm7KQykNi1G1LbuVie9MehmoK
# +hBiZnnrcfZSnBSokrvO2QEWHC1xnZ5wM82UEjprFYOkchU+6RcoCjjmIFGfgSzN
# j1MIbf4lcJ5FoV1Mg6FwF45CijOXHVXrzkisMZ9puDpFjjEV6TAY6INgMkhLev/A
# Vow0sF8MfQztJIlFYdFEkZ5NF/IyzoC2Yb9iw4bCKdBrdD3As6mvoGSNjCC6lOdz
# 6EerJK3NhFgwggZxMIIEWaADAgECAgphCYEqAAAAAAACMA0GCSqGSIb3DQEBCwUA
# MIGIMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMTIwMAYDVQQD
# EylNaWNyb3NvZnQgUm9vdCBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkgMjAxMDAeFw0x
# MDA3MDEyMTM2NTVaFw0yNTA3MDEyMTQ2NTVaMHwxCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1w
# IFBDQSAyMDEwMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAqR0NvHcR
# ijog7PwTl/X6f2mUa3RUENWlCgCChfvtfGhLLF/Fw+Vhwna3PmYrW/AVUycEMR9B
# GxqVHc4JE458YTBZsTBED/FgiIRUQwzXTbg4CLNC3ZOs1nMwVyaCo0UN0Or1R4HN
# vyRgMlhgRvJYR4YyhB50YWeRX4FUsc+TTJLBxKZd0WETbijGGvmGgLvfYfxGwScd
# JGcSchohiq9LZIlQYrFd/XcfPfBXday9ikJNQFHRD5wGPmd/9WbAA5ZEfu/QS/1u
# 5ZrKsajyeioKMfDaTgaRtogINeh4HLDpmc085y9Euqf03GS9pAHBIAmTeM38vMDJ
# RF1eFpwBBU8iTQIDAQABo4IB5jCCAeIwEAYJKwYBBAGCNxUBBAMCAQAwHQYDVR0O
# BBYEFNVjOlyKMZDzQ3t8RhvFM2hahW1VMBkGCSsGAQQBgjcUAgQMHgoAUwB1AGIA
# QwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTADAQH/MB8GA1UdIwQYMBaAFNX2
# VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0wS6BJoEeGRWh0dHA6Ly9jcmwu
# bWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01pY1Jvb0NlckF1dF8yMDEw
# LTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYBBQUHMAKGPmh0dHA6Ly93
# d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9vQ2VyQXV0XzIwMTAtMDYt
# MjMuY3J0MIGgBgNVHSABAf8EgZUwgZIwgY8GCSsGAQQBgjcuAzCBgTA9BggrBgEF
# BQcCARYxaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL1BLSS9kb2NzL0NQUy9kZWZh
# dWx0Lmh0bTBABggrBgEFBQcCAjA0HjIgHQBMAGUAZwBhAGwAXwBQAG8AbABpAGMA
# eQBfAFMAdABhAHQAZQBtAGUAbgB0AC4gHTANBgkqhkiG9w0BAQsFAAOCAgEAB+aI
# UQ3ixuCYP4FxAz2do6Ehb7Prpsz1Mb7PBeKp/vpXbRkws8LFZslq3/Xn8Hi9x6ie
# JeP5vO1rVFcIK1GCRBL7uVOMzPRgEop2zEBAQZvcXBf/XPleFzWYJFZLdO9CEMiv
# v3/Gf/I3fVo/HPKZeUqRUgCvOA8X9S95gWXZqbVr5MfO9sp6AG9LMEQkIjzP7QOl
# lo9ZKby2/QThcJ8ySif9Va8v/rbljjO7Yl+a21dA6fHOmWaQjP9qYn/dxUoLkSbi
# OewZSnFjnXshbcOco6I8+n99lmqQeKZt0uGc+R38ONiU9MalCpaGpL2eGq4EQoO4
# tYCbIjggtSXlZOz39L9+Y1klD3ouOVd2onGqBooPiRa6YacRy5rYDkeagMXQzafQ
# 732D8OE7cQnfXXSYIghh2rBQHm+98eEA3+cxB6STOvdlR3jo+KhIq/fecn5ha293
# qYHLpwmsObvsxsvYgrRyzR30uIUBHoD7G4kqVDmyW9rIDVWZeodzOwjmmC3qjeAz
# LhIp9cAvVCch98isTtoouLGp25ayp0Kiyc8ZQU3ghvkqmqMRZjDTu3QyS99je/WZ
# ii8bxyGvWbWu3EQ8l1Bx16HSxVXjad5XwdHeMMD9zOZN+w2/XU/pnR4ZOC+8z1gF
# Lu8NoFA12u8JJxzVs341Hgi62jbb01+P3nSISRKhggLOMIICNwIBATCB+KGB0KSB
# zTCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcT
# B1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UE
# CxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0aW9uczEmMCQGA1UECxMdVGhhbGVz
# IFRTUyBFU046N0JGMS1FM0VBLUI4MDgxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1l
# LVN0YW1wIFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVAKCir3PxP6RCCyVMJSAVoMV6
# 1yNeoIGDMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24x
# EDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
# bjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwDQYJKoZI
# hvcNAQEFBQACBQDjzjZBMCIYDzIwMjEwMjEwMTgzODU3WhgPMjAyMTAyMTExODM4
# NTdaMHcwPQYKKwYBBAGEWQoEATEvMC0wCgIFAOPONkECAQAwCgIBAAICC9ECAf8w
# BwIBAAICEbwwCgIFAOPPh8ECAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGE
# WQoDAqAKMAgCAQACAwehIKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQDT
# g+3VyKt7oN2kQ6EttIcAG7IjYSAyI7b7mR+CHGpKkGIOSvhoCmadHVYIOwzYam5+
# CBt23KND2pSdw7muK2Qn+LZ2Qw8BWkPndlvc56cUXrY50fsCu0Hv74AAR4yCXsQ5
# YVrvZnqevT7vT1H4m2AAyGWcjAE1RsuUgaJInKkVDzGCAw0wggMJAgEBMIGTMHwx
# CzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRt
# b25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1p
# Y3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABUcNQ51lsqsanAAAAAAFR
# MA0GCWCGSAFlAwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQw
# LwYJKoZIhvcNAQkEMSIEIG9fFvVJ5SM9j4mEU4VaZWGs7wjVx/ehPdFbdUmuST9i
# MIH6BgsqhkiG9w0BCRACLzGB6jCB5zCB5DCBvQQgLs1cmYj41sFZBwCmFvv9ScP5
# tuuUhxsv/t0B9XF65UEwgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0Eg
# MjAxMAITMwAAAVHDUOdZbKrGpwAAAAABUTAiBCCbckIA6FZlZ0tTZcJPfnjtTZP3
# wie3lw1FASs2NGsTJDANBgkqhkiG9w0BAQsFAASCAQAEvT9637Zw3gwQFctxVaa8
# siFH93S+I+UQiT49JPPtprio2hcMvBoEfbAORssxU0T2DYU3LpREdCWFAtxuF80j
# qp7+q353xAGnYDSkhEJk2BhIYzHWPEtRtdgpmAaxUsNoS8Ec4dlOj9VSD7pzhIj6
# glaB5/Rfn6sO9Gci77UyFAe32q4EE9FlleM5uxmptjr8rnqck3dLBx1OLnjKBTBL
# JNtOllp+1pAxI461v1C7Q2EN51iFssRe6M1XQNavCJz95Hbnua5ct+JLSI0u3xtU
# kZk6hCftVIzAu03FOfSbxLRHtRqWLZrwed8isf9BEZr1+sH9e/JCkuJF9tJ6R+O3
# SIG # End signature block
