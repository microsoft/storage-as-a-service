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

	return await fetch(endpoint, options)
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

	return await fetch(endpoint, options)
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
 * Delete a user role from the storage account container
 */
export const deleteRoleAssignment = async (storageaccount, container, guid) => {
	const endpoint = URLS.deleteRoleAssignment.endpoint.replace('{storageaccount}', storageaccount).replace('{container}', container).replace('{guid}', guid)
	const options = getOptions(URLS.deleteRoleAssignment.method)

	try {
		var response = await fetch(endpoint, options);
		let deleteResponse = {
			IsSucces: false,
			Message: ""
		};

		// If the result code is success (should always be HTTP 201)
		if (response.ok) {
			deleteResponse.IsSuccess = true
			deleteResponse.Message = "Deleted."
		} else {
			deleteResponse.Message = "Failed to delete."
		}

		return deleteResponse
	}
	catch (error) {
		console.error(error);
	}
}

/**
 * Create a role assigment for the storage account container
 */
export const createRoleAssignment = async (storageaccount, container, userObject) => {
	const endpoint = URLS.createRoleAssignment.endpoint.replace('{storageaccount}', storageaccount).replace('{container}', container)
	const options = getOptions(URLS.createRoleAssignment.method)

	options.body = JSON.stringify({
		identity: userObject.principalName,
		role: userObject.roleName
	})

	try {
		var response = await fetch(endpoint, options);
		let roleAssignmentResponse = {
			roleAssignment: {},
			Message: "",
			isSuccess: false
		};

		// If the result code is success (should always be HTTP 201)
		if (response.ok)
		{
			let body = await response.json();
			roleAssignmentResponse.roleAssignment = body
			roleAssignmentResponse.isSuccess = true
			roleAssignmentResponse.Message = "Role assignment successful"
		}
		else
		{
			roleAssignmentResponse.Message = response.statusText;
		}

		return roleAssignmentResponse
	}
	catch (error) {
		console.error(error);
	}
}
