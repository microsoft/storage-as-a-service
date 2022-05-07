#Requires -Modules "Az"
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
		[Microsoft.Azure.PowerShell.Cmdlets.Websites.Models.Api20201201.StaticSiteArmResource]$swa
	)

	$requiredProperties = @("API_CLIENT_ID", "API_CLIENT_SECRET", "AZURE_CLIENT_ID", "AZURE_CLIENT_SECRET", "AZURE_TENANT_ID", `
			"CacheConnection", "DATALAKE_STORAGE_ACCOUNTS")
	# Properties that should be in the Static Web App app settings, but are technically optional
	$recommendedProperties = @("APPINSIGHTS_INSTRUMENTATIONKEY", "CONFIGURATION_API_KEY", "FILESYSTEMS_API_KEY", "COST_PER_TB")

	# Get the App Settings of the Static Web App
	$appSettings = Get-SwaAppSettings $swa.Name

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

	# TODO: This returns $true on a partial match
	#$appSettings.PSObject.Properties.Name
	[bool]$propertyExists = [bool]($appSettings.PSObject.Properties.Name -match "\b$propertyName\b")
	#[bool]$propertyExists = [bool](Get-Member -Name $propertyName -InputObject $appSettings -MemberType Property)
	[bool]$propertyValid = $true

	if (! $propertyExists) {
		Write-Host "$IssueDetectedText Static Web App configuration must have a setting '$propertyName'." -ForegroundColor Red
		$propertyValid = $false
	}

	# TODO: Value validation

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

	# TODO: This returns $true on a partial match
	[bool]$propertyExists = [bool]($appSettings.PSObject.Properties.Name -match "\b$propertyName\b")

	if (! $propertyExists) {
		Write-Warning "$RecommendationText Static Web App configuration should have a setting '$propertyName'."
	}
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

	Confirm-SwaAppSettings $swa
}

Set-StrictMode -Version "Latest"

$PSDefaultParameterValues = @{}
$PSDefaultParameterValues += @{'*:ErrorAction' = 'SilentlyContinue' }

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

