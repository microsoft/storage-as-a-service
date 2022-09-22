// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Add the endpoints here for Microsoft Graph API services you'd like to use.
const URLS = {
	serverStatus: {
		method: 'GET',
		endpoint: '/api/ServerStatus'
	},
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
	},
	createRoleAssignment: {
		method: 'POST',
		endpoint: '/api/FileSystems/{storageaccount}/{container}/authorization'
	},
	deleteRoleAssignment: {
		method: 'DELETE',
		endpoint: '/api/FileSystems/{storageaccount}/{container}/authorization/{guid}'
	}
}

export default URLS