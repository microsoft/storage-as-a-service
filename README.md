# SAS for EDU

![image](/assets/sas-welcome-page.png)

SAS is a Storage-as-a-Service platform designed to automate storage allocation in EDU institutions. Its main goal is to provide agility to stakeholders on having access to object storage infrastructure in Microsoft Azure.

Some of the capabilities currently provided by the system are:

* Dynamic creation of top level folder and file systems in Azure Data Lake Storage (ADLS) Gen 2.
* Dynamic addition of object owner as "Execute" in File System's ACL.
* Automatic creation of initial folder under the File System.
* Dynamic addition of folder's owner under initial folder.
* Exposure of "how to use" the storage infrastructure through Web UI.

## Background

Why do we need this? There are many reasons to want this simplified portal. We have observed that many research institutions are not comfortable with providing their users with access to Azure portal. As such, they want to provide a limited UI.

### Azure "limitations" regarding permissions

Limit of Role Assignments per subscription. Currently only 2000 assignments to a single subscription, its resource group, or resources is allowed, [Azure Subscription Limits](https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits#azure-rbac-limits).

As such, if the resource institution want to create 500 containers each with 4 users who have access, they can easily hit the limit. Using groups and other aggregate constructs make it easier, but the limit still exists.

This is where using the Access Control Lists of the Azure Data Lake can provide some additional scope.

### POSIX Access Control Lists

In the Azure Data Lake, each diretory or file can have 32 ACL entries, of which 28 are really available to use. This allows the filesystem owner to create Top Level Folders that have up to 28 user or groups assigned to them. Each folder under these, can also have additonal ACL provided. See limits in [Data Lake Storage Access Control](https://docs.microsoft.com/en-us/azure/storage/blobs/data-lake-storage-access-control#what-are-the-limits-for-azure-role-assignments-and-acl-entries)

## Installation

The installation requires a GitHub account, an Azure Static Web App, a Key Vault, an Application Registration in Azure AD, and of course the Azure Storage Accounts with Hierarchical Namespace enabled.

The installation instructions can be found at [Installation](/docs/Installation.md)
