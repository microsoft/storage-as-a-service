# PowerShell script to deploy the template with generic parameter values
#
# For usage instructions, refer to https://github.com/Microsoft/storage-as-a-service

#Requires -Modules "Az", "Microsoft.Graph"
#Requires -PSEdition Core

# TODO: Add storage accounts parameter

# TODO: Add Azure Cache for Redis

# TODO: Use DeploymentScripts when scripting's needed?
# TODO: Generate secrets inside Bicep?

# TODO: Create API app registration in addition to SWA app registration

[CmdletBinding()]
Param(
	#
	[Parameter(Position = 1)]
	[int]$Sequence = 1,
	#
	[Parameter(Position = 2)]
	[ValidateSet('eastus2', 'centralus', 'westeurope', 'eastasia', 'westus2')]
	[string]$Location = 'eastus2',
	# The environment descriptor
	[Parameter(Position = 3)]
	[ValidateSet('test', 'demo', 'prod')]
	[string]$Environment = 'test',
	#
	[Parameter(Position = 4)]
	# TODO: Use {token} instead of positional param and replace() instead of format()
	[string]$NamingConvention = '{0}-saas-{1}-{2}-{3}',
	#
	[Parameter()]
	[bool]$DoNotCreateAppReg = $false,
	#
	[Parameter()]
	[bool]$DeploySampleStorageAccount = $false
)

[string]$ServicePrincipalName = "Storage-as-a-Service $Sequence"

#region PS FUNCTIONS

function New-RandomPassword {
	[CmdletBinding()]
	param (
		[Parameter(Mandatory)]
		[int]$PasswordLength
	)

	# ASCII codes of allowed characters
	# Digits + uppercase + lowercase
	$CodeSpace = (48..57) + (65..90) + (97..122)

	# Generate password
	[string]$Password = $null
	$CodeSpace | Get-Random -Count $PasswordLength | `
		ForEach-Object { $Password += [char]$_ }

	return $Password
}

function Set-AdminConsent {
	[CmdletBinding()]
	param (
		[Parameter(Mandatory)]
		[string]$applicationId,
		# The Azure Context]
		[Parameter(Mandatory)]
		[object]$context
	)

	$token = [Microsoft.Azure.Commands.Common.Authentication.AzureSession]::Instance.AuthenticationFactory.Authenticate(
		$context.Account, $context.Environment, $context.Tenant.Id, $null, "Never", $null, "74658136-14ec-4630-ad9b-26e160ff0fc6")
	$headers = @{
		'Authorization'          = 'Bearer ' + $token.AccessToken
		'X-Requested-With'       = 'XMLHttpRequest'
		'x-ms-client-request-id' = [guid]::NewGuid()
		'x-ms-correlation-id'    = [guid]::NewGuid()
	}

	$url = "https://main.iam.ad.ext.azure.com/api/RegisteredApplications/$applicationId/Consent?onBehalfOfAll=true"
	Invoke-RestMethod -Uri $url -Headers $headers -Method POST -ErrorAction Stop
}

function New-AppRegistrationClientSecret {
	[CmdletBinding()]
	param (
		[Parameter()]
		[string]$AppRegObjectId
	)

	[datetime]$SpnCredentialExpires = (Get-Date).AddYears(2)

	$PasswordCredential = @{
		DisplayName = "Created during deployment of Storage-as-a-Service"
		EndDateTime = $SpnCredentialExpires
	}

	$NewAppPwd = Add-MgApplicationPassword -ApplicationId $NewMgApp.Id `
		-PasswordCredential $PasswordCredential

	# Returns the client secret and the expiration date
	return @{
		SpnCredentialExpires = $NewAppPwd.EndDateTime
		SecretText           = $NewAppPwd.SecretText
	}
}

function New-AppRegistration {
	[CmdletBinding()]
	param (
		[Parameter()]
		[string]$AppName
	)
}

#endregion

#region SERVICE PRINCIPAL

################################################################################
# SERVICE PRINCIPAL / APP REGISTRATION
################################################################################

# Create Service Principal for the application
## Creates a new application ID too
$ServicePrincipal = Get-AzADServicePrincipal -DisplayName $ServicePrincipalName
[bool]$CreatedSPN = $false

# Connect to the Microsoft Graph
Connect-MgGraph -Scopes "Application.ReadWrite.All"

