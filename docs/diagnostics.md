# About Diagnostics

## Prerequisites

You'll need the Az and Az.ResourceGraph PowerShell modules.

This command assumes that you've already logged into Azure with `Login-AzAccount` and selected the subscription with `Select-AzSubscription`.

The command uses the Azure CLI and assumes you've logged in with the same account.

## Running Diagnostics

`./diagnostics/diag.ps1 'static-web-app-name' 'resource-group-name' [-Verbose]`

The command will not output a comprehensive list of issues if there are multiple issues. After remediating the application configuration, run the diagnostics again to see remaining issues.

If there are no issues detected, the script will output "No issues detected."

## Checks

The diagnostics script, found in [diag.ps1](/diagnostics/diag.ps1), check for the following configuration settings.

### Static Web App Properties

- SkuTier is Standard

### Static Web App App Settings

#### Required

- API_CLIENT_ID
- ...

#### Recommended

- APPINSIGHTS_INSTRUMENTATIONKEY
- ...

### Storage Accounts

All storage accounts in the App Setting DATALAKE_STORAGE_ACCOUNTS

- exist
- give RBAC 'User Access Administrator' and 'Storage Blob Data Owner' to the principal specified in the App Setting AZURE_CLIENT_ID
