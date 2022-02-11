# SAS for EDU

![image](/docs/assets/sas-welcome-page.png)

SAS is a Storage-as-a-Service platform designed to automate storage allocation in EDU institutions. Its main goal is to provide agility to stakeholders on having access to object storage infrastructure in Microsoft Azure.

Some of the capabilities currently provided by the system are:

* Dynamic creation of top level folder and file systems in Azure Data Lake Storage (ADLS) Gen 2.
* Dynamic addition of object owner as "Execute" in File System's ACL.
* Automatic creation of initial folder under the File System.
* Dynamic addition of folder's owner under initial folder.
* Exposure of "how to use" the storage infrastructure through Web UI.

## Deploy SAS for EDU

In order to deploy this solution to your environment, you'll need to setup some variables in the build process and create a static web app in Azure. To accomplish this, do the following:

1. [Fork the code](#fork-the-code)
1. [Create a Static Web App](#create-a-static-web-app)
1. [Create an application registration](#create-an-application-registration)
1. [Prepare the storage accounts](#prepare-the-storage-accounts)
1. [Add a GitHub secret](#add-a-github-secret)
1. [Configure the Static Web App](#configure-the-static-web-app)
1. [Build](#build)

### Fork the code

Fork this repo into your GitHub account. You can name the repo whatever you like.

### Create a Static Web App

1. Navigate to the Azure Portal and create a new Static Web App.
1. Name the app according to your organization's naming convention.
1. Choose the **Standard** hosting plan, which is required to enable custom authentication.
1. Select your preferred region.
1. Select **Other** as the deployment source.
1. Select **Review + create** and **Create**.

When the Static Web App is created, copy the Static Web App's *URL* for use later.

Select **Manage deployment token** and copy the token for use later.

### Create an Application Registration

Follow these steps to create a new Application Registration in Azure Active Directory:

1. In the [Azure Portal](https://portal.azure.com), navigate to *Azure Active Directory*.
1. Select **App registrations**.
1. Select **+ New registration**.
1. Provide an application name of your choice. Your users might need to consent, so make the application name descriptive.

    You can grant admin consent for the entire organization.

1. Choose the single tenant option.
1. For Redirect URI, select **Web** and paste the URL of your Static Web App followed by `/.auth/login/aad/callback`.

    For example, the redirect URI might be `https://awesome-sauce-1234abcd.azurestaticapps.net/.auth/login/aad/callback`.

1. Select **Register** to create the application registration.

When the application registration is created, copy the Directory (tenant) ID and Application (client) ID for use later.

#### Enable ID tokens

1. Select **Authentication** in the menu bar of the application registration.
1. In the *Implicit grant and hybrid flows* section, select **ID tokens (used for implicit and hybrid flows)**.
1. Select **Save**.

#### Add logout URL

1. Go to the Azure AD app registration and add a URL to the Front-channel logout URL. Paste the URL of your Static Web App followed by `/.auth/logout/aad/callback`.

#### Create a client secret

1. Select **Certificates & secrets** in the menu bar of the application registration.
1. In the *Client secrets* section, select **+ New client secret**.
1. Enter a name for the client secret. For example, MyStaticWebApp.
1. Choose an appropriate expiration timeframe for the secret.

    > **Note**
    >
    >You must rotate the secret before the expiration date by generating a new secret and updating the application settings with the new value.

1. Select **Add**.

Copy the value of the client secret for use later.

### API Permissions

The App Registration requires a Admin level permission to be granted. Navigate to the app registration and select the **API Permissions**.  Select **Add a permision** and choose the **Microsoft Graph**.  Select **Application Permissions** and search for **User.Read.All**.  Select **User.Read.All** and then add permissions.

When done, select the button that says **Grant admin consent for (your tenant name)**. You will need to have the correct Azure AD permissions to do so, such as Global Admin.

### Prepare the storage accounts

In order to allow this application to modify storage accounts, you need to assign two permissions, *Storage Blob Data Owner* and *User Access Administrator*, roles to the application registration for each of the storage accounts to be managed.

If you named the application *Storage-as-a-Service*, the RBAC entry would look like this:

![image](/docs/assets/sa-rbac.png)

### Add GitHub secrets

The GitHub workflow has a required secret that enables it to deploy the code to the app in Azure. Create the following repository secrets by going to Settings -> Secrets.

Secret | Value | Notes
--- | --- | ---
SAS_DEPLOYMENT_TOKEN | | The deployment token of your Static Web App.
AZURE_TENANT_ID | | Your Azure AD tenant ID.

### Configure the Static Web App

Add the following application settings to the Static Web App using the Configuration pane.

| Name | Value |
| --- | --- |
| AZURE_CLIENT_ID | The application ID from the app registration. |
| AZURE_CLIENT_SECRET | The application secret from the app registration. |
| AZURE_TENANT_ID | The tenant ID of your Azure AD. |
| COST_PER_TB | A numeric value for your monthly cost per terabyte of storage. |
| DATALAKE_STORAGE_ACCOUNTS | A comma-separated list of one or more ADLS Gen2 storage account names that have been prepared following the instructions above. |
| FILESYSTEMS_API_KEY | The shared API key to POST to the FileSystems API to create new containers. We recommend retrieving this secret from Key Vault. |
| CONFIGURATION_API_KEY | The shared API key to call the Configuration API. We recommend retrieving this secret from Key Vault. |

![App Settings](/docs/assets/app-settings.png)

### Build

Run the *Azure Static Web Apps CI/CD* workflow.

[![Azure Static Web Apps CI/CD](../../../actions/workflows/azure-swa-deploy.yml/badge.svg)](../../actions/workflows/azure-swa-deploy.yml)

## Monitor the application with Application Insights

Optional, but recommended.

Go back to the Static Web App and select Application Insights. Enable Application Insights and select the instance to wish to use or create new from this location.
