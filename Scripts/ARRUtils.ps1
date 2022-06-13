# This Powershell script is an example for the usage of the Azure Remote Rendering service
# Documentation: https://docs.microsoft.com/en-us/azure/remote-rendering/samples/powershell-example-scripts

$Global:ARRAuthenticationToken = $null

$ARRAuthenticationEndpoint = "https://sts.mixedreality.azure.com"

$ARRServiceEndpoints = @{
    australiaeast  = "https://remoterendering.australiaeast.mixedreality.azure.com"
    eastus         = "https://remoterendering.eastus.mixedreality.azure.com"
    eastus2        = "https://remoterendering.eastus2.mixedreality.azure.com"
    japaneast      = "https://remoterendering.japaneast.mixedreality.azure.com"
    northeurope    = "https://remoterendering.northeurope.mixedreality.azure.com"
    southcentralus = "https://remoterendering.southcentralus.mixedreality.azure.com"
    southeastasia  = "https://remoterendering.southeastasia.mixedreality.azure.com"
    uksouth        = "https://remoterendering.uksouth.mixedreality.azure.com"
    westeurope     = "https://remoterendering.westeurope.mixedreality.azure.com"
    westus2        = "https://remoterendering.westus2.mixedreality.azure.com"
}

# depending on the chosen size a more powerful VM will be allocated
$ARRAvailableVMSizes = @{
    standard = $true
    premium  = $true
}

$docsAvailableString = "Documentation is available at https://docs.microsoft.com/en-us/azure/remote-rendering/samples/powershell-example-scripts"
function CheckPrerequisites() {
    $azStorageInstalled = Get-Module -ListAvailable -Name Az.Storage
    if (-Not $azStorageInstalled) {
        WriteErrorResponse "Az.Storage module is not installed - Install it via 'Install-Module -Name Az -AllowClobber'. $($docsAvailableString)"
        return $False
    }

    $azAccountsInstalled = Get-Module -ListAvailable -Name Az.Accounts
    if (-Not $azAccountsInstalled) {
        WriteErrorResponse "Az.Accounts module is not installed - Install it via 'Install-Module -Name Az -AllowClobber'. $($docsAvailableString)"
        return $False
    }
    return $True
}

function CheckLogin() {
    $context = Get-AzContext
    if (!$context) {
        WriteErrorResponse "Not logged into a subscription. You need to log in via the Connect-AzAccount command. $($docsAvailableString)"
        return $False
    }
    WriteSuccess "Using Subscription: '$($context.Name)' TenantId: '$($context.Tenant.Id)'"
    return $True
}

# Format output messages
function WriteError([string] $message) {
    Write-Host -ForegroundColor Red $message;
}

function WriteSuccess([string] $message) {
    Write-Host -ForegroundColor Green $message;
}
function WriteSuccessResponse([string] $message) {
    Write-Host -ForegroundColor Green "********************************************************************************************************************";
    WriteInformation($message)
    Write-Host -ForegroundColor Green "********************************************************************************************************************";
}

function WriteErrorResponse([string] $message) {
    Write-Host -ForegroundColor Red "********************************************************************************************************************";
    WriteInformation($message)
    Write-Host -ForegroundColor Red "********************************************************************************************************************";
}

function WriteInformation([string] $message) {
    Write-Host -ForegroundColor White $message;
}

function WriteLine {
    Write-Host `n;
    Write-Host "--------------------------------------------------------------------------------------------------------------------" ;
    Write-Host `n;
}

function WriteProgress($activity, $status) {
    Write-Progress -Activity $activity -Status $status;
}

function HandleException($exception) {
    if ($null -ne $exception.Response.Headers -and $exception.Response.Headers.Contains("MS-CV")) {
        $exceptionObject = "Response's MS-CV is '$($exception.Response.Headers.GetValues('MS-CV'))'`r`n"
    }
    else {
        $exceptionObject = ""
    }

    $exceptionObject += $exception.Response | ConvertTo-Json
    WriteErrorResponse($exceptionObject)
}

function LoadConfigs( [string] $fileLocation = [string]::Empty ) {
    WriteInformation("Loading configuration from file: $configFile ...")

    $config = Get-Content -Path $configFile -Raw | ConvertFrom-Json

    return $config
}

function mergehashtables($htold, $htnew) {
    $keys = $htold.getenumerator() | foreach-object { $_.key }
    $keys | foreach-object {
        $key = $_
        if ($htnew.containskey($key)) {
            $htold.remove($key)
        }
    }
    $htnew = $htold + $htnew
    return $htnew
}
function GetResponseBody($response) {
    $responseBody = ConvertFrom-Json $([string]::new($response.Content))

    return $responseBody
}

$defaultConfigContent = '{
    "accountSettings": {
      "arrAccountId": "<fill in the account ID from the Azure Portal>",
      "arrAccountKey": "<fill in the account key from the Azure Portal>",
      "region": "<select from available regions: australiaeast, eastus, eastus2, japaneast, northeurope, southcentralus, southeastasia, uksouth, westeurope, westus2>",
      "authenticationEndpoint": null,
      "serviceEndpoint": null
    },
    "renderingSessionSettings": {
      "vmSize": "<standard or premium>",
      "maxLeaseTime": "<hh:mm:ss>"
    },
    "assetConversionSettings": {
      "resourceGroup": "<resource group which contains the storage account you created, only needed when uploading or generating SAS>",
      "storageAccountName": "<name of the storage account you created>",
      "blobInputContainerName": "<input container inside the storage container>",
      "blobOutputContainerName": "<output container inside the storage container>",
      "localAssetDirectoryPath": "<fill in a path to a local directory containing your asset (and files referenced from it like textures)>",
      "inputFolderPath": "<optional: base folderpath in the input container for asset upload. uses / as dir separator>",
      "inputAssetPath": "<the path to the asset under inputcontainer/inputfolderpath pointing to the input asset e.g. box.fbx>",
      "outputFolderPath": "<optional: base folderpath in the output container - the converted asset and log files will be placed here>",
      "outputAssetFileName": "<optional: filename for the converted asset, this will be placed in the output container under the outputpath>",
      "storageContext": null,
      "outputContainerSAS": null,
      "inputContainerSAS": null
    }
}'

