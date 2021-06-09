Param(
    [string] $DownloadDestDir = (Join-Path $PSScriptRoot "/../Unity/MRPackages"),
    [string] $DependenciesJson = (Join-Path $PSScriptRoot "unity_sample_dependencies.json")
)

. "$PSScriptRoot\ARRUtils.ps1" #include ARRUtils for Logging

$success = $True

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

$DownloadDestDir = NormalizePath($DownloadDestDir)
$DependenciesJson = NormalizePath($DependenciesJson)

if (-not (Test-Path $DownloadDestDir -PathType Container))
{
    New-Item -Path $DownloadDestDir -ItemType Directory | Out-Null
}

function GetPackageDownloadDetails([string] $registry, [string] $package, [string] $version = "latest") 
{
    $details = $null
    $uri = "$registry/$package"
    try
    {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $uri
        $json = $response | ConvertFrom-Json

        if ($version -like "latest")
        {
            $version = $json.'dist-tags'.latest
        }

        $versionInfo = $json.versions.($version)
        if ($null -eq $versionInfo)
        {
            WriteError "No info found for version $version of package $package."
        }
        $details = $json.versions.($version).dist
    }
    catch
    {
        WriteError "Web request to $uri failed."
        WriteError $_
    }
    return $details
}

# supress progress bar for API requests
$oldProgressPreference = $ProgressPreference
$ProgressPreference = 'SilentlyContinue'


$packages = ((Get-Content $DependenciesJson) | ConvertFrom-Json).packages| ForEach-Object {
    $package = $_

    $details = GetPackageDownloadDetails $package.registry $package.name $package.version
    if ($null -eq $details)
    {
        WriteError "Failed to get details for $($package.name)@$($package.version) from $($package.registry)."
        $success = $False
        return
    }
    
    $packageUrl = $details.tarball
    $fileName = [System.IO.Path]::GetFileName($packageUrl)
    $downloadPath = Join-Path $DownloadDestDir $fileName
    $downloadPath = [System.IO.Path]::GetFullPath($downloadPath)

    [PSCustomObject]@{
        Name = $package.name
        Version = $package.version
        Registry = $package.registry
        Url = $packageUrl
        Sha1sum = $details.shasum
        DownloadPath = $downloadPath
    }
}

# restore progress preference
$ProgressPreference = $oldProgressPreference

$DownloadFile = {
    param([string] $url, [string] $dest)
    try
    {
        $uri = New-Object "System.Uri" "$url"
        $request = [System.Net.HttpWebRequest]::Create($uri)
    
        $response = $request.GetResponse()
        $responseStream = $response.GetResponseStream()
        $contentLength = $response.get_ContentLength()
    
        $destStream = New-Object -TypeName System.IO.FileStream -ArgumentList $dest, Create
        $buf = new-object byte[] 16KB
    
        $srcFileName = [System.IO.Path]::GetFileName($url)
        
        $readBytesTotal = $readBytes = $responseStream.Read($buf, 0, $buf.length)
        while ($readBytes -gt 0)
        {
            $destStream.Write($buf, 0, $readBytes)
            $readBytes = $responseStream.Read($buf, 0, $buf.length)
            $readBytesTotal = $readBytesTotal + $readBytes
            $percentComplete = (($readBytesTotal / $contentLength)  * 100)
            $status = "Downloaded {0:N2} MB of  {1:N2} MB" -f ($readBytesTotal/1MB), ($contentLength/1MB)
            Write-Progress -Activity "Downloading '$($srcFileName)'" -Status $status -PercentComplete $percentComplete
        }
        Write-Progress -Completed -Activity "Finished downloading '$($srcFileName)'"

        $destStream.Flush()
        $destStream.Close()
        $destStream.Dispose()
    
        $responseStream.Dispose()        
    }
    catch {
        Write-Error "Error downloading $url to $dest"
        Write-Error "$_"
    }

}

$jobs = @()
foreach ($package in $packages)
{
    if (-not (Test-Path $package.DownloadPath -PathType Leaf))
    {
        WriteInformation "Downloading $($package.Name)@$($package.Version) from $($package.Url)."
        $jobs += Start-Job -ScriptBlock $DownloadFile -ArgumentList @($package.Url, $package.DownloadPath)
        $progressId++
    }
    else
    {
        WriteInformation "$($package.DownloadPath) already exists. Skipping download."
    }
}

