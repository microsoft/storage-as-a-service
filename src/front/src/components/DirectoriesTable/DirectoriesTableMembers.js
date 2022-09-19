// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React, { useState } from 'react'
import PropTypes from 'prop-types'
import Chip from '@mui/material/Chip'
import Popover from '@mui/material/Popover'

const DirectoriesTableMembers = ({ members, strings }) => {
    const [anchorEl, setAnchorEl] = useState(null)

    const open = Boolean(anchorEl)

    const handlePopoverClose = () => {
        setAnchorEl(null)
    }

    const handlePopoverOpen = event => {
        setAnchorEl(event.currentTarget)
    }

	let keyCount = 0;

    if (members.length > 10) {
        return (
            <>
                <div onMouseEnter={handlePopoverOpen} onMouseLeave={handlePopoverClose}>
                    <Chip className='member-chip' label={strings.members(members.length)} />
                </div>
                <Popover
                    anchorEl={anchorEl}
                    anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
                    disableRestoreFocus
                    onClose={handlePopoverClose}
                    open={open}
                    sx={{ pointerEvents: 'none' }}
                    transformOrigin={{
                        vertical: 'top',
                        horizontal: 'left',
                    }}
                >
                    <div className='members-popover'>
                        {members.map(item => (<div key={keyCount++} className='member-text'>{item}</div>))}
                    </div>
                </Popover>
            </>
        )
    } else {
        return members.map(item => (<Chip key={keyCount++} className='member-chip' label={`${item}`} />))
    }
}

DirectoriesTableMembers.propTypes = {
    members: PropTypes.array,
    strings: PropTypes.shape({
        members: PropTypes.func,
    })
}

DirectoriesTableMembers.defaultProps = {
    members: [],
    strings: {
        members: count => `${count} members`,
    }
}

export default DirectoriesTableMembers