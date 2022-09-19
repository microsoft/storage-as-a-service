// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React, { useEffect, useState } from 'react'
import PropTypes from 'prop-types'
import Link from "@mui/material/Link"
import LogInButton from "../LogInButton"
import './LandingPage.css'
import { getServerStatus } from '../../services/StorageManager.service'

const LandingPage = ({ strings }) => {

	const [serverStatus, setServerStatus] = useState('');

	useEffect(() => {
		getServerStatus()
		.then(u => {
			setServerStatus(u.message);
		})

		return function cleanup() {
           //mounted = false
        }
	});

	return (
		<div>
			<div className='landingpage'>
				<div className='access'>
					<div className='notes'>
						To have access to the storage as a service platform, you need to use your corporative credentials.
						If you have it handy, please click on the "Log in" button below.  If you don't, please click on
						the "How to gain access" link below.
					</div>
					<div className='login'>
						<LogInButton strings={strings} />
					</div>
					<div className='link'>
						<Link href=''>How to gain access</Link>
					</div>
				</div>
				<div className='divider' />
				<div className='three'>
					<h5>
						What you can do here?
					</h5>
					<ol className='cando'>
						<li>Ask for a new space to store your data files in a highly scalable way in the cloud.</li>
						<li>Manage who has access to the space you are creating.</li>
						<li>Have a view about capacity of your space, cost and more.</li>
						<li>Move data between different layers as the data changes in terms of priority.</li>
						<li>Decommission the storage when it is no longer needed.</li>
					</ol>
				</div>
			</div>
			<div>
				{serverStatus && <div><br /><i>Server Status: </i><b>{serverStatus}</b></div>}
			</div>
		</div>
	);
}

LandingPage.propTypes = {
    strings: PropTypes.shape({
        logIn: PropTypes.string
    })
}

LandingPage.defaultProps = {
    strings: {
        logIn: 'Log In'
    }
}

export default LandingPage