function WriteJobProgress
{
    param($job)
    # Make sure the first child job exists
    if($null -ne $job.ChildJobs[0])
    {
        # Extracts the latest progress of the job and writes the progress
        $jobProgressHistory = $job.ChildJobs[0].Progress;

        if ($jobProgressHistory.count -gt 0)
        {
            $latest = $jobProgressHistory | select-object -last 1
            $latestActivity = $latest | Select-Object -expand Activity;
            $latestStatus = $latest | Select-Object -expand StatusDescription;
            $latestPercentComplete = $latest | Select-Object -expand PercentComplete;

            if ($latest.RecordType -eq 'Completed')
            {
                Write-Progress -ParentId 0 -Completed -Id $job.Id -Activity $latestActivity
            }
            else
            {
                Write-Progress -ParentId 0 -Id $job.Id -Activity $latestActivity -Status $latestStatus -PercentComplete $latestPercentComplete;
            }
        }
    }
}

if ($jobs.Count -gt 0)
{
    do
    {
        Start-Sleep -milliseconds 250
    
        $jobsProgress = $jobs | Select-Object Id,State, @{ N='RecordType'; E={$_.ChildJobs[0].Progress | Select-Object -Last 1 -Exp RecordType}}
        $running = @( $jobsProgress | Where-Object{$_.State -eq 'Running' -or $_.RecordType -ne 'Completed'} )
        $completedCount = ($jobs.Count - $running.Count)
    
        $complete = (($completedCount / $jobs.Count) * 100)
        $status = "$($completedCount) / $($jobs.Count) Completed"
        Write-Progress -Id 0 -Activity "Downloading Packages" -Status $status -PercentComplete $complete
        $jobs | ForEach-Object{ WriteJobProgress($_) }
    }
    while ( $completedCount -lt $jobs.Count )
    
    Write-Progress -Completed -Id 0 -Activity "Downloading Packages"
    
    foreach ($job in $jobs)
    {
        #Propagate messages and errors
        if($null -ne $job.ChildJobs[0])
        {
            $Job.ChildJobs[0].Information | Foreach-Object { WriteInformation $_ }
            $Job.ChildJobs[0].Error | Foreach-Object { WriteError $_ }
        }
    }

    $jobs | Remove-Job
}

foreach ($package in $packages)
{        
    $packageShasum = $package.Sha1sum.ToLower()
    if (Test-Path $package.DownloadPath -PathType Leaf)
    {
        $localShasum = (Get-FileHash -Algorithm SHA1 $package.DownloadPath).Hash.ToLower()
        if (-not ($packageShasum -like $localShasum))
        {
            WriteError "SHA1 hashes for $($package.Name)@$($package.Version) and $($package.DownloadPath) do not match."
            WriteError "$packageShasum != $localShasum"
            $success = $False
        }
    }
    else
    {
        WriteError "Download failed for $($package.Name)@$($package.Version). File not found: $($package.DownloadPath)."
        $success = $False
    }
}

if ($success)
{
    WriteSuccess "Downloading dependency packages succeeded."
}
else
{
    WriteError "Downloading dependency packages failed."
    exit 1
}

