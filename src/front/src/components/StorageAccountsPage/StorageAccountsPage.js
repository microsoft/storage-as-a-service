// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React, { useCallback, useEffect, useState } from 'react'
import PropTypes from 'prop-types'
import useAuthentication from '../../hooks/useAuthentication'
import { getFileSystems, getDirectories, createFolder } from '../../services/StorageManager.service'
import Alert from '@mui/material/Alert'
import Container from '@mui/material/Container'
import Grid from '@mui/material/Grid'
import Snackbar from '@mui/material/Snackbar'
import DirectoriesManager from '../DirectoriesManager'
import Selector from '../Selector'

/**
 * Renders list of Storage Accounts
 */
const StorageAccountsPage = ({ strings }) => {
	const { account, isAuthenticated } = useAuthentication()

	const [selectedStorageAccount, setSelectedStorageAccount] = useState('')
	const [selectedFileSystem, setSelectedFileSystem] = useState('')

	const [storageAccounts, setStorageAccounts] = useState([])
	const [directories, setDirectories] = useState([])

	const [toastMessage, setToastMessage] = useState()
	const [isToastOpen, setToastOpen] = useState(false)
	const [toastSeverity, setToastSeverity] = useState('success')

	// When authenticated, retrieve the list of File Systems for the selected Azure Data Lake Storage Account
	useEffect(() => {
		const retrieveAccountsAndFileSystems = async () => {
			try {
				const _storageAccounts = await getFileSystems()
				setStorageAccounts(_storageAccounts)

				if (_storageAccounts.length > 0) {
					// Set the first storage account retrieved from the API as the selected one
					setSelectedStorageAccount(_storageAccounts[0].name)
					displayToast(strings.accountsLoaded)
				}
				else {
					displayErrorToast(strings.noAccountsLoaded)
				}
			}
			catch (error) {
				console.error(error)
			}
		}

		isAuthenticated
			&& retrieveAccountsAndFileSystems()
	}, [isAuthenticated, strings.accountsLoaded, strings.noAccountsLoaded])


	// When the selected file system (container) changes, retrieve the list of Directories for the selected File System
	useEffect(() => {
		const clearDirectories = () => {
			setDirectories([])
		}

		const retrieveDirectories = async (storageAccount, fileSystem) => {
			try {
				const _directories = await getDirectories(storageAccount, fileSystem)
				setDirectories(_directories)
			}
			catch (error) {
				console.error(error)
			}
		}

		clearDirectories()

		// Only retrieve directories if there is a file system (container) selected
		selectedFileSystem
			&& retrieveDirectories(selectedStorageAccount, selectedFileSystem)
	}, [selectedFileSystem]) // eslint-disable-line react-hooks/exhaustive-deps
	// Disabling the rule that checks to make sure all dependent objects are included, because we don't want to trigger on change of selectedStorageAccount


	const displayToast = message => {
		setToastMessage(message)
		setToastSeverity('success')
		setToastOpen(true)
	}

	const displayErrorToast = message => {
		setToastMessage(message)
		setToastSeverity('error')
		setToastOpen(true)
	}

	const handleCreateDirectory = (data) => {
		// Calls the API to save the directory
		createFolder(selectedStorageAccount, selectedFileSystem, account.userDetails, data)
			.then((response) => {
				if (response.Folder) {
					// Match sort order to sort order from API (by URI)
					const _directories = [...directories, response.Folder].sort((a, b) => a.uri < b.uri ? -1 : a.uri > b.uri ? 1 : 0)
					setDirectories(_directories)

					let thisToastMessage = strings.directoryCreated(response.Folder.name)

					// If a message was provided in addition to the FolderDetail object
					if (response.Message)
						// Append it to the toast
						// TODO: Create a visual line break. <br /> gets displayed as <br />, not a line break
						thisToastMessage += '\n' + response.Message;

					// Display a toast
					displayToast(thisToastMessage)
				}
				else {
					// Log and display the error message
					console.error(response.Message);
					displayErrorToast(response.Message);
				}
			})
			.catch(error => {
				console.error(error)
				displayErrorToast(error)
			})
	}


	const handleStorageAccountChange = useCallback(id => {
		// To avoid a warning from MUI, clear the selected file system first
		setSelectedFileSystem('')
		setSelectedStorageAccount(id)
	}, [])


	const handleFileSystemChange = useCallback(id => {
		setSelectedFileSystem(id)
	}, [])


	const storageAccountItems = storageAccounts.map(account => account.name)
	const fileSystemItems = selectedStorageAccount ?
		storageAccounts.find(account => account.name === selectedStorageAccount).fileSystems
		: []


	return (
		<>
			<Container>
				<Grid container spacing={2} sx={{ justifyContent: 'center', marginBottom: '10px' }}>
					<Grid item md={6}>
						<Selector
							id='storageAccountSelector'
							items={storageAccountItems}
							label={strings.storageAccountLabel}
							onChange={handleStorageAccountChange}
							selectedItem={selectedStorageAccount}
						/>
					</Grid>
					<Grid item md={6}>
						<Selector
							id='fileSystemSelector'
							items={fileSystemItems}
							label={strings.fileSystemLabel}
							onChange={handleFileSystemChange}
							selectedItem={selectedFileSystem}
						/>
					</Grid>
					<Grid item>
						<DirectoriesManager
							data={directories}
							onCreateDirectory={handleCreateDirectory}
							strings={strings}
						/>
					</Grid>
				</Grid>
			</Container>

			<Snackbar
				open={isToastOpen}
				anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
				autoHideDuration={5000}
				onClose={() => setToastOpen(false)}
			>
				<Alert severity={toastSeverity}>{toastMessage}</Alert>
			</Snackbar>
		</>
	)
}

StorageAccountsPage.propTypes = {
	strings: PropTypes.shape({
		fileSystemLabel: PropTypes.string,
		storageAccountLabel: PropTypes.string
	})
}

StorageAccountsPage.defaultProps = {
	strings: {
		fileSystemLabel: 'File System',
		storageAccountLabel: 'Storage Account'
	}
}

export default StorageAccountsPage
