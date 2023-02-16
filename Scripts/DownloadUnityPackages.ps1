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
            Write-Error "No info found for version $version of package $package."
            throw
        }
        $details = $json.versions.($version).dist
    }
    catch
    {
        Write-Error "Web request to $uri failed."
        Write-Error $_
        throw
    }

    if ($null -eq $details) 
    {
        Write-Error "Failed to get details for $($package.name)@$($package.version) from $($package.registry)."
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

        Write-Warning "SHA1 hashes for $($packageDetails.Name)@$($packageDetails.Version) and $($packageDetails.DownloadPath) do not match."
        Write-Warning "$packageShasum != $localShasum"
        Write-Warning "Deleting file and re-downloading."
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
            Write-Error "SHA1 hashes for $($packageDetails.Name)@$($packageDetails.Version) and $($packageDetails.DownloadPath) do not match."
            Write-Error "$packageShasum != $localShasum"
            throw
        }
    }
    else
    {
        Write-Error "Download failed for $($packageDetails.Name)@$($packageDetails.Version). File not found: $($packageDetails.DownloadPath)."
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

$failedJobs = @()
$failed = $false
$sw = [System.Diagnostics.Stopwatch]::StartNew()

do
{
    $completedJobCount = 0
    foreach ($download in $downloads)
    {
        if ($download.Completed -or $failedJobs -contains $download.JobObject.Id)
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
                $failedJobs += $download.JobObject.Id
                $download.JobObject.ChildJobs[0].Error | Write-Error
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
while ($completedJobCount -lt $downloads.Count)

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
# MIInkwYJKoZIhvcNAQcCoIInhDCCJ4ACAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCCKsXVecpk+m0UT
# R4ZPeKXqgo+7lAuduRLNjFUYmvJ/T6CCDXYwggX0MIID3KADAgECAhMzAAACy7d1
# OfsCcUI2AAAAAALLMA0GCSqGSIb3DQEBCwUAMH4xCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNpZ25p
# bmcgUENBIDIwMTEwHhcNMjIwNTEyMjA0NTU5WhcNMjMwNTExMjA0NTU5WjB0MQsw
# CQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9u
# ZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMR4wHAYDVQQDExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIB
# AQC3sN0WcdGpGXPZIb5iNfFB0xZ8rnJvYnxD6Uf2BHXglpbTEfoe+mO//oLWkRxA
# wppditsSVOD0oglKbtnh9Wp2DARLcxbGaW4YanOWSB1LyLRpHnnQ5POlh2U5trg4
# 3gQjvlNZlQB3lL+zrPtbNvMA7E0Wkmo+Z6YFnsf7aek+KGzaGboAeFO4uKZjQXY5
# RmMzE70Bwaz7hvA05jDURdRKH0i/1yK96TDuP7JyRFLOvA3UXNWz00R9w7ppMDcN
# lXtrmbPigv3xE9FfpfmJRtiOZQKd73K72Wujmj6/Su3+DBTpOq7NgdntW2lJfX3X
# a6oe4F9Pk9xRhkwHsk7Ju9E/AgMBAAGjggFzMIIBbzAfBgNVHSUEGDAWBgorBgEE
# AYI3TAgBBggrBgEFBQcDAzAdBgNVHQ4EFgQUrg/nt/gj+BBLd1jZWYhok7v5/w4w
# RQYDVR0RBD4wPKQ6MDgxHjAcBgNVBAsTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEW
# MBQGA1UEBRMNMjMwMDEyKzQ3MDUyODAfBgNVHSMEGDAWgBRIbmTlUAXTgqoXNzci
# tW2oynUClTBUBgNVHR8ETTBLMEmgR6BFhkNodHRwOi8vd3d3Lm1pY3Jvc29mdC5j
# b20vcGtpb3BzL2NybC9NaWNDb2RTaWdQQ0EyMDExXzIwMTEtMDctMDguY3JsMGEG
# CCsGAQUFBwEBBFUwUzBRBggrBgEFBQcwAoZFaHR0cDovL3d3dy5taWNyb3NvZnQu
# Y29tL3BraW9wcy9jZXJ0cy9NaWNDb2RTaWdQQ0EyMDExXzIwMTEtMDctMDguY3J0
# MAwGA1UdEwEB/wQCMAAwDQYJKoZIhvcNAQELBQADggIBAJL5t6pVjIRlQ8j4dAFJ
# ZnMke3rRHeQDOPFxswM47HRvgQa2E1jea2aYiMk1WmdqWnYw1bal4IzRlSVf4czf
# zx2vjOIOiaGllW2ByHkfKApngOzJmAQ8F15xSHPRvNMmvpC3PFLvKMf3y5SyPJxh
# 922TTq0q5epJv1SgZDWlUlHL/Ex1nX8kzBRhHvc6D6F5la+oAO4A3o/ZC05OOgm4
# EJxZP9MqUi5iid2dw4Jg/HvtDpCcLj1GLIhCDaebKegajCJlMhhxnDXrGFLJfX8j
# 7k7LUvrZDsQniJZ3D66K+3SZTLhvwK7dMGVFuUUJUfDifrlCTjKG9mxsPDllfyck
# 4zGnRZv8Jw9RgE1zAghnU14L0vVUNOzi/4bE7wIsiRyIcCcVoXRneBA3n/frLXvd
# jDsbb2lpGu78+s1zbO5N0bhHWq4j5WMutrspBxEhqG2PSBjC5Ypi+jhtfu3+x76N
# mBvsyKuxx9+Hm/ALnlzKxr4KyMR3/z4IRMzA1QyppNk65Ui+jB14g+w4vole33M1
# pVqVckrmSebUkmjnCshCiH12IFgHZF7gRwE4YZrJ7QjxZeoZqHaKsQLRMp653beB
# fHfeva9zJPhBSdVcCW7x9q0c2HVPLJHX9YCUU714I+qtLpDGrdbZxD9mikPqL/To
# /1lDZ0ch8FtePhME7houuoPcMIIHejCCBWKgAwIBAgIKYQ6Q0gAAAAAAAzANBgkq
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
# Z25pbmcgUENBIDIwMTECEzMAAALLt3U5+wJxQjYAAAAAAsswDQYJYIZIAWUDBAIB
# BQCgga4wGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEO
# MAwGCisGAQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEIChipHQwTmCiYwllBrgP/9R5
# een3BTKB2yXDEdCdMTmlMEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8A
# cwBvAGYAdKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEB
# BQAEggEAS27UVJU9ROecv65xkom5nnn9s3kyTHJnb8qtvkBftKfA3151Eu4e4ICm
# Cgp34ayAT0PDrYFacMveQlRfSGZPwm3t3OFdM0eR6qOA1jA6+Pb+z1sxHzMrGaVD
# lGnmImebH3dGNLSZG/2lnn/ul+P9o6d5daOnU5W6+dqB/yzitybNCv9paPQ2M3Ty
# aDrM7OP7AzUtD6BZt6TXOOvr2trFh16+9qNChyIbEuU45dFTfnHKkKBsm9zC3dBs
# iVyP4j+ZyZHG561puSjAwPF2agz8u5fSvzubVWqNHKzs1nOu7L35lTe/fv8863Mx
# LAXIy6l3kfMMjd+S+rqVuKd1yxXuCKGCFv0wghb5BgorBgEEAYI3AwMBMYIW6TCC
# FuUGCSqGSIb3DQEHAqCCFtYwghbSAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFRBgsq
# hkiG9w0BCRABBKCCAUAEggE8MIIBOAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFl
# AwQCAQUABCCXmS818tXxDStLSNi3o89QgIuDyEa4n6oeRDBn5VZ5SwIGY+ZwXkzB
# GBMyMDIzMDIxNTEwNDEwMS40NThaMASAAgH0oIHQpIHNMIHKMQswCQYDVQQGEwJV
# UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UE
# ChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1l
# cmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjoyMjY0LUUz
# M0UtNzgwQzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaCC
# EVQwggcMMIIE9KADAgECAhMzAAABwT6gg5zgCa/FAAEAAAHBMA0GCSqGSIb3DQEB
# CwUAMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
# EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNV
# BAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIyMTEwNDE5MDEy
# N1oXDTI0MDIwMjE5MDEyN1owgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMx
# JjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOjIyNjQtRTMzRS03ODBDMSUwIwYDVQQD
# ExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNlMIICIjANBgkqhkiG9w0BAQEF
# AAOCAg8AMIICCgKCAgEA5LHXMydw2hUC4pJU0I5uPJnMeRm8LKC4xaIDu3Fxx3Ip
# Z/We2qXLj4NOmow/WPFeY4vaT4/S4T9xoDsFGg5wEJM6OLZVfa7BUNu0tDt4rkl7
# QBYNHzz6pcr9bwaq2qm7x6P9yi5W0Y8sjoj+QTgtmmXoxCoNXhJ1oG6GbqADQXDZ
# kTcDjIAiteE6TxrhBpIb7e6upifTGZNfcChPfuzHq61FSIwJ0XCxcaR1BwAlSKhb
# /NUOuQGPr9Zzd6OnIcA+RctxwKgfOKB9aWEEHlt0jhKKgpEBvcJnMMP+WaTwmMho
# b1e+hoCEFx/nI0YHupi6082kFdNFraE72msOYQrwrUyWCeSmN202LZDpTzxZVty6
# QrBOk+f+BErsR+M5evkKuUTWVJHI3vtNgb6K5+gk6EuQw0ocsDdspiPp+qlxBaW5
# 0yUbr6wnfzYjJh7QkPcfBIZbJAhWQHaV0uS3T7OkObdCssCRMWH7VWUAeSbemuUq
# OXCR7rdpFTfY/SXKO9lCIQBAQSh+wzwh5Zv1b+jT2zWwVl82By3YHmST8b8CKnRX
# SCjLtgoyy7ERLwkbzPIkCfBXcyVneC1w2/wUnqPiAjK0wQfztfXFfoMQr8YUcLHn
# Atek8OVNPuRIV6bcERbF6rtFXmnjjD4ZwVxIZ/HM4cjeVGsEwkFA9XTzqX9W1P8C
# AwEAAaOCATYwggEyMB0GA1UdDgQWBBRfr2MJ6x7yE+gP5uX9xWGTwpRC+jAfBgNV
# HSMEGDAWgBSfpxVdAF5iXYP05dJlpxtTNRnpcjBfBgNVHR8EWDBWMFSgUqBQhk5o
# dHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NybC9NaWNyb3NvZnQlMjBU
# aW1lLVN0YW1wJTIwUENBJTIwMjAxMCgxKS5jcmwwbAYIKwYBBQUHAQEEYDBeMFwG
# CCsGAQUFBzAChlBodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NlcnRz
# L01pY3Jvc29mdCUyMFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNydDAMBgNV
# HRMBAf8EAjAAMBMGA1UdJQQMMAoGCCsGAQUFBwMIMA0GCSqGSIb3DQEBCwUAA4IC
# AQBfuiaBgsecHvM90RZiTDlfHblL09r9X+5q9ckuMR0Bs1Sr5B2MALhT5Y0R3ggL
# ufRX6RQQbSc7WxRXIMr5tFEgB5zy/7Yg81Cn2dhTf1GzjCb7/n3wtJSGtr2QwHsa
# 1ehYWdMfi+ETLoEX1G79VPFrs0t6Giwpr74tv+CLE3s6m10VOwe80wP4yuT3eiFf
# qRV8poUFSdL2wclgQKoSwbCpbJlNC/ESaDQbbQFli9uO5j2f/G7S4TMG/gyyxvMQ
# 5QJui9Fw2s7qklmozQoX2Ah4aKubKe9/VZveiETNYl1AZPj0kj1g51VNyWjvHw+H
# z1xZekWIpfMXQEi0wrGdWeiW4i8l92rY3ZbdHsErFYqzh6FRFOeXgazNsfkLmwy+
# TK17mA7CTEUzaAWMq5+f9K4Y/3mhB4r6UristkWpdkPWEo8b9tbkdKSY00E+FS5D
# UtjgAdCaRBNaBu8cFYCbErh9roWDxc+Isv8yMQAUDuEwXSy0ExnIAlcVIrhzL40O
# sG2ca5R5BgAevGP1Hj9ej4l/y+Sh0HVcN9N6LmPDmI/MaU2rEZ7Y+jRfCZ1d+l5D
# ESdLXIxDTysYXkT+3VM/1zh6y2s0Zsb/3vPaGnp2zejwf2YlGWl1XpChNZTelF5e
# OCCfSzUUn3qHe7IyyDKhahgbnKpmwcEkMVBs+RHbVkNWqDCCB3EwggVZoAMCAQIC
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
# U046MjI2NC1FMzNFLTc4MEMxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1w
# IFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVAESKOtSK7RVVK+Si+aqFd0YSY+VPoIGD
# MIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNV
# BAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQG
# A1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwDQYJKoZIhvcNAQEF
# BQACBQDnlt17MCIYDzIwMjMwMjE1MTIyNjM1WhgPMjAyMzAyMTYxMjI2MzVaMHQw
# OgYKKwYBBAGEWQoEATEsMCowCgIFAOeW3XsCAQAwBwIBAAICGjEwBwIBAAICEp0w
# CgIFAOeYLvsCAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGEWQoDAqAKMAgC
# AQACAwehIKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQCKwevuc4YRmcUl
# vv2XgqJn+qYqYKwumhGN1JIIcJwhjfKFAEzpjOOfkurkF8hfr2iRxlW097e9SLYj
# 4anAhbGtHUyjzC1zcvRF1LJxs5ZyqSGORLD0KmXquSrh1Otf2OeDbAwl/2aFaf1n
# P8wXV6xPX5BxhzRcR+ADNQKStkLOPTGCBA0wggQJAgEBMIGTMHwxCzAJBgNVBAYT
# AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYD
# VQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBU
# aW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABwT6gg5zgCa/FAAEAAAHBMA0GCWCGSAFl
# AwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQwLwYJKoZIhvcN
# AQkEMSIEIGdSTSfy+yjAoVPpmW1cDwUDQ8XMe1IHYNAabwujmf8iMIH6BgsqhkiG
# 9w0BCRACLzGB6jCB5zCB5DCBvQQgCrkg6tgYHeSgIsN3opR2z7EExWA0YkirkvVY
# STBgdtQwgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3Rv
# bjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0
# aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAA
# AcE+oIOc4AmvxQABAAABwTAiBCC3QDlR4w9004tgtUlCRF+jjh/2VPtd7hIrAt/y
# hh7tYDANBgkqhkiG9w0BAQsFAASCAgB/McZQig19rCo6xPUSvjahBdFzlaK3EboM
# +iXA9yDaH7MewP8ycOZLD1FND781cARsLrzvaoX4hF/au5AMulWNrgyesWUizm6Q
# 8HzhlD8hof4iNc3qwBjnQhcgdqxyvLxtUmUAT0EhyVeq4lJIodt6OXbLEMBN9HPc
# 3OLFgQB7nsx/NXbbA4QbaZ7FhW18camrW5pSro7jpe0hJCdKOH7HhScIpqvO6Aw/
# +w+gCI9X3Q9GeS86FbISZsQxUtQf8xvsQYzlFOmMfn1+u5s8Va/9AmVpVhiOVxoj
# AEn8lGnpBHxWQxlsANj0Nm8x6Ou4PGBSKcgF9dyIRX0Wk75DeDoxUJw71iu3mEJR
# hNf12P3oN7HMBHQRukena8NgcW/xxiSQPtw0z+oYQbUh7iWZyrv17UzM7QFmq1Zx
# xmuULyiq386ENroowda0cau7E+z+am1tx0rVPcJ/8BxGcbWNs5f5Jv58StiFxGXj
# ff8bKnN8Bk1DXMy/Gc98Phq6s5DolDpOaMsQbKl+sJ3uMXa9djYuxKNb2vWQRCE5
# vtt7ZFZNqEswHdPyaeIExB92RU3v4t9bZTfOFVeThPod+tOevtV12A6Yiby3uR0o
# zG6inNNrXlREbdmoDIoKK4h3b5LqO+YVNJvtl4uE+RHzv74wyJ4EEp+c6ESAJorb
# SnsrZ7cF0Q==
# SIG # End signature block
