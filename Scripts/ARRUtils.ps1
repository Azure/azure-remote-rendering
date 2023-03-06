# This Powershell script is an example for the usage of the Azure Remote Rendering service
# Documentation: https://docs.microsoft.com/en-us/azure/remote-rendering/samples/powershell-example-scripts

$Global:ARRAuthenticationToken = $null

$AllPublicRegions = @("australiaeast", "eastus", "eastus2", "japaneast", "northeurope", "southcentralus", "southeastasia", "uksouth", "westeurope", "westus2")

function ToDomain {
    param ([ValidateSet("Rendering", "Account")] $Type, $Region)

    $suffix = ".mixedreality.azure.com"

    $cleanedRegion = $Region -replace $suffix, "" # If someone specifies eastus.mixedreality.azure.com it should still work

    if( $cleanedRegion -Match "https://(?<content>.*)"){
        $cleanedRegion = $matches['content'] ## If the url contains https:// remove it.
    }
    if ($AllPublicRegions.Contains($cleanedRegion)) {
        $prefix = if ($Type -eq "Rendering") {
            "remoterendering"
        } else {
            "sts"
        }

        return "https://$Prefix.$($cleanedRegion)$suffix"
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
            $sessionId = "Sample-Session-$(New-Guid)"
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
# MIInkgYJKoZIhvcNAQcCoIIngzCCJ38CAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBKFd36U3KztjOx
# icDXbDRcFYbsRSfI9Cex+Hgo+nMryqCCDXYwggX0MIID3KADAgECAhMzAAACy7d1
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
# /Xmfwb1tbWrJUnMTDXpQzTGCGXIwghluAgEBMIGVMH4xCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNp
# Z25pbmcgUENBIDIwMTECEzMAAALLt3U5+wJxQjYAAAAAAsswDQYJYIZIAWUDBAIB
# BQCgga4wGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQwHAYKKwYBBAGCNwIBCzEO
# MAwGCisGAQQBgjcCARUwLwYJKoZIhvcNAQkEMSIEINdpPpWP89fIri36iXA23BXi
# zZLDqIXjBt8+i0cq8K8UMEIGCisGAQQBgjcCAQwxNDAyoBSAEgBNAGkAYwByAG8A
# cwBvAGYAdKEagBhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20wDQYJKoZIhvcNAQEB
# BQAEggEAs1h/W0Ei+6lL8EF7M226oSbdS62YAfbP/iuxCILE31mCxbgzdRqN+7Au
# ZOqI6wGvKVUl+F4tfS7WX+CEm66JA14wNeW5D8S1dkqHyKM0U1h7U6guZPXc6j1S
# 6N4P8a6nU6DoKkaVGnd6UcsDHFBllftLxHUB0/hJoK1Wid95a8VEHuqkXCtTUEeT
# nhP9W73YsIJOOyR2BRYi+phLSGU4pxnxQvMEa6SMJG0mBIFVOpqVWnOvGKvVARoY
# pnNuqCsM6pToaDHyvjSe6xwPALXLG4vYg2kahwoxYRkdumgikRW4bL0ldsNaXq7n
# MRAoHURYlFxdZJGbjYqiiLDu3/QIiqGCFvwwghb4BgorBgEEAYI3AwMBMYIW6DCC
# FuQGCSqGSIb3DQEHAqCCFtUwghbRAgEDMQ8wDQYJYIZIAWUDBAIBBQAwggFQBgsq
# hkiG9w0BCRABBKCCAT8EggE7MIIBNwIBAQYKKwYBBAGEWQoDATAxMA0GCWCGSAFl
# AwQCAQUABCA+jGPXh1UTnlP1C8ov+2DlWDv5doAcvptsxaKKQtaIpwIGY+5PnlHQ
# GBIyMDIzMDMwMjEzMTMxNi45MVowBIACAfSggdCkgc0wgcoxCzAJBgNVBAYTAlVT
# MRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQK
# ExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVy
# aWNhIE9wZXJhdGlvbnMxJjAkBgNVBAsTHVRoYWxlcyBUU1MgRVNOOjEyQkMtRTNB
# RS03NEVCMSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNloIIR
# VDCCBwwwggT0oAMCAQICEzMAAAHKT8Kz7QMNGGwAAQAAAcowDQYJKoZIhvcNAQEL
# BQAwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcT
# B1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UE
# AxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMjIxMTA0MTkwMTQw
# WhcNMjQwMjAyMTkwMTQwWjCByjELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hp
# bmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jw
# b3JhdGlvbjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0aW9uczEm
# MCQGA1UECxMdVGhhbGVzIFRTUyBFU046MTJCQy1FM0FFLTc0RUIxJTAjBgNVBAMT
# HE1pY3Jvc29mdCBUaW1lLVN0YW1wIFNlcnZpY2UwggIiMA0GCSqGSIb3DQEBAQUA
# A4ICDwAwggIKAoICAQDDAZyr2PnStYSRwtKUZkvB5RV/FdFpSOI+zJo1XE90xGzc
# JV7nyyK78SRpW3u3s81M+Sj+zyU226wB4sSfOSLjjLGTZz16SbwTVJDZhX1vz8s7
# F8pqlny1WU/LHDoOYXM0VCOJ9WbwSJnuUVGhjjjy+lxsEPXyqNg0X/ZndJByFyx1
# XU31jpXZYaXnlWYuoVFfn52m12Ot4FfOLdZb1OygIRZxgIErnBiBL21PZJJJPNp7
# eOZ3DjSD4s4jKtU8XYOjORK2/okEM+/BqFdakoak7usesoX6jsQI39WJAUxnKn+/
# F4+JQAEM2rMRQjyzuSViZ4de+N5A6r8IzcL9jxuPd8k5udkft4Be9EOfFPxHpb+4
# PWYZQm+/0z0Ey7eeEqkqZLHPM7ku1wwSHa0xfGEwYY0xQ/cM4Qrdf7b8sPVnTe6w
# lOTmkc2gf+AMi9unvzsLDjS2wCmIC+2sdjC5vROoi/xnLraXyfyz8y/8/vrgJOqv
# FxfNqUEeH5fLhc+OZp2c+RknJyncpzNuSD1Bu8mnQf/QWzAdL558Wh+kM0nAuHWG
# z9oyLUr+jMS/v9Ysg+wOArXp9T9rHJuowqTQ07GB6VSMBgqXjBTRjpDir03/0/AB
# LRyyJ9CFjWihB8AjSIMIJIQBOyUPxtM7S1G2p1wh1q85F6rOg928C/cOvVddDwID
# AQABo4IBNjCCATIwHQYDVR0OBBYEFPrH/qVLgRJDwpmF3RGBTtFhczx/MB8GA1Ud
# IwQYMBaAFJ+nFV0AXmJdg/Tl0mWnG1M1GelyMF8GA1UdHwRYMFYwVKBSoFCGTmh0
# dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY3JsL01pY3Jvc29mdCUyMFRp
# bWUtU3RhbXAlMjBQQ0ElMjAyMDEwKDEpLmNybDBsBggrBgEFBQcBAQRgMF4wXAYI
# KwYBBQUHMAKGUGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2VydHMv
# TWljcm9zb2Z0JTIwVGltZS1TdGFtcCUyMFBDQSUyMDIwMTAoMSkuY3J0MAwGA1Ud
# EwEB/wQCMAAwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZIhvcNAQELBQADggIB
# ANjQeSoJLcq4p58Vfz+ub920P3Trp0oSV42quLBmqwzhibwTDhCKo6o7uZahhhjg
# rnLx5dI4co1c5+k7pFtpiPyMI5wkAHm2ouXmGIyBoxsBUuuXWGWLH2yWg7jks43Q
# mmEq9rcPoBUoDs/vyYD2JEdlhRWtGLJ+9CNbGZSfGGKzx+ib3b79EdwRnUOHn6ni
# DN54vzhiXTRbKr0RyAEop+CrSUKNY1KrUBQbWwQuWBc5K8pnj+Vdcf4x+Fwd73VY
# shpmRL8e73B1NPojXgEL3vKEOxlZcCXQgnzTUjpS0QWkKxN47JkEnsIXSt/mXEny
# 0T2iM2zKpckq7BWfR7AIyRmrP9wTC/0UTHxCaxnRk2h1O2yX5X11mb55SswpmTo8
# qwoCu1D6MeR9WweAo4OWh6Wk6YeqBftRs7Q1WciWk/nmBBOpXvq9TvBFelR/PsqE
# TcFlc2DAbTl1GcJcPCuGFjP4i1vOzUrVHwjhgwMmNb3QBIKD0l/7HKBEpkYoeOjY
# GzZfJoq43U/oUUIhVc3sqAeX9tmJqQaruTlNDg5crnGSEIeGN2Ae7GPeErkBo7L4
# ZfE7+NvKoZGp5LF/5NM+5aENa6sijfdEwMZ7kNsiaNxtyPp1WFB6+ocKVHU4dJ+v
# 7ybWFZEkaULVq1w5YpqMCvA5RGolJWVOHBWAjLMY2aPOMIIHcTCCBVmgAwIBAgIT
# MwAAABXF52ueAptJmQAAAAAAFTANBgkqhkiG9w0BAQsFADCBiDELMAkGA1UEBhMC
# VVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNV
# BAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEyMDAGA1UEAxMpTWljcm9zb2Z0IFJv
# b3QgQ2VydGlmaWNhdGUgQXV0aG9yaXR5IDIwMTAwHhcNMjEwOTMwMTgyMjI1WhcN
# MzAwOTMwMTgzMjI1WjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3Rv
# bjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0
# aW9uMSYwJAYDVQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDCCAiIw
# DQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAOThpkzntHIhC3miy9ckeb0O1YLT
# /e6cBwfSqWxOdcjKNVf2AX9sSuDivbk+F2Az/1xPx2b3lVNxWuJ+Slr+uDZnhUYj
# DLWNE893MsAQGOhgfWpSg0S3po5GawcU88V29YZQ3MFEyHFcUTE3oAo4bo3t1w/Y
# JlN8OWECesSq/XJprx2rrPY2vjUmZNqYO7oaezOtgFt+jBAcnVL+tuhiJdxqD89d
# 9P6OU8/W7IVWTe/dvI2k45GPsjksUZzpcGkNyjYtcI4xyDUoveO0hyTD4MmPfrVU
# j9z6BVWYbWg7mka97aSueik3rMvrg0XnRm7KMtXAhjBcTyziYrLNueKNiOSWrAFK
# u75xqRdbZ2De+JKRHh09/SDPc31BmkZ1zcRfNN0Sidb9pSB9fvzZnkXftnIv231f
# gLrbqn427DZM9ituqBJR6L8FA6PRc6ZNN3SUHDSCD/AQ8rdHGO2n6Jl8P0zbr17C
# 89XYcz1DTsEzOUyOArxCaC4Q6oRRRuLRvWoYWmEBc8pnol7XKHYC4jMYctenIPDC
# +hIK12NvDMk2ZItboKaDIV1fMHSRlJTYuVD5C4lh8zYGNRiER9vcG9H9stQcxWv2
# XFJRXRLbJbqvUAV6bMURHXLvjflSxIUXk8A8FdsaN8cIFRg/eKtFtvUeh17aj54W
# cmnGrnu3tz5q4i6tAgMBAAGjggHdMIIB2TASBgkrBgEEAYI3FQEEBQIDAQABMCMG
# CSsGAQQBgjcVAgQWBBQqp1L+ZMSavoKRPEY1Kc8Q/y8E7jAdBgNVHQ4EFgQUn6cV
# XQBeYl2D9OXSZacbUzUZ6XIwXAYDVR0gBFUwUzBRBgwrBgEEAYI3TIN9AQEwQTA/
# BggrBgEFBQcCARYzaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9Eb2Nz
# L1JlcG9zaXRvcnkuaHRtMBMGA1UdJQQMMAoGCCsGAQUFBwMIMBkGCSsGAQQBgjcU
# AgQMHgoAUwB1AGIAQwBBMAsGA1UdDwQEAwIBhjAPBgNVHRMBAf8EBTADAQH/MB8G
# A1UdIwQYMBaAFNX2VsuP6KJcYmjRPZSQW9fOmhjEMFYGA1UdHwRPME0wS6BJoEeG
# RWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01pY1Jv
# b0NlckF1dF8yMDEwLTA2LTIzLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYBBQUH
# MAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9vQ2Vy
# QXV0XzIwMTAtMDYtMjMuY3J0MA0GCSqGSIb3DQEBCwUAA4ICAQCdVX38Kq3hLB9n
# ATEkW+Geckv8qW/qXBS2Pk5HZHixBpOXPTEztTnXwnE2P9pkbHzQdTltuw8x5MKP
# +2zRoZQYIu7pZmc6U03dmLq2HnjYNi6cqYJWAAOwBb6J6Gngugnue99qb74py27Y
# P0h1AdkY3m2CDPVtI1TkeFN1JFe53Z/zjj3G82jfZfakVqr3lbYoVSfQJL1AoL8Z
# thISEV09J+BAljis9/kpicO8F7BUhUKz/AyeixmJ5/ALaoHCgRlCGVJ1ijbCHcNh
# cy4sa3tuPywJeBTpkbKpW99Jo3QMvOyRgNI95ko+ZjtPu4b6MhrZlvSP9pEB9s7G
# dP32THJvEKt1MMU0sHrYUP4KWN1APMdUbZ1jdEgssU5HLcEUBHG/ZPkkvnNtyo4J
# vbMBV0lUZNlz138eW0QBjloZkWsNn6Qo3GcZKCS6OEuabvshVGtqRRFHqfG3rsjo
# iV5PndLQTHa1V1QJsWkBRH58oWFsc/4Ku+xBZj1p/cvBQUl+fpO+y/g75LcVv7TO
# PqUxUYS8vwLBgqJ7Fx0ViY1w/ue10CgaiQuPNtq6TPmb/wrpNPgkNWcr4A245oyZ
# 1uEi6vAnQj0llOZ0dFtq0Z4+7X6gMTN9vMvpe784cETRkPHIqzqKOghif9lwY1NN
# je6CbaUFEMFxBmoQtB1VM1izoXBm8qGCAsswggI0AgEBMIH4oYHQpIHNMIHKMQsw
# CQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9u
# ZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSUwIwYDVQQLExxNaWNy
# b3NvZnQgQW1lcmljYSBPcGVyYXRpb25zMSYwJAYDVQQLEx1UaGFsZXMgVFNTIEVT
# TjoxMkJDLUUzQUUtNzRFQjElMCMGA1UEAxMcTWljcm9zb2Z0IFRpbWUtU3RhbXAg
# U2VydmljZaIjCgEBMAcGBSsOAwIaAxUAo47nlwxPizI8/qcKWDYhZ9qMyqSggYMw
# gYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UE
# BxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYD
# VQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMDANBgkqhkiG9w0BAQUF
# AAIFAOeqmcwwIhgPMjAyMzAzMDIxMTQzMDhaGA8yMDIzMDMwMzExNDMwOFowdDA6
# BgorBgEEAYRZCgQBMSwwKjAKAgUA56qZzAIBADAHAgEAAgIYHjAHAgEAAgISrTAK
# AgUA56vrTAIBADA2BgorBgEEAYRZCgQCMSgwJjAMBgorBgEEAYRZCgMCoAowCAIB
# AAIDB6EgoQowCAIBAAIDAYagMA0GCSqGSIb3DQEBBQUAA4GBAJPtx79vs9usA7Ek
# 39Ap4IXGgX7LymBKnuwPgMSKvYbnQSOq4WCcN8P+3rWaEZPq0yWvJ6uDt/4wPtqD
# C2KXBqvKY0yVfBJ1IpBICCu1D6XR6Orjov+6tTabr6rHPr+Iq4Lqpcv2m9XVk4xG
# W270mUkyryD2N09rLOJoiW9Q+2BnMYIEDTCCBAkCAQEwgZMwfDELMAkGA1UEBhMC
# VVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNV
# BAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9zb2Z0IFRp
# bWUtU3RhbXAgUENBIDIwMTACEzMAAAHKT8Kz7QMNGGwAAQAAAcowDQYJYIZIAWUD
# BAIBBQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRABBDAvBgkqhkiG9w0B
# CQQxIgQgMOCVvK2WB2g4gTrWE16y7Cc2Com7/dEsXkqvFf1fRjYwgfoGCyqGSIb3
# DQEJEAIvMYHqMIHnMIHkMIG9BCATPRvzm+tVTayVkCTiO3VIMSTojNkDBUKhbAXc
# rNwa4DCBmDCBgKR+MHwxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9u
# MRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRp
# b24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQSAyMDEwAhMzAAAB
# yk/Cs+0DDRhsAAEAAAHKMCIEII++PFk8SPwN/hO+DGyezABNG0OGK+ItYRYsszz/
# UhXhMA0GCSqGSIb3DQEBCwUABIICAF86e8y+CyU9a2f+rC6Vk/Pb4x90w1xrL6S8
# uVvt0gQBN7G/oM3T9u3/jxAzxYmeu0MgELFXG76H3niTDGe0fTix+BQ0gnTp0CF5
# jVwQVXgGt2Rjnf7QIdLEYv5fZm4MygswxTyo87jUlg+qcwiWCiRO1/ppgqnQM1Xz
# ZSmX+PzUA+W4sX92CxW9Q9ZN1hSHTcnWce1m3hgD1B6a8I9I004k0TGVq+bftp7x
# zGeSBsx1FFlqekKq0dY1m5HJwMRATuExwRjfkuCnxiR/Q+Qjjgeob2gbcc0YrjdZ
# uYf1+DzjtUWc5rGn08Mw2Pxc0HnxEyCX7P6mOx5SS6mmSFC15HJAx1+m2Ah+5E8n
# eUf+xePt/XZtDTDi/wduna7hsIyITzYDAYGv3q9h04RUt6vqQGf4hze5is0DbjGc
# E6xiT2srW0nqOQpaO5/y+j+EpWXkZbANJbuStdWAaz86a+lR7hdQHrfYrsFHvbT9
# ZCmKkXPHw4wHyFyqIf1L4knK5mNpeVXM/ZDqschY7sDEPoGBa5vzMaRuLLb5iEjR
# fV86mjTVIYegtx+ft/f5PSpV+wCBk3gp+jT7NhpPte7WVEX3p1gBDqjoBEBmFle+
# /CUxmhXyHUHZRaoSyzTNbc2flvWwyDYOWKIacY3gqLZKkb5gG7YTnCgcA/HsN7qR
# rEAczPoj
# SIG # End signature block
