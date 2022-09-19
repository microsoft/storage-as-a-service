// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from 'react'
import PropTypes from 'prop-types'
import Button from '@mui/material/Button'
import CancelIcon from '@mui/icons-material/Close'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import DirectoryDetails from '../DirectoryDetails'
import ConnectDetails from '../ConnectDetails/ConnnectDetails'

const DirectoryDetailsModal = ({ data, onCancel, open, strings }) => {

	const handleClose = () => {
		onCancel && onCancel()
	}

	return (
		<Dialog onClose={handleClose} open={open} maxWidth='lg'>
			<DialogTitle>
				{strings.directoryDetailsTitle}
			</DialogTitle>
			<DialogContent>
				<DirectoryDetails data={data} strings={strings} />
				<ConnectDetails data={data} strings={strings} />
			</DialogContent>
			<DialogActions>
				<Button variant='outlined' startIcon={<CancelIcon />} onClick={handleClose}>{strings.close}</Button>
			</DialogActions>
		</Dialog>
	)
}

DirectoryDetailsModal.propTypes = {
	data: PropTypes.object,
	onCancel: PropTypes.func,
	open: PropTypes.bool,
	strings: PropTypes.shape({
		close: PropTypes.string
	})
}

DirectoryDetailsModal.defaultProps = {
	data: {},
	open: false,
	strings: {
		close: 'Close'
	}
}

export default DirectoryDetailsModal