function GetDefaultConfig() {
    $defaultConfig = ConvertFrom-Json($defaultConfigContent)
    return $defaultConfig
}

# merge config
function LoadConfig(
    [string] $fileLocation,
    [string] $AuthenticationEndpoint,
    [string] $ServiceEndpoint,
    [string] $ArrAccountId,
    [string] $ArrAccountKey,
    [string] $Region,
    [string] $VmSize,
    [string] $MaxLeaseTime,
    [string] $StorageAccountName,
    [string] $ResourceGroup,
    [string] $BlobInputContainerName,
    [string] $BlobOutputContainerName,
    [string] $LocalAssetDirectoryPath,
    [string] $InputAssetPath,
    [string] $InputFolderPath,
    [string] $OutputFolderPath,
    [string] $OutputAssetFileName
) {
    try {
        $configFromFile = Get-Content -Path $fileLocation -Raw | ConvertFrom-Json
    }
    catch {
        WriteError("Could not parse config json file at: $fileLocation. Please ensure that it is a valid json file (use a json linter, often a stray comma can make your file invalid)")
        return $null
    }

    $defaultConfig = GetDefaultConfig
    $config = $defaultConfig
    if ([bool]($configFromFile | get-member -name "accountSettings")) {
        $configFromFile.accountSettings.psobject.properties | ForEach-Object {
            $config.accountSettings | Add-Member -MemberType $_.MemberType -Name $_.Name -Value $_.Value -Force
        }
    }

    if ([bool]($configFromFile | get-member -name "renderingSessionSettings")) {
        $configFromFile.renderingSessionSettings.psobject.properties | ForEach-Object {
            $config.renderingSessionSettings | Add-Member -MemberType $_.MemberType -Name $_.Name -Value $_.Value -Force
        }
    }

    if ([bool]($configFromFile | get-member -name "assetConversionSettings")) {
        $configFromFile.assetConversionSettings.psobject.properties | ForEach-Object {
            $config.assetConversionSettings | Add-Member -MemberType $_.MemberType -Name $_.Name -Value $_.Value -Force
        }
    }

    if (-Not [string]::IsNullOrEmpty($LocalAssetDirectoryPath)) {
        $config.assetConversionSettings.localAssetDirectoryPath = $LocalAssetDirectoryPath
    }

    if (-Not [string]::IsNullOrEmpty($InputAssetPath)) {
        $config.assetConversionSettings.inputAssetPath = $InputAssetPath
    }

    if (-Not [string]::IsNullOrEmpty($OutputAssetFileName)) {
        $config.assetConversionSettings.outputAssetFileName = $OutputAssetFileName
    }

    if (-Not [string]::IsNullOrEmpty($InputFolderPath)) {
        $config.assetConversionSettings.inputFolderPath = $InputFolderPath
    }

    if (-Not [string]::IsNullOrEmpty($OutputFolderPath)) {
        $config.assetConversionSettings.outputFolderPath = $OutputFolderPath
    }

    if (-Not [string]::IsNullOrEmpty($OutputAssetFileName)) {
        $config.assetConversionSettings.outputAssetFileName = $OutputAssetFileName
    }

    if (-Not [string]::IsNullOrEmpty($VmSize)) {
        $config.renderingSessionSettings.vmSize = $VmSize
    }

    if (-Not [string]::IsNullOrEmpty($MaxLeaseTime)) {
        $config.renderingSessionSettings.maxLeaseTime = $MaxLeaseTime
    }

    if (-Not [string]::IsNullOrEmpty($ResourceGroup)) {
        $config.assetConversionSettings.resourceGroup = $ResourceGroup
    }

    if (-Not [string]::IsNullOrEmpty($StorageAccountName)) {
        $config.assetConversionSettings.storageAccountName = $StorageAccountName
    }

    if (-Not [string]::IsNullOrEmpty($BlobInputContainerName)) {
        $config.assetConversionSettings.blobInputContainerName = $BlobInputContainerName
    }

    if (-Not [string]::IsNullOrEmpty($BlobOutputContainerName)) {
        $config.assetConversionSettings.blobOutputContainerName = $BlobOutputContainerName
    }

    if (-Not [string]::IsNullOrEmpty($Region)) {
        $config.accountSettings.region = $Region
    }

    if (-Not [string]::IsNullOrEmpty($ArrAccountId)) {
        $config.accountSettings.arrAccountId = $ArrAccountId
    }

    if (-Not [string]::IsNullOrEmpty($ArrAccountKey)) {
        $config.accountSettings.arrAccountKey = $ArrAccountKey
    }

    if ([string]::IsNullOrEmpty($config.accountSettings.authenticationEndpoint)) {
        $config.accountSettings.authenticationEndpoint = $ARRAuthenticationEndpoint
    }

    if ($ARRServiceEndpoints.ContainsKey($config.accountSettings.region) -and [string]::IsNullOrEmpty($config.accountSettings.serviceEndpoint)) {
        $config.accountSettings.serviceEndpoint = $ARRServiceEndpoints[$config.accountSettings.region]
    }

    if (-Not [string]::IsNullOrEmpty($AuthenticationEndpoint)) {
        $config.accountSettings.authenticationEndpoint = $AuthenticationEndpoint
    }

    if (-Not [string]::IsNullOrEmpty($ServiceEndpoint)) {
        $config.accountSettings.serviceEndpoint = $ServiceEndpoint
    }

    return $config
}

