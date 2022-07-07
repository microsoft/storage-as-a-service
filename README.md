# Storage-as-a-Service for EDU

![workflow](https://github.com/microsoft/storage-as-a-service/actions/workflows/azure-swa-deploy.yml/badge.svg)
[![CodeQL](https://github.com/microsoft/storage-as-a-service/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/microsoft/storage-as-a-service/actions/workflows/codeql-analysis.yml)
![GitHub issues by-label](https://img.shields.io/github/issues/microsoft/storage-as-a-service/enhancement?label=enhancement%20issues)
![GitHub issues by-label](https://img.shields.io/github/issues/microsoft/storage-as-a-service/bug?label=bug%20issues)
![GitHub issues by-label](https://img.shields.io/github/issues/microsoft/storage-as-a-service/documentation?label=docs%20issues)

A Storage-as-a-Service platform designed to automate storage allocation in EDU institutions. Its main goal is to provide agility to stakeholders on having access to object storage infrastructure in Microsoft Azure.

Some of the capabilities currently provided by the system are:

* Dynamic creation of top level folder and file systems in Azure Data Lake Storage (ADLS) Gen 2.
* Dynamic addition of object owner as "Execute" in File System's ACL.
* Automatic creation of initial folder under the File System.
* Dynamic addition of folder's owner under initial folder.
* Exposure of "how to use" the storage infrastructure through Web UI.

![Home page screenshot](/docs/assets/sas-welcome-page.png)

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

The installation instructions can be found at [Installation](/docs/Installation.md).

![Architecture Diagram](/docs/assets/Architecture.png)

*Download a [Visio file](/docs/assets/Architecture.vsdx) of this diagram.*

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
