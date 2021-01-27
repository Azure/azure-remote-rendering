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
# MIInLQYJKoZIhvcNAQcCoIInHjCCJxoCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCDcz8g4gElADSs3
# wrkXbsA0+EiHRBFOlsHsMlv79Pi/KaCCEWkwggh7MIIHY6ADAgECAhM2AAABOgNw
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
# 6I1lUE5iixnoEqFKWTX5j/TLUjGCFRowghUWAgEBMFgwQTETMBEGCgmSJomT8ixk
# ARkWA0dCTDETMBEGCgmSJomT8ixkARkWA0FNRTEVMBMGA1UEAxMMQU1FIENTIENB
# IDAxAhM2AAABOgNwleIA4C4dAAEAAAE6MA0GCWCGSAFlAwQCAQUAoIGuMBkGCSqG
# SIb3DQEJAzEMBgorBgEEAYI3AgEEMBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3
# AgEVMC8GCSqGSIb3DQEJBDEiBCC87GG+13Q3cLNkQ8mYzaKA4xUjqnG77cIjEU5E
# irwejzBCBgorBgEEAYI3AgEMMTQwMqAUgBIATQBpAGMAcgBvAHMAbwBmAHShGoAY
# aHR0cDovL3d3dy5taWNyb3NvZnQuY29tMA0GCSqGSIb3DQEBAQUABIIBAK3lekV8
# vP7Uo9MKmSxXtexStXud7+dvTL2qVjgE/uaLYeH8bI36UYkbTtp+Al/RtEjaYEvu
# Mt9S+UWoeSBP2YUueK4z8KsTGwGLni79TtVDSE4cDCnMlNN2LsICK00/wf1vUkJE
# nnMeyJmNG0Zq3A3wmm2CGDI4IABTaAPq9FfMHy0850mgH8BYohuCykrYONo5EGFT
# HLnD1PK/iDLYn2gu5Jtws87cucP0ndAgK3TPdrfCDiOceCEpCeSxAaPXPxdimQHs
# M9tAv2m0ely9IBDETiahcM4WRW2aTVSQjX9mxtmqONLFMeUxGenScLpRCWl4z67B
# qL0V+Kyc1TGfC3WhghLiMIIS3gYKKwYBBAGCNwMDATGCEs4wghLKBgkqhkiG9w0B
# BwKgghK7MIIStwIBAzEPMA0GCWCGSAFlAwQCAQUAMIIBUQYLKoZIhvcNAQkQAQSg
# ggFABIIBPDCCATgCAQEGCisGAQQBhFkKAwEwMTANBglghkgBZQMEAgEFAAQgZFF7
# J9Mv4ejXJDN21InbhqML0lccd8vnp86+76G5KycCBmAGHgxnPhgTMjAyMTAxMjcx
# ODMyNDMuNDc2WjAEgAIB9KCB0KSBzTCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0
# aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046RUFDRS1FMzE2LUM5MUQxJTAj
# BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2Wggg45MIIE8TCCA9mg
# AwIBAgITMwAAAUzFTMHQ228/sgAAAAABTDANBgkqhkiG9w0BAQsFADB8MQswCQYD
# VQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEe
# MBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3Nv
# ZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDAeFw0yMDExMTIxODI2MDBaFw0yMjAyMTEx
# ODI2MDBaMIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUw
# IwYDVQQLExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1U
# aGFsZXMgVFNTIEVTTjpFQUNFLUUzMTYtQzkxRDElMCMGA1UEAxMcTWljcm9zb2Z0
# IFRpbWUtU3RhbXAgU2VydmljZTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoC
# ggEBAMphYFHDrMe576NV7IEKD/jk37xPiaTjee2zK3XP+qUJpBVMY2ICxaRRhy1C
# nyf/5vWRpn33Bk9xbGegnpbkoL880bNpSZ6uWcpzSgFBOdmNUrTBt96RWXaPY7kt
# UMBZEWviSf3yCV2IXgWYAQFuZ9ssQ9Ygjpo1pvUrtaoUwAjiaM436UCU9fW1D+kc
# EH05m4hucWbE8JW+O9b3bletiv78n+fC6oKk6aSQRRFL4OJiovS+ib175G6pSf9w
# DRk9X3kO661OtCcrHZAfwe2MHXDP4eZfGRksA/IvvrLFNcajI7It6Tx+onDyR5ig
# Ri+kCJoTG0YUGC1UMjCK05WtDrsCAwEAAaOCARswggEXMB0GA1UdDgQWBBQBlh6n
# BApe5yeVQgGA9BBH3mb6fDAfBgNVHSMEGDAWgBTVYzpcijGQ80N7fEYbxTNoWoVt
# VTBWBgNVHR8ETzBNMEugSaBHhkVodHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtp
# L2NybC9wcm9kdWN0cy9NaWNUaW1TdGFQQ0FfMjAxMC0wNy0wMS5jcmwwWgYIKwYB
# BQUHAQEETjBMMEoGCCsGAQUFBzAChj5odHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20v
# cGtpL2NlcnRzL01pY1RpbVN0YVBDQV8yMDEwLTA3LTAxLmNydDAMBgNVHRMBAf8E
# AjAAMBMGA1UdJQQMMAoGCCsGAQUFBwMIMA0GCSqGSIb3DQEBCwUAA4IBAQBPBOSw
# 99ZDrqiAYq9362Z3HYhBhoSXvMeICG9xw7rlp8hAtmiSHPIAcM74xkfYZndBf1ZQ
# 5unU5YmV+/PG/Qu7NX8ZKgkcsNW8UPAnVbTpR+vNmf//kXdiDJP3b8U7nMzZ05pe
# RKMV4vUOEYD6+ww8HNSSBEjRVfaESBLZ3opjPoxzayaop+WXU5ZWtloml3oLrnum
# 1sicTVqw30mM2jY/wJJH/bK4bTRzzv7t7n18gB/+XC/YR/j2+tIuntj0xL0QUFG0
# XuBAL+6zLSCtJR36q0hP/77Zsk0txL95mNcrRfRQJy4xT5lkGIZXbAyEQg51BG5a
# omVO/1+05vrtz8prMIIGcTCCBFmgAwIBAgIKYQmBKgAAAAAAAjANBgkqhkiG9w0B
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
# vM9YBS7vDaBQNdrvCScc1bN+NR4Iuto229Nfj950iEkSoYICyzCCAjQCAQEwgfih
# gdCkgc0wgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYD
# VQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAj
# BgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRo
# YWxlcyBUU1MgRVNOOkVBQ0UtRTMxNi1DOTFEMSUwIwYDVQQDExxNaWNyb3NvZnQg
# VGltZS1TdGFtcCBTZXJ2aWNloiMKAQEwBwYFKw4DAhoDFQA9mVtOCSgTYnYdGM1j
# KASXGuD3oKCBgzCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5n
# dG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9y
# YXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMA0G
# CSqGSIb3DQEBBQUAAgUA47vRETAiGA8yMDIxMDEyNzE5NDYyNVoYDzIwMjEwMTI4
# MTk0NjI1WjB0MDoGCisGAQQBhFkKBAExLDAqMAoCBQDju9ERAgEAMAcCAQACAgMg
# MAcCAQACAhJPMAoCBQDjvSKRAgEAMDYGCisGAQQBhFkKBAIxKDAmMAwGCisGAQQB
# hFkKAwKgCjAIAgEAAgMHoSChCjAIAgEAAgMBhqAwDQYJKoZIhvcNAQEFBQADgYEA
# PxJ6GAwyh0xYrQdW6Xv5LiW4oKv7tyCRNsuvH8ZOdHnABe39xHjv0oWtYHax2BgU
# siyydwFk4cUMYdQV2mKMakL1WAiUFmRU2P0cCDjlByAYiDgGgY5ohgc3zycp9ygQ
# hVtzn8E6n/cRmhZgj74u85SKCTHfYOKQYkbswwAMjQsxggMNMIIDCQIBATCBkzB8
# MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVk
# bW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1N
# aWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAUzFTMHQ228/sgAAAAAB
# TDANBglghkgBZQMEAgEFAKCCAUowGgYJKoZIhvcNAQkDMQ0GCyqGSIb3DQEJEAEE
# MC8GCSqGSIb3DQEJBDEiBCBaeUFVm9dzoo47q8sJ4JZQKtCXcIgBEpyuwtOzuNuA
# sTCB+gYLKoZIhvcNAQkQAi8xgeowgecwgeQwgb0EINvCpbu/UEsy0RBMIOH6Twst
# hlN90/tz2a8QYmfEr04lMIGYMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENB
# IDIwMTACEzMAAAFMxUzB0NtvP7IAAAAAAUwwIgQgz9pJXpqwPl6yiZHtUqULQ4rX
# Nqiiq4O3Nvd9LcQts4EwDQYJKoZIhvcNAQELBQAEggEAWNRrWEUlSSXYFw1WQyiv
# Nv1DfpaEElGPt0CMgxGDp08ZTVQbToSZHkOfaoZKfQY+N1s3dABhqmiyspQux0rO
# 6GvXPxNAxsNeXlwc424tun2ql/1uhk2UCSovve8Cj5F+e4nu8OR6DwBccMPpC9YO
# EhGMYipHVzsGBh/TDN5Hr5/YrHMJ3dAvrRoKNDJNBZfw1+pYk1ZAVlLuAFRfGMwm
# Of8CsaXW3bGCu9+TaRy3sgx/D98o1xrLlEc6L27Tin8I6N7CEN6sAKlwhOpu8PSV
# 7I8DjQZIt/kdl0HVBUpMKIWwEIBF2YzvlZB163y276+d7Bm8ZoGdT8+U5ewaUAFY
# JA==
# SIG # End signature block