function VerifyAccountSettings($config, $defaultConfig, $serviceEndpoint) {
    $ok = $true
    if ($config.accountSettings.arrAccountId -eq $defaultConfig.accountSettings.arrAccountId) {
        WriteError("accountSettings.arrAccountId not filled in - fill in the account ID from the Azure Portal")
        $ok = $false
    }
    else {
        try {
            $guid = [GUID]$config.accountSettings.arrAccountId
        }
        catch {
            $guidString = $config.accountSettings.arrAccountId
            WriteError("accountSettings.arrAccount id : ' $guidString' is not a valid GUID. Please enter a valid GUID")
            $ok = $false
        }
    }
    if ($config.accountSettings.arrAccountKey -eq $defaultConfig.accountSettings.arrAccountKey) {
        WriteError("accountSettings.arrAccountKey not filled in - fill in the account key from the Azure Portal")
        $ok = $false
    }
    if ([string]::IsNullOrEmpty($serviceEndpoint)) {
        $regionString = ($ARRServiceEndpoints.keys -join ", ")
        if ($config.accountSettings.region -eq $defaultConfig.accountSettings.region) {
            WriteError("accountSettings.region not filled in - select a region out of: $regionString")
            $ok = $false
        }
        elseif (-Not $ARRServiceEndpoints.ContainsKey($config.accountSettings.region)) {
            $selectedRegion = $config.accountSettings.region
            WriteError("accountSettings.region '$selectedRegion' not valid - select a region out of: $regionString")
            $ok = $false
        }

    }

    return $ok
}

function VerifyStorageSettings($config, $defaultConfig) {
    $ok = $true

    if ($config.assetConversionSettings.resourceGroup -eq $defaultConfig.assetConversionSettings.resourceGroup) {
        WriteError("assetConversionSettings.resourceGroup not filled in - fill in the resource group your storage containers reside in")
        $ok = $false
    }
    if ($config.assetConversionSettings.storageAccountName -eq $defaultConfig.assetConversionSettings.storageAccountName) {
        WriteError("assetConversionSettings.storageAccountName not filled in - fill in the name of the storage account your storage containers reside in")
        $ok = $false
    }
    if ($config.assetConversionSettings.blobInputContainerName -eq $defaultConfig.assetConversionSettings.blobInputContainerName) {
        WriteError("assetConversionSettings.blobInputContainerName not filled in - fill in the name of the input storage container the models will reside in")
        $ok = $false
    }
    if ($config.assetConversionSettings.blobOutputContainerName -eq $defaultConfig.assetConversionSettings.blobOutputContainerName) {
        WriteError("assetConversionSettings.blobOutputContainerName not filled in - fill in the name of the output storage container where the ingested models will be placed in")
        $ok = $false
    }

    return $ok
}

function VerifyRenderingSessionSettings($config, $defaultConfig) {
    $ok = $true
    $vmSizesString = ($ARRAvailableVMSizes.keys -join ", ")
    if ($config.renderingSessionSettings.vmSize -eq $defaultConfig.renderingSessionSettings.vmSize) {
        WriteError("renderingSessionSettings.vmSize not filled in - select a vmSize out of: $vmSizesString")
        $ok = $false
    }

    try {
        $t = [timespan]$config.renderingSessionSettings.maxLeaseTime
    }
    catch {
        $timespan = $config.renderingSessionSettings.maxLeaseTime
        WriteError("renderingSessionSettings.maxLeaseTime '$timespan' not valid - provide a time in hh:mm:ss format")
        $ok = $false
    }

    return $ok
}

function ValidateConversionSettings($config, $defaultConfig, $OnlyConvertNoUpload) {
    # model settings
    if ($null -eq $config.assetConversionSettings) {
        WriteError("Please ensure the config file has a section for assetConversionSettings ...")
        return $false
    }

    if ($config.assetConversionSettings.inputFolderPath -eq $defaultConfig.assetConversionSettings.inputFolderPath) {
        $config.assetConversionSettings.inputFolderPath = ""
    }
    if (([string]::IsNullOrEmpty($config.assetConversionSettings.inputFolderPath) -eq $False) -And $config.assetConversionSettings.inputFolderPath -notmatch '/$') {
        $config.assetConversionSettings.inputFolderPath += '/'
    }

    if ($config.assetConversionSettings.outputFolderPath -eq $defaultConfig.assetConversionSettings.outputFolderPath) {
        $config.assetConversionSettings.outputFolderPath = ""
    }
    if (([string]::IsNullOrEmpty($config.assetConversionSettings.outputFolderPath) -eq $False) -And $config.assetConversionSettings.outputFolderPath -notmatch '/$') {
        $config.assetConversionSettings.outputFolderPath += '/'
    }

    if ($config.assetConversionSettings.outputAssetFileName -eq $defaultConfig.assetConversionSettings.outputAssetFileName) {
        $config.assetConversionSettings.outputAssetFileName = ""
    }

    if ($config.assetConversionSettings.inputAssetPath -eq $defaultConfig.assetConversionSettings.inputAssetPath) {
        WriteError("assetConversionSettings does not have a inputAssetPath value ... specify the inputAssetPath in config.json or via the -InputAssetPath <path to asset under inputContainer/InputFolderPath> command line argument")
        return $false
    }
    else {
        $extension = [System.IO.Path]::GetExtension($config.assetConversionSettings.inputAssetPath).ToLower()
    }

    if ($OnlyConvertNoUpload -eq $False) {
        if ($config.assetConversionSettings.localAssetDirectoryPath -eq $defaultConfig.assetConversionSettings.localAssetDirectoryPath) {
            WriteError("modelSettings does not have a localAssetDirectoryPath value ... specify the directory containing asset data in config.json or via the -LocalAssetDirectoryPath <path to model> command line argument")
            return $false
        }
    }

    return $true
}

