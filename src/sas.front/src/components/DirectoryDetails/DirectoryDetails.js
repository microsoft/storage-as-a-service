import React from 'react'
import PropTypes from 'prop-types'
import Grid from '@mui/material/Grid'
import './DirectoryDetails.css'

const DirectoryDetails = ({ data, strings }) => {

    const format = value => {
        return value ? value : '-'
    }

    return (
        <Grid container className='directoryDetails'>
            <Grid item xs={12} className='title'>
            <div className='label'>{strings.folderLabel}:</div> {data.name}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.totalFilesLabel}:</div> {format(data.totalFiles)}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.sizeLabel}:</div> {format(data.size)}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.costLabel}:</div> {format(data.cost)}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.fundCodeLabel}:</div> {format(data.fundCode)}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.ownerLabel}:</div> {format(data.owner)}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.createdLabel}:</div> {format(data.createdOn)}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.accessTierLabel}:</div> {format(data.accessTier)}
            </Grid>
            <Grid item md={4} sm={6} xs={12} className='detail'>
                <div className='label'>{strings.regionLabel}:</div> {format(data.region)}
            </Grid>
            <Grid item xs={12} className='detail'>
                <div className='label'>{strings.uriLabel}:</div> {format(data.uri)}
            </Grid>
        </Grid>
    )
}

DirectoryDetails.propTypes = {
    data: PropTypes.object,
    strings: PropTypes.shape({
        accessTierLabel: PropTypes.string,
        costLabel: PropTypes.string,
        createdLabel: PropTypes.string,
        folderLabel: PropTypes.string,
        fundCodeLabel: PropTypes.string,
        ownerLabel: PropTypes.string,
        regionLabel: PropTypes.string,
        sizeLabel: PropTypes.string,
        totalFilesLabel: PropTypes.string,
        uriLabel: PropTypes.string,
    })
}

DirectoryDetails.defaultProps = {
    data: {},
    strings: {
        accessTierLabel: 'Storage type',
        costLabel: 'Monthly cost',
        createdLabel: 'Created on',
        folderLabel: 'Folder',
        fundCodeLabel: 'Fund code',
        ownerLabel: 'Owner',
        regionLabel: 'Region',
        sizeLabel: 'Total size',
        totalFilesLabel: 'Total files',
        uriLabel: 'URL',
    }
}

export default DirectoryDetails
