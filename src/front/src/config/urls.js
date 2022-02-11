// Add the endpoints here for Microsoft Graph API services you'd like to use.
const URLS = {
	fileSystems: {
		method: 'GET',
		endpoint: '/api/FileSystems'
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