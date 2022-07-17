#Requires -Modules "Az", "Az.ResourceGraph", "Microsoft.Graph"
#Requires -PSEdition Core

# Use these parameters to customize the deployment instead of modifying the default parameter values
[CmdletBinding()]
Param(
	# The name of the Static Web App, in the current subscription
	[Parameter(Mandatory)]
	[string]$swaName,
	# The name of the resource group of the Static Web App
	[Parameter(Mandatory)]
	[string]$rgName
)

$IssueDetectedText = 'ISSUE DETECTED:'
$RecommendationText = 'RECOMMENDATION:'

function Get-Swa() {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		[string]$swaName,
		[Parameter(Position = 2)]
		[string]$rgName
	)

	Write-Verbose "Attempting to get Static Web Application '$swaName' in resource group '$rgName'."
	return Get-AzStaticWebApp -Name $swaName -ResourceGroupName $rgName
}

function Confirm-SwaProperties {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		[Microsoft.Azure.PowerShell.Cmdlets.Websites.Models.Api20201201.StaticSiteArmResource]$swa
	)

	[string]$ExpectedSku = 'Standard'
	[string]$ActualSku = $swa.SkuTier

	if ($ActualSku -ne $ExpectedSku) {
		throw "Expected SKU: $ExpectedSku - Found SKU: $ActualSku.`n`tThe storage-as-a-service app requires a $ExpectedSku tier Static Web App."
	}
}

function Get-SwaAppSettings {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		[string]$swaName
	)

	# Must use az cli for this :(
	# Ensure az cli has the same subscription context
	az account set --subscription $(Get-AzContext).Subscription.Id | Out-Null
	[string]$settingsJson = az staticwebapp appsettings list --name $swaName --query properties

	return ConvertFrom-Json $settingsJson -Depth 2
}

function Confirm-SwaAppSettings {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		$appSettings
	)

	# LATER: Provide impact statement for each setting

	# TODO: Re-add API_CLIENT_ID, API_CLIENT_SECRET
	$requiredProperties = @("API_CLIENT_ID", "API_CLIENT_SECRET", "AZURE_CLIENT_ID", "AZURE_CLIENT_SECRET", "AZURE_TENANT_ID", `
			"CacheConnection", "DATALAKE_STORAGE_ACCOUNTS")
	# Properties that should be in the Static Web App app settings, but are technically optional
	$recommendedProperties = @("APPINSIGHTS_INSTRUMENTATIONKEY", "CONFIGURATION_API_KEY", "FILESYSTEMS_API_KEY", "COST_PER_TB")

	# Test the recommended properties first
	# This will not throw, just output a warning
	foreach ($property in $recommendedProperties) {
		Write-Verbose "Evaluating recommended App Setting '$property'"
		Test-RecommendedAppSetting $appSettings $property
	}

	# Test the required properties - list all missing ones
	# Assume all is OK
	[bool]$RequiredAppSettingsOK = $true

	foreach ($property in $requiredProperties) {
		Write-Verbose "Evaluating required App Setting '$property'"
		$RequiredAppSettingsOK = $(Test-RequiredAppSetting $appSettings $property) `
			-and $RequiredAppSettingsOK
	}

	# If at least one required App Setting is missing
	if (! $RequiredAppSettingsOK) {
		# Throw
		throw "At least one required App Setting is missing from the Static Web Application. Review the output above for details."
	}
}

function Test-RequiredAppSetting {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		[PSobject]$appSettings,
		[Parameter(Position = 2)]
		[string]$propertyName
	)

	# Check if the specified property name occurs in the collection of
	# property names of the App Settings object
	[bool]$propertyExists = [bool]($appSettings.PSObject.Properties.Name -match "\b$propertyName\b")
	[bool]$propertyValid = $true

	if (! $propertyExists) {
		Write-Host "$IssueDetectedText Static Web App configuration must have a setting '$propertyName'." -ForegroundColor Red
		$propertyValid = $false
	}
	else {
		# TODO: Value validation
	}

	return $propertyValid
}

function Test-RecommendedAppSetting {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		[PSobject]$appSettings,
		[Parameter(Position = 2)]
		[string]$propertyName
	)

	[bool]$propertyExists = [bool]($appSettings.PSObject.Properties.Name -match "\b$propertyName\b")

	if (! $propertyExists) {
		Write-Warning "$RecommendationText Static Web App configuration should have a setting '$propertyName'."
	}
}

function Get-StorageAccounts {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		$appSettings
	)

	[string[]]$separators = ",", ";"
	[System.StringSplitOptions]$option = [System.StringSplitOptions]::RemoveEmptyEntries

	[string[]]$storageAccounts = $appSettings.DATALAKE_STORAGE_ACCOUNTS.ToString().Split($separators, $option)

	# TODO: Warn on duplicates

	return $storageAccounts
}