# If the service principal doesn't exist yet
if (! $ServicePrincipal -and ! $DoNotCreateAppReg) {
	Write-Verbose "Creating new Azure AD App Registration $ServicePrincipalName"

	# Define SPN's permissions to Microsoft Graph
	# TODO: Add GroupMember.Read.All, API app permission
	$AppAccess = @{
		# Microsoft Graph
		ResourceAppId  = "00000003-0000-0000-c000-000000000000";
		ResourceAccess = @(
			@{
				# Microsoft.Graph/User.Read.All
				Id   = "df021288-bdef-4463-88db-98f22de89214";
				Type = "Role"
			}
		)
	}

	$NewMgApp = New-MgApplication -DisplayName $ServicePrincipalName `
		-Description $ServicePrincipalName `
		-RequiredResourceAccess $AppAccess -SignInAudience "AzureADMyOrg" -Tags @("WindowsAzureActiveDirectoryIntegratedApp") `
		-Notes "Created by $ServicePrincipalName Deployment"

	Write-Verbose "Created new app registration with App Id $($NewMgApp.AppId) and Object Id $($NewMgApp.Id)"

	# The parameter is called "ApplicationId," but it refers to the app registration's Object ID
	$NewAppPwd = New-AppRegistrationClientSecret -AppRegObjectId $NewMgApp.Id

	Write-Verbose "Created new application password (will expire $SpnCredentialExpires)"

	# LATER: How to set a logo? Use -LogoInputFile
	# LATER: Assign application owner?

	Write-Verbose "Creating new Service Principal $ServicePrincipalName"

	$ServicePrincipal = New-MgServicePrincipal -AppId $NewMgApp.AppId `
		-Tags @("WindowsAzureActiveDirectoryIntegratedApp") `
		-Description "$ServicePrincipalName service principal"

	# Allow for time to process the SPN before granting admin consent
	Start-Sleep 30

	# Grant admin consent for Role assigned above
	Set-AdminConsent -applicationId $NewMgApp.AppId -context (Get-AzContext)

	$CreatedSPN = $true
}
else {
	$MgApp = Get-MgApplication -Filter "AppId eq '$($ServicePrincipal.AppId)'"

	# Create a new client secret
	$NewAppPwd = New-AppRegistrationClientSecret $MgApp.Id
}

#endregion SERVICE PRINCIPAL

$TemplateParameters = @{
	# REQUIRED
	location                   = $Location
	environment                = $Environment
	azTenantId                 = $(Get-AzContext).Tenant.Id
	# This is the Application (client) ID property
	azClientId                 = $ServicePrincipal.AppId
	azClientSecret             = if ($NewAppPwd) { $NewAppPwd.SecretText } else { "" }
	configApiKey               = (New-RandomPassword -PasswordLength 20)
	fileSystemsApiKey          = (New-RandomPassword -PasswordLength 20)
	# Object ID (not Application ID) of the Enterprise Application
	appRegPrincipalId          = $ServicePrincipal.Id

	# OPTIONAL
	sequence                   = $Sequence
	# {0} is the resource type, {1} is the environment, {2} is the location, {3} is the sequence
	# e.g., rg-saas-test-eastus2-01
	# All four placeholders must be present, but you can place them in any desired order
	deploySampleStorageAccount = $DeploySampleStorageAccount
	namingFormat               = $NamingConvention
	tags                       = @{
		'date-created' = (Get-Date -Format 'yyyy-MM-dd')
		purpose        = $Environment
		lifetime       = 'short'
	}
}

$DeploymentResult = New-AzDeployment -Location $Location -Name "saas-$Environment-$(Get-Date -Format 'yyyyMMddThhmmssZ' -AsUTC)" `
	-TemplateFile ".\main.bicep" -TemplateParameterObject $TemplateParameters

if ($DeploymentResult.ProvisioningState -eq 'Succeeded') {
	$DeploymentOutputs = $DeploymentResult.Outputs

	# Retrieve the deployment token from the Static Web App
	$Secret = Get-AzStaticWebAppSecret -ResourceGroupName $DeploymentOutputs.rgName.Value -Name $DeploymentOutputs.swaName.Value
	$Secret


	# Modify app registration to include redirect URIs, homepage
	if ($CreatedSPN) {
		[string]$CallBackPath = '.auth/login/aad/callback'
		[string]$SignoutCallBackPath = $CallBackPath.Replace('login', 'logout')
		[string]$WebAppHostName = $DeploymentOutputs.defaultHostName.Value

		# Set the app registration's redirect and logout URIs based on the host name
		Update-MgApplication -ApplicationId $NewMgApp.Id -Web @{
			RedirectUris          = "https://$WebAppHostName/$CallBackPath"
			LogoutUrl             = "https://$WebAppHostName/$SignoutCallBackPath"
			HomePageUrl           = "https://$WebAppHostName"
			ImplicitGrantSettings = @{
				EnableIdTokenIssuance = $true
			}
		}
	}
}
