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
# MIIoLAYJKoZIhvcNAQcCoIIoHTCCKBkCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBhcaqsf3zH/yZW
# 8ZB4iMgxUSIJWUpLUBSnFE98y10yIaCCDXYwggX0MIID3KADAgECAhMzAAADTrU8
# esGEb+srAAAAAANOMA0GCSqGSIb3DQEBCwUAMH4xCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNpZ25p
# bmcgUENBIDIwMTEwHhcNMjMwMzE2MTg0MzI5WhcNMjQwMzE0MTg0MzI5WjB0MQsw
# CQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9u
# ZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMR4wHAYDVQQDExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24wggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIB
# AQDdCKiNI6IBFWuvJUmf6WdOJqZmIwYs5G7AJD5UbcL6tsC+EBPDbr36pFGo1bsU
# p53nRyFYnncoMg8FK0d8jLlw0lgexDDr7gicf2zOBFWqfv/nSLwzJFNP5W03DF/1
# 1oZ12rSFqGlm+O46cRjTDFBpMRCZZGddZlRBjivby0eI1VgTD1TvAdfBYQe82fhm
# WQkYR/lWmAK+vW/1+bO7jHaxXTNCxLIBW07F8PBjUcwFxxyfbe2mHB4h1L4U0Ofa
# +HX/aREQ7SqYZz59sXM2ySOfvYyIjnqSO80NGBaz5DvzIG88J0+BNhOu2jl6Dfcq
# jYQs1H/PMSQIK6E7lXDXSpXzAgMBAAGjggFzMIIBbzAfBgNVHSUEGDAWBgorBgEE
# AYI3TAgBBggrBgEFBQcDAzAdBgNVHQ4EFgQUnMc7Zn/ukKBsBiWkwdNfsN5pdwAw
# RQYDVR0RBD4wPKQ6MDgxHjAcBgNVBAsTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEW
# MBQGA1UEBRMNMjMwMDEyKzUwMDUxNjAfBgNVHSMEGDAWgBRIbmTlUAXTgqoXNzci
# tW2oynUClTBUBgNVHR8ETTBLMEmgR6BFhkNodHRwOi8vd3d3Lm1pY3Jvc29mdC5j
# b20vcGtpb3BzL2NybC9NaWNDb2RTaWdQQ0EyMDExXzIwMTEtMDctMDguY3JsMGEG
# CCsGAQUFBwEBBFUwUzBRBggrBgEFBQcwAoZFaHR0cDovL3d3dy5taWNyb3NvZnQu
# Y29tL3BraW9wcy9jZXJ0cy9NaWNDb2RTaWdQQ0EyMDExXzIwMTEtMDctMDguY3J0
# MAwGA1UdEwEB/wQCMAAwDQYJKoZIhvcNAQELBQADggIBAD21v9pHoLdBSNlFAjmk
# mx4XxOZAPsVxxXbDyQv1+kGDe9XpgBnT1lXnx7JDpFMKBwAyIwdInmvhK9pGBa31
# TyeL3p7R2s0L8SABPPRJHAEk4NHpBXxHjm4TKjezAbSqqbgsy10Y7KApy+9UrKa2
# kGmsuASsk95PVm5vem7OmTs42vm0BJUU+JPQLg8Y/sdj3TtSfLYYZAaJwTAIgi7d
# hzn5hatLo7Dhz+4T+MrFd+6LUa2U3zr97QwzDthx+RP9/RZnur4inzSQsG5DCVIM
# pA1l2NWEA3KAca0tI2l6hQNYsaKL1kefdfHCrPxEry8onJjyGGv9YKoLv6AOO7Oh
# JEmbQlz/xksYG2N/JSOJ+QqYpGTEuYFYVWain7He6jgb41JbpOGKDdE/b+V2q/gX
# UgFe2gdwTpCDsvh8SMRoq1/BNXcr7iTAU38Vgr83iVtPYmFhZOVM0ULp/kKTVoir
# IpP2KCxT4OekOctt8grYnhJ16QMjmMv5o53hjNFXOxigkQWYzUO+6w50g0FAeFa8
# 5ugCCB6lXEk21FFB1FdIHpjSQf+LP/W2OV/HfhC3uTPgKbRtXo83TZYEudooyZ/A
# Vu08sibZ3MkGOJORLERNwKm2G7oqdOv4Qj8Z0JrGgMzj46NFKAxkLSpE5oHQYP1H
# tPx1lPfD7iNSbJsP6LiUHXH1MIIHejCCBWKgAwIBAgIKYQ6Q0gAAAAAAAzANBgkq
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
# /Xmfwb1tbWrJUnMTDXpQzTGCGgwwghoIAgEBMIGVMH4xCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNp
# Z25pbmcgUENBIDIwMTECEzMAAANOtTx6wYRv6ysAAAAAA04wDQYJYIZIAWUDBAIB
# BQCgga4wGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEO
# MAwGCisGAQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEILvOYQTjlO71KwMlq8AI5DXY
# KHY+DATV5mSrQWO98tF1MEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8A
# cwBvAGYAdKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEB
# BQAEggEADKvou6kox+0an+/cL9BmyzounyRywAZsxyJ8yqRMN+ozHkLOhQS37+ZB
# T/rmHlrlCoEUqy7BIwDL5PbptQXvYVjG6gvPVpdM4ufvOvcOWe2dzEcuIgo4SA0l
# EdpPIHrMGBoEFND28AlHpuPXyHS52QxxsXEUDjVM+Hv+J+ZKo5aRpDHIB6LR7bia
# Gskfh41/XKqtcnthwlhtDe2FE60lP6pglCBIDvKRNm+r1TJwcGSqjNgXACkebK7K
# wZvsGpoMkp/WAvFG8q8goxs2f5NOtbmy96+IOYcw086+2uxCqlselfKQab7RI86f
# X4AIuElrkpT/9DeuLxmofrfjn67W66GCF5YwgheSBgorBgEEAYI3AwMBMYIXgjCC
# F34GCSqGSIb3DQEHAqCCF28wghdrAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFRBgsq
# hkiG9w0BCRABBKCCAUAEggE8MIIBOAIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFl
# AwQCAQUABCBJDxVpP/On8PhfBuZDtaYaHsijZOPGtYgoNx45svh9cwIGZNT7frf0
# GBIyMDIzMDgxNjE1NDMyNi4wM1owBIACAfSggdGkgc4wgcsxCzAJBgNVBAYTAlVT
# MRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQK
# ExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVy
# aWNhIE9wZXJhdGlvbnMxJzAlBgNVBAsTHm5TaGllbGQgVFNTIEVTTjpGMDAyLTA1
# RTAtRDk0NzElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaCC
# Ee0wggcgMIIFCKADAgECAhMzAAABzg8Y90WX58b/AAEAAAHOMA0GCSqGSIb3DQEB
# CwUAMHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
# EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNV
# BAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwMB4XDTIzMDUyNTE5MTIw
# OFoXDTI0MDIwMTE5MTIwOFowgcsxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNo
# aW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
# cG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVyaWNhIE9wZXJhdGlvbnMx
# JzAlBgNVBAsTHm5TaGllbGQgVFNTIEVTTjpGMDAyLTA1RTAtRDk0NzElMCMGA1UE
# AxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZTCCAiIwDQYJKoZIhvcNAQEB
# BQADggIPADCCAgoCggIBALkKTBnXKNjHd+cpPD9WfPAYaEo4iOowNK4d0aDwmgnY
# s3acFRSQDDGFgBHhlBaD1BuPFn8NasPbF5TAuco4o0M22Ff29oqP5fkK3GLqKhNV
# TtQHmhDjAamRXaSZ+enRFgZ2G1znwBqhRkVy+F0GUnbIRPC4K4RXqmTgnuCEbKaz
# OvQdWpH0HYwmNXyt48v4cYjMhtguuTSNaZopFE+wV/EorDeeMWAWVKpoEDhJW5XQ
# VgT/i3Rn1SNdQ8urD6IN4tS/eyneH0zig0hnbXpU+VWV/cU4VEuVmDx4J5fjGVoG
# NaJr3mdeVcxdBapojCSA3hOxHPKaB5a5XMylE8FaTqA6PPHBeeMhxarHWU/OchK/
# /G2fUekH4IgXlI8qQJ/yUI+JnUS0HTUXMdGP5iFFYmOxGXWAtbWLQQuutZUAWsM+
# siyEEqjPpJQDIoPvI/Hrj1LIsYGj+8kfFmAyViKtyR/16SWms0N6iyWGt4hjMAms
# wn5ihaf0PGaD/uEYL9cfx6WOYmoEurIcKZ2BL5YCuPzhyJEayapFpQH/iEUZSQu/
# UZra8Q+1UHJSaMdwCmklHU86aLUBNX0W6w5BDGQPaddZIpZ8oCkPu0Z81kxG+S58
# cXAZ/k2c3QCKbSWMGDU7mCzjcWiT+5J7XbNAvSFI/0l/6IIv409M8A8k8Sxt1hq9
# AgMBAAGjggFJMIIBRTAdBgNVHQ4EFgQUMPsIRkCjkwyOmX4VZOxTKfnihcUwHwYD
# VR0jBBgwFoAUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXwYDVR0fBFgwVjBUoFKgUIZO
# aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwvTWljcm9zb2Z0JTIw
# VGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3JsMGwGCCsGAQUFBwEBBGAwXjBc
# BggrBgEFBQcwAoZQaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jZXJ0
# cy9NaWNyb3NvZnQlMjBUaW1lLVN0YW1wJTIwUENBJTIwMjAxMCgxKS5jcnQwDAYD
# VR0TAQH/BAIwADAWBgNVHSUBAf8EDDAKBggrBgEFBQcDCDAOBgNVHQ8BAf8EBAMC
# B4AwDQYJKoZIhvcNAQELBQADggIBAJcgFhb/RRVMMNk799DZey00LgNkbxE9uuVk
# fIW3nvP9Vao+63qu6BrIZbVRy8m7F/xES2JO2Z/lJDk3am7SdOXo7gG1Jwi8z8ry
# l7/KPvvHy7q9+TxDpIM5mgWkilWQUffLruKx/j9J+g30zactv3emj862Uz0BxZMl
# uq6fML6SZ4tzEmYLEtyyIEwnplO9I1ZCeJyAy6/s8XSKn0diPMVag2ryqeuzuD75
# gY9dTt6mXUpIFOv1RHsef+Q1yoKJl/bYOGhk9U/v69kdNj45ejGgQQ6fMBsCUEK6
# n/Btc0mKyyIrxnJEldP9HgVWYTbYJK+m76nkx5GUFPjamQiGEyWpeWx1B1P1xYi2
# JbzJZGloTVkjWbL0Vbwkz0O0JC2Ldm4YdI2m+bhABj/rWvKsqiXhEt/kX5ICXijN
# W8zw8ox5F0SajbA7NkrgwTsPuIfMtxinm5zDSSCzLf3p9pzOxd2ctKiZcwCuy0aQ
# e+38XeGrZ4RJDY+Ctf6Cl0Gjy49XXrLCaekDBiox1kz7IfoH//gyxcS7dGDlA6g5
# Z2M1UHtCbow8mD68Nfn1e8yaJsDNumXEjKntLWTLsv6DeGrorGUpD1ptSNilo4Wc
# e0oMUn6/HOdwQxURXcgdB2MLu6uo4GA7u/Kl4jCNDrz2q7Y024hDpxlmAyfwS9JJ
# ZHAMbyCJMIIHcTCCBVmgAwIBAgITMwAAABXF52ueAptJmQAAAAAAFTANBgkqhkiG
# 9w0BAQsFADCBiDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAO
# BgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEy
# MDAGA1UEAxMpTWljcm9zb2Z0IFJvb3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIw
# MTAwHhcNMjEwOTMwMTgyMjI1WhcNMzAwOTMwMTgzMjI1WjB8MQswCQYDVQQGEwJV
# UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UE
# ChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGlt
# ZS1TdGFtcCBQQ0EgMjAxMDCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIB
# AOThpkzntHIhC3miy9ckeb0O1YLT/e6cBwfSqWxOdcjKNVf2AX9sSuDivbk+F2Az
# /1xPx2b3lVNxWuJ+Slr+uDZnhUYjDLWNE893MsAQGOhgfWpSg0S3po5GawcU88V2
# 9YZQ3MFEyHFcUTE3oAo4bo3t1w/YJlN8OWECesSq/XJprx2rrPY2vjUmZNqYO7oa
# ezOtgFt+jBAcnVL+tuhiJdxqD89d9P6OU8/W7IVWTe/dvI2k45GPsjksUZzpcGkN
# yjYtcI4xyDUoveO0hyTD4MmPfrVUj9z6BVWYbWg7mka97aSueik3rMvrg0XnRm7K
# MtXAhjBcTyziYrLNueKNiOSWrAFKu75xqRdbZ2De+JKRHh09/SDPc31BmkZ1zcRf
# NN0Sidb9pSB9fvzZnkXftnIv231fgLrbqn427DZM9ituqBJR6L8FA6PRc6ZNN3SU
# HDSCD/AQ8rdHGO2n6Jl8P0zbr17C89XYcz1DTsEzOUyOArxCaC4Q6oRRRuLRvWoY
# WmEBc8pnol7XKHYC4jMYctenIPDC+hIK12NvDMk2ZItboKaDIV1fMHSRlJTYuVD5
# C4lh8zYGNRiER9vcG9H9stQcxWv2XFJRXRLbJbqvUAV6bMURHXLvjflSxIUXk8A8
# FdsaN8cIFRg/eKtFtvUeh17aj54WcmnGrnu3tz5q4i6tAgMBAAGjggHdMIIB2TAS
# BgkrBgEEAYI3FQEEBQIDAQABMCMGCSsGAQQBgjcVAgQWBBQqp1L+ZMSavoKRPEY1
# Kc8Q/y8E7jAdBgNVHQ4EFgQUn6cVXQBeYl2D9OXSZacbUzUZ6XIwXAYDVR0gBFUw
# UzBRBgwrBgEEAYI3TIN9AQEwQTA/BggrBgEFBQcCARYzaHR0cDovL3d3dy5taWNy
# b3NvZnQuY29tL3BraW9wcy9Eb2NzL1JlcG9zaXRvcnkuaHRtMBMGA1UdJQQMMAoG
# CCsGAQUFBwMIMBkGCSsGAQQBgjcUAgQMHgoAUwB1AGIAQwBBMAsGA1UdDwQEAwIB
# hjAPBgNVHRMBAf8EBTADAQH/MB8GA1UdIwQYMBaAFNX2VsuP6KJcYmjRPZSQW9fO
# mhjEMFYGA1UdHwRPME0wS6BJoEeGRWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9w
# a2kvY3JsL3Byb2R1Y3RzL01pY1Jvb0NlckF1dF8yMDEwLTA2LTIzLmNybDBaBggr
# BgEFBQcBAQROMEwwSgYIKwYBBQUHMAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNv
# bS9wa2kvY2VydHMvTWljUm9vQ2VyQXV0XzIwMTAtMDYtMjMuY3J0MA0GCSqGSIb3
# DQEBCwUAA4ICAQCdVX38Kq3hLB9nATEkW+Geckv8qW/qXBS2Pk5HZHixBpOXPTEz
# tTnXwnE2P9pkbHzQdTltuw8x5MKP+2zRoZQYIu7pZmc6U03dmLq2HnjYNi6cqYJW
# AAOwBb6J6Gngugnue99qb74py27YP0h1AdkY3m2CDPVtI1TkeFN1JFe53Z/zjj3G
# 82jfZfakVqr3lbYoVSfQJL1AoL8ZthISEV09J+BAljis9/kpicO8F7BUhUKz/Aye
# ixmJ5/ALaoHCgRlCGVJ1ijbCHcNhcy4sa3tuPywJeBTpkbKpW99Jo3QMvOyRgNI9
# 5ko+ZjtPu4b6MhrZlvSP9pEB9s7GdP32THJvEKt1MMU0sHrYUP4KWN1APMdUbZ1j
# dEgssU5HLcEUBHG/ZPkkvnNtyo4JvbMBV0lUZNlz138eW0QBjloZkWsNn6Qo3GcZ
# KCS6OEuabvshVGtqRRFHqfG3rsjoiV5PndLQTHa1V1QJsWkBRH58oWFsc/4Ku+xB
# Zj1p/cvBQUl+fpO+y/g75LcVv7TOPqUxUYS8vwLBgqJ7Fx0ViY1w/ue10CgaiQuP
# Ntq6TPmb/wrpNPgkNWcr4A245oyZ1uEi6vAnQj0llOZ0dFtq0Z4+7X6gMTN9vMvp
# e784cETRkPHIqzqKOghif9lwY1NNje6CbaUFEMFxBmoQtB1VM1izoXBm8qGCA1Aw
# ggI4AgEBMIH5oYHRpIHOMIHLMQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGlu
# Z3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
# cmF0aW9uMSUwIwYDVQQLExxNaWNyb3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMScw
# JQYDVQQLEx5uU2hpZWxkIFRTUyBFU046RjAwMi0wNUUwLUQ5NDcxJTAjBgNVBAMT
# HE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2WiIwoBATAHBgUrDgMCGgMVAF2N
# lRsU0DIKRUToN1VbKLNNhAfMoIGDMIGApH4wfDELMAkGA1UEBhMCVVMxEzARBgNV
# BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jv
# c29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAg
# UENBIDIwMTAwDQYJKoZIhvcNAQELBQACBQDoh2K6MCIYDzIwMjMwODE2MTQ1OTA2
# WhgPMjAyMzA4MTcxNDU5MDZaMHcwPQYKKwYBBAGEWQoEATEvMC0wCgIFAOiHYroC
# AQAwCgIBAAICOzQCAf8wBwIBAAICEoIwCgIFAOiItDoCAQAwNgYKKwYBBAGEWQoE
# AjEoMCYwDAYKKwYBBAGEWQoDAqAKMAgCAQACAwehIKEKMAgCAQACAwGGoDANBgkq
# hkiG9w0BAQsFAAOCAQEA5G2nFX0FPO9yqnq7b/7vg3RyEx5tYe+dTn0X3JNSJ0h9
# 7dAVtbjQBGfG8x82YjRqAjSRkycBBYn7YevM4LjUie6QQzoXOUD3lrIS7MXGpMKd
# QbXD3BTCXwrxMq5LSaVZkRm81Dvu+ObuEs9vIKW/KCS5NYeap01utZmEUIf5J36Y
# WNA71W9pcAWa0gpGtXFHCyifLfGe+rjvND6pq6A9Lm3e2DoLe91SMOb+OpNp8Pgq
# eTZqqPjtdJFGbXAmV1FsuadfNYsoW7kiKMOneGDVhfxHCj2YxsLpJ9dmjh2knmNQ
# TNpG/iKHcqfAwps17mZJjNfE+cDgsQwmAWsILdL0cDGCBA0wggQJAgEBMIGTMHwx
# CzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRt
# b25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1p
# Y3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAABzg8Y90WX58b/AAEAAAHO
# MA0GCWCGSAFlAwQCAQUAoIIBSjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQw
# LwYJKoZIhvcNAQkEMSIEIINy1QOLrxbBYDdt+x3cLEo93Y414SsMG/1rTiYC9kFq
# MIH6BgsqhkiG9w0BCRACLzGB6jCB5zCB5DCBvQQgMmzPQXovRcNSbnclEF80xmzM
# qyWUsMyJoVnc8v8urGcwgZgwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0Eg
# MjAxMAITMwAAAc4PGPdFl+fG/wABAAABzjAiBCAqdszAqnHCbKBzIAl09vPby+Xe
# J/TW9rIBs+q6DVs2gjANBgkqhkiG9w0BAQsFAASCAgA+bhlgJpICQ6wsAIcVdDVr
# LB6KK46XHtR9v0UWUhoketdRWCkjZhQYeUC/Ljz3su6MC3LdwoPhMLX3hlI0kERc
# NphCo4N19Fa34OkcoXvbxYjh2KiiiPnB87DzrWdPo3yynowJON8Ob8MPN7eRMK1M
# +v+E6Gyj+RbHPPLfxwEgbWnBO7YOVFmmrLiHxfKmXd5EIfEA9KeIdjHlEWSQ0pbw
# gjZ4VCnsTBlGvAN9OtovCVp+3H3Iq6QHEdSiiEvFJLjbv0mUSC+pck0wcCNHqDDZ
# mcgXrI/Ss0yMQnobrWIUMOrkt2X4IfDcogNVUGARt5eMNG5MgBC4qDH3dB8xbXHY
# +3xK8gItB+lzBwAmuDawvzSxVCvZ9Pj6O2++LwN5fxjzp5+D8pjarK2QZYZwgrtC
# 4K+ZH1D1Qmsg/R/RSL8OSunZHU1n6S+0IaWSaGa/w17ktxYUXyRG67yrUXBv1E5+
# P0JHBigXVX+dtRA3F5/eh2e1MXPxF6aukcaAbnc3LezLRn57FcXUAY2nmgBPwxLG
# +2inEYSr6v/FOB6VjLy/fuwgAvJzg+2c50Smnom8dLLm28XixLA1NdtHoS8NYt2T
# ujPV6+F1vroqYknX5G8Qfz39DEF5r9+9gRMIPMLpM+TxEEh7J2hGbuqztcTR8582
# 3AbM17Psvye/KUUw9xkOAw==
# SIG # End signature block