# reads config and gets adds azure specific fields to the config
function AddStorageAccountInformationToConfig($config) {
    # Get Storage Account information
    WriteLine
    WriteInformation ("Populating Storage Account information for file upload...")
    $resourceGroup = $config.assetConversionSettings.resourceGroup
    $storageAccountName = $config.assetConversionSettings.storageAccountName
    $assetConversionSettings = $config.assetConversionSettings

    $storageAccountKeys = (Get-AzStorageAccountKey -ResourceGroupName $resourceGroup -Name $storageAccountName -erroraction 'silentlycontinue')
    if ($null -ne $storageAccountKeys) {
        $storageAccountKey = $storageAccountKeys.Value[0]
        WriteSuccess("Retrieved StorageAccountKey ...")
    }
    else {
        $context = Get-AzContext
        WriteError("Could not retrieve storage account key for storage account named: '$storageAccountName' in resource group '$resourceGroup' using the currently logged in user ")
        WriteError("Ensure that the storage account configuration is correct and the account is accessible to the current logged in user/subscription '$($context.Name)' TenantId: '$($context.Tenant.Id) and allows listing keys.")
        WriteError("In case your organization has more than one subscription you might need to specify the SubscriptionId and Tenant arguments to Connect-AzAccount. Find details at https://docs.microsoft.com/powershell/module/az.accounts/connect-azaccount")
        return $null
    }

    $storageContext = New-AzStorageContext -StorageAccountName $storageAccountName -StorageAccountKey $storageAccountKey

    $assetConversionSettings.storageContext = $storageContext

    WriteSuccess("Successfully added storage settings to the configurations ...")
    if ([bool]($config | get-member -name "assetConversionSettings")) {
        $assetConversionSettings.psobject.properties | ForEach-Object {
            $config.assetConversionSettings | Add-Member -MemberType $_.MemberType -Name $_.Name -Value $_.Value -Force
        }
    }
    return $config
}

function GenerateInputContainerSAS([string]$blobEndPoint, [string]$blobContainerName, $storageContext, [DateTime]$startTime = [DateTime]::Now, [Int]$TokenlifeTimeInHours = 24) {
    WriteLine
    WriteInformation ("Generating Input container SAS ...")

    $endTime = $startTime.AddHours($TokenlifeTimeInHours)

    $inputContainerSAS = New-AzStorageContainerSASToken -Container $blobContainerName -Permission rwdl -ExpiryTime $endTime -Context $storageContext

    if ($null -ne $inputContainerSAS) {
        WriteSuccess("Successfully generated Input container SAS ...")
        WriteInformation("Input container SAS: $inputContainerSAS")
        return $inputContainerSAS
    }
    else {
        WriteError("Unable to generate Input container SAS. Please ensure parameters are valid - Exiting.")
        exit 1
    }
}

function GenerateOutputmodelSASUrl([string]$containerName, [string]$blobPath, $storageContext, [DateTime]$startTime = [DateTime]::Now, [Int]$TokenlifeTimeInHours = 24) {
    WriteLine
    WriteInformation ("Generating SAS URI for ingested model - this URI is valid for $TokenlifeTimeInHours hours")

    $endTime = $startTime.AddHours($TokenlifeTimeInHours)

    $blobSASUri = New-AzStorageBlobSASToken -FullUri -Container $containerName -Blob $blobPath -Permission r -StartTime $startTime -ExpiryTime $endTime -Context $storageContext

    if ($null -ne $blobSASUri) {
        WriteSuccess("Successfully generated model SAS URI")
        return $blobSASUri
    }
    else {
        WriteError("Unable to generate model sas URI Please ensure parameters are valid - Exiting.")
        exit 1
    }
}

function GenerateOutputContainerSAS([string]$blobEndPoint, [string]$blobContainerName, $storageContext, [DateTime]$startTime = [DateTime]::Now, [Int]$TokenlifeTimeInHours = 24) {
    WriteLine
    WriteInformation ("Generating ouptut container SAS...")

    $endTime = $startTime.AddHours($TokenlifeTimeInHours)
    $outputContainerSAS = New-AzStorageContainerSASToken -Context $storageContext -Name $blobContainerName -Permission rwdl -ExpiryTime $endTime

    if ($null -ne $outputContainerSAS) {
        WriteSuccess("Successfully generated output container SAS ...")
        WriteInformation("Container SAS: $outputContainerSAS")
        return $outputContainerSAS
    }
    else {
        WriteError("Unable to generate output container SAS. Please ensure parameters are valid - Exiting.")
        exit 1
    }
}

function GetAuthenticationToken([string]$authenticationEndpoint, [GUID]$accountId, [string]$accountKey) {
    if ($Global:ARRAuthenticationToken) {
        return $Global:ARRAuthenticationToken
    }
    else {
        WriteLine
        WriteInformation ("Getting an authentication token ...")

        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12;
        $webResponse = Invoke-WebRequest -UseBasicParsing -Uri "$authenticationEndpoint/accounts/$accountId/token" -Headers @{ Authorization = "Bearer ${accountId}:$accountKey" }

        if ($webResponse.StatusCode -eq 200) {
            $response = ConvertFrom-Json -InputObject $webResponse.Content
            $Global:ARRAuthenticationToken = $response.AccessToken;

            return $Global:ARRAuthenticationToken
        }
        else {
            WriteError("Unable to get an authentication token - please check your accountId and accountKey - Exiting.")
            exit 1
        }
    }
}