function Get-ApiPrincipalId {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		$appSettings
	)

	Write-Verbose "Looking up AAD Object ID for Static Web App's AZURE_CLIENT_ID"
	$AppClientId = $appSettings.AZURE_CLIENT_ID

	$App = Get-MgApplication -Filter "AppId eq '$AppClientId'"

	return $App.Id
}

function Confirm-StorageAccount {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		[string]$storageAccountName,
		[Parameter(Position = 2)]
		[string]$apiPrincipalId
	)

	# Confirm the storage account exists
	# Cannot use Get-AzStorageAccount because we don't know the subscription or resource group of the storage account
	# Use Resource Graph instead, because a storage account must be globally unique
	# Same method is used in the application code
	Write-Verbose "Querying Azure Resource Graph for ADLS Gen 2 account '$storageAccountName'"

	$response = Search-AzGraph -First 1 -Verbose:$false -Query @"
	Resources
	| where name == '$storageAccountName'
		and type =~ 'microsoft.storage/storageaccounts'
		and kind == 'StorageV2'
		and properties['isHnsEnabled']
	| limit 1
	| project id, resourceGroup
"@

	# If the storage account wasn't found with the Graph query
	if ($response.Count -lt 1) {
		throw @"
Cannot find storage account with name '$storageAccountName'. Possible causes:
		- The account does not exist.
		- You don't have access to the subscription where the account is created.
		- The storage account is not a General Purpose V2 account.
		- The storage account does not have hierarchical namespace enabled (ADLS Gen 2).
"@
	}

	#$storageAccountRG = $response.ResourceGroup
	$storageAccountId = $response.Id

	Write-Verbose "Retrieving Azure role assignments"
	$roleAssignments = Get-AzRoleAssignment -Scope $storageAccountId -ObjectId $apiPrincipalId

	# LATER: Look up instead of hardcoding?
	$requiredAssignmentRoleDefinitionIds = @(
		@{Id = "b7e6dc6d-f1e8-4753-8033-0f276bb0955b"; Name = "Storage Blob Data Owner" },
		@{Id = "18d7d88d-d35e-4fb5-a5c3-7773c20a72d9"; Name = "User Access Administrator" })

	[int]$requiredAssignmentCount = $requiredAssignmentRoleDefinitionIds.Length

	if (! $roleAssignments -or $roleAssignments.Count -lt $requiredAssignmentCount) {
		throw "The AAD principal '$apiPrincipalId' does not have the minimum required ($requiredAssignmentCount) role assignments on storage account '$storageAccountName'."
	}

	foreach ($roleDefinition in $requiredAssignmentRoleDefinitionIds) {
		Write-Verbose "Verifying that AAD principal '$apiPrincipalId' has '$($roleDefinition.Name)' assigned on storage account '$storageAccountName'"

		if (! $($roleAssignments | Where-Object { $_.RoleDefinitionId -eq $roleDefinition.Id })) {
			throw "The AAD principal '$apiPrincipalId' does not have the '$($roleDefinition.Name)' (role definition ID '$($roleDefinition.Id)') assigned on storage account '$storageAccountName'."
		}
	}

	# TODO: Validate no network ACLs
}

function Diagnose() {
	[CmdletBinding()]
	param (
		[Parameter(Position = 1)]
		[string]$swaName,
		[Parameter(Position = 2)]
		[string]$rgName
	)

	$swa = Get-Swa $swaName $rgName

	if (! $swa) {
		throw "Cannot find a Static Web Application '$swaName' in resource group '$rgName'."
	}

	Write-Verbose "Found $($swa.Id)"

	Confirm-SwaProperties $swa

	# Get the App Settings of the Static Web App
	$appSettings = Get-SwaAppSettings $swaName

	# Confirm all required App Settings are present
	Confirm-SwaAppSettings $appSettings

	# Get the SWA's AAD object ID (based on the client ID found in App Settings)
	[string]$apiPrincipalId = Get-ApiPrincipalId $appSettings
	# Get the storage account list configured for the app
	[string[]]$storageAccounts = Get-StorageAccounts $appSettings

	if ($storageAccounts.Length -lt 1) {
		throw "No storage account names are configured in DATALAKE_STORAGE_ACCOUNTS"
	}

	foreach ($storageAccount in $storageAccounts) {
		Confirm-StorageAccount $storageAccount $apiPrincipalId
	}
}

Set-StrictMode -Version "Latest"

$PSDefaultParameterValues = @{}
$PSDefaultParameterValues += @{'*:ErrorAction' = 'SilentlyContinue' }
Set-Item -Path Env:\SuppressAzurePowerShellBreakingChangeWarnings -Value $true

Connect-MgGraph -Scopes "Application.Read.All"

try {
	Diagnose $swaName $rgName

	Write-Host "`nNo issues detected." -ForegroundColor Green
}
catch {
	Write-Host "$IssueDetectedText $_" -ForegroundColor Red
}
finally {
	Write-Host "`nDiagnostics finished." -ForegroundColor White
}

