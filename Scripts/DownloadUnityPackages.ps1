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
# MIIrZgYJKoZIhvcNAQcCoIIrVzCCK1MCAQExDzANBglghkgBZQMEAgEFADB5Bgor
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
# lu5jKbKdjW6f5HJ+Ir36JVMt0PWH9LHLEOlky2KZvgKAlCUxghlDMIIZPwIBATBY
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
# lbZUf6higGBpvYnn9E0vspVFRlGYzZuAmMawoYIXCzCCFwcGCisGAQQBgjcDAwEx
# ghb3MIIW8wYJKoZIhvcNAQcCoIIW5DCCFuACAQMxDzANBglghkgBZQMEAgEFADCC
# AVUGCyqGSIb3DQEJEAEEoIIBRASCAUAwggE8AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEIA6qUCgJHk1gEfVdRUJ4DvKcUHZFDQvdcFKdoN8/r8qxAgZi
# 2t3fNNUYEzIwMjIwNzIyMjIzMzMxLjc0NFowBIACAfSggdSkgdEwgc4xCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xKTAnBgNVBAsTIE1pY3Jvc29m
# dCBPcGVyYXRpb25zIFB1ZXJ0byBSaWNvMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVT
# Tjo2MEJDLUUzODMtMjYzNTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAg
# U2VydmljZaCCEV4wggcQMIIE+KADAgECAhMzAAABpllFgzlNnutLAAEAAAGmMA0G
# CSqGSIb3DQEBCwUAMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9u
# MRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRp
# b24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIy
# MDMwMjE4NTEyMVoXDTIzMDUxMTE4NTEyMVowgc4xCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xKTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRpb25z
# IFB1ZXJ0byBSaWNvMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjo2MEJDLUUzODMt
# MjYzNTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZTCCAiIw
# DQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBANmYv3tSI+fJ/NQJnjz7JvCnc+Xm
# 0rKoe9YKD4MvMYCul7egdrT/zv5vFbQgjNQ74672fNweaztkR65V8y29u5PL2sf0
# 1p+uche0Zu4tSig+GsQ6ZQl9tjPRAY/3ITBHDeIYyvq8Wne9+7NoPLhxDSO6dtX7
# YCuQ4zcTP3SE6MvB4b5NighdtvoZVaYk1lXpjUTfdmKoX1ABq1sJbULSnSi0Qd4v
# vl3mZ9jxwv9dR/nlZP62lrZYZq7LPtHD6BlmclB5PT89DnSm1sjaZnFHrKzOsmq5
# GlmL5SFugCCZOoKz133FJeQaFMcXBZSCQjNABWBbHIRCE1ysHHG83DdonRmnC8EO
# lYeRwTWz/QCz6q0riOIbYyC/A2BgUEpu9/9EymrTsyMr2/zS8GdEybQ5W7f0Wrcr
# mKB/Y62+g6TmfOS8NtU+L1jGoKNG6Q5RlfJwZu8J/Q9dl4OxyHKuy78+wm6HsF7u
# AizpsWh63UUaoK/OGQiBG3NJ+kef5eWpnva4ZJfhAnqYTAZD1uHgf8VfQjnl0BB2
# YXzK9WaTqde8d+8qCxVKr5hJYvbO+X3+2k5PCirUK/SboreX+xUhVaQEhVDYqlat
# yPttI7Z2IrkhMzwFvc+p0QeyMiNmo2cBZejx8icDOcUidwymDUYqGPE7MA8vtKW3
# feeSSYJsCEkuUO/vAgMBAAGjggE2MIIBMjAdBgNVHQ4EFgQUOlQhO/zGlqK99UkN
# L/Gu/AryN9gwHwYDVR0jBBgwFoAUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXwYDVR0f
# BFgwVjBUoFKgUIZOaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwv
# TWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3JsMGwGCCsG
# AQUFBwEBBGAwXjBcBggrBgEFBQcwAoZQaHR0cDovL3d3dy5taWNyb3NvZnQuY29t
# L3BraW9wcy9jZXJ0cy9NaWNyb3NvZnQlMjBUaW1lLVN0YW1wJTIwUENBJTIwMjAx
# MCgxKS5jcnQwDAYDVR0TAQH/BAIwADATBgNVHSUEDDAKBggrBgEFBQcDCDANBgkq
# hkiG9w0BAQsFAAOCAgEAgMDxWDTpGqLnFoPhm/iDfwHGF8xr2NbrJl8egEg2ThTJ
# sTf0wBE+ZQsnYfrRmXBbe6sCXLVN70qPuI+OEbN5MOai7Bue1/4j5VTkWquH5GZe
# Vat2N+dD7lSUWp0dU8j+uBhBL5GFSmoDVVm+zW2GR2juPI1v254AJTb2l458anlk
# JjGvmYn2BtRS13h/wDR7hrQaI7BgdyHWAV5+HEj5UhrIrrvtwJiivSaUEA3qK6ZK
# /rZIQv/uORDkONw+2pHHIE1SXm/WIlhrVS2HIogfr3JjqvZion6LJSD741j8xVDL
# iClwAbspHoVFjxtxBcMjqPx6aWCJS8vjSoTnhkV4PO55mqsM7Q8XQRGQhA7w4zNQ
# OJu9kD4xFdYpPUmLN/daIcEElofBjGz+sEd1B4yqqIk3u2G4VygTXFmthL8chSo7
# r+GIvTqWKhSA/sanS4N3jCgCCe3FTSJsp4g5nwavLvWAtzcOIvSRorGmAeN0m2wg
# zBK95T/qgrGGDXSos1JNDWRVBnP0qsw1Qoq5G0D8hxvQPs3X43KBv1GJl0wo5rcC
# +9OMWxJlB63gtToQsA1CErYoYLMZtUzJL74jwZk/grpHEQhIhB3sneC8wzGKJuft
# 7YO/HWCpuwdChIjynTnBh+yFGMdg3wRrIbOcw/iKmXZopMTQMOcmIeIwJAezA7Aw
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
# 8cirOoo6CGJ/2XBjU02N7oJtpQUQwXEGahC0HVUzWLOhcGbyoYIC0TCCAjoCAQEw
# gfyhgdSkgdEwgc4xCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
# DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24x
# KTAnBgNVBAsTIE1pY3Jvc29mdCBPcGVyYXRpb25zIFB1ZXJ0byBSaWNvMSYwJAYD
# VQQLEx1UaGFsZXMgVFNTIEVTTjo2MEJDLUUzODMtMjYzNTElMCMGA1UEAxMcTWlj
# cm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIaAxUAanQzrZW9
# TB93Ve7Pa4UPao2ffK2ggYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0Eg
# MjAxMDANBgkqhkiG9w0BAQUFAAIFAOaFXF0wIhgPMjAyMjA3MjIyMTI2NTNaGA8y
# MDIyMDcyMzIxMjY1M1owdjA8BgorBgEEAYRZCgQBMS4wLDAKAgUA5oVcXQIBADAJ
# AgEAAgEFAgH/MAcCAQACAhD8MAoCBQDmhq3dAgEAMDYGCisGAQQBhFkKBAIxKDAm
# MAwGCisGAQQBhFkKAwKgCjAIAgEAAgMHoSChCjAIAgEAAgMBhqAwDQYJKoZIhvcN
# AQEFBQADgYEAdWjqUHNfH+DvXoIdO23DVBJWPI7cYx+KksWlIVVkD1/+0P10oLMb
# S2W2JNdr9j4mTubQf8PdgR2JELlvXRG4xnwUbOJT1z9Bb1Gs87HMj/yXsF/Y5lNd
# r2IYAnIsw2YiAL+W0bCUL0PhcXJ42l4VyUQzSoSPkBwWQ5E7D7p+g08xggQNMIIE
# CQIBATCBkzB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYw
# JAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAaZZRYM5
# TZ7rSwABAAABpjANBglghkgBZQMEAgEFAKCCAUowGgYJKoZIhvcNAQkDMQ0GCyqG
# SIb3DQEJEAEEMC8GCSqGSIb3DQEJBDEiBCBdj5yp8O14HJyY/cp0fxO0Kkn6nsSe
# GtsJ97Ex7jsQgzCB+gYLKoZIhvcNAQkQAi8xgeowgecwgeQwgb0EIIMLGYvDP3R9
# a+EwpslMBBoq3cOhd6ICF+nxMP22BKsNMIGYMIGApH4wfDELMAkGA1UEBhMCVVMx
# EzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoT
# FU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgUENBIDIwMTACEzMAAAGmWUWDOU2e60sAAQAAAaYwIgQglYqOw4i58eD2
# fI8igMG5TLHWQ02/mIlIH/sHJfF54rowDQYJKoZIhvcNAQELBQAEggIATSUDyNSb
# vFneqzksdf3RutapNoHp3FSoLlcj5q9/iwkkNXjrrIEl5mRURP7HtRCk13RqI0Vz
# 0/PYOBlZ0ZzP+kiNLsI5qfztYD/6QKbJdV+yHSTFhnGApUqPAiiJLv4u/iFRKLyX
# Uorq1ozw1UyXWwvKSWHIG5KBsTcKccBtclZIOQJYIRU0m9y9KYeqBLLIXUg8Rbw5
# 0nTrHb10T5qd5H3zeDH5Kp1nnxruTnaeE6ih4rdKIVcYJ03LwOft2RYtDUeJ96eF
# cOVJZj8Q0AOZMb5z2xSTiW8kVSolgscmpjl5r3Y+JrVJR5XrM1nXlF3GSKE6in/M
# 0Z0qfy1MdzS90z7EX9GrD5loUxZfkMdZ2yKhDtdUY6OBQPR7oHgg7AlMEwydqvgE
# 3zdUHJPwuwMLhcNoNsahvjQULvER4ilgN0sKD0tRgi5SrrJunpNZF6haWV6DVUR8
# VfBDtwcnXaGyPAXhcVuEYpcYWBrNZKbiN8d8vOcL11ripITgQytxWhyU9Z4Z21nx
# Z3/NjwWQdBJmRkg1p9lTf/1LHNFF1D7O4+I21o49EybS9GiVnqxkQx6s61ROqmKd
# fSvTvU2ASFn8ViotMdwGtfQJeAuEFe5IM45MXpqTYT1ieqQTt7gk2idDjCMW2hwb
# 32cHoY0gfYYUAcfUi+KevCh7DxD9lFErorI=
# SIG # End signature block
