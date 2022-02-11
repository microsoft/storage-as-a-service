import React from 'react'
import PropTypes from 'prop-types'
import Table from 'react-bootstrap/Table'
import DetailsIcon from '@mui/icons-material/InfoTwoTone'
import EditIcon from '@mui/icons-material/EditOutlined'
import DirectoriesTableMembers from './DirectoriesTableMembers'
import './DirectoriesTable.css'

export const DirectoriesTable = ({ data, onDetails, onEdit, strings }) => {
    return (
        <Table striped bordered hover className='directoriesTable'>
            <thead>
                <tr>
                    <th>
                        {strings.folderLabel}
                    </th>
                    <th>
                        {strings.spaceUsedLabel}
                    </th>
                    <th>
                        {strings.monthlyCostLabel}
                    </th>
                    <th>
                        {strings.whoHasAccessLabel}
                    </th>
                    <th>
                        {strings.fundCodeLabel}
                    </th>
                    <th>
                        {strings.actionsLabel}
                    </th>
                </tr>
            </thead>
            <tbody>
                {data.map(row => {
                    return (
                        <tr key={row.name}>
                            <td className='name'>
                                {row.name}
                            </td>
                            <td className='spaceused'>
                                {row.size}
                            </td>
                            <td className='costs'>
                                {row.cost}
                            </td>
                            <td className='owner'>
                                <DirectoriesTableMembers members={row.userAccess} strings={strings} />
                            </td>
                            <td className='fundcode'>
                                {row.fundCode}
                            </td>
                            <td className='actions'>
                                {onEdit && <EditIcon onClick={() => onEdit(row)} className='action' />}
                                {onDetails && <DetailsIcon onClick={() => onDetails(row)} className='action' />}
                            </td>
                        </tr>
                    )
                })}
            </tbody>
        </Table>
    )
}

DirectoriesTable.propTypes = {
    data: PropTypes.array,
    onDetails: PropTypes.func,
    onEdit: PropTypes.func,
    strings: PropTypes.shape({
        actionsLabel: PropTypes.string,
        folderLabel: PropTypes.string,
        fundCodeLabel: PropTypes.string,
        monthlyCostLabel: PropTypes.string,
        spaceUsedLabel: PropTypes.string,
        whoHasAccessLabel: PropTypes.string,
    })
}

DirectoriesTable.defaultProps = {
    data: [],
    strings: {
        actionsLabel: 'Actions',
        folderLabel: 'Folder',
        fundCodeLabel: 'Fund Code',
        monthlyCostLabel: 'Monthly Cost',
        spaceUsedLabel: 'Space Used',
        whoHasAccessLabel: 'Who Has Access?',
    }
}

export default DirectoriesTable
