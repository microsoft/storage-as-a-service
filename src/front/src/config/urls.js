// Add the endpoints here for Microsoft Graph API services you'd like to use.
const URLS = {
	storageAccounts: {
		method: 'GET',
		endpoint: '/api/StorageAccounts'
	},
	fileSystems: {
		method: 'GET',
		endpoint: '/api/StorageAccounts/{account}'
	},
	listDirectories: {
		method: 'GET',
		endpoint: '/api/TopLevelFolders/{account}/{filesystem}'
	},
	createFolder: {
		method: 'POST',
		endpoint: '/api/TopLevelFolders/{account}/{filesystem}'
	}
}

export default URLS