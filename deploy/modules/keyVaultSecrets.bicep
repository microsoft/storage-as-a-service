param kvSecretFileSystemsApiKeyName string
param kvSecretConfigApiKeyName string
param kvSecretAzureClientSecretName string

param keyVaultName string

@secure()
param kvSecretFileSystemsApiKeyValue string
@secure()
param kvSecretConfigApiKeyValue string
@secure()
param kvSecretAzureClientSecretValue string

var setAzClientSecretValue = length(kvSecretAzureClientSecretValue) > 0

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
	name: keyVaultName
}

// At this time, the app registration client secret is only provided during the first deployment with a new app registration
resource azureClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = if (setAzClientSecretValue) {
	name: '${keyVaultName}/${kvSecretAzureClientSecretName}'
	properties: {
		contentType: 'The AAD application registration\'s client secret.'
		value: kvSecretAzureClientSecretValue
	}
}

resource configApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
	name: '${keyVaultName}/${kvSecretConfigApiKeyName}'
	properties: {
		contentType: 'The shared API key for the Configuration POST endpoint.'
		value: kvSecretConfigApiKeyValue
	}
}

resource fileSystemsApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
	name: '${keyVaultName}/${kvSecretFileSystemsApiKeyName}'
	properties: {
		contentType: 'The shared API key for the FileSystems POST endpoint.'
		value: kvSecretFileSystemsApiKeyValue
	}
}

output azureClientSecretSecretUri string = setAzClientSecretValue ? azureClientSecretSecret.properties.secretUri : ''
output configApiKeySecretUri string = configApiKeySecret.properties.secretUri
output fileSystemsApiKeySecretUri string = fileSystemsApiKeySecret.properties.secretUri
