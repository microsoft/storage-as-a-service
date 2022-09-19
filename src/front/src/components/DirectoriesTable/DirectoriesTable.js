// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from 'react'
import PropTypes from 'prop-types'
import Table from 'react-bootstrap/Table'
import DetailsIcon from '@mui/icons-material/InfoTwoTone'
import EditIcon from '@mui/icons-material/EditOutlined'
import DirectoriesTableMembers from './DirectoriesTableMembers'
import './DirectoriesTable.css'
import StorageExplorerIcon from '../../images/storage-explorer.svg'
import IconButton from "@mui/material/IconButton"
import Tooltip from '@mui/material/Tooltip'

export const DirectoriesTable = ({ data, onDetails, onEdit, strings }) => {
	return (
		<Table striped bordered hover className='directoriesTable'>
			<thead>
				<tr>
					<th>
						{strings.folderLabel}
					</th>
					<th>
						{strings.spaceUsedLabel}
					</th>
					<th>
						{strings.monthlyCostLabel}
					</th>
					<th>
						{strings.whoHasAccessLabel}
					</th>
					<th>
						{strings.fundCodeLabel}
					</th>
					<th>
						{strings.actionsLabel}
					</th>
				</tr>
			</thead>
			<tbody>
				{data.map(row => {
					return (
						<tr key={row.name}>
							<td className='name'>
								{row.name}
							</td>
							<td className='spaceused'>
								{row.size}
							</td>
							<td className='costs'>
								{row.cost}
							</td>
							<td className='owner'>
								<DirectoriesTableMembers members={row.userAccess} strings={strings} />
							</td>
							<td className='fundcode'>
								{row.fundCode}
							</td>
							<td className='actions'>
								{onEdit && <EditIcon onClick={() => onEdit(row)} className='action' />}
								{onDetails && <Tooltip arrow title="Open details" placement='top'><DetailsIcon onClick={() => onDetails(row)} className='action' /></Tooltip>}
								<Tooltip arrow title={strings.openInStorageExplorerLabel} placement='top'>
									<IconButton aria-label={strings.openInStorageExplorerLabel} size='small' onClick={() => { window.open(row.storageExplorerURI); return false }}>
										<img src={StorageExplorerIcon} title={strings.openInStorageExplorerLabel} alt={strings.openInStorageExplorerLabel} />
									</IconButton>
								</Tooltip>
							</td>
						</tr>
					)
				})}
			</tbody>
		</Table>
	)
}

DirectoriesTable.propTypes = {
	data: PropTypes.array,
	onDetails: PropTypes.func,
	onEdit: PropTypes.func,
	strings: PropTypes.shape({
		actionsLabel: PropTypes.string,
		folderLabel: PropTypes.string,
		fundCodeLabel: PropTypes.string,
		monthlyCostLabel: PropTypes.string,
		spaceUsedLabel: PropTypes.string,
		whoHasAccessLabel: PropTypes.string,
		openInStorageExplorerLabel: PropTypes.string
	})
}

DirectoriesTable.defaultProps = {
	data: [],
	strings: {
		actionsLabel: 'Actions',
		folderLabel: 'Folder',
		fundCodeLabel: 'Fund Code',
		monthlyCostLabel: 'Monthly Cost',
		spaceUsedLabel: 'Space Used',
		whoHasAccessLabel: 'Who Has Access?',
		openInStorageExplorerLabel: 'Open in Storage Explorer'
	}
}

export default DirectoriesTable
