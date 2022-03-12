param keyVaultName string
param swaPrincipalId string
param appRegPrincipalId string
param roles object
param storageAccountName string

var hasStorageAccount = length(storageAccountName) > 0

// Verify the existence of Azure resources and use their reference
resource keyVault 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing = {
	name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' existing = if (hasStorageAccount) {
	name: storageAccountName
}

// Assign SWA system assigned managed identity access to read secrets from Key Vault
resource keyVaultRbac 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
	name: guid('KeyVault', swaPrincipalId)
	scope: keyVault
	properties: {
		principalId: swaPrincipalId
		roleDefinitionId: roles['Key Vault Secrets User']
	}
}

// Assign app registration access to manage all storage blobs on the sample storage account
resource storageAccountSbdoRbac 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = if (hasStorageAccount) {
	name: guid('Storage', swaPrincipalId, 'SBDO')
	scope: storageAccount
	properties: {
		principalId: appRegPrincipalId
		roleDefinitionId: roles['Storage Blob Data Owner']
	}
}

// Assign app registration access to manage RBAC on the sample storage account
resource storageAccountUaaRbac 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = if (hasStorageAccount) {
	name: guid('Storage', swaPrincipalId, 'UAA')
	scope: storageAccount
	properties: {
		principalId: appRegPrincipalId
		roleDefinitionId: roles['User Access Administrator']
	}
}
