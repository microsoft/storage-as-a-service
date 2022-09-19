// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from 'react'
import PropTypes from 'prop-types'
import Button from "@mui/material/Button"
import CloudDownload from '@mui/icons-material/CloudDownload'
import step2Image from './StorageExplorer.png'
import step3Image from './SelectResource.png'
import step4Image from './SelectAuthenticationMethod.png'
import dlStep1Image from './directLinkOpen.png'
import dlStep2Image from './directLinkAccount.png'
import dlStep3Image from './directLinkName.png'
import dlStep4Image from './directLinkSummary.png'
import azcopyLogin from './azcopy-login.png'
import azcopyEntercode from './azcopy-entercode.png'
import './ConnectDetails.css'
import Tab from "@mui/material/Tab"
import Tabs from "@mui/material/Tabs"
import Box from "@mui/material/Box"
import StorageExplorerIcon from '../../images/storage-explorer.svg'

function TabPanel(props) {
	const { children, value, index, ...other } = props;

	return (
		<div
			role="tabpanel"
			hidden={value !== index}
			id={`simple-tabpanel-${index}`}
			aria-labelledby={`simple-tab-${index}`}
			{...other}
		>
			{value === index && (
				<Box sx={{ p: 3 }}>
					{children}
				</Box>
			)}
		</div>
	);
}

TabPanel.propTypes = {
	children: PropTypes.node,
	index: PropTypes.number.isRequired,
	value: PropTypes.number.isRequired,
};

const ConnectDetails = ({ uri, storageExplorerURI, strings }) => {

	const [value, setValue] = React.useState(0);

	const handleChange = (event, newValue) => {
		setValue(newValue);
	};

	const seSteps =
		[
			{ name: strings.step2Label, image: step2Image },
			{ name: strings.step3Label, image: step3Image },
			{ name: strings.step4Label, image: step4Image }
		]

	const dlSteps =
		[
			{ name: strings.deepLinkStep1Label, image: dlStep1Image },
			{ name: strings.deepLinkStep2Label, image: dlStep2Image },
			{ name: strings.deepLinkStep3Label, image: dlStep3Image },
			{ name: strings.deepLinkStep4Label, image: dlStep4Image }
		]

	const azSteps =
		[
			{ name: strings.azStep1, image: azcopyLogin },
			{ name: strings.azStep2, image: azcopyEntercode }
		]

	let source = uri
	let target = "https://[destaccount].dfs.core.windows.net/[container]/[path/to/folder]"
	let stepCount = 0;

	return (
		<div className='connectDetails'>
			<div className='title'>
				{strings.connectTitle}
			</div>
			<Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
				<Tabs value={value} onChange={handleChange}>
					<Tab label={strings.storageExplorerLinkLabel} icon={<img alt="" src={StorageExplorerIcon} />} iconPosition="start" />
					<Tab label={strings.storageExplorerLabel} icon={<img alt="" src={StorageExplorerIcon} />} iconPosition="start" />
					<Tab label={strings.azcopyLabel} />
				</Tabs>
			</Box>
			<TabPanel value={value} index={0}>
				<div className="storageExplorer">
					<div className='storageExplorer-steps'>
						<div className='step' key={stepCount++} >
							<div className='step-label'>
								{strings.step1Label}
							</div>
							<div className='step-content'>
								<Button target='_blank' href={strings.storageExplorerUrl} variant='outlined' startIcon={<CloudDownload />}>{strings.download}</Button>
								|
								<Button variant="outlined" aria-label={strings.openInStorageExplorerLabel}
										startIcon={<img src={StorageExplorerIcon}
										title={strings.openInStorageExplorerLabel} alt={strings.openInStorageExplorerLabel} />}
										size='large' onClick={() => { window.open(storageExplorerURI); return false }}>
									Click to open in Storage Explorer
								</Button>
							</div>
						</div>
						{dlSteps.map(step => (
							<div className='step' key={stepCount++}>
								<div className='step-divider' />
								<div className='step-label'>{step.name}</div>
								<div className='step-content'><img src={step.image} alt={step.name} />
								</div>
							</div>
						))}
					</div>
				</div>
			</TabPanel>
			<TabPanel value={value} index={1}>
				<div className='storageExplorer'>
					<div className='storageExplorer-steps'>
						<div className='step' key={stepCount++}>
							<div className='step-label'>
								{strings.step1Label}
							</div>
							<div className='step-content'>
								<Button target='_blank' href={strings.storageExplorerUrl} variant='outlined' startIcon={<CloudDownload />}>{strings.download}</Button>
							</div>
						</div>
						{
							seSteps.map(step => (
								<div className='step' key={stepCount++}>
									<div className='step-divider' />
									<div className='step-label'>
										{step.name}
									</div>
									<div className='step-content'>
										<img src={step.image} alt={step.name} />
									</div>
								</div>
							))
						}
					</div>
				</div>
			</TabPanel>
			<TabPanel value={value} index={2}>
				<div className="storageExplorer">
					<div className='storageExplorer-steps'>
					<div className='step' key={stepCount++}>
						<div className='step-label'>Use azcopy command</div>
						<div className='step-content'>
							<p align="left">Using the azcopy command makes it easy to move and copy files in
							command line or automated processes. Note that when copying files from a folder to a folder, ensure
							the target folder has a / at the end.</p>
							<p align="left">
								azcopy cp "{source}" "{target}"
								--overwrite=prompt
								--s2s-preserve-access-tier=false
								--include-directory-stub=false
								--recursive
								--log-level=INFO</p>
						</div>
					</div>
						<div className='step' key={stepCount++}>
							<div className='step-divider' />
							<div className='step-label'>1.	Download AzCopy version 10.15 or later</div>
							<div className='step-content'>
								<a  rel="noopener noreferrer" target="_blank"
									href="https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10#download-azcopy">
									Download AzCopy
								</a>
							</div>
						</div>
						{azSteps.map(step => (
							<div className='step' key={stepCount++}>
								<div className='step-divider' />
								<div className='step-label'>{step.name}</div>
								<div className='step-content'><img src={step.image} alt={step.name} />
								</div>
							</div>
						))}
						<div className='step' key={stepCount++}>
							<div className='step-divider' />
							<div className='step-label'>4. Use azcopy command</div>
							<div className='step-content'>azcopy cp "{source}" "{target}" --recursive=true</div>
						</div>
					</div>
				</div>
			</TabPanel>
		</div>
	)
}


