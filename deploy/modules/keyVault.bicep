param location string
param keyVaultName string
param isProduction bool

resource keyVault 'Microsoft.KeyVault/vaults@2021-10-01' = {
	name: keyVaultName
	location: location
	properties: {
		createMode: 'default'
		enabledForDeployment: false
		enabledForTemplateDeployment: false
		// This is not for production, allow immediate delete
		enableSoftDelete: isProduction
		enableRbacAuthorization: true
		sku: {
			name: 'standard'
			family: 'A'
		}
		tenantId: subscription().tenantId
	}
}
