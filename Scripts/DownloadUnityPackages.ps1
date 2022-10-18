Param(
    [string] $DownloadDestDir = (Join-Path $PSScriptRoot "/../Unity/MRPackages"),
    [string] $DependenciesJson = (Join-Path $PSScriptRoot "unity_sample_dependencies.json"),
    [switch] $Verbose
)

. "$PSScriptRoot\ARRUtils.ps1" #include ARRUtils for Logging

### Globals ###
$success = $True

### Functions ###
function NormalizePath([string] $path) {
    if (-not [System.IO.Path]::IsPathRooted($path)) {
        $wd = Get-Location
        $joined = Join-Path $wd $path
        $path = ([System.IO.Path]::GetFullPath($joined))
    }
    return $path
}

function GetPackageDownloadDetails([string] $registry, [string] $package, [string] $version = "latest") {
    $details = $null
    $uri = "$registry/$package"
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri
        $json = $response | ConvertFrom-Json

        if ($version -like "latest") {
            $version = $json.'dist-tags'.latest
        }

        $versionInfo = $json.versions.($version)
        if ($null -eq $versionInfo) {
            WriteError "No info found for version $version of package $package."
        }
        $details = $json.versions.($version).dist
    }
    catch {
        WriteError "Web request to $uri failed."
        WriteError $_
    }
    return $details
}

