// Add the endpoints here for Microsoft Graph API services you'd like to use.
const URLS = {
    fileSystems: {
        method: 'GET',
        endpoint: '/api/filesystems'
    },
    listDirectories: {
        method: 'GET',
        endpoint: '/api/toplevelfolders/{account}/{filesystem}'
    },
    createFolder: {
        method: 'POST',
        endpoint: '/api/toplevelfolders/{account}/{filesystem}'
    }
}

export default URLS