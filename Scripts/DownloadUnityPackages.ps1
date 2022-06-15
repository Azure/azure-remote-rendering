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
# MIIrWgYJKoZIhvcNAQcCoIIrSzCCK0cCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBWlogFMezq0pvD
# sPGoRks1M6BvinvuwYCs5ssbdQ2Ux6CCEXkwggiJMIIHcaADAgECAhM2AAABfv9v
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
# lu5jKbKdjW6f5HJ+Ir36JVMt0PWH9LHLEOlky2KZvgKAlCUxghk3MIIZMwIBATBY
# MEExEzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTAT
# BgNVBAMTDEFNRSBDUyBDQSAwMQITNgAAAX7/b/0EpCVYEgACAAABfjANBglghkgB
# ZQMEAgEFAKCBrjAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQggdcvwzj9ia85Y5bv
# VABx5/FrnSGV98VAQ+HuHrXdftIwQgYKKwYBBAGCNwIBDDE0MDKgFIASAE0AaQBj
# AHIAbwBzAG8AZgB0oRqAGGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbTANBgkqhkiG
# 9w0BAQEFAASCAQBUG6b2J0dXpG5dEIslnCbpH4ZYTDwgibD3T83EAU6jsN2ny20o
# CUBxb/zxwMExsCqa79uq2TplK8b0+h57fY6fdu2U26J0RixYJV+ARfvasxpHPVY5
# kesR06DMABIPn6R9cGNsEjB5+agWXmH9ocEL6kNi5WaVQ1F4b2p1lt6UYHlX/0L1
# Ftt1WgLgR4ovk8hJcA2XG9Ufxs6YpIhy15o/MqwZPY/bXBP2OGdjiQ6rgUtD/xtV
# 8u6yLOK0sbj4r69eLU+xMS3vEu6j4KEHauhJbnIisBgnMgUM/Kurt/APScimb40r
# lbZUf6higGBpvYnn9E0vspVFRlGYzZuAmMawoYIW/zCCFvsGCisGAQQBgjcDAwEx
# ghbrMIIW5wYJKoZIhvcNAQcCoIIW2DCCFtQCAQMxDzANBglghkgBZQMEAgEFADCC
# AVAGCyqGSIb3DQEJEAEEoIIBPwSCATswggE3AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEIA6qUCgJHk1gEfVdRUJ4DvKcUHZFDQvdcFKdoN8/r8qxAgZi
# gjw9ndkYEjIwMjIwNjE1MTE0MzQyLjY4WjAEgAIB9KCB0KSBzTCByjELMAkGA1UE
# BhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAc
# BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0
# IEFtZXJpY2EgT3BlcmF0aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046M0JC
# RC1FMzM4LUU5QTExJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZp
# Y2WgghFXMIIHDDCCBPSgAwIBAgITMwAAAZ3+ieX5e7tMwAABAAABnTANBgkqhkiG
# 9w0BAQsFADB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYw
# JAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDAeFw0yMTEyMDIx
# OTA1MTlaFw0yMzAyMjgxOTA1MTlaMIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRp
# b25zMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjozQkJELUUzMzgtRTlBMTElMCMG
# A1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZTCCAiIwDQYJKoZIhvcN
# AQEBBQADggIPADCCAgoCggIBAOARaHrQHEkW5H6aUW4UK0beZHZcc0J88xNdynTp
# h7AC1LVtsbMruEMLUlEx62FyaIoz95t0Jwbq/qTiVDIuVJoeYeQhQLmk0S2W63Om
# xU36Oj41t3K73DQEyHgrN924t3Ft1rVXO0oNJeMTd4SXk5/7mFjekeglf02H/mvi
# 1zg2+q3229Vxh4jGlyFnzUArf5TOkD6FxHodLrtcDz5xBQHV8bJGhWHWwK7T6h91
# UNxY4b+1xIq0lyH55EBUN7shGbzh8+w9MXPZ0glEkD89RplH7fFbgV3Vlss1r/Ax
# ure9pi0qiBpJmKILJTFTubCgDfaLJNwYcLuEfwyBlZU4QG7sJ828zKuxHxC6+7eb
# 3UOqxzmBkczG+B4A70suJppT6SViYVZC8temKVLWINdv/zb5OPAa3ESdzzH8S8uS
# TtSSPi4pnonzKeWA+E9Gp6NygqUewqDFaYPfDMRdbVrT13UvYijTTjDTWLfVAAwH
# 8YuGCaYwgTlnhUjYmWH2xfaeTKHlA6dg7OcQKTjNr1wh0wo/6x17aeG+9xJ2sZdD
# x9Y7/43WaNPRIiRfjVdmOb5AwZHTe1rrel+yPeDcTlrWvSj1oeBy6mFbSXCtsED9
# MYyjnMLxnlYj5HCmvQwCINkbLmHb64zH/b78XhmLIpq4pep3usiXWx4BhBjEpDhJ
# 6YHRAgMBAAGjggE2MIIBMjAdBgNVHQ4EFgQU1t8QjeEq6MycXhGB29rLitkbVeQw
# HwYDVR0jBBgwFoAUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXwYDVR0fBFgwVjBUoFKg
# UIZOaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwvTWljcm9zb2Z0
# JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3JsMGwGCCsGAQUFBwEBBGAw
# XjBcBggrBgEFBQcwAoZQaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9j
# ZXJ0cy9NaWNyb3NvZnQlMjBUaW1lLVN0YW1wJTIwUENBJTIwMjAxMCgxKS5jcnQw
# DAYDVR0TAQH/BAIwADATBgNVHSUEDDAKBggrBgEFBQcDCDANBgkqhkiG9w0BAQsF
# AAOCAgEAhsdynEu3aHQs0nffiII1liy1rYRqe30lR6KnB5sUjBAyKPtIhDzeInhj
# g4vljCWmnC8XnXoCFwd69gxJxjo0BdIAaGnnFi2QRyR5XqA1tyncLgjfKi1a8N30
# pAKHst0iGmJgJ17RIXg3klFlQdcgxzO82F7z50S6IKdLWxaIY9QXM0l+wBw2zVoG
# Qci1pLEzQBUeBl+ArxHaKFWS2KvHBgbRP2jWHQGREnc9+4kX6c6O3X54VhiCr5s4
# tCz9J2BjgNtRV+u0t0SDZNtL6yJnDh2rMz60t3J7lcbImUoFftoizjF/UeHXKYxf
# bhgmWby/Jf5bjHzLK0+bOI0e2yHF/uUp3U+bu37tRTOLxAGFvLS9it+uehbUrCz7
# Pfi6hzb4PZUXGsff3Gr/wpt54Gm4vn74KKmhlCx3lA7k2LzWcGXCL/vUmxkMSiay
# j+TgmKjK9UAzbzXwKCew0mcxllLEzmTJ5F1iH0huMZ09109Vy/SQ8qs1qU+1E7iK
# HQZWQv9rgF8QG1cN4RQiwzgDTRB6EP4RJXcjRRtb5vg1OZtFyOdUWTm3qe4r8WgV
# Bzk3he55gA7DaYwOECgUT7bG1MeTZ7B33EaRsUOXZvq78VuGxRvn5eg/Q90ncVM0
# /ob/tWviwg7Fqvg+ljrvhpAKIxLLDC0hY6ipFL84/+tKMX0T/F8wggdxMIIFWaAD
# AgECAhMzAAAAFcXna54Cm0mZAAAAAAAVMA0GCSqGSIb3DQEBCwUAMIGIMQswCQYD
# VQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEe
# MBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMTIwMAYDVQQDEylNaWNyb3Nv
# ZnQgUm9vdCBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkgMjAxMDAeFw0yMTA5MzAxODIy
# MjVaFw0zMDA5MzAxODMyMjVaMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEw
# MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEA5OGmTOe0ciELeaLL1yR5
# vQ7VgtP97pwHB9KpbE51yMo1V/YBf2xK4OK9uT4XYDP/XE/HZveVU3Fa4n5KWv64
# NmeFRiMMtY0Tz3cywBAY6GB9alKDRLemjkZrBxTzxXb1hlDcwUTIcVxRMTegCjhu
# je3XD9gmU3w5YQJ6xKr9cmmvHaus9ja+NSZk2pg7uhp7M62AW36MEBydUv626GIl
# 3GoPz130/o5Tz9bshVZN7928jaTjkY+yOSxRnOlwaQ3KNi1wjjHINSi947SHJMPg
# yY9+tVSP3PoFVZhtaDuaRr3tpK56KTesy+uDRedGbsoy1cCGMFxPLOJiss254o2I
# 5JasAUq7vnGpF1tnYN74kpEeHT39IM9zfUGaRnXNxF803RKJ1v2lIH1+/NmeRd+2
# ci/bfV+AutuqfjbsNkz2K26oElHovwUDo9Fzpk03dJQcNIIP8BDyt0cY7afomXw/
# TNuvXsLz1dhzPUNOwTM5TI4CvEJoLhDqhFFG4tG9ahhaYQFzymeiXtcodgLiMxhy
# 16cg8ML6EgrXY28MyTZki1ugpoMhXV8wdJGUlNi5UPkLiWHzNgY1GIRH29wb0f2y
# 1BzFa/ZcUlFdEtsluq9QBXpsxREdcu+N+VLEhReTwDwV2xo3xwgVGD94q0W29R6H
# XtqPnhZyacaue7e3PmriLq0CAwEAAaOCAd0wggHZMBIGCSsGAQQBgjcVAQQFAgMB
# AAEwIwYJKwYBBAGCNxUCBBYEFCqnUv5kxJq+gpE8RjUpzxD/LwTuMB0GA1UdDgQW
# BBSfpxVdAF5iXYP05dJlpxtTNRnpcjBcBgNVHSAEVTBTMFEGDCsGAQQBgjdMg30B
# ATBBMD8GCCsGAQUFBwIBFjNodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3Bz
# L0RvY3MvUmVwb3NpdG9yeS5odG0wEwYDVR0lBAwwCgYIKwYBBQUHAwgwGQYJKwYB
# BAGCNxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMB
# Af8wHwYDVR0jBBgwFoAU1fZWy4/oolxiaNE9lJBb186aGMQwVgYDVR0fBE8wTTBL
# oEmgR4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMv
# TWljUm9vQ2VyQXV0XzIwMTAtMDYtMjMuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggr
# BgEFBQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNS
# b29DZXJBdXRfMjAxMC0wNi0yMy5jcnQwDQYJKoZIhvcNAQELBQADggIBAJ1Vffwq
# reEsH2cBMSRb4Z5yS/ypb+pcFLY+TkdkeLEGk5c9MTO1OdfCcTY/2mRsfNB1OW27
# DzHkwo/7bNGhlBgi7ulmZzpTTd2YurYeeNg2LpypglYAA7AFvonoaeC6Ce5732pv
# vinLbtg/SHUB2RjebYIM9W0jVOR4U3UkV7ndn/OOPcbzaN9l9qRWqveVtihVJ9Ak
# vUCgvxm2EhIRXT0n4ECWOKz3+SmJw7wXsFSFQrP8DJ6LGYnn8AtqgcKBGUIZUnWK
# NsIdw2FzLixre24/LAl4FOmRsqlb30mjdAy87JGA0j3mSj5mO0+7hvoyGtmW9I/2
# kQH2zsZ0/fZMcm8Qq3UwxTSwethQ/gpY3UA8x1RtnWN0SCyxTkctwRQEcb9k+SS+
# c23Kjgm9swFXSVRk2XPXfx5bRAGOWhmRaw2fpCjcZxkoJLo4S5pu+yFUa2pFEUep
# 8beuyOiJXk+d0tBMdrVXVAmxaQFEfnyhYWxz/gq77EFmPWn9y8FBSX5+k77L+Dvk
# txW/tM4+pTFRhLy/AsGConsXHRWJjXD+57XQKBqJC4822rpM+Zv/Cuk0+CQ1Zyvg
# DbjmjJnW4SLq8CdCPSWU5nR0W2rRnj7tfqAxM328y+l7vzhwRNGQ8cirOoo6CGJ/
# 2XBjU02N7oJtpQUQwXEGahC0HVUzWLOhcGbyoYICzjCCAjcCAQEwgfihgdCkgc0w
# gcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdS
# ZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNVBAsT
# HE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRoYWxlcyBU
# U1MgRVNOOjNCQkQtRTMzOC1FOUExMSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1T
# dGFtcCBTZXJ2aWNloiMKAQEwBwYFKw4DAhoDFQC36UNJFf3YoXKKPvUmfbQKhLLK
# 4KCBgzCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# JjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMA0GCSqGSIb3
# DQEBBQUAAgUA5lOdgzAiGA8yMDIyMDYxNTA3NTEzMVoYDzIwMjIwNjE2MDc1MTMx
# WjB3MD0GCisGAQQBhFkKBAExLzAtMAoCBQDmU52DAgEAMAoCAQACAhhmAgH/MAcC
# AQACAhHTMAoCBQDmVO8DAgEAMDYGCisGAQQBhFkKBAIxKDAmMAwGCisGAQQBhFkK
# AwKgCjAIAgEAAgMHoSChCjAIAgEAAgMBhqAwDQYJKoZIhvcNAQEFBQADgYEAmprm
# EktrvF45WnVSmjoAOkU9IDH9ILWYu7wyMO0cbyaHc3Z2Q47ZWJ+3Ap0jW7RuIJdQ
# PRsBzUqwTwzaPEm+Zp170d3VnmyYVAxQsQppV2vkGGr20zfnv6E4Q8dzMXn+mWzf
# vI1XP2El6KlHMUAkkejky+bYVQj+0BWBbt762qQxggQNMIIECQIBATCBkzB8MQsw
# CQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9u
# ZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNy
# b3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAZ3+ieX5e7tMwAABAAABnTAN
# BglghkgBZQMEAgEFAKCCAUowGgYJKoZIhvcNAQkDMQ0GCyqGSIb3DQEJEAEEMC8G
# CSqGSIb3DQEJBDEiBCCRGPqv4J/XRsDJDYvxaV7B3bXYF+ITEhdwitHolw4/lzCB
# +gYLKoZIhvcNAQkQAi8xgeowgecwgeQwgb0EIPUeY63giqBPgDSfgluVf9/MUvIS
# 7g4EM5v6akyVh0WhMIGYMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldh
# c2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBD
# b3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIw
# MTACEzMAAAGd/onl+Xu7TMAAAQAAAZ0wIgQg4McrNG1LrohXGIoKbQx2iqDg34Qa
# kyA6Y6IFsFRJIykwDQYJKoZIhvcNAQELBQAEggIAjwVKII2zmFyWeyuNk2i5gCnz
# 66TejJaGoIopfRoAhkRDtxm3D4+udjmf22WRbmO9ewiRFke/S+5mpgJQGRnv5udp
# 9oBN5X03jlZ2K0ErC3jJyU+cVXlHOSB0I9n/scN01d8vUOaIaZWu2KfIcILHmbtI
# bAdhFj7acd2f2qCc0dcx1H6tvYmhDgxghMV/rJrWqrjGirbkrscUljN9SYS4ULub
# mxZc3yznIvSOTnVOq+usPzxuqJHNPh+AhJV/am4GtD11McyoWoMYTcft/SoXw391
# h4//lXcF5zkaXQIXGdo0xys5i3j3xj4Amz8dCZJ76vVBL5iKn4U/PNaPO/A3+Ryf
# zxDmhq1dRvP541XZVDtYYy4EpehJa6hL8ZHlDqTYUFv4Vm5zq0WrbuOHQvg4sNlN
# dqpSl0oTmL0pr7jLv5+ATbEaN190Ygwr79xtivfCDcrLrJSl9yJ9CImC0C2ish7f
# PAMvt9uc8Vy/yKeICNYrNAUQmj1gQcTwYuY0u957AzX5ztDPvpZlC+Vw5jkKMG5S
# B+aeQUqVBIfd6GmjqlKQ5ecLJRnlPwCk7NJQAEpZc8EQ1Pc9a04UumxAqBd7t81k
# bjiZptX/CmLTC/5AGdEpcDJZHXsVr56V7DQ6+CFMPaWvKLnMk0ZEX260YkgHv+DH
# 41YOBeXClbT4WUxqIHs=
# SIG # End signature block