ConnectDetails.propTypes = {
	uri: PropTypes.string,
	storageExplorerURI: PropTypes.string,
	strings: PropTypes.shape({
		connectTitle: PropTypes.string,
		download: PropTypes.string,
		step1Label: PropTypes.string,
		step2Label: PropTypes.string,
		step3Label: PropTypes.string,
		step4Label: PropTypes.string,
		storageExplorerLabel: PropTypes.string,
		storageExplorerLinkLabel: PropTypes.string,
		azcopyLabel: PropTypes.string,
		deepLinkStep1Label: PropTypes.string,
		deepLinkStep2Label: PropTypes.string,
		deepLinkStep3Label: PropTypes.string,
		deepLinkStep4Label: PropTypes.string,
		azStep1: PropTypes.string
	})
}

/*
TODO: Determine if this is actually needed or just old
ConnectDetails.defaultProps = {
	data: {},
	strings: {
		connectTitle: 'How to connect via',
		download: 'Download',
		step1Label: '1. Download Storage Explorer',
		step2Label: '2. In Storage Explorer, right click on "Storage Accounts", under "Local and Attached", click on Connect to Azure Storage',
		step3Label: '3. Select "ADLS Gen2 container or directory"',
		step4Label: '4. Select "Sign in using Azure Active Directory"',
		storageExplorerLabel: 'Storage Explorer (Manual)',
		storageExplorerLinkLabel: 'Storage Explorer (Direct Link)',
		azcopyLabel: 'azcopy',
		deepLinkStep1Label: '2. Open',
		deepLinkStep2Label: '3. Account',
		deepLinkStep3Label: '4. Name',
		deepLinkStep4Label: '5. Summary',
		azStep1: 'Login Steps',
		azStep2: 'Login to Azure Active Directory'
	}
}
*/

export default ConnectDetails
