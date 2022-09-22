import React from 'react'
import { styled } from '@mui/material/styles';
import LinearProgress, { linearProgressClasses } from '@mui/material/LinearProgress';
import './PageLoader.css'

const PageLoader = ({state}) => {

	const BorderLinearProgress = styled(LinearProgress)(({ theme }) => ({
		height: 5,
		borderRadius: 0,
		[`&.${linearProgressClasses.colorPrimary}`]: {
		backgroundColor: theme.palette.grey[100]
		},
		[`& .${linearProgressClasses.bar}`]: {
		borderRadius: 2,
		backgroundColor: '#1976d2'
		},
	}));

	return (
		<>
			{state ? <div id="loader-wrapper"><div id="loader-overlay"> <BorderLinearProgress /></div></div> : <></>}
		</>
	);

}

PageLoader.propTypes = {};

PageLoader.defaultProps = {};

export default PageLoader;