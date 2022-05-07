# About Diagnostics

## Prerequisites

You'll need the Az, Az.ResourceGraph, and Microsoft.Graph PowerShell modules.

This command assumes that you've already logged into Azure with `Login-AzAccount` and selected the subscription with `Select-AzSubscription`.

The command uses the Azure CLI and assumes you've logged in with the same account.

The script will log you in to the Microsoft Graph.

## Running Diagnostics

`./diagnostics/diag.ps1 'static-web-app-name' 'resource-group-name' [-Verbose]`

The command will not output a comprehensive list of issues if there are multiple issues. After remediating the application configuration, run the diagnostics again to see remaining issues.

If there are no issues detected, the script will output "No issues detected."

## Checks

The diagnostics script, found in [diag.ps1](/diagnostics/diag.ps1), checks for the following configuration settings.

### Static Web App Properties

- SkuTier is Standard

### Static Web App App Settings

#### Required

- API_CLIENT_ID
- ... (pending documentation)

#### Recommended

- APPINSIGHTS_INSTRUMENTATIONKEY
- ... (pending documentation)

### Storage Accounts

All storage accounts in the App Setting DATALAKE_STORAGE_ACCOUNTS:

- exist and have the correct configuration (ADLS Gen 2)
- have RBAC 'User Access Administrator' and 'Storage Blob Data Owner' assigned to the principal specified in the App Setting AZURE_CLIENT_ID
