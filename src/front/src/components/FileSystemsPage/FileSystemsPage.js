import React, { useCallback, useEffect, useState } from 'react'
import PropTypes from 'prop-types'
import useAuthentication from '../../hooks/useAuthentication'
import { getStorageAccounts, getFileSystems } from '../../services/StorageManager.service'
import Alert from '@mui/material/Alert'
import Container from '@mui/material/Container'
import Grid from '@mui/material/Grid'
import Snackbar from '@mui/material/Snackbar'
import DirectoriesManager from '../DirectoriesManager'
import Selector from '../Selector'

import Table from 'react-bootstrap/Table'
import DetailsIcon from '@mui/icons-material/InfoTwoTone'
import EditIcon from '@mui/icons-material/EditOutlined'
import DirectoriesTableMembers from '../DirectoriesTable/DirectoriesTableMembers'
import '../DirectoriesTable/DirectoriesTable.css'
import StorageExplorerIcon from '../../images/storage-explorer.svg'
import IconButton from "@mui/material/IconButton"
import Tooltip from '@mui/material/Tooltip'



/**
 * Renders list of Storage Accounts and FileSystems
 */
const FileSystemsPage = ({ strings }) => {

	// Setup authentication hooks
	const { account, isAuthenticated } = useAuthentication()

	// Setup state hooks
	const [selectedStorageAccount, setSelectedStorageAccount] = useState('')
	const [storageAccounts, setStorageAccounts] = useState([])
	const [fileSystems, setFileSystems] = useState([])
	const [toastMessage, setToastMessage] = useState()
	const [isToastOpen, setToastOpen] = useState(false)
	const [toastSeverity, setToastSeverity] = useState('success')

	// When authenticated, retrieve the list of File Systems for the selected Azure Data Lake Storage Account
	useEffect(() => {
		const retrieveAccountsAndFileSystems = async () => {
			try {
				const _storageAccounts = await getStorageAccounts()
				setStorageAccounts(_storageAccounts)

				if (_storageAccounts.length > 0) {
					// Set the first storage account retrieved from the API as the selected one
					setSelectedStorageAccount(_storageAccounts[0])
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
				const _fileSystems = await getFileSystems(storageAccount)
				setFileSystems(_fileSystems)
			}
			catch (error) {
				console.error(error)
			}
		}

		clearFileSystems()

		// Only retrieve directories if there is a file system (container) selected
		selectedStorageAccount
			&& populateFileSystems(selectedStorageAccount)
	}, [selectedStorageAccount]) // eslint-disable-line react-hooks/exhaustive-deps

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

	//const storageAccountItems = storageAccounts.map(account => account.name)
	/*
	const fileSystemItems = fileSystems ?
		storageAccounts.find(account => account.name === selectedStorageAccount)
					   .fileSystems
		: []
	*/

	const onEdit = async () => {
		// test
	}
	const onDetails = async () => {
		// test
	}


	// Emit HTML
	return (
		<>
			<Container>
				<Grid container spacing={2} sx={{ justifyContent: 'center', marginBottom: '10px' }}>
					<Grid item md={6}>
						<Selector
							id='storageAccountSelector'
							items={storageAccounts}
							label={strings.storageAccountLabel}
							onChange={handleStorageAccountChange}
							selectedItem={selectedStorageAccount}
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
										{strings.fileSystemsPage.spaceUsedLabel}
									</th>
									<th>
										{strings.fileSystemsPage.monthlyCostLabel}
									</th>
									<th>
										{strings.fileSystemsPage.whoHasAccessLabel}
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
											<td className='spaceused'>
												{row.metadata.Size}
											</td>
											<td className='costs'>
												{row.metadata.Cost}
											</td>
											<td className='owner'>
												<table>
												{row.access.map(ac => {return (<tr><td>{ac.roleName}</td><td>{ac.principalName}</td></tr>) })}
												</table>
											</td>
											<td className='fundcode'>
												{row.metadata.FundCode}
											</td>
											<td className='actions'>
												{onEdit && <EditIcon onClick={() => onEdit(row)} className='action' />}
												{onDetails && <Tooltip arrow title="Open details" placement='top'><DetailsIcon onClick={() => onDetails(row)} className='action' /></Tooltip>}
												<Tooltip arrow title={strings.fileSystemsPage.openInStorageExplorerLabel} placement='top'>
													<IconButton aria-label={strings.fileSystemsPage.openInStorageExplorerLabel} size='small' onClick={() => { window.open(row.storageExplorerDirectLink); return false }}>
														<img src={StorageExplorerIcon} title={strings.fileSystemsPage.openInStorageExplorerLabel} alt={strings.fileSystemsPage.openInStorageExplorerLabel} />
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