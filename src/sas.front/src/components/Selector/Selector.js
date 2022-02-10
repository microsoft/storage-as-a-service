import React from "react"
import PropTypes from 'prop-types'
import InputLabel from "@mui/material/InputLabel"
import FormControl from "@mui/material/FormControl"
import MenuItem from "@mui/material/MenuItem"
import Select from "@mui/material/Select"

/**  
 * Renders list of items in a drop down selector
 */
const Selector = ({ items, id, label, onChange, selectedItem, autoSelectFirst }) => {
	const handleChange = event => {
		console.debug('Entered handleChange for id "%s" with value "%s"', id, event.target.value)
		onChange && onChange(event.target.value)
	}

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
				{items.map(item => <MenuItem key={item} value={item}>{item}</MenuItem>)}
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