# SIG # Begin signature block
# MIInKAYJKoZIhvcNAQcCoIInGTCCJxUCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBWlogFMezq0pvD
# sPGoRks1M6BvinvuwYCs5ssbdQ2Ux6CCEWUwggh3MIIHX6ADAgECAhM2AAABOXjG
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
# TmKLGegSoUpZNfmP9MtSMYIVGTCCFRUCAQEwWDBBMRMwEQYKCZImiZPyLGQBGRYD
# R0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxBTUUgQ1MgQ0EgMDEC
# EzYAAAE5eMY59eV3J+oAAQAAATkwDQYJYIZIAWUDBAIBBQCgga4wGQYJKoZIhvcN
# AQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisGAQQBgjcCARUw
# LwYJKoZIhvcNAQkEMSIEIIHXL8M4/YmvOWOW71QAcefxa50hlffFQEPh7h613X7S
# MEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8AcwBvAGYAdKEagBhodHRw
# Oi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEAj9VIJSZp7sbg
# 910MlSLK7HixBtsLzCoj+82iL6koAJXKZOgrUH77Fw2eQrDojO5duyRyi5IDmQLQ
# gzKsORerAynem/xAPtkvD9kIQQwe457J4FmUVgpBHeOI4nV3PdvLvcFljnYh52Kz
# p1KtaILNLlxevMsfuO36LtoQ4fjoAbpFPRPDHZh6st0AnyVwCDwh+U2IzzSq2d6o
# AEZIuB/uHg1fGr8nHvIysQpyWxk504Q2P7aavJ/xx9eh6VzhaFYIZDkb04yj+ffa
# AeRHImtaNqbGU/EgIV6XTI+Fu2aUkwHOw2WazrGv9RrE3cBXlj5vahJnHn9R1nGx
# Q3Mv8IW0ZaGCEuEwghLdBgorBgEEAYI3AwMBMYISzTCCEskGCSqGSIb3DQEHAqCC
# ErowghK2AgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFQBgsqhkiG9w0BCRABBKCCAT8E
# ggE7MIIBNwIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFlAwQCAQUABCARYfSLxzDr
# 8E+meD66pfSG2J2fcxGz55MTfQ4HJ/Fo7wIGYK5bNkykGBIyMDIxMDYwODE0NTE0
# OS4xNFowBIACAfSggdCkgc0wgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMx
# JjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOjIyNjQtRTMzRS03ODBDMSUwIwYDVQQD
# ExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNloIIOOTCCBPEwggPZoAMCAQIC
# EzMAAAFKpPcxxP8iokkAAAAAAUowDQYJKoZIhvcNAQELBQAwfDELMAkGA1UEBhMC
# VVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNV
# BAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRp
# bWUtU3RhbXAgUENBIDIwMTAwHhcNMjAxMTEyMTgyNTU4WhcNMjIwMjExMTgyNTU4
# WjCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcT
# B1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UE
# CxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0aW9uczEmMCQGA1UECxMdVGhhbGVz
# IFRTUyBFU046MjI2NC1FMzNFLTc4MEMxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1l
# LVN0YW1wIFNlcnZpY2UwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDe
# yihmZKJYLL1RGjSjE1WWYBJfKIbC4B0eIFBVi2b1sy23oA6ESLaXxXfvZmltoTxZ
# YE/sL+5cX+jgeBxWGYB3yKXGYRlOv3m7Mpl2AJgCsyqYe9acSVORdtvGE0ky3KEg
# CFDQWVXUxCGSCxD0+YCO+2LLu2CjLn0pomT86mJZBF9v3R4TnKKPdM4CCUUxtbtp
# Be8Omuw+dMhyhOOnhhMKsIxMREQgjbRQQ0K032CA/yHI9MyopGI4iUWmjzY57wWk
# Sf3hZBs/IA9l8mF45bDYwxj2hj0E7f0Zt568XMlxsgiCIVnQTFzEy5ewTAyiniwU
# NHeqRX0tS0SaPqWiigYlAgMBAAGjggEbMIIBFzAdBgNVHQ4EFgQUcYxhGDH6wIY1
# ipP/fX64JiqpP+EwHwYDVR0jBBgwFoAU1WM6XIoxkPNDe3xGG8UzaFqFbVUwVgYD
# VR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwv
# cHJvZHVjdHMvTWljVGltU3RhUENBXzIwMTAtMDctMDEuY3JsMFoGCCsGAQUFBwEB
# BE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9j
# ZXJ0cy9NaWNUaW1TdGFQQ0FfMjAxMC0wNy0wMS5jcnQwDAYDVR0TAQH/BAIwADAT
# BgNVHSUEDDAKBggrBgEFBQcDCDANBgkqhkiG9w0BAQsFAAOCAQEAUQuu3UY4BRUv
# ZL+9lX3vIEPh4NxaV9k2MjquJ67T6vQ9+lHcna9om2cuZ+y6YV71ttGw07oFB4sL
# sn1p5snNqBHr6PkqzQs8V3I+fVr/ZUKQYLS+jjOesfr9c2zc6f5qDMJN1L8rBOWn
# +a5LXxbT8emqanI1NSA7dPYV/NGQM6j35Tz8guQo9yfA0IpUM9v080mb3G4AjPb7
# sC7vafW2YSXpjT/vty6x5HcnHx2X947+0AQIoBL8lW9pq55aJhSCgsiVtXDqwYyK
# sp7ULeTyvMysV/8mZcokW6/HNA0MPLWKV3sqK4KFXrfbABfrd4P3GM1aIFuKsIbs
# mZhJk5U0ijCCBnEwggRZoAMCAQICCmEJgSoAAAAAAAIwDQYJKoZIhvcNAQELBQAw
# gYgxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdS
# ZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xMjAwBgNVBAMT
# KU1pY3Jvc29mdCBSb290IENlcnRpZmljYXRlIEF1dGhvcml0eSAyMDEwMB4XDTEw
# MDcwMTIxMzY1NVoXDTI1MDcwMTIxNDY1NVowfDELMAkGA1UEBhMCVVMxEzARBgNV
# BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jv
# c29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAg
# UENBIDIwMTAwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCpHQ28dxGK
# OiDs/BOX9fp/aZRrdFQQ1aUKAIKF++18aEssX8XD5WHCdrc+Zitb8BVTJwQxH0Eb
# GpUdzgkTjnxhMFmxMEQP8WCIhFRDDNdNuDgIs0Ldk6zWczBXJoKjRQ3Q6vVHgc2/
# JGAyWGBG8lhHhjKEHnRhZ5FfgVSxz5NMksHEpl3RYRNuKMYa+YaAu99h/EbBJx0k
# ZxJyGiGKr0tkiVBisV39dx898Fd1rL2KQk1AUdEPnAY+Z3/1ZsADlkR+79BL/W7l
# msqxqPJ6Kgox8NpOBpG2iAg16HgcsOmZzTznL0S6p/TcZL2kAcEgCZN4zfy8wMlE
# XV4WnAEFTyJNAgMBAAGjggHmMIIB4jAQBgkrBgEEAYI3FQEEAwIBADAdBgNVHQ4E
# FgQU1WM6XIoxkPNDe3xGG8UzaFqFbVUwGQYJKwYBBAGCNxQCBAweCgBTAHUAYgBD
# AEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMBAf8wHwYDVR0jBBgwFoAU1fZW
# y4/oolxiaNE9lJBb186aGMQwVgYDVR0fBE8wTTBLoEmgR4ZFaHR0cDovL2NybC5t
# aWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWljUm9vQ2VyQXV0XzIwMTAt
# MDYtMjMuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggrBgEFBQcwAoY+aHR0cDovL3d3
# dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNSb29DZXJBdXRfMjAxMC0wNi0y
# My5jcnQwgaAGA1UdIAEB/wSBlTCBkjCBjwYJKwYBBAGCNy4DMIGBMD0GCCsGAQUF
# BwIBFjFodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vUEtJL2RvY3MvQ1BTL2RlZmF1
# bHQuaHRtMEAGCCsGAQUFBwICMDQeMiAdAEwAZQBnAGEAbABfAFAAbwBsAGkAYwB5
# AF8AUwB0AGEAdABlAG0AZQBuAHQALiAdMA0GCSqGSIb3DQEBCwUAA4ICAQAH5ohR
# DeLG4Jg/gXEDPZ2joSFvs+umzPUxvs8F4qn++ldtGTCzwsVmyWrf9efweL3HqJ4l
# 4/m87WtUVwgrUYJEEvu5U4zM9GASinbMQEBBm9xcF/9c+V4XNZgkVkt070IQyK+/
# f8Z/8jd9Wj8c8pl5SpFSAK84Dxf1L3mBZdmptWvkx872ynoAb0swRCQiPM/tA6WW
# j1kpvLb9BOFwnzJKJ/1Vry/+tuWOM7tiX5rbV0Dp8c6ZZpCM/2pif93FSguRJuI5
# 7BlKcWOdeyFtw5yjojz6f32WapB4pm3S4Zz5Hfw42JT0xqUKloakvZ4argRCg7i1
# gJsiOCC1JeVk7Pf0v35jWSUPei45V3aicaoGig+JFrphpxHLmtgOR5qAxdDNp9Dv
# fYPw4TtxCd9ddJgiCGHasFAeb73x4QDf5zEHpJM692VHeOj4qEir995yfmFrb3ep
# gcunCaw5u+zGy9iCtHLNHfS4hQEegPsbiSpUObJb2sgNVZl6h3M7COaYLeqN4DMu
# Ein1wC9UJyH3yKxO2ii4sanblrKnQqLJzxlBTeCG+SqaoxFmMNO7dDJL32N79ZmK
# LxvHIa9Zta7cRDyXUHHXodLFVeNp3lfB0d4wwP3M5k37Db9dT+mdHhk4L7zPWAUu
# 7w2gUDXa7wknHNWzfjUeCLraNtvTX4/edIhJEqGCAsswggI0AgEBMIH4oYHQpIHN
# MIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQL
# ExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMg
# VFNTIEVTTjoyMjY0LUUzM0UtNzgwQzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIaAxUAvATuhoUgysEzdykE1bRB4oh6
# a5iggYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDANBgkqhkiG
# 9w0BAQUFAAIFAORp/LAwIhgPMjAyMTA2MDgyMjI2NTZaGA8yMDIxMDYwOTIyMjY1
# NlowdDA6BgorBgEEAYRZCgQBMSwwKjAKAgUA5Gn8sAIBADAHAgEAAgIRPTAHAgEA
# AgIRjTAKAgUA5GtOMAIBADA2BgorBgEEAYRZCgQCMSgwJjAMBgorBgEEAYRZCgMC
# oAowCAIBAAIDB6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEBBQUAA4GBAB/2utXA
# oFVoXRRnFRyaTyL+VbCncG4kxB1e32RZnbgSEmzWqpBaPy5DbZ3S2SHVyg03NmJ3
# jaUTBnMGhOB9ChND8Rwe8Y+IYpDNQ8kJ2oBh6rh0EK1Gcm3flHPTmqofVwb0iFw+
# OGu04t/34r8BF78MXuTpl0D11iEZYg97MZtlMYIDDTCCAwkCAQEwgZMwfDELMAkG
# A1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQx
# HjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9z
# b2Z0IFRpbWUtU3RhbXAgUENBIDIwMTACEzMAAAFKpPcxxP8iokkAAAAAAUowDQYJ
# YIZIAWUDBAIBBQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRABBDAvBgkq
# hkiG9w0BCQQxIgQgrhx8uMXcO3OxOMQc6IGG2ReFjSfSS7DfzItGgXuvo98wgfoG
# CyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCBsHZLXrbnbV/5J+2KvwFWIgVmQavp+
# BBVUPM1A9yJRAzCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEw
# AhMzAAABSqT3McT/IqJJAAAAAAFKMCIEINOeInwAjuWLuc1TevOGtYyLb+o2VkrF
# H1fgi4ISLG8+MA0GCSqGSIb3DQEBCwUABIIBAHCaJsKPpCps4yuikjzyfBUJJOLk
# QQfZ0yjphBCt5i338wG14bHzbrlakqAmDvondCShyLFF9i7B1jYYkDrqHJxZWcxM
# fEUuUcRA8LyyxM8f8CYlwjr69ZeYctJrlhBsNrkEaDsN6/4t2Hp3btUWGzuoCYOI
# oFK2KJrDuG3DMKlz0MliRFHGd7RH3biu0/NyQnUANAr+tBJl39F9YcGABp5cJjSJ
# JDOxbs2Ds94OktznSUdfAy/cl4QDHtXX7BrURKV5YtUlS6EupJ16e3Pm9JAmfP2T
# B+513dmAij1VtiBFd4ZusBjgUiqBHNnavsnZkk00Q9Z5FjClWU5h09nG+N8=
# SIG # End signature block
