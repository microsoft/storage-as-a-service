param location string
param storageAccountName string

resource storage 'Microsoft.Storage/storageAccounts@2021-08-01' = {
	name: storageAccountName
	location: location
	kind: 'StorageV2'
	sku: {
		name: 'Standard_LRS'
	}
	properties: {
		isHnsEnabled: true
		allowBlobPublicAccess: false
		allowSharedKeyAccess: false
		minimumTlsVersion: 'TLS1_2'
		supportsHttpsTrafficOnly: true
	}
}

output storageAccountName string = storage.name
