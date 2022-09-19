// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React, { useState } from 'react'
import PropTypes from 'prop-types'
import Button from '@mui/material/Button'
import AddIcon from '@mui/icons-material/AddOutlined'
import DirectoriesTable from '../DirectoriesTable'
import DirectoryEditorModal from '../DirectoryEditorModal'
import DirectoryDetailsModal from '../DirectoryDetailsModal'
import './DirectoriesManager.css'

const DirectoriesManager = ({ data, onCreateDirectory, strings }) => {
    const [editor, setEditor] = useState({ show: false, data: {} })
    const [details, setDetails] = useState({ show: false, data: {} })

    const handleAdd = () => {
        setEditor({ show: true, data: {} })
    }

    const handleCancelDetails = () => {
        setDetails({ show: false, data: {} })
    }

    const handleCancelEdit = () => {
        setEditor({ show: false, data: {} })
    }

    const handleCreateDirectory = (data) => {
        onCreateDirectory && onCreateDirectory(data)

        // Hide the editor modal
        setEditor({ show: false, data: {} })
    }

    const handleDetails = (rowData) => {
        setDetails({ show: true, data: rowData })
    }

    return (
        <div className='directoriesManager'>
            <div className='actionsBar'>
                <Button variant='contained' startIcon={<AddIcon />} onClick={handleAdd}>
                    {strings.newFolder}
                </Button>
            </div>

            <DirectoriesTable
                data={data}
                onAdd={handleAdd}
                onDetails={handleDetails}
                strings={strings.directoriesTable}
                />

            {editor.show &&
                <DirectoryEditorModal
                    data={editor.data}
                    onCancel={handleCancelEdit}
                    onCreate={handleCreateDirectory}
                    open={editor.show}
                    strings={strings.directoryEditor}
                />
            }

            {details.show &&
                <DirectoryDetailsModal
                    data={details.data}
                    onCancel={handleCancelDetails}
                    open={details.show}
                    strings={strings.directoryDetails}
                />
            }
        </div>
    )
}


DirectoriesManager.propTypes = {
    data: PropTypes.array,
    storageAccount: PropTypes.string,
    fileSystem: PropTypes.string,
    strings: PropTypes.object
}


DirectoriesManager.defaultProps = {
    data: [],
    strings: {}
}

export default DirectoriesManager