# Create a Session by calling REST API <endpoint>/accounts/<accountId>/sessions/<sessionId>/
# returns a session ID which can be used to retrieve session status
function CreateRenderingSession([string] $authenticationEndpoint, [string] $serviceEndpoint, [string] $accountId, [string] $accountKey, [string] $vmSize = "standard", [string] $maxLeaseTime = "4:0:0", [hashtable] $additionalParameters, [string] $sessionId) {
    try {
        $maxLeaseTimeInMinutes = ([timespan]$maxLeaseTime).TotalMinutes -as [int]

        $body =
        @{
            # defaults to 4 Hours
            maxLeaseTimeMinutes = $maxLeaseTimeInMinutes;
            # defaults to "standard"
            size                = $vmSize;
        }

        if ($additionalParameters) {
            $additionalParameters.Keys | % { $body += @{ $_ = $additionalParameters.Item($_) } }
        }

        if ([string]::IsNullOrEmpty($sessionId)) {
            $sessionId = "Sample-Session-$(New-Guid)"
        }

        $url = "$serviceEndpoint/accounts/$accountId/sessions/${sessionId}?api-version=2021-01-01-preview"

        WriteInformation("Creating Rendering Session ...")
        WriteInformation("  Authentication endpoint: $authenticationEndpoint")
        WriteInformation("  Service endpoint: $serviceEndpoint")
        WriteInformation("  sessionId: $sessionId")
        WriteInformation("  maxLeaseTime: $maxLeaseTime")
        WriteInformation("  size: $vmSize")
        WriteInformation("  additionalParameters: $($additionalParameters | ConvertTo-Json)")

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey

        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method PUT -ContentType "application/json" -Body ($body | ConvertTo-Json) -Headers @{ Authorization = "Bearer $token" }

        WriteSuccess("Successfully created the session with Id: $sessionId")
        #WriteSuccessResponse($response.RawContent)

        return $sessionId
    }
    catch {
        WriteError("Unable to start the rendering session ...")
        HandleException($_.Exception)
        throw
    }
}

# call "<endPoint>/accounts/<accountId>/sessions/<sessionId>/:stop" with Method POST to stop a session
function StopSession([string] $authenticationEndpoint, [string] $serviceEndpoint, [string] $accountId, [string] $accountKey, [string] $sessionId) {
    try {
        $url = "$serviceEndpoint/accounts/$accountId/sessions/$sessionId/:stop?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method POST -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

        WriteSuccessResponse($response.RawContent)
        WriteInformation("Successfully stopped session.")

        return $response
    }
    catch {
        WriteError("Unable to stop session with Id: $sessionId")
        HandleException($_.Exception)
        throw
    }
}

#call REST API <endpoint>/accounts/<accountId>/sessions/<SessionId>
function GetSessionProperties([string] $authenticationEndpoint, [string] $serviceEndpoint, [string] $accountId, [string] $accountKey, [string] $sessionId) {
    try {
        $url = "$serviceEndpoint/accounts/$accountId/sessions/${sessionId}?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -authenticationEndpoint $authenticationEndpoint -accountId $accountId -accountKey $accountKey
        $response = Invoke-WebRequest -UseBasicParsing -Uri $url -Method GET -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

        #WriteSuccessResponse($response.RawContent)

        return $response
    }
    catch {
        WriteError("Unable to get the status of the session with Id: $sessionId")
        HandleException($_.Exception)
        throw
    }
}

function FormatMilliseconds($millisec) {
    
    $secs = [int]($millisec / 1000)

    if ($secs -ge 60) {
        $mins = [Math]::Floor($secs / 60.0)
        $secs = $secs - ($mins * 60)

        if ($mins -eq 1) {
            return "$mins minute $secs seconds"
        }

        return "$mins minutes $secs seconds"
    }

    return "$secs seconds"
}

