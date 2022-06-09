const strings = {
	error: 'An error occured, see the console for details.',
	appTitle: 'Storage as a Service',
	companyName: 'Contoso University',
	directoryCreated: name => `Folder '${name}' Created!`,
	accountsLoaded: 'Storage accounts and file systems loaded',
	noAccountsLoaded: 'No storage accounts or file systems available',
	fileSystemLabel: 'File System',
	logIn: 'Log In',
	logOut: 'Log out',
	logoImage: '',
	logoText: 'Contoso University',
	members: 'members',
	newFolder: 'New Folder',
	storageAccountLabel: 'Storage Account',
	welcome: name => `Welcome, ${name}`,
	directoriesTable: {
		actionsLabel: 'Actions',
		folderLabel: 'Folder',
		fundCodeLabel: 'Fund Code',
		members: count => `${count} members`,
		monthlyCostLabel: 'Monthly Cost',
		spaceUsedLabel: 'Space Used',
		whoHasAccessLabel: 'Who Has Access?',
		openInStorageExplorerLabel: 'Open in Storage Explorer',
	},
	directoryDetails: {
		accessTierLabel: 'Storage type',
		close: 'Close',
		connectTitle: 'How to connect?',
		costLabel: 'Monthly cost',
		createdLabel: 'Created on',
		departmentLabel: 'Department',
		directoryDetailsTitle: 'Directory Details',
		download: 'Download',
		folderLabel: 'Folder',
		fundCodeLabel: 'Fund code',
		ownerLabel: 'Owner',
		userAccessLabel: 'User Access',
		regionLabel: 'Region',
		sizeLabel: 'Total size',
		step1Label: '1. Download Storage Explorer',
		step2Label: '2. In Storage Explorer, right click on "Storage Accounts", under "Local and Attached", click on Connect to Azure Storage',
		step3Label: '3. Select "ADLS Gen2 container or directory"',
		step4Label: '4. Select "Sign in using Azure Active Directory"',
		storageExplorerLabel: 'Via Storage Explorer',
		storageExplorerUrl: 'https://aka.ms/storageexplorer',
		totalFilesLabel: 'Total files',
		openInStorageExplorerLabel: 'Open in Storage Explorer',
		copyToClipboardLabel: 'Copy to clipboard',
		uriLabel: 'URL',
	},
	directoryEditor: {
		cancel: 'Cancel',
		fundCodeLabel: 'Fund code',
		directoryNameLabel: 'Folder name',
		userAccessLabel: 'Group/User access list',
		save: 'Save',
		title: 'Creating a new folder',
	},
}

export default strings
