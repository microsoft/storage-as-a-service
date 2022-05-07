# About Diagnostics

## Running Diagnostics

`./diagnostics/diag.ps1 'static-web-app-name' 'resource-group-name'`

This command assumes that you've already logged into Azure with `Login-AzAccount` and selected the subscription with `Select-AzSubscription`.

## Checks

The diagnostics script, found in [diag.ps1](/diagnostics/diag.ps1), check for the following configuration settings.

## Static Web App Properties

- SkuTier
