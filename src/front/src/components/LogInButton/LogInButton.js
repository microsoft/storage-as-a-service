// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from 'react'
import PropTypes from 'prop-types'
import Button from '@mui/material/Button'
import Person from '@mui/icons-material/PersonTwoTone'

/**
 * Renders a button which, when selected, will open a popup for login
 */
const LogInButton = ({ strings }) => {
    return (<Button variant='outlined' startIcon={<Person />} href="/.auth/login/aad">{strings.logIn}</Button>)
}

LogInButton.propTypes = {
    strings: PropTypes.shape({
        logIn: PropTypes.string
    })
}

LogInButton.defaultProps = {
    strings: {
        logIn: 'Log In'
    }
}

export default LogInButton
