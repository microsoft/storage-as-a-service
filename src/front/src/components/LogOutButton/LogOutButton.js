// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from 'react'
import PropTypes from 'prop-types'
import Button from '@mui/material/Button'

/**
 * Renders a button which, when selected, will open a popup for logout
 */
const LogOutButton = ({ strings }) => {
    return (<Button variant='text' href='/.auth/logout'>{strings.logOut}</Button>)
}

LogOutButton.propTypes = {
    strings: PropTypes.shape({
        logOut: PropTypes.string
    })
}

LogOutButton.defaultProps = {
    strings: {
        logOut: 'Log out'
    }
}

export default LogOutButton
