// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React, { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Grid from '@mui/material/Grid'
import TextField from '@mui/material/TextField'
import CancelIcon from '@mui/icons-material/Close'
import SaveIcon from '@mui/icons-material/SaveOutlined'
import './DirectoryEditorModal.css'

const DirectoryEditorModal = ({ data, onCancel, onCreate, open, strings }) => {
    const [formData, setFormData] = useState({})

    // Set the default form values
    useEffect(() => {
        setFormData({
            name: data.name ? data.name : '',
            fundCode: data.fundCode ? data.fundCode : '',
			userAccess: data.userAccess ? data.userAccess : ''
        })

    }, [data])


    const handleCreateClick = () => {
        submitForm()
    }


    const handleClose = () => {
        onCancel && onCancel()
    }


    const handleInputChange = event => {
        updateState(event.target.name, event.target.value)
    }

    const handleEnterInFundCode = event => {
        if (event.key === 'Enter') {
            submitForm()
        }
    }


    const submitForm = () => {
        onCreate && onCreate(formData)
    }


    const updateState = (id, value) => {
        setFormData({
            ...formData,
            [id]: value
        })
    }


    return (
        <Dialog onClose={handleClose} open={open} >
            <DialogTitle>{strings.title}</DialogTitle>

            <DialogContent>
                <Grid container spacing={2}>
                    <Grid item xs={12}>
                        <TextField
                            autoFocus
                            id='name'
                            name='name'
                            label={strings.directoryNameLabel}
                            fullWidth
                            variant='standard'
                            defaultValue={data.name}
                            onChange={handleInputChange}
                        />
                    </Grid>
                    <Grid item xs={12}>
                        <TextField
                            id='fundCode'
                            name='fundCode'
                            label={strings.fundCodeLabel}
                            fullWidth
                            variant='standard'
                            defaultValue={data.fundCode}
                            onChange={handleInputChange}
                            onKeyPress={handleEnterInFundCode}
                        />
                    </Grid>
					<Grid item xs={12}>
                        <TextField
                            id='userAccess'
                            name='userAccess'
                            label={strings.userAccessLabel}
                            fullWidth
                            variant='standard'
                            defaultValue={data.userAccess}
                            onChange={handleInputChange}

                        />
                    </Grid>
                </Grid>
            </DialogContent>

            <DialogActions>
                <Button variant='outlined' startIcon={<CancelIcon />} onClick={handleClose}>{strings.cancel}</Button>
                <Button variant='contained' startIcon={<SaveIcon />} onClick={handleCreateClick}>{strings.save}</Button>
            </DialogActions>
        </Dialog>
    )
}

DirectoryEditorModal.propTypes = {
    data: PropTypes.object,
    onCancel: PropTypes.func,
    onCreate: PropTypes.func,
    onUpdate: PropTypes.func,
    open: PropTypes.bool,
    strings: PropTypes.shape({
        cancel: PropTypes.string,
        fundCodeLabel: PropTypes.string,
        directoryNameLabel: PropTypes.string,
        save: PropTypes.string,
        title: PropTypes.string,
    }),
}

DirectoryEditorModal.defaultProps = {
    data: {},
    open: false,
    strings: {
        cancel: 'Cancel',
        fundCodeLabel: 'Fund code',
        directoryNameLabel: 'Folder name',
        save: 'Save',
        title: 'Creating a new folder',
    },
}

export default DirectoryEditorModal
