# Deploying Storage-as-a-Service for EDU with templates

## Prerequisites

The template-based deployment uses PowerShell and Bicep. You'll need the following on the system from which you'll deploy Storage-as-a-Service:

* PowerShell 7+
* Bicep 0.4+
* PowerShell Az module 6+

Before running the PowerShell scripts, you should be logged in to the Azure tenant and have selected the target subscription:

```powershell
Login-AzAccount
Select-AzSubscription <your-subscription-id>
```

The user account logging in should be a Global Admin and have at least the Contributor role on the subscription.

You might be prompted during the script execution to log in again to access the Microsoft Graph.

## Simple deployment using all defaults

This deployment will create an instance of Storage-as-a-Service in the East US 2 region.

### Simple Usage

```powershell
./deploy.ps1 [-Verbose]
```

This will deploy a new instance of the Storage-as-a-Service project in a new resource group `rg-saas-prod

## Customizing deployment with parameters

The PowerShell scripts, `deploy.ps1`, accepts parameters that allow you to customize the most commonly asked for changes. In addition, you can also modify the script itself to modify ARM template parameters that aren't exposed as PowerShell script parameters.

### PowerShell parameters

The PowerShell script supports the following parameters

* Sequence (default: `1`): a number to disambiguate multiple deployments of Storage-as-a-Service.
* NamingConvention (default: `'{0}-saas-{1}-{2}-{3}'`). See [below](#structure-of-the-naming) for understanding the placeholders `{0}`-`{3}`.
* Environment (default: `'test'`). Choose from `test`, `demo`, or `prod`.
* Location (default: `'eastus2'`). Static Web Apps are available in select regions only. You can deploy the Storage-as-a-Service instance in a region different than the region where your storage accounts are.
* DoNotCreateAppReg (default: `$false`)
* DeploySampleStorageAccount (default: `$false`): If `$true`, the script will deploy a new hierarchical storage account with RBAC assignments appropriate for use by the service.

#### Parameter Usage

```powershell
./deploy.ps1 -Sequence 2 -Location 'eastasia' -Environment 'test' -NamingConvention 'saas-{0}-{2}-{1}-{3}' -DoNotCreateAppReg $false -DeploySampleStorageAccount $true [-Verbose]`
```

This will deploy a second instance of the Storage-as-a-Service project with a new Azure AD App Registration (`Storage-as-a-Service 2`) in a new resource group called `saas-rg-eastasia-test-02`.

#### Structure of the naming convention

The naming convention allows you to match the names of the created resource group and resources to your organizational standard. The default naming convention follows the example found in the Microsoft Cloud Adoption Framework.

If you customize the naming convention, you must provide all four placeholders.

* `{0}`: The abbreviation of the resource type, e.g., `rg` for a resource group and `log` for a Log Analytics Workspace.
* '{1}`: The environment.
* '{2}`: The location.
* '{3}`: The sequence.

For example, if your naming convention is `workload_environment_location_resourceType_sequence`, then specify the naming convention parameter as `saas_{1}_{2}_{0}_{3}`. In that example, the name of your Static Web App will become `saas_test_eastus2_stapp_01`.

However, the Key Vault and Log Analytics Workspace names will use hyphens ('-') because domain names don't support `_`.

> **Note**
>
> Deployment names will not follow this naming convention. They are fixed at this time.

### Advanced customization with ARM parameters

Documentation pending.

## Unsupported scenarios

A few scenarios are currently not supported by the deployment:

* **User account running the deployment is not an AAD Global Admin.** The Global Admin role is required to grant admin consent for the AAD App Registration. In the future, the script might support not attempting to create an App Registration and granting consent, however, the script will always need to be able to create a new client secret and update the App Registration with URIs.
* **Single App Registration for multiple deployments.** This is a supported scenario, however, the script will overwrite the Redirect URI for the second and subsequent deployments.

If one or more of these apply to you, please use the [manual deployment method](Installation.md).
