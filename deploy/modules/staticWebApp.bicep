param location string
param swaName string
param azTenantId string
param azClientId string

param appInsightsKey string = ''
param sampleStorageAccountName string = ''

param kvSecretFileSystemsApiKeyUri string
param kvSecretConfigApiKeyUri string
param kvSecretAzureClientSecretUri string

// TODO: Support custom domain name
resource staticWebApp 'Microsoft.Web/staticSites@2021-03-01' = {
	name: swaName
	location: location
	sku: {
		name: 'Standard'
		tier: 'Standard'
	}
	identity: {
		type: 'SystemAssigned'
	}
	properties: {}
}

resource swaConfigAppSettings 'Microsoft.Web/staticSites/config@2021-03-01' = {
	name: 'appsettings'
	kind: 'string'
	parent: staticWebApp
	properties: {
		AZURE_TENANT_ID: azTenantId
		AZURE_CLIENT_ID: azClientId
		COST_PER_TB: '25'
		DATALAKE_STORAGE_ACCOUNTS: length(sampleStorageAccountName) > 0 ? sampleStorageAccountName : ''
		APPINSIGHTS_INSTRUMENTATIONKEY: length(appInsightsKey) > 0 ? appInsightsKey : ''
		AZURE_CLIENT_SECRET: '@Microsoft.KeyVault(SecretUri=${kvSecretAzureClientSecretUri})'
		CONFIGURATION_API_KEY: '@Microsoft.KeyVault(SecretUri=${kvSecretConfigApiKeyUri})'
		FILESYSTEMS_API_KEY: '@Microsoft.KeyVault(SecretUri=${kvSecretFileSystemsApiKeyUri})'
	}
}

output staticWebApp object = staticWebApp
output principalId string = staticWebApp.identity.principalId
output swaName string = staticWebApp.name
output defaultHostName string = staticWebApp.properties.defaultHostname