$DownloadFile = {
    param([string] $url, [string] $dest, [bool] $verbose)
    $retries = 1
    while ($retries -le 3) {
        try {
            $responseStream = $null
            $destStream = $null
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $uri = New-Object "System.Uri" "$url"
            $request = [System.Net.HttpWebRequest]::Create($uri)
        
            $response = $request.GetResponse()
            $responseStream = $response.GetResponseStream()
            $contentLength = $response.get_ContentLength()
        
            $destStream = New-Object -TypeName System.IO.FileStream -ArgumentList $dest, Create
            $buf = new-object byte[] 16KB
        
            $srcFileName = [System.IO.Path]::GetFileName($url)
            
            $lastLogPercentage = 0
            $readBytesTotal = $readBytes = $responseStream.Read($buf, 0, $buf.length)
            while ($readBytes -gt 0) {
                if ( $($sw.Elapsed.TotalMinutes) -gt 5) {
                    throw "Timeout"
                }
                $destStream.Write($buf, 0, $readBytes)
                $readBytes = $responseStream.Read($buf, 0, $buf.length)
                $readBytesTotal = $readBytesTotal + $readBytes
                $percentComplete = (($readBytesTotal / $contentLength) * 100)
                $status = "Downloaded {0:N2} MB of  {1:N2} MB (Attempt $retries)" -f ($readBytesTotal / 1MB), ($contentLength / 1MB)
                Write-Progress -Activity "Downloading '$($srcFileName)'" -Status $status -PercentComplete $percentComplete
                
                if ($verbose -and ($percentComplete - $lastLogPercentage -gt 5)) {
                    $lastLogPercentage = $percentComplete
                    Write-Host "DL (Attempt $retries) `"$($srcFileName)`" at $([math]::round($percentComplete,2))%"
                }
            }
            Write-Progress -Completed -Activity "Finished downloading '$($srcFileName)'"
            return
        }
        catch {
            Write-Host "Error downloading $url to $dest"
            Write-Host "$_"
            $retries += 1
        }
        finally {
            if ($null -ne $destStream) {
                $destStream.Flush()
                $destStream.Close()
                $destStream.Dispose()
                $destStream = $null
            }
            if ($null -ne $responseStream) {
                $responseStream.Dispose()
                $responseStream = $null
            }
        }
    }
}

function WriteJobProgress {
    param($job)
    # Make sure the first child job exists
    if ($null -ne $job.ChildJobs[0]) {
        # Extracts the latest progress of the job and writes the progress
        $jobProgressHistory = $job.ChildJobs[0].Progress;

        if ($jobProgressHistory.count -gt 0) {
            $latest = $jobProgressHistory | select-object -last 1
            $latestActivity = $latest | Select-Object -expand Activity;
            $latestStatus = $latest | Select-Object -expand StatusDescription;
            $latestPercentComplete = $latest | Select-Object -expand PercentComplete;

            if ($latest.RecordType -eq 'Completed') {
                Write-Progress -ParentId 0 -Completed -Id $job.Id -Activity $latestActivity
            }
            else {
                Write-Progress -ParentId 0 -Id $job.Id -Activity $latestActivity -Status $latestStatus -PercentComplete $latestPercentComplete;
            }
        }
    }
}

### Main script ###
$DownloadDestDir = NormalizePath($DownloadDestDir)
$DependenciesJson = NormalizePath($DependenciesJson)

if (-not (Test-Path $DownloadDestDir -PathType Container)) {
    New-Item -Path $DownloadDestDir -ItemType Directory | Out-Null
}

# supress progress bar for API requests
$oldProgressPreference = $ProgressPreference
$ProgressPreference = 'SilentlyContinue'


$packages = ((Get-Content $DependenciesJson) | ConvertFrom-Json).packages | ForEach-Object {
    $package = $_

    $details = GetPackageDownloadDetails $package.registry $package.name $package.version
    if ($null -eq $details) {
        WriteError "Failed to get details for $($package.name)@$($package.version) from $($package.registry)."
        $success = $False
        return
    }
    
    $packageUrl = $details.tarball
    $fileName = [System.IO.Path]::GetFileName($packageUrl)
    $downloadPath = Join-Path $DownloadDestDir $fileName
    $downloadPath = [System.IO.Path]::GetFullPath($downloadPath)

    [PSCustomObject]@{
        Name         = $package.name
        Version      = $package.version
        Registry     = $package.registry
        Url          = $packageUrl
        Sha1sum      = $details.shasum
        DownloadPath = $downloadPath
    }
}

# restore progress preference
$ProgressPreference = $oldProgressPreference

# Create jobs for missing or broken files
$jobs = @()
foreach ($package in $packages) {

    if ((Test-Path $package.DownloadPath -PathType Leaf)) {
        $packageShasum = $package.Sha1sum.ToLower()
        $localShasum = (Get-FileHash -Algorithm SHA1 $package.DownloadPath).Hash.ToLower()
        if ($packageShasum -like $localShasum) {
            WriteInformation "$($package.DownloadPath) already exists. Skipping download."
            continue
        }

        WriteError "SHA1 hashes for $($package.Name)@$($package.Version) and $($package.DownloadPath) do not match."
        WriteError "$packageShasum != $localShasum"
        WriteError "Deleting file and re-downloading."
        Remove-Item $package.DownloadPath -Force
    }

    WriteInformation "Downloading $($package.Name)@$($package.Version) from $($package.Url)."
    $jobs += Start-Job -ScriptBlock $DownloadFile -ArgumentList @($package.Url, $package.DownloadPath, $Verbose) -Name $($package.Name)
    $progressId++
}

# Start jobs
if ($jobs.Count -gt 0) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -milliseconds 250
    
        $jobsProgress = $jobs | Select-Object Id, State, @{ N = 'RecordType'; E = { $_.ChildJobs[0].Progress | Select-Object -Last 1 -Exp RecordType } }
        $running = @( $jobsProgress | Where-Object { $_.State -eq 'Running' -or $_.RecordType -ne 'Completed' } )
        $completedCount = ($jobs.Count - $running.Count)
    
        $complete = (($completedCount / $jobs.Count) * 100)
        $status = "$($completedCount) / $($jobs.Count) Completed"
        Write-Progress -Id 0 -Activity "Downloading Packages" -Status $status -PercentComplete $complete

        if ($Verbose -and $($sw.Elapsed.TotalSeconds) -gt 30) {
            Write-Host "Periodic job status:"
            $jobs | Format-Table -AutoSize
            $jobs | Receive-Job
            $sw.Restart()
        } 

        $jobs | ForEach-Object { WriteJobProgress($_) }
    }
    while ( $completedCount -lt $jobs.Count )
    $jobs | Receive-Job
    Write-Progress -Completed -Id 0 -Activity "Downloading Packages"
    
    foreach ($job in $jobs) {
        #Propagate messages and errors
        if ($null -ne $job.ChildJobs[0]) {
            $Job.ChildJobs[0].Information | Foreach-Object { WriteInformation $_ }
            $Job.ChildJobs[0].Error | Foreach-Object { WriteError $_ }
        }
    }

    $jobs | Remove-Job
}

# Verify download integrity
foreach ($package in $packages) {        
    $packageShasum = $package.Sha1sum.ToLower()
    if (Test-Path $package.DownloadPath -PathType Leaf) {
        $localShasum = (Get-FileHash -Algorithm SHA1 $package.DownloadPath).Hash.ToLower()
        if (-not ($packageShasum -like $localShasum)) {
            WriteError "SHA1 hashes for $($package.Name)@$($package.Version) and $($package.DownloadPath) do not match."
            WriteError "$packageShasum != $localShasum"
            $success = $False
        }
    }
    else {
        WriteError "Download failed for $($package.Name)@$($package.Version). File not found: $($package.DownloadPath)."
        $success = $False
    }
}

if ($success) {
    WriteSuccess "Downloading dependency packages succeeded."
}
else {
    WriteError "Downloading dependency packages failed."
    exit 1
}

# SIG # Begin signature block
# MIIrUAYJKoZIhvcNAQcCoIIrQTCCKz0CAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBmJjInk+EC+y1D
# 6SiqSDioNAuEjjHCTuF7ZI+Y2dLErqCCEW4wggh+MIIHZqADAgECAhM2AAABqFMr
# 1lCrrLlTAAIAAAGoMA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMjA2MTAxODI3MDNaFw0yMzA2MTAxODI3MDNaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQCcR14fE/zz1vuavAvRnSShaXgugn7N4cntQOaFpADHJEgjD21dIr3XRT+l
# OV7aV9u8v29lHVTzPJHDLpZpU1P+uH7A6b0AqquxtoGqUKxNGWIP9MiCJvIO4oh/
# 9pX4CiTXK+hZWDZ42f/TpcRtcD+Tnnj8zNEXDEck2c710ZzyGR75RnFv3gBkHolZ
# 9dDfHNhhC09CZWxMjaJD9oNiSRL6ciCz8iNsKieEH3dXihzisvyv0LkPb2QM5lFD
# Zjb/V6pdXnBvtBJ1qGnGTfjntx9xpMaybPqc9CrsOo71GoM7l1gPbZXlsZCR/nAy
# ELSOVCrWoUCFsPeeReRrl4Suu7NzAgMBAAGjggWKMIIFhjApBgkrBgEEAYI3FQoE
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
# aG9yaXR5MB0GA1UdDgQWBBRRyFv7WPuHGFUtB2tMHCxFXGqL8jAOBgNVHQ8BAf8E
# BAMCB4AwRQYDVR0RBD4wPKQ6MDgxHjAcBgNVBAsTFU1pY3Jvc29mdCBDb3Jwb3Jh
# dGlvbjEWMBQGA1UEBRMNMjM2MTY3KzQ3MDg2MDCCAeYGA1UdHwSCAd0wggHZMIIB
# 1aCCAdGgggHNhj9odHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtpaW5mcmEvQ1JM
# L0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmwxLmFtZS5nYmwv
# Y3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmwyLmFtZS5n
# YmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmwzLmFt
# ZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGMWh0dHA6Ly9jcmw0
# LmFtZS5nYmwvY3JsL0FNRSUyMENTJTIwQ0ElMjAwMSgyKS5jcmyGgb1sZGFwOi8v
# L0NOPUFNRSUyMENTJTIwQ0ElMjAwMSgyKSxDTj1CWTJQS0lDU0NBMDEsQ049Q0RQ
# LENOPVB1YmxpYyUyMEtleSUyMFNlcnZpY2VzLENOPVNlcnZpY2VzLENOPUNvbmZp
# Z3VyYXRpb24sREM9QU1FLERDPUdCTD9jZXJ0aWZpY2F0ZVJldm9jYXRpb25MaXN0
# P2Jhc2U/b2JqZWN0Q2xhc3M9Y1JMRGlzdHJpYnV0aW9uUG9pbnQwHwYDVR0jBBgw
# FoAUllGE4Gtve/7YBqvD8oXmKa5q+dQwHwYDVR0lBBgwFgYKKwYBBAGCN1sBAQYI
# KwYBBQUHAwMwDQYJKoZIhvcNAQELBQADggEBAA6gUBsTqq/7VEAoeMyfnqxERg2I
# q+GLXVtoYo6Cl5Ve4rO1fnH19Db8Old1Q6RV7TDpxWi617bhZAAJcpT7wA2MQypo
# mrkOqxHnZDzpXou+NRDAxJ82CeOdv6aS1RfWzyA0BDY1Rayob5DRyYpsQY0WOduw
# W8IqAMc4UdUlphvm00JTSeRaQyfVoDTLvy7bAu9pCFJLHcjYDw5YtdZdqGUCFXxk
# QXThRq0z9DADkO5sKCJTAr9MJd9GiFgii44PiHgxDnfejSwLeRXO+FJd2TYNeaDI
# GrMes/60k9ADwgrkAnB7J4msql03f1nFX3rWuu7gZeBAx9ngvr7m/T3M//swggjo
# MIIG0KADAgECAhMfAAAAUeqP9pxzDKg7AAAAAABRMA0GCSqGSIb3DQEBCwUAMDwx
# EzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxEDAOBgNV
# BAMTB2FtZXJvb3QwHhcNMjEwNTIxMTg0NDE0WhcNMjYwNTIxMTg1NDE0WjBBMRMw
# EQYKCZImiZPyLGQBGRYDR0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQD
# EwxBTUUgQ1MgQ0EgMDEwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDJ
# mlIJfQGejVbXKpcyFPoFSUllalrinfEV6JMc7i+bZDoL9rNHnHDGfJgeuRIYO1LY
# /1f4oMTrhXbSaYRCS5vGc8145WcTZG908bGDCWr4GFLc411WxA+Pv2rteAcz0eHM
# H36qTQ8L0o3XOb2n+x7KJFLokXV1s6pF/WlSXsUBXGaCIIWBXyEchv+sM9eKDsUO
# LdLTITHYJQNWkiryMSEbxqdQUTVZjEz6eLRLkofDAo8pXirIYOgM770CYOiZrcKH
# K7lYOVblx22pdNawY8Te6a2dfoCaWV1QUuazg5VHiC4p/6fksgEILptOKhx9c+ia
# piNhMrHsAYx9pUtppeaFAgMBAAGjggTcMIIE2DASBgkrBgEEAYI3FQEEBQIDAgAC
# MCMGCSsGAQQBgjcVAgQWBBQSaCRCIUfL1Gu+Mc8gpMALI38/RzAdBgNVHQ4EFgQU
# llGE4Gtve/7YBqvD8oXmKa5q+dQwggEEBgNVHSUEgfwwgfkGBysGAQUCAwUGCCsG
# AQUFBwMBBggrBgEFBQcDAgYKKwYBBAGCNxQCAQYJKwYBBAGCNxUGBgorBgEEAYI3
# CgMMBgkrBgEEAYI3FQYGCCsGAQUFBwMJBggrBgEFBQgCAgYKKwYBBAGCN0ABAQYL
# KwYBBAGCNwoDBAEGCisGAQQBgjcKAwQGCSsGAQQBgjcVBQYKKwYBBAGCNxQCAgYK
# KwYBBAGCNxQCAwYIKwYBBQUHAwMGCisGAQQBgjdbAQEGCisGAQQBgjdbAgEGCisG
# AQQBgjdbAwEGCisGAQQBgjdbBQEGCisGAQQBgjdbBAEGCisGAQQBgjdbBAIwGQYJ
# KwYBBAGCNxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMBIGA1UdEwEB/wQI
# MAYBAf8CAQAwHwYDVR0jBBgwFoAUKV5RXmSuNLnrrJwNp4x1AdEJCygwggFoBgNV
# HR8EggFfMIIBWzCCAVegggFToIIBT4YxaHR0cDovL2NybC5taWNyb3NvZnQuY29t
# L3BraWluZnJhL2NybC9hbWVyb290LmNybIYjaHR0cDovL2NybDIuYW1lLmdibC9j
# cmwvYW1lcm9vdC5jcmyGI2h0dHA6Ly9jcmwzLmFtZS5nYmwvY3JsL2FtZXJvb3Qu
# Y3JshiNodHRwOi8vY3JsMS5hbWUuZ2JsL2NybC9hbWVyb290LmNybIaBqmxkYXA6
# Ly8vQ049YW1lcm9vdCxDTj1BTUVSb290LENOPUNEUCxDTj1QdWJsaWMlMjBLZXkl
# MjBTZXJ2aWNlcyxDTj1TZXJ2aWNlcyxDTj1Db25maWd1cmF0aW9uLERDPUFNRSxE
# Qz1HQkw/Y2VydGlmaWNhdGVSZXZvY2F0aW9uTGlzdD9iYXNlP29iamVjdENsYXNz
# PWNSTERpc3RyaWJ1dGlvblBvaW50MIIBqwYIKwYBBQUHAQEEggGdMIIBmTBHBggr
# BgEFBQcwAoY7aHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraWluZnJhL2NlcnRz
# L0FNRVJvb3RfYW1lcm9vdC5jcnQwNwYIKwYBBQUHMAKGK2h0dHA6Ly9jcmwyLmFt
# ZS5nYmwvYWlhL0FNRVJvb3RfYW1lcm9vdC5jcnQwNwYIKwYBBQUHMAKGK2h0dHA6
# Ly9jcmwzLmFtZS5nYmwvYWlhL0FNRVJvb3RfYW1lcm9vdC5jcnQwNwYIKwYBBQUH
# MAKGK2h0dHA6Ly9jcmwxLmFtZS5nYmwvYWlhL0FNRVJvb3RfYW1lcm9vdC5jcnQw
# gaIGCCsGAQUFBzAChoGVbGRhcDovLy9DTj1hbWVyb290LENOPUFJQSxDTj1QdWJs
# aWMlMjBLZXklMjBTZXJ2aWNlcyxDTj1TZXJ2aWNlcyxDTj1Db25maWd1cmF0aW9u
# LERDPUFNRSxEQz1HQkw/Y0FDZXJ0aWZpY2F0ZT9iYXNlP29iamVjdENsYXNzPWNl
# cnRpZmljYXRpb25BdXRob3JpdHkwDQYJKoZIhvcNAQELBQADggIBAFAQI7dPD+jf
# XtGt3vJp2pyzA/HUu8hjKaRpM3opya5G3ocprRd7vdTHb8BDfRN+AD0YEmeDB5HK
# QoG6xHPI5TXuIi5sm/LeADbV3C2q0HQOygS/VT+m1W7a/752hMIn+L4ZuyxVeSBp
# fwf7oQ4YSZPh6+ngZvBHgfBaVz4O9/wcfw91QDZnTgK9zAh9yRKKls2bziPEnxeO
# ZMVNaxyV0v152PY2xjqIafIkUjK6vY9LtVFjJXenVUAmn3WCPWNFC1YTIIHw/mD2
# cTfPy7QA1pT+GPARAKt0bKtq9aCd/Ym0b5tPbpgCiRtzyb7fbNS1dE740re0COE6
# 7YV2wbeo2sXixzvLftH8L7s9xv9wV+G22qyKt6lmKLjFK1yMw4Ni5fMabcgmzRvS
# jAcbqgp3tk4a8emaaH0rz8MuuIP+yrxtREPXSqL/C5bzMzsikuDW9xH10graZzSm
# PjilzpRfRdu20/9UQmC7eVPZ4j1WNa1oqPHfzET3ChIzJ6Q9G3NPCB+7KwX0OQmK
# yv7IDimj8U/GlsHD1z+EF/fYMf8YXG15LamaOAohsw/ywO6SYSreVW+5Y0mzJutn
# BC9Cm9ozj1+/4kqksrlhZgR/CSxhFH3BTweH8gP2FEISRtShDZbuYymynY1un+Ry
# fiK9+iVTLdD1h/SxyxDpZMtimb4CgJQlMYIZODCCGTQCAQEwWDBBMRMwEQYKCZIm
# iZPyLGQBGRYDR0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxBTUUg
# Q1MgQ0EgMDECEzYAAAGoUyvWUKusuVMAAgAAAagwDQYJYIZIAWUDBAIBBQCgga4w
# GQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisG
# AQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEIB8fHGVUFW49VOyt3npkszzkJpmcHvtJ
# GWw0yYGwx1jLMEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8AcwBvAGYA
# dKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEA
# AJXXK0v2BlNko6lDB3qJdlGSL7rsJ/I0BaWcU2PD9kXND9KPXAjM5Yl7lTtPCp6O
# f1S/2mBU9Jbd96Hkn5vpWy7bvJ9ki3aEPJ4JFCQHKbF962uzYttU/vvWyJ8mpHYu
# A8/oLL1LSj6seawIqubsGnraXZQRtdQIug8KbOm6LaR9OyoiEVNEvvgNFWqFG6M+
# SdVVXbcaIg6uGYaxiEGCdBEBtL+wUM6FWPce6ElxxvoG5DGUcWup8O5dKFVYiTU2
# s+y/SMPXqUbtFKU55QGl7Ht7LxWLngxAJLUiFE7wA7ckim36BIQzfiURz1LOmfFJ
# 2Xfe+O8E1q5eTjAnoN2ekqGCFwAwghb8BgorBgEEAYI3AwMBMYIW7DCCFugGCSqG
# SIb3DQEHAqCCFtkwghbVAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFRBgsqhkiG9w0B
# CRABBKCCAUAEggE8MIIBOAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFlAwQCAQUA
# BCC1pHBPtB4iX1oPmDFTg89Lr12addNNaYGwbL8r2sALbQIGY0hJyiuTGBMyMDIy
# MTAxODExMDQ0Ni4zNjZaMASAAgH0oIHQpIHNMIHKMQswCQYDVQQGEwJVUzETMBEG
# A1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWlj
# cm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1lcmljYSBP
# cGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjo0OUJDLUUzN0EtMjMz
# QzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaCCEVcwggcM
# MIIE9KADAgECAhMzAAABlwPPWZxriXg/AAEAAAGXMA0GCSqGSIb3DQEBCwUAMHwx
# CzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRt
# b25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1p
# Y3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIxMTIwMjE5MDUxNFoXDTIz
# MDIyODE5MDUxNFowgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9u
# MRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRp
# b24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNV
# BAsTHVRoYWxlcyBUU1MgRVNOOjQ5QkMtRTM3QS0yMzNDMSUwIwYDVQQDExxNaWNy
# b3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNlMIICIjANBgkqhkiG9w0BAQEFAAOCAg8A
# MIICCgKCAgEA7QBK6kpBTfPwnv3LKx1VnL9YkozUwKzyhDKij1E6WCV/EwWZfPCz
# a6cOGxKT4pjvhLXJYuUQaGRInqPks2FJ29PpyhFmhGILm4Kfh0xWYg/OS5Xe5pNl
# 4PdSjAxNsjHjiB9gx6U7J+adC39Ag5XzxORzsKT+f77FMTXg1jFus7ErilOvWi+z
# nMpN+lTMgioxzTC+u1ZmTCQTu219b2FUoTr0KmVJMQqQkd7M5sR09PbOp4cC3jQs
# +5zJ1OzxIjRlcUmLvldBE6aRaSu0x3BmADGt0mGY0MRsgznOydtJBLnerc+QK0kc
# xuO6rHA3z2Kr9fmpHsfNcN/eRPtZHOLrpH59AnirQA7puz6ka20TA+8MhZ19hb8m
# srRo9LmirjFxSbGfsH3ZNEbLj3lh7Vc+DEQhMH2K9XPiU5Jkt5/6bx6/2/Od3aNv
# C6Dx3s5N3UsW54kKI1twU2CS5q1Hov5+ARyuZk0/DbsRus6D97fB1ZoQlv/4trBc
# MVRz7MkOrHa8bP4WqbD0ebLYtiExvx4HuEnh+0p3veNjh3gP0+7DkiVwIYcfVclI
# hFFGsfnSiFexruu646uUla+VTUuG3bjqS7FhI3hh6THov/98XfHcWeNhvxA5K+fi
# +1BcSLgQKvq/HYj/w/Mkf3bu73OERisNaacaaOCR/TJ2H3fs1A7lIHECAwEAAaOC
# ATYwggEyMB0GA1UdDgQWBBRtzwHPKOswbpZVC9Gxvt1+vRUAYDAfBgNVHSMEGDAW
# gBSfpxVdAF5iXYP05dJlpxtTNRnpcjBfBgNVHR8EWDBWMFSgUqBQhk5odHRwOi8v
# d3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NybC9NaWNyb3NvZnQlMjBUaW1lLVN0
# YW1wJTIwUENBJTIwMjAxMCgxKS5jcmwwbAYIKwYBBQUHAQEEYDBeMFwGCCsGAQUF
# BzAChlBodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NlcnRzL01pY3Jv
# c29mdCUyMFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNydDAMBgNVHRMBAf8E
# AjAAMBMGA1UdJQQMMAoGCCsGAQUFBwMIMA0GCSqGSIb3DQEBCwUAA4ICAQAESNhh
# 0iTtMx57IXLfh4LuHbD1NG9MlLA1wYQHQBnR9U/rg3qt3Nx6e7+QuEKMEhKqdLf3
# g5RR4R/oZL5vEJVWUfISH/oSWdzqrShqcmT4Oxzc2CBs0UtnyopVDm4W2Cumo3qu
# ykYPpBoGdeirvDdd153AwsJkIMgm/8sxJKbIBeT82tnrUngNmNo8u7l1uE0hsMAq
# 1bivQ63fQInr+VqYJvYT0W/0PW7pA3qh4ocNjiX6Z8d9kjx8L7uBPI/HsxifCj/8
# mFRvpVBYOyqP7Y5di5ZAnjTDSHMZNUFPHt+nhFXUcHjXPRRHCMqqJg4D63X6b0V0
# R87Q93ipwGIXBMzOMQNItJORekHtHlLi3bg6Lnpjs0aCo5/RlHCjNkSDg+xV7qYe
# a37L/OKTNjqmH3pNAa3BvP/rDQiGEYvgAbVHEIQz7WMWSYsWeUPFZI36mCjgUY6V
# 538CkQtDwM8BDiAcy+quO8epykiP0H32yqwDh852BeWm1etF+Pkw/t8XO3Q+diFu
# 7Ggiqjdemj4VfpRsm2tTN9HnAewrrb0XwY8QE2tp0hRdN2b0UiSxMmB4hNyKKXVa
# DLOFCdiLnsfpD0rjOH8jbECZObaWWLn9eEvDr+QNQPvS4r47L9Aa8Lr1Hr47VwJ5
# E2gCEnvYwIRDzpJhMRi0KijYN43yT6XSGR4N9jCCB3EwggVZoAMCAQICEzMAAAAV
# xedrngKbSZkAAAAAABUwDQYJKoZIhvcNAQELBQAwgYgxCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xMjAwBgNVBAMTKU1pY3Jvc29mdCBSb290IENl
# cnRpZmljYXRlIEF1dGhvcml0eSAyMDEwMB4XDTIxMDkzMDE4MjIyNVoXDTMwMDkz
# MDE4MzIyNVowfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
# BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEm
# MCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwggIiMA0GCSqG
# SIb3DQEBAQUAA4ICDwAwggIKAoICAQDk4aZM57RyIQt5osvXJHm9DtWC0/3unAcH
# 0qlsTnXIyjVX9gF/bErg4r25PhdgM/9cT8dm95VTcVrifkpa/rg2Z4VGIwy1jRPP
# dzLAEBjoYH1qUoNEt6aORmsHFPPFdvWGUNzBRMhxXFExN6AKOG6N7dcP2CZTfDlh
# AnrEqv1yaa8dq6z2Nr41JmTamDu6GnszrYBbfowQHJ1S/rboYiXcag/PXfT+jlPP
# 1uyFVk3v3byNpOORj7I5LFGc6XBpDco2LXCOMcg1KL3jtIckw+DJj361VI/c+gVV
# mG1oO5pGve2krnopN6zL64NF50ZuyjLVwIYwXE8s4mKyzbnijYjklqwBSru+cakX
# W2dg3viSkR4dPf0gz3N9QZpGdc3EXzTdEonW/aUgfX782Z5F37ZyL9t9X4C626p+
# Nuw2TPYrbqgSUei/BQOj0XOmTTd0lBw0gg/wEPK3Rxjtp+iZfD9M269ewvPV2HM9
# Q07BMzlMjgK8QmguEOqEUUbi0b1qGFphAXPKZ6Je1yh2AuIzGHLXpyDwwvoSCtdj
# bwzJNmSLW6CmgyFdXzB0kZSU2LlQ+QuJYfM2BjUYhEfb3BvR/bLUHMVr9lxSUV0S
# 2yW6r1AFemzFER1y7435UsSFF5PAPBXbGjfHCBUYP3irRbb1Hode2o+eFnJpxq57
# t7c+auIurQIDAQABo4IB3TCCAdkwEgYJKwYBBAGCNxUBBAUCAwEAATAjBgkrBgEE
# AYI3FQIEFgQUKqdS/mTEmr6CkTxGNSnPEP8vBO4wHQYDVR0OBBYEFJ+nFV0AXmJd
# g/Tl0mWnG1M1GelyMFwGA1UdIARVMFMwUQYMKwYBBAGCN0yDfQEBMEEwPwYIKwYB
# BQUHAgEWM2h0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvRG9jcy9SZXBv
# c2l0b3J5Lmh0bTATBgNVHSUEDDAKBggrBgEFBQcDCDAZBgkrBgEEAYI3FAIEDB4K
# AFMAdQBiAEMAQTALBgNVHQ8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAfBgNVHSME
# GDAWgBTV9lbLj+iiXGJo0T2UkFvXzpoYxDBWBgNVHR8ETzBNMEugSaBHhkVodHRw
# Oi8vY3JsLm1pY3Jvc29mdC5jb20vcGtpL2NybC9wcm9kdWN0cy9NaWNSb29DZXJB
# dXRfMjAxMC0wNi0yMy5jcmwwWgYIKwYBBQUHAQEETjBMMEoGCCsGAQUFBzAChj5o
# dHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY1Jvb0NlckF1dF8y
# MDEwLTA2LTIzLmNydDANBgkqhkiG9w0BAQsFAAOCAgEAnVV9/Cqt4SwfZwExJFvh
# nnJL/Klv6lwUtj5OR2R4sQaTlz0xM7U518JxNj/aZGx80HU5bbsPMeTCj/ts0aGU
# GCLu6WZnOlNN3Zi6th542DYunKmCVgADsAW+iehp4LoJ7nvfam++Kctu2D9IdQHZ
# GN5tggz1bSNU5HhTdSRXud2f8449xvNo32X2pFaq95W2KFUn0CS9QKC/GbYSEhFd
# PSfgQJY4rPf5KYnDvBewVIVCs/wMnosZiefwC2qBwoEZQhlSdYo2wh3DYXMuLGt7
# bj8sCXgU6ZGyqVvfSaN0DLzskYDSPeZKPmY7T7uG+jIa2Zb0j/aRAfbOxnT99kxy
# bxCrdTDFNLB62FD+CljdQDzHVG2dY3RILLFORy3BFARxv2T5JL5zbcqOCb2zAVdJ
# VGTZc9d/HltEAY5aGZFrDZ+kKNxnGSgkujhLmm77IVRrakURR6nxt67I6IleT53S
# 0Ex2tVdUCbFpAUR+fKFhbHP+CrvsQWY9af3LwUFJfn6Tvsv4O+S3Fb+0zj6lMVGE
# vL8CwYKiexcdFYmNcP7ntdAoGokLjzbaukz5m/8K6TT4JDVnK+ANuOaMmdbhIurw
# J0I9JZTmdHRbatGePu1+oDEzfbzL6Xu/OHBE0ZDxyKs6ijoIYn/ZcGNTTY3ugm2l
# BRDBcQZqELQdVTNYs6FwZvKhggLOMIICNwIBATCB+KGB0KSBzTCByjELMAkGA1UE
# BhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAc
# BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0
# IEFtZXJpY2EgT3BlcmF0aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046NDlC
# Qy1FMzdBLTIzM0MxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZp
# Y2WiIwoBATAHBgUrDgMCGgMVAGFA0rCNmEk0zU12DYNGMU3B1mPRoIGDMIGApH4w
# fDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1Jl
# ZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMd
# TWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwDQYJKoZIhvcNAQEFBQACBQDm
# +LbgMCIYDzIwMjIxMDE4MTMyMzQ0WhgPMjAyMjEwMTkxMzIzNDRaMHcwPQYKKwYB
# BAGEWQoEATEvMC0wCgIFAOb4tuACAQAwCgIBAAICDUMCAf8wBwIBAAICElcwCgIF
# AOb6CGACAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGEWQoDAqAKMAgCAQAC
# AwehIKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQAuIghtJavrfrBK0P/P
# 4WkqH2gL6MwpYJLH0FXPF9Pn2KT4qkmwbYVwXYgoQruTryA0/eTIZmNlraefTbKA
# QpJXnz9Y+dQl5q7WlNFeU+DvAsOLXnP40MHOJW76SonCAIZVsmRDixCIUnMxiW7E
# TlqTW+CrTcbG5nAqT0ZJyDRliTGCBA0wggQJAgEBMIGTMHwxCzAJBgNVBAYTAlVT
# MRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQK
# ExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1l
# LVN0YW1wIFBDQSAyMDEwAhMzAAABlwPPWZxriXg/AAEAAAGXMA0GCWCGSAFlAwQC
# AQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQwLwYJKoZIhvcNAQkE
# MSIEIPX3/uC1KGR6vhEerv+hM6zoZJWdZNtkHjdfZoDe/xuHMIH6BgsqhkiG9w0B
# CRACLzGB6jCB5zCB5DCBvQQgW3vaGxCVejj+BAzFSfMxfHQ+bxxkqCw8LkMY/QZ4
# pr8wgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAZcD
# z1mca4l4PwABAAABlzAiBCCy691A+Z5KB1Ap+8N66bqaYU41GSNm0ZwIHpv/+ZgU
# oDANBgkqhkiG9w0BAQsFAASCAgC4x9w3OdwpXHnvA17wd6/iheTzXB4ptDTFzW3d
# l4vbS8s691+9QKwuZyIKg1K3FJ+ZVPioBW9SMB4SD5hXJ2d/1iprxliBjsDFz4OA
# JzJyFjIP8fibASijmT1HRnCAWmZYRfviVHMqY2P9ekcdnAoielhD7EgM9egsGOPC
# 8SCoEX7Ma0urDLqwnw9ZEKS4c5U8mj4N+uIZTxliOHleEQnE2N09XhgEpZN0e3iH
# 8qDHuoIn/lE3lHkKri/tJEbWpaW3gqkjMyyj3EhctVyll9Dwh76h4OBwP1pD5TeR
# GIIYR2E/QCxJWKXJPWf24T8wSPx76uxolM2xn2S5SWJRWfoEx7urRv+fAqRGnNOl
# d/yUNneDzvCRSFfyTif6fRIpHndX6WZZxGYaekfArNgp6RpeClcnBu8En0k/p2Wk
# 5Z5F2YzWfY0A3DDowEEq6Y5dNI0hNWd9X6T1jqtFwNx+uuVgVP8/yJayF3QPi8qo
# nprQXdIY/revkb06m3/6wiRWB5O5hZSgR3Q3b6BplT37SwkIz+jfH8axR9cXH/Fj
# izl0jj+3C+XbH5m8N8/GT+GPSCp5ehUfuCQkplkGEtt2sm70PwxWCdj98fj/RubA
# qNx9AIcf5ttEBr96+LbLYQTJ2wjbQqWV98kl/NnyfxHBP4P5iEsGfayZ751XeSSI
# Vmqy4w==
# SIG # End signature block
