// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
import URLS from '../config/urls'
import HttpException from './HttpException'

/**
 * Returns any errors on the server
 */
 export const getServerStatus = async () => {
	const { endpoint, method } = URLS.serverStatus
	const options = getOptions(method)

	return fetch(endpoint, options)
		.then(response => {
			if (response.status === 200) {
				return response.json()
			} else {
				throw new HttpException(response.status, response.statusText)
			}
		})
		.catch(error => {
			console.log(`Call to API (${endpoint}) failed with the following details:`)
			console.log(error)
			throw error
		})
}


/**
 * Returns the list of storage accounts and their file systems (containers)
 */
 export const getStorageAccounts = async () => {
	const { endpoint, method } = URLS.storageAccounts
	const options = getOptions(method)

	return fetch(endpoint, options)
		.then(response => {
			if (response.status === 200) {
				var json = response.json();
				return json;
			} else {
				throw new HttpException(response.status, response.statusText)
			}
		})
		.catch(error => {
			console.log(`Call to API (${endpoint}) failed with the following details:`)
			console.log(error)
			throw error
		})
}


/**
 * Returns the list of file systems (containers) for a storage account
 */
export const getFileSystems = async (storageAccount) => {
	const endpoint = URLS.fileSystems.endpoint.replace('{account}', storageAccount)
	const options = getOptions(URLS.fileSystems.method)

	return fetch(endpoint, options)
		.then(response => {
			if (response.status === 200) {
				return response.json()
			} else {
				throw new HttpException(response.status, response.statusText)
			}
		})
		.catch(error => {
			console.log(`Call to API (${endpoint}) failed with the following details:`)
			console.log(error)
			throw error
		})
}

/**
 * Returns the list of directories
 */
export const getDirectories = async (storageAccount, fileSystem) => {
	const endpoint = URLS.listDirectories.endpoint.replace('{account}', storageAccount).replace('{filesystem}', fileSystem)
	const options = getOptions(URLS.listDirectories.method)

	return fetch(endpoint, options)
		.then(response => {
			if (response.status === 200) {
				return response.json()
			} else {
				throw new HttpException(response.status, response.statusText)
			}
		})
		.catch(error => {
			console.log(`Call to API (${endpoint}) failed with the following details:`)
			console.log(error)
			throw error
		})
}


/**
 * Create the options object to pass to the API call
 */
const getOptions = (method) => {
	const options = {
		method: method,
	}

	return options
}


/**
 * Create a new folder in the storage account container
 */
export const createFolder = async (storageAccount, fileSystem, owner, content) => {
	const endpoint = URLS.createFolder.endpoint.replace('{account}', storageAccount).replace('{filesystem}', fileSystem)
	const options = getOptions(URLS.createFolder.method)
	let userAccessList = content.userAccess ? content.userAccess.replace(" ", "").replace(";", ",").split(",") : ''

	options.body = JSON.stringify({
		Folder: content.name,
		FundCode: content.fundCode,
		FolderOwner: owner,
		UserAccessList: userAccessList
	})

	try {
		var response = await fetch(endpoint, options);
		let folderResponse = {
			Folder: "",
			Message: ""
		};

		let body = await response.json();

		// If the result code is success (should always be HTTP 201)
		if (response.status >= 200 && response.status <= 299)
			folderResponse.Folder = body.folderDetail

		// Regardless of success, there can always be a message
		folderResponse.Message = body.message ? body.message : body.Message

		return folderResponse
	}
	catch (error) {
		console.error(error);
	}
}
