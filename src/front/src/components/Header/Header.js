// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from 'react'
import PropTypes from 'prop-types'
import useAuthentication from '../../hooks/useAuthentication'
import Avatar from '@mui/material/Avatar'
import LogOutButton from '../LogOutButton'
import './Header.css'

const Header = ({ strings }) => {
    const { account, isAuthenticated } = useAuthentication()

    const logo = strings.logoImage ?
        (<img
            alt={strings.logoText}
            src={strings.logoImage}
            width='100'
            height='45'
        />) :
        (<div className='logo-text'>{strings.logoText}</div>)

    return (
        <div className='header'>
            <div className='header-logo'>
                {logo}
                <div className='header-divider' />
                <h3>{strings.appTitle}</h3>
            </div>
            <div className='header-profile'>
                {isAuthenticated &&
                    <>
                        <div className='header-profile-image'>
                            <Avatar alt={account.userDetails} />
                        </div>
                        <div className='header-profile-greeting'>
                            {strings.welcome(account.userDetails)}<br />
                            <LogOutButton strings={strings} />
                        </div>
                    </>
                }
            </div>
        </div>
    )
}

Header.propTypes = {
    strings: PropTypes.shape({
        logOut: PropTypes.string,
        title: PropTypes.string,
        welcome: PropTypes.func,
    })
}

Header.defaultProps = {
    strings: {
        logOut: 'Log out',
        title: 'Storage as a Service',
        welcome: name => `Welcome, ${name}`
    }
}

export default Header
