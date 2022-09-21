// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React, { useState } from 'react'
import PropTypes from 'prop-types'
import Grid from '@mui/material/Grid'
import IconButton from "@mui/material/IconButton"
import './DirectoryDetails.css'
import DirectoriesTableMembers from '../DirectoriesTable/DirectoriesTableMembers'
import CopyIcon from '@mui/icons-material/ContentCopy';
import FilledCopyIcon from '@mui/icons-material/ContentCopyTwoTone';
import StorageExplorerIcon from '../../images/storage-explorer.svg'
import Tooltip from "@mui/material/Tooltip"

const DirectoryDetails = ({ data, strings }) => {

	// The state of the Copy command icon
	const [copied, setCopied] = useState(false)

	const format = value => {
		if (Array.isArray(value))
			return value.join(", ");
		return value ? value : '-';
	}

	const handleCopyUrl = (url) => {
		// Copy the URL to the clipboard
		navigator.clipboard.writeText(url)
		// Change the icon for three seconds to provide visual feedback
		setCopied(true)
		let timer = setInterval(() => { unsetCopied(timer) }, 3000)
	}

	const unsetCopied = (timer) => {
		setCopied(false)
		clearInterval(timer)
	}

	function getMembers() {
		return data.access.map( ac => {return ac.roleName + ": " + ac.principalName} );
	}

	return (
		<Grid container className='directoryDetails'>
			<Grid item xs={12} className='title'>
				<div className='label'>{strings.folderLabel}:</div>{data.name}
			</Grid>
			<Grid item sm={6} xs={12} className='detail'>
				<div className='label'>{strings.fundCodeLabel}:</div>{format(data.metadata.FundCode)}
			</Grid>
			<Grid item sm={6} xs={12} className='detail'>
				<div className='label'>{strings.lastModifiedLabel}:</div>{format(data.metadata.LastModified)}
			</Grid>
			<Grid item xs={12} className='detail'>
				<div className='label'>{strings.ownerLabel}:</div>{format(data.metadata.Owner)}
			</Grid>
			<Grid item xs={12} className='detail'>
				<div className='label'>{strings.userAccessLabel}:</div>
				<DirectoriesTableMembers  members={getMembers()}></DirectoriesTableMembers>
			</Grid>
			<Grid item xs={12} className='detail'>
				<div className='label'>{strings.uriLabel}:</div><span>{format(data.uri)}</span>
				<Tooltip arrow title={strings.copyToClipboardLabel} placement='top'>
					<IconButton aria-label={strings.copyToClipboardLabel} onClick={() => handleCopyUrl(data.uri)}
					color={copied ? 'success' : 'secondary'} size='small'>
						{copied ? <FilledCopyIcon /> : <CopyIcon />}
					</IconButton>
				</Tooltip>
				<Tooltip arrow title={strings.openInStorageExplorerLabel} placement='top'>
					<IconButton aria-label={strings.openInStorageExplorerLabel} size='large' onClick={() => { window.open(data.storageExplorerURI); return false }}>
						<img src={StorageExplorerIcon} title={strings.openInStorageExplorerLabel} alt={strings.openInStorageExplorerLabel} />
					</IconButton>
				</Tooltip>
			</Grid>
		</Grid>
	)
}

DirectoryDetails.propTypes = {
	data: PropTypes.object,
	strings: PropTypes.shape({
		accessTierLabel: PropTypes.string,
		costLabel: PropTypes.string,
		createdLabel: PropTypes.string,
		folderLabel: PropTypes.string,
		fundCodeLabel: PropTypes.string,
		ownerLabel: PropTypes.string,
		userAccessLabel: PropTypes.string,
		regionLabel: PropTypes.string,
		sizeLabel: PropTypes.string,
		totalFilesLabel: PropTypes.string,
		uriLabel: PropTypes.string,
		openInStorageExplorerLabel: PropTypes.string,
		copyToClipboardLabel: PropTypes.string
	})
}

DirectoryDetails.defaultProps = {
	data: {},
	strings: {
		accessTierLabel: 'Storage type',
		costLabel: 'Monthly cost',
		createdLabel: 'Created on',
		folderLabel: 'Folder',
		fundCodeLabel: 'Fund code',
		ownerLabel: 'Owner',
		userAccessLabel: 'User Access',
		regionLabel: 'Region',
		sizeLabel: 'Total size',
		totalFilesLabel: 'Total files',
		uriLabel: 'URL',
		openInStorageExplorerLabel: 'Open in Storage Explorer',
		copyToClipboardLabel: 'Copy to clipboard'
	}
}

export default DirectoryDetails
