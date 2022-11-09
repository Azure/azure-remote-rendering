Param(
    [string] $DownloadDestDir = (Join-Path $PSScriptRoot "/../Unity/MRPackages"),
    [string] $DependenciesJson = (Join-Path $PSScriptRoot "unity_sample_dependencies.json"),
    [switch] $Verbose
)

. "$PSScriptRoot\ARRUtils.ps1" #include ARRUtils for Logging

### Functions ###
function NormalizePath([string] $path)
{
    if (-not [System.IO.Path]::IsPathRooted($path))
    {
        $wd = Get-Location
        $joined = Join-Path $wd $path
        $path = ([System.IO.Path]::GetFullPath($joined))
    }
    return $path
}

$DownloadPackage = {
    param(
        [string] $root,
        [string] $DownloadDestDir,
        [string] $registry,
        [string] $package,
        [string] $version = "latest",
        [switch] $verbose
    )

    . "$root\ARRUtils.ps1" #include ARRUtils for Logging

    #region GetDetails
    $details = $null
    $uri = "$registry/$package"
    try 
    {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri
        $json = $response | ConvertFrom-Json

        if ($version -like "latest") {
            $version = $json.'dist-tags'.latest
        }

        $versionInfo = $json.versions.($version)
        if ($null -eq $versionInfo) {
            WriteError "No info found for version $version of package $package."
            throw
        }
        $details = $json.versions.($version).dist
    }
    catch
    {
        WriteError "Web request to $uri failed."
        WriteError $_
        throw
    }

    if ($null -eq $details) 
    {
        WriteError "Failed to get details for $($package.name)@$($package.version) from $($package.registry)."
        throw
    }
    
    $packageUrl = $details.tarball
    $fileName = [System.IO.Path]::GetFileName($packageUrl)
    $downloadPath = Join-Path $DownloadDestDir $fileName
    $downloadPath = [System.IO.Path]::GetFullPath($downloadPath)

    $packageDetails = [PSCustomObject]@{
        Name         = $package
        Version      = $version
        Registry     = $registry
        Url          = $packageUrl
        Sha1sum      = $details.shasum
        DownloadPath = $downloadPath
    }
    #endregion

    #region CheckLocalPackages
    if ((Test-Path $packageDetails.DownloadPath -PathType Leaf))
    {
        $packageShasum = $packageDetails.Sha1sum.ToLower()
        $localShasum = (Get-FileHash -Algorithm SHA1 $packageDetails.DownloadPath).Hash.ToLower()
        if ($packageShasum -like $localShasum)
        {
            WriteInformation "$($packageDetails.DownloadPath) already exists. Skipping download."
            return
        }

        WriteError "SHA1 hashes for $($packageDetails.Name)@$($packageDetails.Version) and $($packageDetails.DownloadPath) do not match."
        WriteError "$packageShasum != $localShasum"
        WriteError "Deleting file and re-downloading."
        Remove-Item $packageDetails.DownloadPath -Force
    }
    #endregion

    #region Download
    WriteInformation "Downloading $($packageDetails.Name)@$($packageDetails.Version) from $($packageDetails.Url)."
    $retries = 1
    while ($retries -le 3)
    {
        try
        {
            $responseStream = $null
            $destStream = $null
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $uri = New-Object "System.Uri" "$($packageDetails.Url)"
            $request = [System.Net.HttpWebRequest]::Create($uri)
        
            $response = $request.GetResponse()
            $responseStream = $response.GetResponseStream()
            $contentLength = $response.get_ContentLength()
        
            $destStream = New-Object -TypeName System.IO.FileStream -ArgumentList $packageDetails.DownloadPath, Create
            $buf = new-object byte[] 16KB
        
            $srcFileName = [System.IO.Path]::GetFileName($packageDetails.Url)
            
            $lastLogPercentage = 0

            do
            {
                if ($($sw.Elapsed.TotalMinutes) -gt 5)
                {
                    throw "Timeout"
                }

                $destStream.Write($buf, 0, $readBytes)
                $readBytes = $responseStream.Read($buf, 0, $buf.length)
                $readBytesTotal = $readBytesTotal + $readBytes
                
                $percentComplete = (($readBytesTotal / $contentLength) * 100)
                if (($percentComplete - $lastLogPercentage) -gt 5)
                {
                    $lastLogPercentage = $percentComplete
                    $status = "Downloaded {0:N2} MB of {1:N2} MB (Attempt $retries)" -f ($readBytesTotal / 1MB), ($contentLength / 1MB)
                    Write-Progress -Activity "Downloading '$($srcFileName)'" -Status $status -PercentComplete $percentComplete

                    if ($verbose)
                    {
                        Write-Host "DL (Attempt $retries) `"$($srcFileName)`" at $([math]::round($percentComplete,2))%"
                    }
                }
            }
            while ($readBytes -gt 0)

            Write-Progress -Completed -Activity "Finished downloading '$($srcFileName)'"
            return
        }
        catch
        {
            Write-Host "Error downloading $($packageDetails.Url) to $($packageDetails.DownloadPath)"
            Write-Host "$_"
            $retries += 1
        }
        finally
        {
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
    #endregion

    #region Verify
    $packageShasum = $packageDetails.Sha1sum.ToLower()
    if (Test-Path $packageDetails.DownloadPath -PathType Leaf)
    {
        $localShasum = (Get-FileHash -Algorithm SHA1 $packageDetails.DownloadPath).Hash.ToLower()
        if (-not ($packageShasum -like $localShasum))
        {
            WriteError "SHA1 hashes for $($packageDetails.Name)@$($packageDetails.Version) and $($packageDetails.DownloadPath) do not match."
            WriteError "$packageShasum != $localShasum"
            throw
        }
    }
    else
    {
        WriteError "Download failed for $($packageDetails.Name)@$($packageDetails.Version). File not found: $($packageDetails.DownloadPath)."
        throw
    }
    #endregion
}

### Main script ###
$DownloadDestDir = NormalizePath($DownloadDestDir)
$DependenciesJson = NormalizePath($DependenciesJson)

if (-not (Test-Path $DownloadDestDir -PathType Container))
{
    New-Item -Path $DownloadDestDir -ItemType Directory | Out-Null
}

$downloads = @()
foreach ($package in ((Get-Content $DependenciesJson) | ConvertFrom-Json).packages)
{
    $download = Start-Job -ScriptBlock $DownloadPackage -ArgumentList $PSScriptRoot, $DownloadDestDir, $package.registry, $package.name, $package.version, $Verbose

    $downloads += [PSCustomObject]@{
        JobObject = $download
        Completed = $false
    }
}

$failed = $false
$sw = [System.Diagnostics.Stopwatch]::StartNew()

while (-not $failed -and $completedJobCount -lt $downloads.Count)
{
    $completedJobCount = 0
    foreach ($download in $downloads)
    {
        if ($download.Completed)
        {
            $completedJobCount++
            continue
        }

        if ($download.JobObject.State -ne "NotStarted" -and $download.JobObject.State -ne "Running")
        {
            $download.Completed = $true
            $download.JobObject.ChildJobs[0].Output | Write-Host
            $download.JobObject.ChildJobs[0].Information | Write-Host

            if ($download.JobObject.ChildJobs[0].Error.Count -ne 0)
            {
                $failed = $true
                $download.JobObject.ChildJobs[0].Error | Write-Error
                break
            }
        }

        $latestProgress = $download.JobObject.ChildJobs[0].Progress[-1]
        if ($null -ne $latestProgress -and $null -ne $latestProgress.Activity)
        {
            if ($latestProgress.RecordType -eq "Completed")
            {
                Write-Progress -ParentId 0 -Completed -Id $download.JobObject.Id -Activity $latestProgress
            }
            else
            {
                Write-Progress -ParentId 0 -Id $download.JobObject.Id -Activity $latestProgress.Activity -Status $latestProgress.StatusDescription -PercentComplete $latestProgress.PercentComplete
            }
        }
    }

    $status = "$completedJobCount of $($downloads.Count) Downloads completed"
    Write-Progress -Id 0 -Activity "Downloading Packages" -Status $status -PercentComplete (($completedJobCount / $downloads.Count) * 100)

    if ($Verbose -and $($sw.Elapsed.TotalSeconds) -gt 30)
    {
        Write-Host "Periodic job status:"
        $downloads.JobObject | Format-Table -AutoSize
        $downloads.JobObject | Receive-Job
        $sw.Restart()
    } 
}

if (-not $failed)
{
    WriteSuccess "Downloading dependency packages succeeded."
}
else
{
    WriteError "Downloading dependency packages failed."
    exit 1
}

# SIG # Begin signature block
# MIIrZAYJKoZIhvcNAQcCoIIrVTCCK1ECAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCAuCsbg4PvYsu5r
# 6cgAIWCDuwE6K+uezyC83smNnLWLr6CCEXkwggiJMIIHcaADAgECAhM2AAABqdaQ
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
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgxIu7wNPsuvw1tKSn
# 2IVXONbrgB0uOkg7Eum88579EeYwQgYKKwYBBAGCNwIBDDE0MDKgFIASAE0AaQBj
# AHIAbwBzAG8AZgB0oRqAGGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbTANBgkqhkiG
# 9w0BAQEFAASCAQBEhjDq48sX0DbLrJeKy1UoNouqyplz0Hk0/PeYUiYhhbte5EcN
# 4lv4NuM3divT8qxbzrLZ3jNiCEm6gg3q9USRqH4UKOTm/1bu6JvW6khE1ZU9PnTQ
# s4U8v6pW0CSmeF2qnV377HEtyZWaF+xYZrSAm3M/iEym0CmgjebR6Nw5uKTLRQsM
# 8/wWbZVmJuz9TWK2s5ooe61Dt4Vp6YP7S4v2JLUFC7ExuOUpwdOXjm/2hRx1utR/
# J9ogqmv7RSXUEhUIwFuxvqvqD6cmL9XzjU0yyTRpaJ61I2QQSUw0sNT3ybIC1boX
# DISeJWuCHzrREZk8RLMfhIDybgkagv0I2pqcoYIXCTCCFwUGCisGAQQBgjcDAwEx
# ghb1MIIW8QYJKoZIhvcNAQcCoIIW4jCCFt4CAQMxDzANBglghkgBZQMEAgEFADCC
# AVUGCyqGSIb3DQEJEAEEoIIBRASCAUAwggE8AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEIL3hF7hI9WbvgZuEYJuMS0sHoycv1IJ+5LCqqG43YzxrAgZj
# YsUW2XIYEzIwMjIxMTA5MTUzMDIwLjQ3NlowBIACAfSggdSkgdEwgc4xCzAJBgNV
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
# 9w0BCRABBDAvBgkqhkiG9w0BCQQxIgQgQxX0y7LlXRgU8JxK5ydoGPwYME66b98G
# ioQhTSeH1UEwgfoGCyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCBWtQJDHFq8EeBz
# 3TXugCqRhSI/JCZbATYEIwTG8bMewDCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0
# YW1wIFBDQSAyMDEwAhMzAAABqqUxmwvLsggOAAEAAAGqMCIEIDq2mVUESOuFZBNl
# 3ixuczQIqJxaAcFfBATel3zqaQLVMA0GCSqGSIb3DQEBCwUABIICADMxVGRN3Xc1
# qcmbu0Sveg1sSWhEbxWfA2dcegaRWSGFd9YQdYmwtl8rq1DLu7+DW0EMrhdGzydd
# U6VYc2FmxIcjH99diAWgawOm7QjvUYx+oRPiGLUh3EtPxpMBfxxHVNOFAfrTr1nh
# u+wfl/KsqsMwX9lsNxTnEeonOTi8hT9vrj00CCIRTO3WxTtinqJankdj57es2bxv
# hv14zpEMG2oQtkcSPB9ycJ1btACpTfj4VlW5pO8Zn4SO5S/oKoqX/lGAmtvKeDLR
# mXd67SfVwirAJfflHOpGt4Bj2A662NAik5NOW9mohx7FdLFkRvwmUVDxX7soWU6R
# mFYbF3/2MEmzRfAlSG7Vi6IZZGXnYTphAkkPBVnFn+vBzULrNq5kLc7dboOqgZ+F
# f2luZp0X7xaG/88v35KvI/JUWcGjGlmNCWPkhjdwJFaeYIR1kgUEQXrA9lcLmPyA
# 9s1Nu+zLs/S/Z3FwZH1CEfbWyHSDsRLrZ1Melr3kXJrI20br3z/hrpD00FAoHK7p
# ZVmEDco6rUCwSVMPfdHcpEBwDGYwXGClryL31gTP4pGs4w0diXC/NjXst2rJdHot
# HKK8KwPVTV3k73xOFI6E92p6LGMLGnR+mNrSQroh2mWfTs2oUeVD0tABTSXgs05R
# 0Br6OqP0bGjuJpqQ+foY3TFXzJ12l7q2
# SIG # End signature block
