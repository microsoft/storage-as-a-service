import React from 'react'
import PropTypes from 'prop-types'
import Button from "@mui/material/Button"
import CloudDownload from '@mui/icons-material/CloudDownload'
import step2Image from './StorageExplorer.png'
import step3Image from './SelectResource.png'
import step4Image from './SelectAuthenticationMethod.png'
import './ConnectDetails.css'
import Tab from "@mui/material/Tab"
import Tabs from "@mui/material/Tabs"
import Box from "@mui/material/Box"
import Typography from '@mui/material/Typography';
import StorageExplorerIcon from '../../images/storage-explorer.svg'
import rcloneIcon from './rclone.png'

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
					<Typography>{children}</Typography>
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

const ConnectDetails = ({ data, strings }) => {

	const [value, setValue] = React.useState(0);

	const handleChange = (event, newValue) => {
		setValue(newValue);
	};

	return (
		<div className='connectDetails'>
			<div className='title'>
				{strings.connectTitle}
			</div>
			<Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
				<Tabs value={value} onChange={handleChange}>
					<Tab label={strings.storageExplorerLabel} icon={<img alt="" src={StorageExplorerIcon} />} iconPosition="start" />
					<Tab label={strings.storageExplorerLinkLabel} icon={<img alt="" src={StorageExplorerIcon} />} iconPosition="start" />
					<Tab label={strings.azcopyLabel} />
					<Tab label={strings.rcloneLabel} icon={<img alt="" src={rcloneIcon} width="18px" />} iconPosition="start" />
				</Tabs>
			</Box>
			<TabPanel value={value} index={0}>
				<div className='storageExplorer'>
					{/* <div className='storageExplorer-title'>
						{strings.storageExplorerLabel}:
					</div> */}
					<div className='storageExplorer-steps'>
						<div className='step'>
							<div className='step-label'>
								{strings.step1Label}
							</div>
							<div className='step-content'>
								<Button target='_blank' href={strings.storageExplorerUrl} variant='outlined' startIcon={<CloudDownload />}>{strings.download}</Button>
							</div>
						</div>
						<div className='step-divider' />
						<div className='step'>
							<div className='step-label'>
								{strings.step2Label}
							</div>
							<div className='step-content'>
								<img src={step2Image} alt={strings.step2Label} />
							</div>
						</div>
						<div className='step-divider' />
						<div className='step'>
							<div className='step-label'>
								{strings.step3Label}
							</div>
							<div className='step-content'>
								<img src={step3Image} alt={strings.step3Label} />
							</div>
						</div>
						<div className='step-divider' />
						<div className='step'>
							<div className='step-label'>
								{strings.step4Label}
							</div>
							<div className='step-content'>
								<img src={step4Image} alt={strings.step4Label} />
							</div>
						</div>
					</div>
				</div>
			</TabPanel>
			<TabPanel value={value} index={1}>
				<p>Download and install Storage Explorer first.</p>
				<Button variant="outlined" aria-label={strings.openInStorageExplorerLabel} startIcon={<img src={StorageExplorerIcon} title={strings.openInStorageExplorerLabel} alt={strings.openInStorageExplorerLabel} />} size='large' onClick={() => { window.open(data.storageExplorerURI); return false }}>
					Click to open in Storage Explorer
				</Button>
				<p>TODO: Screenshots of Storage Explorer dialogs</p>
			</TabPanel>
			<TabPanel value={value} index={2}>
				<p>TODO: Detail and screenshots</p>
				<p>Get azcopy 10.15</p>
				<p>Log in with azcopy</p>
				<p>Execute an azcopy command to copy between storage folders: <pre>azcopy cp ...</pre></p>
			</TabPanel>
			<TabPanel value={value} index={2}>
				<p>Pending</p>
			</TabPanel>
		</div>
	)
}

ConnectDetails.propTypes = {
	strings: PropTypes.shape({
		connectTitle: PropTypes.string,
		download: PropTypes.string,
		step1Label: PropTypes.string,
		step2Label: PropTypes.string,
		step3Label: PropTypes.string,
		step4Label: PropTypes.string,
		storageExplorerLabel: PropTypes.string,
		storageExplorerLinkLabel: PropTypes.string,
		rcloneLabel: PropTypes.string,
		azcopyLabel: PropTypes.string,
	})
}

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
		rcloneLabel: 'rclone',
		azcopyLabel: 'azcopy',
	}
}

export default ConnectDetails