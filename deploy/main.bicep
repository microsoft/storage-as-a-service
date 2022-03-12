targetScope = 'subscription'

@allowed([
	'eastus2'
	'centralus'
	'westeurope'
	'eastasia'
	'westus2'
])
param location string
@allowed([
	'test'
	'demo'
	'prod'
])
param environment string
param azTenantId string
@secure()
param azClientSecret string
param azClientId string
param appRegPrincipalId string
@secure()
param fileSystemsApiKey string
@secure()
param configApiKey string

param azClientSecetExpiration string

// Optional parameters
param tags object = {}
param sequence int = 1
param namingFormat string = '{0}-saas-{1}-{2}-{3}'
param deploySampleStorageAccount bool = false
param deploymentTime string = utcNow()
param deploymentNamePrefix string = 'saas-'

var sequenceFormatted = format('{0:00}', sequence)
var baseName = format(namingFormat, '{0}', environment, location, sequenceFormatted)
var isProduction = (environment != 'demo' && environment != 'test')

var rgName = format(baseName, 'rg')
var keyVaultName = replace(format(baseName, 'kv'), '_', '-')

// Key Vault secret names
var kvSecretFileSystemsApiKeyName = 'Saas-FileSystems-Api-Key'
var kvSecretConfigApiKeyName = 'Saas-Config-Api-Key'
var kvSecretAzureClientSecretName = 'Saas-Azure-Client-Secret'

////////////////////////////////////////////////////////////////////////////////
// DEPLOYMENTS
////////////////////////////////////////////////////////////////////////////////

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
	name: rgName
	location: location
	tags: tags
}

module keyVault 'modules/keyVault.bicep' = {
	name: '${deploymentNamePrefix}kv-${deploymentTime}'
	scope: resourceGroup
	params: {
		location: location
		keyVaultName: keyVaultName
		isProduction: isProduction
	}
}

module logAnalytics 'modules/logAnalytics.bicep' = {
	name: '${deploymentNamePrefix}log-${deploymentTime}'
	scope: resourceGroup
	params: {
		location: location
		logName: replace(format(baseName, 'log'), '_', '-')
	}
}

module appInsights 'modules/appInsights.bicep' = {
	name: '${deploymentNamePrefix}appi-${deploymentTime}'
	scope: resourceGroup
	dependsOn: [
		logAnalytics
	]
	params: {
		location: location
		appInsightsName: format(baseName, 'appi')
		logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
	}
}

module sampleStorageAccount 'modules/storage.bicep' = if (deploySampleStorageAccount) {
	name: '${deploymentNamePrefix}storage-${deploymentTime}'
	scope: resourceGroup
	params: {
		location: location
		storageAccountName: toLower(format(replace(replace(baseName, '-', ''), '_', ''), 'st'))
	}
}

module keyVaultSecrets 'modules/keyVaultSecrets.bicep' = {
	name: '${deploymentNamePrefix}kvSecrets-${deploymentTime}'
	scope: resourceGroup
	dependsOn: [
		keyVault
	]
	params: {
		keyVaultName: keyVaultName
		kvSecretFileSystemsApiKeyName: kvSecretFileSystemsApiKeyName
		kvSecretConfigApiKeyName: kvSecretConfigApiKeyName
		kvSecretAzureClientSecretName: kvSecretAzureClientSecretName
		kvSecretAzureClientSecretValue: azClientSecret
		kvSecretConfigApiKeyValue: configApiKey
		kvSecretFileSystemsApiKeyValue: fileSystemsApiKey
	}
}

module staticWebApp 'modules/staticWebApp.bicep' = {
	name: '${deploymentNamePrefix}stapp-${deploymentTime}'
	scope: resourceGroup
	dependsOn: [
		keyVault
		keyVaultSecrets
		appInsights
	]
	params: {
		location: location
		swaName: format(baseName, 'stapp')
		azTenantId: azTenantId
		azClientId: azClientId
		sampleStorageAccountName: deploySampleStorageAccount ? sampleStorageAccount.outputs.storageAccountName : ''
		appInsightsKey: appInsights.outputs.instrumentationKey
		// The URIs of the Key Vault secrets will be used in the SWA Application Settings
		kvSecretAzureClientSecretUri: keyVaultSecrets.outputs.azureClientSecretSecretUri
		kvSecretConfigApiKeyUri: keyVaultSecrets.outputs.configApiKeySecretUri
		kvSecretFileSystemsApiKeyUri: keyVaultSecrets.outputs.fileSystemsApiKeySecretUri
	}
}

// The necessary RBAC roles are defined in this central file
// Read it so the output (an object) can be passed along to future modules
module rolesModule 'modules/roles.bicep' = {
	name: '${deploymentNamePrefix}GetRoles-${deploymentTime}'
	scope: resourceGroup
}

module roleAssignments 'modules/roleAssignments.bicep' = {
	name: '${deploymentNamePrefix}AssignRoles-${deploymentTime}'
	scope: resourceGroup
	params: {
		swaPrincipalId: staticWebApp.outputs.principalId
		appRegPrincipalId: appRegPrincipalId
		keyVaultName: keyVaultName
		storageAccountName: deploySampleStorageAccount ? sampleStorageAccount.outputs.storageAccountName : ''
		roles: rolesModule.outputs.roles
	}
}

// TODO: Deploy Logic App for calling the CalculateFolderSize API

output rgName string = rgName
output isProduction bool = isProduction
output staticWebApp object = staticWebApp.outputs.staticWebApp
output swaName string = staticWebApp.outputs.swaName
output defaultHostName string = staticWebApp.outputs.defaultHostName
