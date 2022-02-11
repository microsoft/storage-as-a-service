import React from 'react'
import PropTypes from 'prop-types'
import Button from "@mui/material/Button"
import CloudDownload from '@mui/icons-material/CloudDownload'
import step2Image from './StorageExplorer.png'
import step3Image from './SelectResource.png'
import step4Image from './SelectAuthenticationMethod.png'
import './ConnectDetails.css'

const ConnectDetails = ({ strings }) => {
    return (
        <div className='connectDetails'>
            <div className='title'>
                {strings.connectTitle}
            </div>
            <div className='storageExplorer'>
                <div className='storageExplorer-title'>
                    {strings.storageExplorerLabel}:
                </div>
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
    })
}

ConnectDetails.defaultProps = {
    data: {},
    strings: {
        connectTitle: 'How to connect?',
        download: 'Download',
        step1Label: '1. Download Storage Explorer',
        step2Label: '2. In Storage Explorer, right click on "Storage Accounts", under "Local and Attached", click on Connect to Azure Storage',
        step3Label: '3. Select "ADLS Gen2 container or directory"',
        step4Label: '4. Select "Sign in using Azure Active Directory"',
        storageExplorerLabel: 'Via Storage Explorer',
    }
}

export default ConnectDetails