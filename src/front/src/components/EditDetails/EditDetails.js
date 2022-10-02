// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as React from 'react';
import PropTypes from 'prop-types'
import Grid from '@mui/material/Grid'
import './EditDetails.css'
import { useAutocomplete } from '@mui/base/AutocompleteUnstyled';
import { createRoleAssignment, deleteRoleAssignment } from '../../services/StorageManager.service'
import CloseIcon from '@mui/icons-material/Close';
import { red } from '@mui/material/colors';
import { styled } from '@mui/material/styles';
import { useTheme } from '@mui/material/styles';
import Box from '@mui/material/Box';
import OutlinedInput from '@mui/material/OutlinedInput';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Select from '@mui/material/Select';
import Chip from '@mui/material/Chip';
import AssignmentIndIcon from '@mui/icons-material/AssignmentInd';
import Button from '@mui/material/Button';

const ITEM_HEIGHT = 48;
const ITEM_PADDING_TOP = 8;
const MenuProps = {
	PaperProps: {
		style: {
			maxHeight: ITEM_HEIGHT * 4.5 + ITEM_PADDING_TOP,
			width: 250,
		},
	},
};

const roles = ["Owner", "Contributor", "Reader"]

function getStyles(role, roleName, theme) {
	return {
		fontWeight:
			roleName.indexOf(role) === -1
				? theme.typography.fontWeightRegular
				: theme.typography.fontWeightMedium,
	};
}

const Root = styled('div')(
	({ theme }) => `
  color: ${theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,.85)'
		};
  font-size: 14px;
`,
);

const InputWrapper = styled('div')(
	({ theme }) => `
  border: 1px solid ${theme.palette.mode === 'dark' ? '#434343' : '#d9d9d9'};
  background-color: ${theme.palette.mode === 'dark' ? '#141414' : '#fff'};
  border-radius: 4px;
  padding: 1px;
  display: flex;
  flex-wrap: wrap;

  &:hover {
    border-color: ${theme.palette.mode === 'dark' ? '#177ddc' : '#40a9ff'};
  }

  &.focused {
    border-color: ${theme.palette.mode === 'dark' ? '#177ddc' : '#40a9ff'};
    box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.2);
  }

  & input {
    background-color: ${theme.palette.mode === 'dark' ? '#141414' : '#fff'};
    color: ${theme.palette.mode === 'dark' ? 'rgba(255,255,255,0.65)' : 'rgba(0,0,0,.85)'
		};
    height: 30px;
    box-sizing: border-box;
    padding: 4px 6px;
    width: 0;
    min-width: 30px;
    flex-grow: 1;
    border: 0;
    margin: 0;
    outline: 0;
  }
`,
);

function Tag(props) {
	const { label, onDelete, ...other } = props;
	return (
		<div {...other}>
			<span>{label}</span>
			<CloseIcon {...other} onClick={onDelete} />
		</div>
	);
}

Tag.propTypes = {
	principalName: PropTypes.string.isRequired,
	roleName: PropTypes.func.isRequired,
};


const EditDetails = ({ data, storageAccount, strings }) => {
	const [roleName, setRoleName] = React.useState('')
	const [principalName, setPrincipalName] = React.useState('')
	const [newData] = React.useState(data)
	const [assignmentData, setAssignmentData] = React.useState(data.access.filter(element => !element.isInherited))
	const [selectedStorageAccount] = React.useState(storageAccount)
	const theme = useTheme()

	const {
		getRootProps,
		getInputProps,
		value,
		focused,
		setAnchorEl,
	} = useAutocomplete({
		id: 'User-Accesses',
		value: [...assignmentData],
		multiple: true,
		options: [],
		getOptionLabel: (option) => option
	});

	function handleRoleChange(event) {
		const {
			target: { value },
		} = event;
		setRoleName(value);
	}

	function handlePrincipalChange(event) {
		setPrincipalName(event.target.value)
	}

	function onCustomDelete(event) {
		const deleteStuff = async() => {
			const index = event.index
			const deleteAssignment = assignmentData[index]
			await deleteRoleAssignment(selectedStorageAccount, newData.name, deleteAssignment.roleAssignmentId)
			console.log("I just deleted " + deleteAssignment.roleAssignmentId)
			setAssignmentData(assignmentData.filter(item => assignmentData.indexOf(item) !== index))
			newData.access = newData.access.filter( item => item.roleAssignmentId !== deleteAssignment.roleAssignmentId)
		}
		deleteStuff();
	}

	function handleAdd() {
		const addStuff = async() => {
			let roleAssignmentResponse = await createRoleAssignment(selectedStorageAccount, newData.name,
												{ "roleName": roleName, "principalName": principalName })
			if (roleAssignmentResponse.isSuccess) {
				setAssignmentData(assignmentData.concat(roleAssignmentResponse.roleAssignment))
				newData.access = newData.access.concat(roleAssignmentResponse.roleAssignment)
				setPrincipalName('')
				setRoleName('')
			}
		}
		// TODO: Set Message in dialog
		addStuff();
	}

	let chipCount = 0;

	return (
		<Grid container className='EditDetails' spacing={4}>
			<Grid item xs={12} className='title'>
				<div className='label'>{(<Chip key={newData.name} label={newData.name} />)} <AssignmentIndIcon /> {strings.editorTitle}  </div>
			</Grid>
			<Grid item xs={12} className='AddRoleAssignment'>
				<TextField id="outlined-multiline-flexible" label="Email / UPN"
					sx={{ width: 500, margin: 2 }}
					value={principalName}
					onChange={handlePrincipalChange}
				/>
				<Select	id="outlined-multiline-select" label="Role"
				 	sx={{ width: 300, margin: 2 }}
					value={roleName}
					onChange={handleRoleChange}
					MenuProps={MenuProps}
					>
					{
						roles.map((name) => (<MenuItem key={name} value={name} style={getStyles(name, roleName, theme)}>{name}</MenuItem>))
					}
				</Select>
				<Button sx={{ margin: 2 }} xs={1} variant="outlined" onClick={handleAdd}>Add</Button>
			</Grid>
			<Grid item xs={12} md={12} className='RoleAssignmentList'>
				<Root>
					<div {...getRootProps()}>
						<InputWrapper xs={12} md={12} ref={setAnchorEl} className={focused ? 'focused' : ''}>
							{value.map((option, index) => (
								<Chip key={chipCount++}
									label={<div>
											<span>{option.roleName + " : " + option.principalName}</span>
											<CloseIcon sx={{ m:1, color: red[500] }} onClick={() => onCustomDelete({index})} />
										  </div>}
								/>
								))}
								<input {...getInputProps()} />
						</InputWrapper>
					</div>
				</Root>
			</Grid>
		</Grid>
	)
}

EditDetails.propTypes = {
	data: PropTypes.object,
	storageAccount: PropTypes.string,
	strings: PropTypes.shape({
		roleName: PropTypes.string,
		principalName: PropTypes.string
	})
}

EditDetails.defaultProps = {
	data: {},
	storageAccount: 'Storage Account',
	strings: {
		roleName: 'Contributor',
		principalName: 'John Snow',
		editorTitle: 'Role Assignment'
	}
}

export default EditDetails