function GetExePathInArrOf($root, $exeName) {
    if (-not $exeName.EndsWith(".exe")) {
        $exeName = "$($exeName).exe"
    }

    $pathsToTest = @(
        (Join-Path (Join-Path $Root "\Bin\x64_vs2019_win10\Dev\") $exeName),
        (Join-Path (Join-Path $Root "\Bin\x64_vs2022_win10\Dev\") $exeName),
        (Join-Path (Join-Path $Root "\Bin\x64_vs2019_win10\Debug\") $exeName),
        (Join-Path (Join-Path $Root "\Bin\x64_vs2022_win10\Debug\") $exeName))

    foreach ($pathToTest in $pathsToTest) {
        if (Test-Path $pathToTest) {
            return $pathToTest
        }
    }

    $errorMsg = "$exeName not available in $root."

    WriteError $errorMsg
    throw $errorMsg
}
# SIG # Begin signature block
# MIIrXAYJKoZIhvcNAQcCoIIrTTCCK0kCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCByi+V9i0P2c4GR
# y8EyVwpX90neAZf251IZfvfoxi8UIKCCEW4wggh+MIIHZqADAgECAhM2AAABfS9g
# xXjWqxyUAAIAAAF9MA0GCSqGSIb3DQEBCwUAMEExEzARBgoJkiaJk/IsZAEZFgNH
# QkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTATBgNVBAMTDEFNRSBDUyBDQSAwMTAe
# Fw0yMTA5MDkwMTI2MjZaFw0yMjA5MDkwMTI2MjZaMCQxIjAgBgNVBAMTGU1pY3Jv
# c29mdCBBenVyZSBDb2RlIFNpZ24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEK
# AoIBAQCxDjrIsLrnzISHuU969Ihnf0M1l83iB5bMfwoeA4Q3P/vyN6iV0SC1VIh1
# 3ISUWBlwl0mQgQbUMpXse9/ESIO6gHoa4sR8bMP814jzYc4WvbqnZBub4+964sQb
# Bxh+on59uF10CrmdGMHIFBDr/y2TgelGaMDAIUVWXB2tQARag8gdEfzLCXsObPQq
# ejRJF1E+c89RkkKBJF6sRXlsbHOhmaDo9KKipP+6tyugZcARtPV5WzBIuDtkBZ2m
# PXNXDpM4l6ENITDZiWCbNIFNbROqqF0ESJlaGNDGxs/vvD4Y9zo9Pgf5g7GOHHMI
# XT/OiGZLMlKWgWCXCRCvxvKhtI4HAgMBAAGjggWKMIIFhjApBgkrBgEEAYI3FQoE
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
# aG9yaXR5MB0GA1UdDgQWBBTNPf032JeslerhQm8AuxJuuT0UNTAOBgNVHQ8BAf8E
# BAMCB4AwRQYDVR0RBD4wPKQ6MDgxHjAcBgNVBAsTFU1pY3Jvc29mdCBDb3Jwb3Jh
# dGlvbjEWMBQGA1UEBRMNMjM2MTY3KzQ2Nzk3MzCCAeYGA1UdHwSCAd0wggHZMIIB
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
# KwYBBQUHAwMwDQYJKoZIhvcNAQELBQADggEBALJt0CX0joH8RLWbu0fscVYHOgeY
# 5PCnQXYmhX2i0noyi+UC0jxrwziZGOOLVjfJqQODln/sJf16Sb6ay2BwT1OPDh6d
# Na/jCageEEbUgt8xZZ0EbW6lNs7pwUjS346yqvdwU5xoa/zz2p6JtM93YDtET5vn
# PRATUa1sw8iN3akopHGqzSw3AmZ5Pdmty4i/ptEgOysBP0PWvOaRWWRm3izyAa5N
# z491JdMjJy3mFYh15fKjFbsMM9QC6fk8X8n+OOaH2w7WO+g1pKxuu9ckQBPN9CGr
# 4Wl6rsBCg9c0Wk12fXQ3LXOn/ve0VM+ogz8JLnA+vFIG94GLLOLi+XC+8iAwggjo
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
# fiK9+iVTLdD1h/SxyxDpZMtimb4CgJQlMYIZRDCCGUACAQEwWDBBMRMwEQYKCZIm
# iZPyLGQBGRYDR0JMMRMwEQYKCZImiZPyLGQBGRYDQU1FMRUwEwYDVQQDEwxBTUUg
# Q1MgQ0EgMDECEzYAAAF9L2DFeNarHJQAAgAAAX0wDQYJYIZIAWUDBAIBBQCgga4w
# GQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEOMAwGCisG
# AQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEIIAqsOPDcAH8jeFVUAvwb9uBHfKRfqDO
# ES0woxycY/bqMEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8AcwBvAGYA
# dKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEBBQAEggEA
# VOuwmDOeKOhKJUpyEyh3P0ahEpt66E3qV5Ge04IwNVK+womDUhi/LnJc/cLNbKD5
# pcc7IbHqwENPKXJHtYBhjXkxcEohNG69IzxKgOXxWPPU01vu+l2c8XI2rsbxAsQc
# IkRGG7w3kyjo7IY/CyA5lmSr9I9iBDNMFJ4YmFyvE5zcjrM0sY8A1kzEY8XuXfAr
# 2NUMVJBCOCx1Kb82/TN+4xbx5x33+Q6t2SoR5Y0yx7MastckiyrX9ZbBNioJHwp/
# a1+1EpeXBSDGW+pes2/lzKJzXLBfdC5hC7vs9bM2HHDRXbBKq2c0UxKtO5JpuXEX
# DJKKpMWw+KtSECDy7M9ZuqGCFwwwghcIBgorBgEEAYI3AwMBMYIW+DCCFvQGCSqG
# SIb3DQEHAqCCFuUwghbhAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFVBgsqhkiG9w0B
# CRABBKCCAUQEggFAMIIBPAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFlAwQCAQUA
# BCBNG38WMlwmxpJuhCT+qJNQpVDqfvtAoJ3dE7SpzvxVhAIGYoS25IqdGBMyMDIy
# MDYxMzA5MTc0MC4yMDFaMASAAgH0oIHUpIHRMIHOMQswCQYDVQQGEwJVUzETMBEG
# A1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWlj
# cm9zb2Z0IENvcnBvcmF0aW9uMSkwJwYDVQQLEyBNaWNyb3NvZnQgT3BlcmF0aW9u
# cyBQdWVydG8gUmljbzEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046QzRCRC1FMzdG
# LTVGRkMxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2WgghFf
# MIIHEDCCBPigAwIBAgITMwAAAaP7mrOOe4ZDTwABAAABozANBgkqhkiG9w0BAQsF
# ADB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQD
# Ex1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDAeFw0yMjAzMDIxODUxMTZa
# Fw0yMzA1MTExODUxMTZaMIHOMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGlu
# Z3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
# cmF0aW9uMSkwJwYDVQQLEyBNaWNyb3NvZnQgT3BlcmF0aW9ucyBQdWVydG8gUmlj
# bzEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046QzRCRC1FMzdGLTVGRkMxJTAjBgNV
# BAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggIiMA0GCSqGSIb3DQEB
# AQUAA4ICDwAwggIKAoICAQDvvU3Ky3sqCnAqi2zbc+zbdiWz9UxM8zIYvOIEumCy
# OwhenVUgOSNWxQh3MOmRdnhfEImn9KNl0l3/46ebIJlGLTGxouJ3gLVkjSucobeI
# skIQcZ9EyEKhfjYrIgcVvnoTGFhGxSPu3EnV/3VsPv2PPzLvbqt1wiuT9hvmYm1c
# DlR/efiIkxp5qHMVoHbNKpQaWta2IN25fF1XuS9qk1JiQb50Kcdm1K7u9Jbdvx6F
# OWwWyygIQj6ccuJ5rK3Tkdxr+FG3wJraUJ7T++fDUT4YNWwAh9OhZb2yMj/P7kbN
# 8dt9t3WmhqSUGEKGaQAYOtqxQ0yePntOrbfsW376fDPZaPGtWoH8WUNaSE9VZyXW
# jvfIFjIjFuuXXhVIlEflp4EFX79oC7L+qO/jnKc8ukR2SJulhBmfSwbee9TXwrMe
# c9CJb6+kszdEG2liUyyFm18G1FSmHm61xFRTMoblRkB3rGQflcFd/OoWKJzMbNI7
# zPBqTnMdMS8spuNlwPfVUqbLor0yYOKPGtQAiW0wVRaBAN1axUmMznUOr818a8cO
# ov09d/JvlxfsirQBJ4aflHgDIZcO4z/fRAJYBlJdCpHAY02E8/oxMj4Cmna1NaH+
# aBYv6vWA5a1b/R+CbFXvBhzDpD0zaAeNNvI/PDhHuNugbH3Fy5ItKYT6e4q1tAG0
# XQIDAQABo4IBNjCCATIwHQYDVR0OBBYEFFBR+7M8Jgixz00vQaNoqy5yY4uqMB8G
# A1UdIwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8GA1UdHwRYMFYwVKBSoFCG
# Tmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29mdCUy
# MFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggrBgEFBQcBAQRgMF4w
# XAYIKwYBBQUHMAKGUGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2Vy
# dHMvTWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3J0MAwG
# A1UdEwEB/wQCMAAwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZIhvcNAQELBQAD
# ggIBAFry3qdpl8OorgcRrtD7LLZlyOYC5oD5EykJ44GZbKHoqbLWvaJLtDE1cZR1
# XXHQWxXFRzC0UZFBSJHyp2nJcpeXso9N8Hg+m/6VHxcg2QfAGaRlF4U2CzUfD3qT
# Osg+oPtBNZx9DIThqBOlxbn5G5+niHTUxrlsAXhK9gzYhoQxpcGlB+RC894bbsjM
# ligIGBdvAuIssoWHb5RvVTeiZwuJnPxCLedAQh6fGUAJOxwt0TpbYNYLuTYxmklX
# YrGouTiVn+nubGEHQwTWClyXYh3otTeyvi+bNb1fgund07BffgDaYqAQwDhpxUmL
# eD/rrVtdYt+4iyy2/duqQi+C8vvhlNMJc2H5+59tkckJrw9daMomR4ZkbLAwarAP
# p7wlbX5x9fNw3+aAQVbJM2XCU1IwsWmoAyuwKgekANx+5f9khXnqn1/w7XZXuAfr
# z1eJatQgrNANSwfZZs0tL8aEQ7rGPNA0ItdCt0n2StYcsmo/WvKW2RtAbAadjcHO
# MbTgxHgU1qAMxfZKOFendPbhRaSay6FfnvHCVP4U9/kpVu3Z6+XbWL84h06Wbrkb
# +ClOhdzkMzaR3+3AS6VikV0YxmHVZwBm/Dc1usFk42YzAjXQhRu6ZCizDhnajwxX
# X5PhGBOUUhvcsUu+nD316kSlbSWUnCBeuHo512xSLOW4fCsBMIIHcTCCBVmgAwIB
# AgITMwAAABXF52ueAptJmQAAAAAAFTANBgkqhkiG9w0BAQsFADCBiDELMAkGA1UE
# BhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAc
# BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9zb2Z0
# IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTAwHhcNMjEwOTMwMTgyMjI1
# WhcNMzAwOTMwMTgzMjI1WjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGlu
# Z3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
# cmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDCC
# AiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAOThpkzntHIhC3miy9ckeb0O
# 1YLT/e6cBwfSqWxOdcjKNVf2AX9sSuDivbk+F2Az/1xPx2b3lVNxWuJ+Slr+uDZn
# hUYjDLWNE893MsAQGOhgfWpSg0S3po5GawcU88V29YZQ3MFEyHFcUTE3oAo4bo3t
# 1w/YJlN8OWECesSq/XJprx2rrPY2vjUmZNqYO7oaezOtgFt+jBAcnVL+tuhiJdxq
# D89d9P6OU8/W7IVWTe/dvI2k45GPsjksUZzpcGkNyjYtcI4xyDUoveO0hyTD4MmP
# frVUj9z6BVWYbWg7mka97aSueik3rMvrg0XnRm7KMtXAhjBcTyziYrLNueKNiOSW
# rAFKu75xqRdbZ2De+JKRHh09/SDPc31BmkZ1zcRfNN0Sidb9pSB9fvzZnkXftnIv
# 231fgLrbqn427DZM9ituqBJR6L8FA6PRc6ZNN3SUHDSCD/AQ8rdHGO2n6Jl8P0zb
# r17C89XYcz1DTsEzOUyOArxCaC4Q6oRRRuLRvWoYWmEBc8pnol7XKHYC4jMYcten
# IPDC+hIK12NvDMk2ZItboKaDIV1fMHSRlJTYuVD5C4lh8zYGNRiER9vcG9H9stQc
# xWv2XFJRXRLbJbqvUAV6bMURHXLvjflSxIUXk8A8FdsaN8cIFRg/eKtFtvUeh17a
# j54WcmnGrnu3tz5q4i6tAgMBAAGjggHdMIIB2TASBgkrBgEEAYI3FQEEBQIDAQAB
# MCMGCSsGAQQBgjcVAgQWBBQqp1L+ZMSavoKRPEY1Kc8Q/y8E7jAdBgNVHQ4EFgQU
# n6cVXQBeYl2D9OXSZacbUzUZ6XIwXAYDVR0gBFUwUzBRBgwrBgEEAYI3TIN9AQEw
# QTA/BggrBgEFBQcCARYzaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9E
# b2NzL1JlcG9zaXRvcnkuaHRtMBMGA1UdJQQMMAoGCCsGAQUFBwMIMBkGCSsGAQQB
# gjcUAgQMHgoAUwB1AGIAQwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTADAQH/
# MB8GA1UdIwQYMBaAFNX2VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0wS6BJ
# oEeGRWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01p
# Y1Jvb0NlckF1dF8yMDEwLTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYB
# BQUHMAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9v
# Q2VyQXV0XzIwMTAtMDYtMjMuY3J0MA0GCSqGSIb3DQEBCwUAA4ICAQCdVX38Kq3h
# LB9nATEkW+Geckv8qW/qXBS2Pk5HZHixBpOXPTEztTnXwnE2P9pkbHzQdTltuw8x
# 5MKP+2zRoZQYIu7pZmc6U03dmLq2HnjYNi6cqYJWAAOwBb6J6Gngugnue99qb74p
# y27YP0h1AdkY3m2CDPVtI1TkeFN1JFe53Z/zjj3G82jfZfakVqr3lbYoVSfQJL1A
# oL8ZthISEV09J+BAljis9/kpicO8F7BUhUKz/AyeixmJ5/ALaoHCgRlCGVJ1ijbC
# HcNhcy4sa3tuPywJeBTpkbKpW99Jo3QMvOyRgNI95ko+ZjtPu4b6MhrZlvSP9pEB
# 9s7GdP32THJvEKt1MMU0sHrYUP4KWN1APMdUbZ1jdEgssU5HLcEUBHG/ZPkkvnNt
# yo4JvbMBV0lUZNlz138eW0QBjloZkWsNn6Qo3GcZKCS6OEuabvshVGtqRRFHqfG3
# rsjoiV5PndLQTHa1V1QJsWkBRH58oWFsc/4Ku+xBZj1p/cvBQUl+fpO+y/g75LcV
# v7TOPqUxUYS8vwLBgqJ7Fx0ViY1w/ue10CgaiQuPNtq6TPmb/wrpNPgkNWcr4A24
# 5oyZ1uEi6vAnQj0llOZ0dFtq0Z4+7X6gMTN9vMvpe784cETRkPHIqzqKOghif9lw
# Y1NNje6CbaUFEMFxBmoQtB1VM1izoXBm8qGCAtIwggI7AgEBMIH8oYHUpIHRMIHO
# MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVk
# bW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSkwJwYDVQQLEyBN
# aWNyb3NvZnQgT3BlcmF0aW9ucyBQdWVydG8gUmljbzEmMCQGA1UECxMdVGhhbGVz
# IFRTUyBFU046QzRCRC1FMzdGLTVGRkMxJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1l
# LVN0YW1wIFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVAB5f6V5CzAGz2qQsGvhl3N0p
# Qw0ToIGDMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24x
# EDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
# bjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwDQYJKoZI
# hvcNAQEFBQACBQDmUXtIMCIYDzIwMjIwNjEzMTMwMDU2WhgPMjAyMjA2MTQxMzAw
# NTZaMHcwPQYKKwYBBAGEWQoEATEvMC0wCgIFAOZRe0gCAQAwCgIBAAICB6kCAf8w
# BwIBAAICETwwCgIFAOZSzMgCAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGE
# WQoDAqAKMAgCAQACAwehIKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQBr
# w1zEr2of0XIJCTAOxvLg6MRg3ukf0qXOq8QfqaZsuLx6dy+rsxLAK7B6naIuZ8uX
# 9kCpwOMSbcQc3W61nqrcn0Hs7hcvbcuoB8R3/2TZ8oT3Kr3cOY574igWptYIkDDr
# ZN8/JQrEAVS19HtOuOhzcK8gkS1oMxJ6ioJ0RZRcVTGCBA0wggQJAgEBMIGTMHwx
# CzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRt
# b25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1p
# Y3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABo/uas457hkNPAAEAAAGj
# MA0GCWCGSAFlAwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQw
# LwYJKoZIhvcNAQkEMSIEIFKWFBPTsVoWAdgWqjSns09p1KX5GAFubvcx+bY5fzrg
# MIH6BgsqhkiG9w0BCRACLzGB6jCB5zCB5DCBvQQgjPi4sAZxzDKDnf7IG2mMacLx
# CZURGZf6Uz5Jc+nrjf4wgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0Eg
# MjAxMAITMwAAAaP7mrOOe4ZDTwABAAABozAiBCCfcaJhBb0A70o6eX9+jQpCJBzN
# 7iWW0VeCrazspKe6SDANBgkqhkiG9w0BAQsFAASCAgDJbnkmL2iAux5nYKZbQdiu
# wbjCjtSuWE3EWlfBiOfSfNb+fMWxTaJHUA0s1a8xfVfpfS22+Ou4apCLBNnLDYHZ
# qrjcTCGM1hoTbjGBbgWLV1ZLvvE2K8TCzjOUQmmKOPNevmk761d6BQ2hPGf5A6e6
# ChDXIjVwg+BlDVRlVfX27Y28P2W7h2pU3IUWrLKHMguaJCvYNbTBgDDFVRaYbvPQ
# op+Q24nCdyqbBv5APv1JFGte8fuyvEHMWRrU4M6ehKTiMvBuiwvIVgoIR0mu8QXR
# 4Fk27XAUb9Qc4SSCXWYVQ0VW5vlGKQTOfQbuGqh9yVQK6IEEP6RjNCec56F2f/Ze
# 2M//ipIacfT99E+yHyaUpPiTilSMZmBW+XQDQZ6aYwXwbGFIIGcNWnZQjEStFPhJ
# /4YpoTBCuUX1OHGtt69uQu6rx/Fw/7tUvPyDd+Pg7We99tKRuGXQcAcv8CwCQFo2
# EOCAYG0JKKgYbnhpX03edeExvuQc9ydvbLZdIwbhzQ6Uq0p8J2ip4R0fI5W5O5LU
# JIx/YWKkTrPMz1gxAT7sCEaw9JCelxtY4F9bnySg8fCANUT7JRvmAfHEd9EvZkAF
# 9LdsZMgWPV+A5QfTRvWP4fdbbD935fB7BeqSQGyxt+4D3d2RNNBvPUHm9117OpjC
# HjcyoRCNfTLuN/LV7RlZ3A==
# SIG # End signature block
