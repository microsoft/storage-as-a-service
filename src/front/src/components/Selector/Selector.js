// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import React from "react"
import PropTypes from 'prop-types'
import InputLabel from "@mui/material/InputLabel"
import FormControl from "@mui/material/FormControl"
import MenuItem from "@mui/material/MenuItem"
import Select from "@mui/material/Select"

/**
 * Renders list of items in a drop down selector
 */
// Added item and value properties to allow for more control over what would be shown in the dropdown
const Selector = ({ items, id, label, onChange, selectedItem, autoSelectFirst, textProperty, valueProperty }) => {
	const handleChange = event => {
		onChange && onChange(event.target.value)
	}

	let itemCollection = (textProperty && valueProperty) ?
		items.map(item => <MenuItem key={item[valueProperty]} value={item[valueProperty]}>{item[textProperty]}</MenuItem>) :
		items.map(item => <MenuItem key={item} value={item}>{item}</MenuItem>)

		return (
			<FormControl fullWidth>
				<InputLabel id={`${id}-select-label`}>{label}</InputLabel>
				<Select
					labelId={`${id}-select-label`}
					id={id}
					label={label}
					value={selectedItem}
					onChange={handleChange}
				>
					{itemCollection}
				</Select>
			</FormControl>
		)

}

Selector.propTypes = {
	id: PropTypes.string,
	items: PropTypes.array,
	label: PropTypes.string,
	onChange: PropTypes.func,
	selectedItem: PropTypes.string
}

Selector.defaultProps = {
	id: 'Selector',
	items: [],
	label: '',
	selectedItem: ''
}

export default Selector
