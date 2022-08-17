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
# MIIrWAYJKoZIhvcNAQcCoIIrSTCCK0UCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCByi+V9i0P2c4GR
# y8EyVwpX90neAZf251IZfvfoxi8UIKCCEXkwggiJMIIHcaADAgECAhM2AAABqdaQ
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
# lu5jKbKdjW6f5HJ+Ir36JVMt0PWH9LHLEOlky2KZvgKAlCUxghk1MIIZMQIBATBY
# MEExEzARBgoJkiaJk/IsZAEZFgNHQkwxEzARBgoJkiaJk/IsZAEZFgNBTUUxFTAT
# BgNVBAMTDEFNRSBDUyBDQSAwMQITNgAAAanWkDBmQ9sfggACAAABqTANBglghkgB
# ZQMEAgEFAKCBrjAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQggCqw48NwAfyN4VVQ
# C/Bv24Ed8pF+oM4RLTCjHJxj9uowQgYKKwYBBAGCNwIBDDE0MDKgFIASAE0AaQBj
# AHIAbwBzAG8AZgB0oRqAGGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbTANBgkqhkiG
# 9w0BAQEFAASCAQAo2bPWKE57G4FSIMAl+wvYhui3Mn5FIX1+SY99EUW0URHWvSIk
# cW5TSC+BaO8qqVAhKiu70AnRu4Cz1JTl2MGWUTMnf6fHWGb3TvyytcFhWpUoIb6X
# GeN6k5JEXsMZteRGiD9knzi3o6uuxZr68RgztE7Nf+birtn5VSLiFhk00QGZc4E1
# Hor+YIFBG7k3hPu3xTHx/ixwsuhP71iSINTmSOzT5OJ/4+9DGdSvwXrXIxA4SZ4H
# hYuuE8MbXT18OlCYaXOwg1F2eUpkoph3bwimSwJxLSL1XKCC+K9r4K8cA/LzxWuD
# 57l3uCqFit5B5itaUgRb/aAcmKxKqQ/JH4HxoYIW/TCCFvkGCisGAQQBgjcDAwEx
# ghbpMIIW5QYJKoZIhvcNAQcCoIIW1jCCFtICAQMxDzANBglghkgBZQMEAgEFADCC
# AVEGCyqGSIb3DQEJEAEEoIIBQASCATwwggE4AgEBBgorBgEEAYRZCgMBMDEwDQYJ
# YIZIAWUDBAIBBQAEINpKM4UKLDaH3aA0Sp0vXT2bW9786pzsKzeCqfMn9pK9AgZi
# 1+wXdZEYEzIwMjIwODE3MjA0MzUwLjM1N1owBIACAfSggdCkgc0wgcoxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29m
# dCBBbWVyaWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOjQ5
# QkMtRTM3QS0yMzNDMSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2
# aWNloIIRVDCCBwwwggT0oAMCAQICEzMAAAGXA89ZnGuJeD8AAQAAAZcwDQYJKoZI
# hvcNAQELBQAwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
# BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEm
# MCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMjExMjAy
# MTkwNTE0WhcNMjMwMjI4MTkwNTE0WjCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgT
# Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29m
# dCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0
# aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBFU046NDlCQy1FMzdBLTIzM0MxJTAj
# BgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggIiMA0GCSqGSIb3
# DQEBAQUAA4ICDwAwggIKAoICAQDtAErqSkFN8/Ce/csrHVWcv1iSjNTArPKEMqKP
# UTpYJX8TBZl88LNrpw4bEpPimO+Etcli5RBoZEieo+SzYUnb0+nKEWaEYgubgp+H
# TFZiD85Lld7mk2Xg91KMDE2yMeOIH2DHpTsn5p0Lf0CDlfPE5HOwpP5/vsUxNeDW
# MW6zsSuKU69aL7Ocyk36VMyCKjHNML67VmZMJBO7bX1vYVShOvQqZUkxCpCR3szm
# xHT09s6nhwLeNCz7nMnU7PEiNGVxSYu+V0ETppFpK7THcGYAMa3SYZjQxGyDOc7J
# 20kEud6tz5ArSRzG47qscDfPYqv1+akex81w395E+1kc4uukfn0CeKtADum7PqRr
# bRMD7wyFnX2FvyaytGj0uaKuMXFJsZ+wfdk0RsuPeWHtVz4MRCEwfYr1c+JTkmS3
# n/pvHr/b853do28LoPHezk3dSxbniQojW3BTYJLmrUei/n4BHK5mTT8NuxG6zoP3
# t8HVmhCW//i2sFwxVHPsyQ6sdrxs/hapsPR5sti2ITG/Hge4SeH7Sne942OHeA/T
# 7sOSJXAhhx9VyUiEUUax+dKIV7Gu67rjq5SVr5VNS4bduOpLsWEjeGHpMei//3xd
# 8dxZ42G/EDkr5+L7UFxIuBAq+r8diP/D8yR/du7vc4RGKw1ppxpo4JH9MnYfd+zU
# DuUgcQIDAQABo4IBNjCCATIwHQYDVR0OBBYEFG3PAc8o6zBullUL0bG+3X69FQBg
# MB8GA1UdIwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8GA1UdHwRYMFYwVKBS
# oFCGTmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29m
# dCUyMFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggrBgEFBQcBAQRg
# MF4wXAYIKwYBBQUHMAKGUGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMv
# Y2VydHMvTWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3J0
# MAwGA1UdEwEB/wQCMAAwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZIhvcNAQEL
# BQADggIBAARI2GHSJO0zHnshct+Hgu4dsPU0b0yUsDXBhAdAGdH1T+uDeq3c3Hp7
# v5C4QowSEqp0t/eDlFHhH+hkvm8QlVZR8hIf+hJZ3OqtKGpyZPg7HNzYIGzRS2fK
# ilUObhbYK6ajeq7KRg+kGgZ16Ku8N13XncDCwmQgyCb/yzEkpsgF5Pza2etSeA2Y
# 2jy7uXW4TSGwwCrVuK9Drd9Aiev5Wpgm9hPRb/Q9bukDeqHihw2OJfpnx32SPHwv
# u4E8j8ezGJ8KP/yYVG+lUFg7Ko/tjl2LlkCeNMNIcxk1QU8e36eEVdRweNc9FEcI
# yqomDgPrdfpvRXRHztD3eKnAYhcEzM4xA0i0k5F6Qe0eUuLduDouemOzRoKjn9GU
# cKM2RIOD7FXuph5rfsv84pM2OqYfek0BrcG8/+sNCIYRi+ABtUcQhDPtYxZJixZ5
# Q8VkjfqYKOBRjpXnfwKRC0PAzwEOIBzL6q47x6nKSI/QffbKrAOHznYF5abV60X4
# +TD+3xc7dD52IW7saCKqN16aPhV+lGyba1M30ecB7CutvRfBjxATa2nSFF03ZvRS
# JLEyYHiE3IopdVoMs4UJ2Iuex+kPSuM4fyNsQJk5tpZYuf14S8Ov5A1A+9Livjsv
# 0BrwuvUevjtXAnkTaAISe9jAhEPOkmExGLQqKNg3jfJPpdIZHg32MIIHcTCCBVmg
# AwIBAgITMwAAABXF52ueAptJmQAAAAAAFTANBgkqhkiG9w0BAQsFADCBiDELMAkG
# A1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQx
# HjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9z
# b2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTAwHhcNMjEwOTMwMTgy
# MjI1WhcNMzAwOTMwMTgzMjI1WjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2Fz
# aGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENv
# cnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAx
# MDCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAOThpkzntHIhC3miy9ck
# eb0O1YLT/e6cBwfSqWxOdcjKNVf2AX9sSuDivbk+F2Az/1xPx2b3lVNxWuJ+Slr+
# uDZnhUYjDLWNE893MsAQGOhgfWpSg0S3po5GawcU88V29YZQ3MFEyHFcUTE3oAo4
# bo3t1w/YJlN8OWECesSq/XJprx2rrPY2vjUmZNqYO7oaezOtgFt+jBAcnVL+tuhi
# JdxqD89d9P6OU8/W7IVWTe/dvI2k45GPsjksUZzpcGkNyjYtcI4xyDUoveO0hyTD
# 4MmPfrVUj9z6BVWYbWg7mka97aSueik3rMvrg0XnRm7KMtXAhjBcTyziYrLNueKN
# iOSWrAFKu75xqRdbZ2De+JKRHh09/SDPc31BmkZ1zcRfNN0Sidb9pSB9fvzZnkXf
# tnIv231fgLrbqn427DZM9ituqBJR6L8FA6PRc6ZNN3SUHDSCD/AQ8rdHGO2n6Jl8
# P0zbr17C89XYcz1DTsEzOUyOArxCaC4Q6oRRRuLRvWoYWmEBc8pnol7XKHYC4jMY
# ctenIPDC+hIK12NvDMk2ZItboKaDIV1fMHSRlJTYuVD5C4lh8zYGNRiER9vcG9H9
# stQcxWv2XFJRXRLbJbqvUAV6bMURHXLvjflSxIUXk8A8FdsaN8cIFRg/eKtFtvUe
# h17aj54WcmnGrnu3tz5q4i6tAgMBAAGjggHdMIIB2TASBgkrBgEEAYI3FQEEBQID
# AQABMCMGCSsGAQQBgjcVAgQWBBQqp1L+ZMSavoKRPEY1Kc8Q/y8E7jAdBgNVHQ4E
# FgQUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXAYDVR0gBFUwUzBRBgwrBgEEAYI3TIN9
# AQEwQTA/BggrBgEFBQcCARYzaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9w
# cy9Eb2NzL1JlcG9zaXRvcnkuaHRtMBMGA1UdJQQMMAoGCCsGAQUFBwMIMBkGCSsG
# AQQBgjcUAgQMHgoAUwB1AGIAQwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTAD
# AQH/MB8GA1UdIwQYMBaAFNX2VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0w
# S6BJoEeGRWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3Rz
# L01pY1Jvb0NlckF1dF8yMDEwLTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYI
# KwYBBQUHMAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWlj
# Um9vQ2VyQXV0XzIwMTAtMDYtMjMuY3J0MA0GCSqGSIb3DQEBCwUAA4ICAQCdVX38
# Kq3hLB9nATEkW+Geckv8qW/qXBS2Pk5HZHixBpOXPTEztTnXwnE2P9pkbHzQdTlt
# uw8x5MKP+2zRoZQYIu7pZmc6U03dmLq2HnjYNi6cqYJWAAOwBb6J6Gngugnue99q
# b74py27YP0h1AdkY3m2CDPVtI1TkeFN1JFe53Z/zjj3G82jfZfakVqr3lbYoVSfQ
# JL1AoL8ZthISEV09J+BAljis9/kpicO8F7BUhUKz/AyeixmJ5/ALaoHCgRlCGVJ1
# ijbCHcNhcy4sa3tuPywJeBTpkbKpW99Jo3QMvOyRgNI95ko+ZjtPu4b6MhrZlvSP
# 9pEB9s7GdP32THJvEKt1MMU0sHrYUP4KWN1APMdUbZ1jdEgssU5HLcEUBHG/ZPkk
# vnNtyo4JvbMBV0lUZNlz138eW0QBjloZkWsNn6Qo3GcZKCS6OEuabvshVGtqRRFH
# qfG3rsjoiV5PndLQTHa1V1QJsWkBRH58oWFsc/4Ku+xBZj1p/cvBQUl+fpO+y/g7
# 5LcVv7TOPqUxUYS8vwLBgqJ7Fx0ViY1w/ue10CgaiQuPNtq6TPmb/wrpNPgkNWcr
# 4A245oyZ1uEi6vAnQj0llOZ0dFtq0Z4+7X6gMTN9vMvpe784cETRkPHIqzqKOghi
# f9lwY1NNje6CbaUFEMFxBmoQtB1VM1izoXBm8qGCAsswggI0AgEBMIH4oYHQpIHN
# MIHKMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQL
# ExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMg
# VFNTIEVTTjo0OUJDLUUzN0EtMjMzQzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUt
# U3RhbXAgU2VydmljZaIjCgEBMAcGBSsOAwIaAxUAYUDSsI2YSTTNTXYNg0YxTcHW
# Y9GggYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDANBgkqhkiG
# 9w0BAQUFAAIFAOanU9gwIhgPMjAyMjA4MTcxOTQ3MzZaGA8yMDIyMDgxODE5NDcz
# NlowdDA6BgorBgEEAYRZCgQBMSwwKjAKAgUA5qdT2AIBADAHAgEAAgIXzzAHAgEA
# AgIRqzAKAgUA5qilWAIBADA2BgorBgEEAYRZCgQCMSgwJjAMBgorBgEEAYRZCgMC
# oAowCAIBAAIDB6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEBBQUAA4GBAJrqaNA6
# MV+1o3wFYipb1TPPpq4TKYoBLjpjfCceFqkCJGYNfGM6gJYc2c3EhaH13BNrKUmy
# B97Vuf+QL6cwIaDubsnYyLCjMU8HpY/OXmy7RIpe622oM1ldmua2+GVgYPrInujd
# nj3aKFt1T3f3PHLVveQwy3xc7PeymgyT+esbMYIEDTCCBAkCAQEwgZMwfDELMAkG
# A1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQx
# HjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9z
# b2Z0IFRpbWUtU3RhbXAgUENBIDIwMTACEzMAAAGXA89ZnGuJeD8AAQAAAZcwDQYJ
# YIZIAWUDBAIBBQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRABBDAvBgkq
# hkiG9w0BCQQxIgQgHB/olaD3fteJi2F6SKk8OZ1m6RKlrM8TvfWt1+oql/IwgfoG
# CyqGSIb3DQEJEAIvMYHqMIHnMIHkMIG9BCBbe9obEJV6OP4EDMVJ8zF8dD5vHGSo
# LDwuQxj9BnimvzCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEw
# AhMzAAABlwPPWZxriXg/AAEAAAGXMCIEIIY+5ioPrkijJdbF9e71r29XGo6+5SSz
# bYHxfrM3wV46MA0GCSqGSIb3DQEBCwUABIICAFintbKgyHr7AFLhFpjb5a+cBPkm
# v2vu7u6O9KTHewaNZEgdhg0j6MlUts1I8vMfHmxYibK+NY/Q1YRa1gFaNYSMX0rg
# h/WVMeKi5EVg+T73FLptIAV7aDNktFJIiroysa8ZL6+V1wT8eaP44xI/E2wy8KRd
# 0ckwRNG3ebW4/t4eYZ9/+t8fPF8mNi1N8O33XqZV19ccoldkpH+eHqK5e4c71uPK
# bVITtDu2hDrjCNJbajUZ5t/pOj85htiwFqt6Fewa+nIV256zxXKFQtwVcmgM0G07
# uBqBj+YWNwGyXU4TDRCFGSV/b9EpYlMznuh7V33XxEf3yTq6TVV4fD+lWbBrQ2A2
# ICrb2UI/MmaYZ+tz9K9k/sz5XpuCJVQmHLoLpBR9zTHGNjF6V4m6UA49kFhgHLvL
# 7AhN8q1E6GwSgjEYlM4smKEGQv8AoJkSWJpCmSregMWCnA5eZHmb0u+4LGlWj0+7
# O/KjErbsgwRUO9hYlZkdRN4dgMRfm83OkOHxcTCA6qWCWE2puwRTSUc4Ku/wjE8s
# mfQ83KVGtkDhWSqJEYKikEjIYEz+6+WXd/Lg+Ko/uxbpjRr5t45g3uDQ16N93zhY
# o6Lcp1q8km8ps/H+3E9sQKcAs896d7yQu4N04am5JIISr140haf2093KoDeIDAcG
# sfheDnmhWdaOqQ8F
# SIG # End signature block
