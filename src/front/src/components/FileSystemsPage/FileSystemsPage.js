// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React, { useCallback, useEffect, useState } from 'react'
import PropTypes from 'prop-types'
import useAuthentication from '../../hooks/useAuthentication'
import { getStorageAccounts, getFileSystems } from '../../services/StorageManager.service'
import Alert from '@mui/material/Alert'
import Container from '@mui/material/Container'
import Grid from '@mui/material/Grid'
import Snackbar from '@mui/material/Snackbar'
import Selector from '../Selector'

import Table from 'react-bootstrap/Table'
import DetailsIcon from '@mui/icons-material/InfoTwoTone'
import EditIcon from '@mui/icons-material/EditOutlined'
import './FileSystemsPage.css'
import StorageExplorerIcon from '../../images/storage-explorer.svg'
import IconButton from "@mui/material/IconButton"
import Tooltip from '@mui/material/Tooltip'

import Button from '@mui/material/Button'
import CancelIcon from '@mui/icons-material/Close'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import DirectoryDetails from '../DirectoryDetails'
import ConnectDetails from '../ConnectDetails/ConnnectDetails'
import PageLoader from '../PageLoader/PageLoader'


/**
 * Renders list of Storage Accounts and FileSystems
 */
const FileSystemsPage = ({ strings }) => {

	// Setup authentication hooks
	const { isAuthenticated } = useAuthentication()

	// Setup state hooks
	const [details, setDetails] = useState({ show: false, data: {} })
	const [selectedStorageAccount, setSelectedStorageAccount] = useState('')
	const [storageAccounts, setStorageAccounts] = useState([])
	const [fileSystems, setFileSystems] = useState([])
	const [toastMessage, setToastMessage] = useState()
	const [loading, setLoading] = useState(false)
	const [isToastOpen, setToastOpen] = useState(false)
	const [toastSeverity, setToastSeverity] = useState('success')

	// When authenticated, retrieve the list of File Systems for the selected Azure Data Lake Storage Account
	useEffect(() => {
		const retrieveAccountsAndFileSystems = async () => {
			try {
				setLoading(true);
				let _storageAccounts = await getStorageAccounts()
				_storageAccounts = _storageAccounts.map((a) => ({...a, concatenatedName : `${a.friendlyName} (${a.storageAccountName})`}));
				setStorageAccounts(_storageAccounts)

				if (_storageAccounts.length > 0) {
					// Set the first storage account retrieved from the API as the selected one
					setSelectedStorageAccount(_storageAccounts[0].storageAccountName)
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

	// When the selected storage account changes, retrieve the list of File Sysetms / Filesystems
	useEffect(() => {
		const clearFileSystems = () => {
			setFileSystems([])
		}

		const populateFileSystems = async (storageAccount) => {
			try {
				setLoading(true);
				const _fileSystems = await getFileSystems(storageAccount)
				setFileSystems(_fileSystems)
				setLoading(false);

				if (_fileSystems.length > 0) {
					displayToast(strings.containersLoaded)
				}
				else {
					displayErrorToast(strings.noContainersLoaded)
				}
			}
			catch (error) {
				console.error(error)
			}
		}

		clearFileSystems()

		// Only retrieve directories if there is a file system (container) selected
		selectedStorageAccount
			&& populateFileSystems(selectedStorageAccount)
	}, [selectedStorageAccount, strings.containersLoaded, strings.noContainersLoaded]) // eslint-disable-line react-hooks/exhaustive-deps

	// Setup Toast messages
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

	const handleStorageAccountChange = useCallback(id => {
		setSelectedStorageAccount(id)
	}, [])

	const onEdit = async () => {
		// test
	}

	const onDetails = (rowData) => {
		setDetails({ show: true, data: rowData })
	}

	const handleCancelDetails = () => {
		setDetails({ show: false, data: {} })
	}

	let rowCount = 0;

	// Emit HTML
	return (
		<>

			<Container>
			<PageLoader state={loading} />
				<Grid container spacing={2} sx={{ justifyContent: 'center', marginBottom: '10px' }}>
					<Grid item md={6}>
						<Selector
							id='storageAccountSelector'
							items={storageAccounts}
							label={strings.storageAccountLabel}
							onChange={handleStorageAccountChange}
							selectedItem={selectedStorageAccount}
							textProperty={'concatenatedName'}
							valueProperty={'storageAccountName'}
						/>
					</Grid>
					<Grid item>
						<Table striped bordered hover className='directoriesTable'>
							<thead>
								<tr>
									<th>
										{strings.fileSystemsPage.containerLabel}
									</th>
									<th>
										Owners
									</th>
									<th>
										Contributors
									</th>
									<th>
										Readers
									</th>
									<th>
										{strings.fileSystemsPage.fundCodeLabel}
									</th>
									<th>
										{strings.fileSystemsPage.actionsLabel}
									</th>
								</tr>
							</thead>
							<tbody>
								{fileSystems.map(row => {
									return (
										<tr key={row.name}>
											<td className='name'>
												{row.name}
											</td>
											<td className='owner'>
												<table><tbody>
													{row.access.filter(ac => { return ac.roleName === 'Owner' })
																.map(ac => { return <tr key={rowCount++}><td>{ac.principalName}</td></tr> })}
												</tbody></table>
											</td>
											<td className='owner'>
												<table><tbody>
													{row.access.filter(ac => { return ac.roleName === 'Contributor' })
																.map(ac => { return <tr key={rowCount++}><td>{ac.principalName}</td></tr> })}
												</tbody></table>
											</td>
											<td className='owner'>
												<table><tbody>
													{row.access.filter(ac => { return ac.roleName === 'Reader' })
																.map(ac => { return <tr key={rowCount++}><td>{ac.principalName}</td></tr> })}
												</tbody></table>
											</td>
											<td className='fundcode'>
												{row.metadata.FundCode}
											</td>
											<td className='actions'>
												{onEdit && <EditIcon onClick={() => onEdit(row)} className='action' />}
												{onDetails &&
													<Tooltip arrow title="Open details" placement='top'>
														<DetailsIcon onClick={() => onDetails(row)} className='action' />
													</Tooltip>
												}
												<Tooltip arrow title={strings.fileSystemsPage.openInStorageExplorerLabel} placement='top'>
													<IconButton aria-label={strings.fileSystemsPage.openInStorageExplorerLabel} size='small'
																onClick={() => { window.open(row.storageExplorerDirectLink); return false }}>
														<img src={StorageExplorerIcon} title={strings.fileSystemsPage.openInStorageExplorerLabel}
															 alt={strings.fileSystemsPage.openInStorageExplorerLabel} />
													</IconButton>
												</Tooltip>
											</td>
										</tr>
									)
								})}
							</tbody>
						</Table>
					</Grid>
				</Grid>
			</Container>
			{details.show &&
				<Dialog onClose={handleCancelDetails} open={details.show} maxWidth='lg'>
					<DialogTitle>
						{strings.directoryDetailsTitle}
					</DialogTitle>
					<DialogContent>
						<DirectoryDetails data={details.data} strings={strings.directoryDetails} />
						<ConnectDetails uri={details.data.uri} storageExplorerURI={details.data.storageExplorerDirectLink} strings={strings.directoryDetails} />
					</DialogContent>
					<DialogActions>
						<Button variant='outlined' startIcon={<CancelIcon />} onClick={handleCancelDetails}>{strings.close}</Button>
					</DialogActions>
				</Dialog>
			}
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

FileSystemsPage.propTypes = {
	strings: PropTypes.shape({
		fileSystemLabel: PropTypes.string,
		storageAccountLabel: PropTypes.string
	})
}

FileSystemsPage.defaultProps = {
	strings: {
		storageAccountLabel: 'Storage Account'
	}
}

export default FileSystemsPage

/*
[
 {
  "containerName": "containerA",
  "storageExplorerDirectLink": "storageexplorer://?v=2&...",
  "metaData": [
   {
	"key": "metadata key 1",
	"value": "metadata value 1"
   },
   {
	"key": "metadata key 1",
	"value": "metadata value 1"
   }
  ],
  "access": [
  {
   "roleName": "Reader",
   "principalName": "UPN or group name",
   "principalId": "guid"
  },
  {
   "roleName": "Contributor",
   "principalName": "UPN or group name",
   "principalId": "guid"
  },
  {
   "roleName": "Owner",
   "principalName": "UPN or group name",
   "principalId": "guid"
  }
  ]
 },
 {
  "containerName": "containerB",
  ...
 }
]



*/