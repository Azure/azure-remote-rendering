# This Powershell script is an example for the usage of the Azure Remote Rendering service
# Documentation: https://docs.microsoft.com/en-us/azure/remote-rendering/samples/powershell-example-scripts

$Global:ARRAuthenticationToken = $null

$AllPublicRegions = @("australiaeast", "eastus", "eastus2", "japaneast", "northeurope", "southcentralus", "southeastasia", "uksouth", "westeurope", "westus2")

function ToDomain {
    param ([ValidateSet("Rendering", "Account")] $Type, $Region)

    $httpsPrefix = "https://"
    $suffix = ".mixedreality.azure.com"
    $regexSuffix = $suffix -replace "\.", "\." # The first parameter is a regex, the second one isn't so replace . with \.

    $cleanedRegion = $Region
    if($Region -match "^($httpsPrefix)?(?<region>.*?)($regexSuffix)?$") {
        $cleanedRegion = $matches['region'] # If the url starts with https:// or ends with .mixedreality.azure.com remove it.
    }
    
    if ($AllPublicRegions.Contains($cleanedRegion)) {
        $prefix = if ($Type -eq "Rendering") {
            "remoterendering"
        } else {
            "sts"
        }

        return "$($httpsPrefix)$Prefix.$($cleanedRegion)$suffix"
    }

    # If it is not found it might be a non-public region, so just simply pass it and hope it works
    return $Region
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
    $exceptionObject = ""
    if ($null -ne ($exception | Select-Object Response).Response) {
        if ($null -ne $exception.Response.Headers -and $exception.Response.Headers.Contains("MS-CV")) {
            $exceptionObject = "Response's MS-CV is '$($exception.Response.Headers.GetValues('MS-CV'))'`r`n"
        }

        $exceptionObject += $exception.Response | ConvertTo-Json
    }
    else
    {
        $exceptionObject = $exception
    }

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
      "arrAccountDomain": "<select from available regions: australiaeast, eastus, eastus2, japaneast, northeurope, southcentralus, southeastasia, uksouth, westeurope, westus2 or specify the full url>"
    },
    "renderingSessionSettings": {
      "remoteRenderingDomain": "<select from available regions: australiaeast, eastus, eastus2, japaneast, northeurope, southcentralus, southeastasia, uksouth, westeurope, westus2 or specify the full url>",
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
    [string] $ArrAccountId,
    [string] $ArrAccountKey,
    [string] $ArrAccountDomain,
    [string] $Region,
    [string] $RemoteRenderingDomain,
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

    $inputRenderingDomain = if (-Not [string]::IsNullOrEmpty($RemoteRenderingDomain)) { $RemoteRenderingDomain } else { $config.renderingSessionSettings.remoteRenderingDomain }
    if (-Not [string]::IsNullOrEmpty($inputRenderingDomain)) {
        $config.renderingSessionSettings.remoteRenderingDomain = ToDomain "Rendering" $inputRenderingDomain
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

    if (-Not [string]::IsNullOrEmpty($ArrAccountId)) {
        $config.accountSettings.arrAccountId = $ArrAccountId
    }

    if (-Not [string]::IsNullOrEmpty($ArrAccountKey)) {
        $config.accountSettings.arrAccountKey = $ArrAccountKey
    }

    $inputAccountDomain = if (-Not [string]::IsNullOrEmpty($ArrAccountDomain)) { $ArrAccountDomain } else { $config.accountSettings.arrAccountDomain }
    if (-Not [string]::IsNullOrEmpty($inputAccountDomain)) {
        $config.accountSettings.arrAccountDomain = ToDomain "Account" $inputAccountDomain
    }

    if (-Not (VerifyAccountIdAndKeySettings $config (GetDefaultConfig))) {
        WriteError("Error reading id and key in accountSettings in $ConfigFile - Exiting.")
        exit 1
    }

    if (-Not [string]::IsNullOrEmpty($Region)) {
        Write-Warning "Parameter -Region is deprecated and should not be used anymore."
        Write-Warning "Please replace it with the -ArrAccountDomain and -RemoteRenderingDomain parameters."
        Write-Warning "Using the -Region parameter will in most cases be slower than using the recommended parameters."
        Write-Warning "For a grace period the -ArrAccountDomain and -RemoteRenderingDomain will be derived from the region parameter if they are empty,"
        Write-Warning "so any other failure you see (like a not-filled-in failure) related to these parameters might be due to the region parameter being wrong."

        if ([string]::IsNullOrEmpty($ArrAccountDomain)) {
            $primaryArrAccountDomain = ToDomain "Account" $Region

            $id = $config.accountSettings.arrAccountId
            $key = $config.accountSettings.arrAccountKey

            # First try the one region specified by the parameter, but we have to try
            # all other regions if that does not work, because that is what was done in older versions
            if (TrySetGlobalAuthenticationToken $primaryArrAccountDomain $id $key ) {
                $config.accountSettings.arrAccountDomain = $primaryArrAccountDomain
            } else {
                foreach ($possibleRegion in $AllPublicRegions) {
                    if ($possibleRegion -eq $ArrAccountDomain) {
                        # We already checked that with the primary region
                        continue
                    }

                    $nextDomain = ToDomain "Account" $possibleRegion

                    if (TrySetGlobalAuthenticationToken $nextDomain $id $key) {
                        $config.accountSettings.arrAccountDomain = $nextDomain
                        break
                    }
                }
            }
        }
    }

    if (-Not [string]::IsNullOrEmpty($RemoteRenderingDomain)) {
        $config.renderingSessionSettings.remoteRenderingDomain = ToDomain "Rendering" $RemoteRenderingDomain
    }

    if (-Not (VerifyAccountDomainSettings $config (GetDefaultConfig))) {
        WriteError("Error reading account domain in accountSettings in $ConfigFile - Exiting.")
        exit 1
    }

    return $config
}

function VerifyAccountIdAndKeySettings($config, $defaultConfig) {
    $ok = $true

    # Check that account id is set and a valid guid
    if ($config.accountSettings.arrAccountId -eq $defaultConfig.accountSettings.arrAccountId) {
        WriteError("accountSettings.arrAccountId not filled in - fill in the account ID from the Azure Portal")
        $ok = $false
    }
    else {
        try {
            [GUID]$config.accountSettings.arrAccountId | Out-Null
        }
        catch {
            $guidString = $config.accountSettings.arrAccountId
            WriteError("accountSettings.arrAccount id : ' $guidString' is not a valid GUID. Please enter a valid GUID")
            $ok = $false
        }
    }

    # Check that account key is set
    if ($config.accountSettings.arrAccountKey -eq $defaultConfig.accountSettings.arrAccountKey) {
        WriteError("accountSettings.arrAccountKey not filled in - fill in the account key from the Azure Portal")
        $ok = $false
    }

    return $ok
}

function VerifyAccountDomainSettings($config, $defaultConfig) {
    # Check that account domain is set
    if ($config.accountSettings.arrAccountDomain -eq $defaultConfig.accountSettings.arrAccountDomain) {
        $regionString = ($AllPublicRegions -join ", ")
        WriteError("accountSettings.arrAccountDomain not filled in - select an account domain out of '$regionString' or specify your complete domain url")
        return $false
    }

    return $true
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

function VerifyRenderingSessionSettings($config, $defaultConfig, $skipVmSettingsCheck) {
    $ok = $true

    if (-Not $skipVmSettingsCheck) {
        if ($config.renderingSessionSettings.vmSize -eq $defaultConfig.renderingSessionSettings.vmSize) {
            $vmSizesString = ($ARRAvailableVMSizes.keys -join ", ")
            WriteError("renderingSessionSettings.vmSize not filled in - select a vmSize out of: $vmSizesString")
            $ok = $false
        }
    
        try {
            [timespan]$config.renderingSessionSettings.maxLeaseTime | Out-Null
        }
        catch {
            $timespan = $config.renderingSessionSettings.maxLeaseTime
            WriteError("renderingSessionSettings.maxLeaseTime '$timespan' not valid - provide a time in hh:mm:ss format")
            $ok = $false
        }
    }

    if ($config.renderingSessionSettings.remoteRenderingDomain -eq $defaultConfig.renderingSessionSettings.remoteRenderingDomain) {
        $regionString = ($AllPublicRegions -join ", ")
        WriteError("renderingSessionSettings.remoteRenderingDomain not filled in - select an rendering domain out of '$regionString' or specify your complete domain url")
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

    if ($OnlyConvertNoUpload -eq $False) {
        if ($config.assetConversionSettings.localAssetDirectoryPath -eq $defaultConfig.assetConversionSettings.localAssetDirectoryPath) {
            WriteError("modelSettings does not have a localAssetDirectoryPath value ... specify the directory containing asset data in config.json or via the -LocalAssetDirectoryPath <path to model> command line argument")
            return $false
        }
    }

    return $true
}

# reads config and adds azure specific fields to the config
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

function GetAuthenticationToken([string]$arrAccountDomain, [GUID]$accountId, [string]$accountKey) {
    if ($Global:ARRAuthenticationToken) {
        return $Global:ARRAuthenticationToken
    }
    else {
        WriteLine
        WriteInformation ("Getting an authentication token ...")

        if ((TrySetGlobalAuthenticationToken $arrAccountDomain $accountId $accountKey)) {
            return $Global:ARRAuthenticationToken
        } else {
            WriteError("Unable to get an authentication token - please check your accountId and accountKey - Exiting.")
            exit 1
        }
    }
}

function TrySetGlobalAuthenticationToken([string]$arrAccountDomain, [GUID]$accountId, [string]$accountKey) {
    if ($Global:ARRAuthenticationToken) {
        return $true
    }
    else {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12;

        try {
            $webResponse = Invoke-WebRequest -UseBasicParsing -Uri "$arrAccountDomain/accounts/$accountId/token" -Headers @{ Authorization = "Bearer ${accountId}:$accountKey" }
    
            if ($webResponse.StatusCode -eq 200) {
                $response = ConvertFrom-Json -InputObject $webResponse.Content
                $Global:ARRAuthenticationToken = $response.AccessToken;
    
                return $true
            }
            else {
                return $false
            }
        } catch {
            return $false
        }
    }
}

# Create a Session by calling REST API <endpoint>/accounts/<accountId>/sessions/<sessionId>/
# returns a session ID which can be used to retrieve session status
function CreateRenderingSession([string] $arrAccountDomain, [string] $remoteRenderingDomain, [string] $accountId, [string] $accountKey, [string] $vmSize = "standard", [string] $maxLeaseTime = "4:0:0", [hashtable] $additionalParameters, [string] $sessionId) {
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
            $sessionId = "Sample-Session-$([System.Guid]::NewGuid())"
        }

        $url = "$remoteRenderingDomain/accounts/$accountId/sessions/${sessionId}?api-version=2021-01-01-preview"

        WriteInformation("Creating Rendering Session ...")
        WriteInformation("  arrAccountDomain: $arrAccountDomain")
        WriteInformation("  remoteRenderingDomain: $remoteRenderingDomain")
        WriteInformation("  sessionId: $sessionId")
        WriteInformation("  maxLeaseTime: $maxLeaseTime")
        WriteInformation("  size: $vmSize")
        WriteInformation("  additionalParameters: $($additionalParameters | ConvertTo-Json)")

        $token = GetAuthenticationToken -arrAccountDomain $arrAccountDomain -accountId $accountId -accountKey $accountKey

        Invoke-WebRequest -UseBasicParsing -Uri $url -Method PUT -ContentType "application/json" -Body ($body | ConvertTo-Json) -Headers @{ Authorization = "Bearer $token" } | Out-Null

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
function StopSession([string] $arrAccountDomain, [string] $remoteRenderingDomain, [string] $accountId, [string] $accountKey, [string] $sessionId) {
    try {
        $url = "$remoteRenderingDomain/accounts/$accountId/sessions/$sessionId/:stop?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -arrAccountDomain $arrAccountDomain -accountId $accountId -accountKey $accountKey
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
function GetSessionProperties([string] $arrAccountDomain, [string] $remoteRenderingDomain, [string] $accountId, [string] $accountKey, [string] $sessionId) {
    try {
        $url = "$remoteRenderingDomain/accounts/$accountId/sessions/${sessionId}?api-version=2021-01-01-preview"

        $token = GetAuthenticationToken -arrAccountDomain $arrAccountDomain -accountId $accountId -accountKey $accountKey
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
        (Join-Path (Join-Path $Root "\Bin\x64_vs2022_win10\Dev\") $exeName),
        (Join-Path (Join-Path $Root "\Bin\x64_vs2019_win10\Dev\") $exeName),
        (Join-Path (Join-Path $Root "\Bin\x64_vs2022_win10\Debug\") $exeName),
        (Join-Path (Join-Path $Root "\Bin\x64_vs2019_win10\Debug\") $exeName))

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
# MIInlgYJKoZIhvcNAQcCoIInhzCCJ4MCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBhcaqsf3zH/yZW
# 8ZB4iMgxUSIJWUpLUBSnFE98y10yIaCCDXYwggX0MIID3KADAgECAhMzAAACy7d1
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
# /Xmfwb1tbWrJUnMTDXpQzTGCGXYwghlyAgEBMIGVMH4xCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNp
# Z25pbmcgUENBIDIwMTECEzMAAALLt3U5+wJxQjYAAAAAAsswDQYJYIZIAWUDBAIB
# BQCgga4wGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEO
# MAwGCisGAQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEILvOYQTjlO71KwMlq8AI5DXY
# KHY+DATV5mSrQWO98tF1MEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8A
# cwBvAGYAdKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEB
# BQAEggEAfjHQ27BiZjA334H/A/BTvFb7w/Vtf1MN8dYDAfkzY+gXFtosWqyOPY+k
# pKW1dA7ogmYSFSEy6BcuPdEwvUDJoi0umqUD2n+v269xwJc3e1cno9IrUoUKpROM
# LrSB89R/MfKRPVwKbPPFDnIijB14kdS8rU5my+w0Jz0yYTD6XJJ8MpyiXym0oSM8
# mR0lmATFpcnJPXdxlk8O4VHAiSM9xolzO35c+0bh27PUlFcVEdkatIUhIJNljuct
# umKwd87NSxyrHv5LDoql+tfgPeGTDwLhFcp6tqP1ZmDnJ3we9Q0NrZ//lPesfN7i
# PjoX9Wex+usVLHaHd+ZtCMeUJezrGqGCFwAwghb8BgorBgEEAYI3AwMBMYIW7DCC
# FugGCSqGSIb3DQEHAqCCFtkwghbVAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFRBgsq
# hkiG9w0BCRABBKCCAUAEggE8MIIBOAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFl
# AwQCAQUABCCWLPx/MzP+zqLp+FEG+KyJd/2u2SoZu4BjRQy6myyCSAIGZDfp2yCo
# GBMyMDIzMDQxOTE2MzAyMy41NzVaMASAAgH0oIHQpIHNMIHKMQswCQYDVQQGEwJV
# UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UE
# ChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1l
# cmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVTTjo4QTgyLUUz
# NEYtOUREQTElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaCC
# EVcwggcMMIIE9KADAgECAhMzAAABwvp9hw5UU0ckAAEAAAHCMA0GCSqGSIb3DQEB
# CwUAMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
# EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNV
# BAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIyMTEwNDE5MDEy
# OFoXDTI0MDIwMjE5MDEyOFowgcoxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMx
# JjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOjhBODItRTM0Ri05RERBMSUwIwYDVQQD
# ExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNlMIICIjANBgkqhkiG9w0BAQEF
# AAOCAg8AMIICCgKCAgEAtfEJvPKOSFn3petp9wco29/UoJmDDyHpmmpRruRVWBF3
# 7By0nvrszScOV/K+LvHWWWC4S9cme4P63EmNhxTN/k2CgPnIt/sDepyACSkya4uk
# qc1sT2I+0Uod0xjy9K2+jLH8UNb9vM3yH/vCYnaJSUqgtqZUly82pgYSB6tDeZIY
# cQoOhTI+M1HhRxmxt8RaAKZnDnXgLdkhnIYDJrRkQBpIgahtExtTuOkmVp2y8YCo
# FPaUhUD2JT6hPiDD7qD7A77PLpFzD2QFmNezT8aHHhKsVBuJMLPXZO1k14j0/k68
# DZGts1YBtGegXNkyvkXSgCCxt3Q8WF8laBXbDnhHaDLBhCOBaZQ8jqcFUx8ZJSXQ
# 8sbvEnmWFZmgM93B9P/JTFTF6qBVFMDd/V0PBbRQC2TctZH4bfv+jyWvZOeFz5yl
# tPLRxUqBjv4KHIaJgBhU2ntMw4H0hpm4B7s6LLxkTsjLsajjCJI8PiKi/mPKYERd
# mRyvFL8/YA/PdqkIwWWg2Tj5tyutGFtfVR+6GbcCVhijjy7l7otxa/wYVSX66Lo0
# alaThjc+uojVwH4psL+A1qvbWDB9swoKla20eZubw7fzCpFe6qs++G01sst1SaA0
# GGmzuQCd04Ue1eH3DFRDZPsN+aWvA455Qmd9ZJLGXuqnBo4BXwVxdWZNj6+b4P8C
# AwEAAaOCATYwggEyMB0GA1UdDgQWBBRGsYh76V41aUCRXE9WvD++sIfGajAfBgNV
# HSMEGDAWgBSfpxVdAF5iXYP05dJlpxtTNRnpcjBfBgNVHR8EWDBWMFSgUqBQhk5o
# dHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NybC9NaWNyb3NvZnQlMjBU
# aW1lLVN0YW1wJTIwUENBJTIwMjAxMCgxKS5jcmwwbAYIKwYBBQUHAQEEYDBeMFwG
# CCsGAQUFBzAChlBodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NlcnRz
# L01pY3Jvc29mdCUyMFRpbWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNydDAMBgNV
# HRMBAf8EAjAAMBMGA1UdJQQMMAoGCCsGAQUFBwMIMA0GCSqGSIb3DQEBCwUAA4IC
# AQARdu3dCkcLLPfaJ3rR1M7D9jWHvneffkmXvFIJtqxHGWM1oqAh+bqxpI7HZz2M
# eNhh1Co+E9AabOgj94Sp1seXxdWISJ9lRGaAAWzA873aTB3/SjwuGqbqQuAvUzBF
# CO40UJ9anpavkpq/0nDqLb7XI5H+nsmjFyu8yqX1PMmnb4s1fbc/F30ijaASzqJ+
# p5rrgYWwDoMihM5bF0Y0riXihwE7eTShak/EwcxRmG3h+OT+Ox8KOLuLqwFFl1si
# TeQCp+YSt4J1tWXapqGJDlCbYr3Rz8+ryTS8CoZAU0vSHCOQcq12Th81p7QlHZv9
# cTRDhZg2TVyg8Gx3X6mkpNOXb56QUohI3Sn39WQJwjDn74J0aVYMai8mY6/WOurK
# MKEuSNhCiei0TK68vOY7sH0XEBWnRSbVefeStDo94UIUVTwd2HmBEfY8kfryp3Rl
# A9A4FvfUvDHMaF9BtvU/pK6d1CdKG29V0WN3uVzfYETJoRpjLYFGq0MvK6QVMmuN
# xk3bCRfj1acSWee14UGjglxWwvyOfNJe3pxcNFOd8Hhyp9d4AlQGVLNotaFvopgP
# LeJwUT3dl5VaAAhMwvIFmqwsffQy93morrprcnv74r5g3ejC39NYpFEoy+qmzLW1
# jFa1aXE2Xb/KZw2yawqldSp0Hu4VEkjGxFNc+AztIUWwmTCCB3EwggVZoAMCAQIC
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
# TY3ugm2lBRDBcQZqELQdVTNYs6FwZvKhggLOMIICNwIBATCB+KGB0KSBzTCByjEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UECxMcTWlj
# cm9zb2Z0IEFtZXJpY2EgT3BlcmF0aW9uczEmMCQGA1UECxMdVGhhbGVzIFRTUyBF
# U046OEE4Mi1FMzRGLTlEREExJTAjBgNVBAMTHE1pY3Jvc29mdCBUaW1lLVN0YW1w
# IFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVAMp1N1VLhPMvWXEoZfmF4apZlnRUoIGD
# MIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNV
# BAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQG
# A1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwDQYJKoZIhvcNAQEF
# BQACBQDn6lB2MCIYDzIwMjMwNDE5MTkzNTE4WhgPMjAyMzA0MjAxOTM1MThaMHcw
# PQYKKwYBBAGEWQoEATEvMC0wCgIFAOfqUHYCAQAwCgIBAAICARUCAf8wBwIBAAIC
# EbIwCgIFAOfrofYCAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYKKwYBBAGEWQoDAqAK
# MAgCAQACAwehIKEKMAgCAQACAwGGoDANBgkqhkiG9w0BAQUFAAOBgQBfOZJhhrYI
# FKb+lceM3BF/v1CuNeyMZrcgeXg8aT8f8UNoitKMyGxto1A8AU/79wjoDUM85vjF
# BCBJxwO7WadGw2y6xkdhgN/bNuMfot4alk/F+Br19wGUnrRmxhtEpyIkZqR/Q7LX
# hzRNYfM0yyR7IZmWPDfKPE261+9++RAkvjGCBA0wggQJAgEBMIGTMHwxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29m
# dCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABwvp9hw5UU0ckAAEAAAHCMA0GCWCG
# SAFlAwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQwLwYJKoZI
# hvcNAQkEMSIEIBGoN0nshV6QoNYxSnGZb0W2ysXh7JkVHkR5YptlXEqRMIH6Bgsq
# hkiG9w0BCRACLzGB6jCB5zCB5DCBvQQgypNgW8fpsMV57r0F5beUuiEVOVe4Bdma
# O+e28mGDUBYwgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGlu
# Z3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
# cmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAIT
# MwAAAcL6fYcOVFNHJAABAAABwjAiBCA0Xa7fXvXQYre7yg36E7KmXseCatxDJkrb
# a16HyujHDDANBgkqhkiG9w0BAQsFAASCAgA39s2/nWrGRXQjAAiJJVtjh01oN1Wp
# g/25aAoLsCZRMhkv+IQJhHFB6nUl63YT8wtEzOSHKRoKOWxR6NtD7vOB9VWQZnUD
# nFJHzM+g6eMFNPiZcpN1LbEAHi2iBOWRaop6hQpKJli0MAs1h1piTCIXYMBqh+OS
# TDaR7wZqlkG+Bs6uqbQwW4YiNZuCZeIzM67bf3n8zm2tvtyuZx9ff0p7Qlw+Ja76
# Owt5U982HqT+kWUtZwG6NnmpjcEtJvoMmidO5r1eP8/VhOy7qf22bC3qIkRJ61h0
# uWtgutv9de9AZLWko0W1rzdNO83Pja6PMQABziwFs+GR+VNHpWuBKGpZlt5TUoI3
# hrPY2Z8kK8W44zjdH4i7TQvit+44IMSQ8o9Z52j25+pxxtWK5WsqKV/UZxGR4nX8
# ajIa2BPtAJ0EM4HAIsIA2KN6rGy+PiJtgfIf5d42wrv2Zmm9KwVr3Vkpdvm+DGk+
# bRcwtuyRmtroGDIa/esorQ+G1xH/ij6s1PRS27HcyV9MJP2ekbz9ufj5dpf1wQxm
# 48kInQt5bAfEtdDOFxVx60JZBvESdOhoy090ejZeoz10rP57nhGpiZLKoLGCbarK
# P+HBOtudbO+T1t0mWvAw5tzqZlbf/2PmC90tTzTQzRXcFaWUXlgGz95aEb+yn+X4
# k+brJ9D719z8pg==
# SIG # End signature block
