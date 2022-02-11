# API


## Endpoints

```
FileSystems: [POST,GET] http://localhost:7071/api/FileSystems/{account?}
TopLevelFoldersGET: [GET] http://localhost:7071/api/TopLevelFolders/{account}/{filesystem}/{user?}
TopLevelFoldersPOST: [POST] http://localhost:7071/api/TopLevelFolders/{account}/{filesystem}
CalculateAllFolderSizes: [POST] http://localhost:7071/api/Configuration/CalculateFolderSizes
```

### FileSystems

GET api/FileSystems/{account?}

This endpoint requires the users to be logged in.
It will return a list of storage accounts and containers the current user has access to read.

POST api/FileSystems/{account?}

This endpoint requires a shared key that is stored in a KeyVault to access it. Provide the shared key in a custom header `Saas-FileSystems-Api-Key`.

It will create a new container for a user using the following body contents.

BODY:
```
{
    "FileSystem": "fs1",
    "FundCode": "1234",
    "Owner": "eduuser@edudemodomain.onmicrosoft.com",
    "StorageAccount": "{overwritten by account URL segment if used, otherwie required.}"   
}
```

### TopLevelFolders

GET api/TopLevelFolders/{account}/{filesystem}/{user?}

This endpoint will calculate the accessible TopLevelFolders for a user within an *account* and *filesystem* for the logged in user.

POST api/TopLevelFolders/{account}/{filesystem}/{user?}

This endpoint will create a new Top Level Folder in the specified filesystem.
The security for the folder will use Access Control Lists.
The user must be in AzureAD.
The user will be granted full RWX POSIX style rights in the Top Level Folder.
They will also be given the X rights in the root folder.

### CalculateAllFolderSizes

POST api/Configuration/CalculateFolderSizes

This endpoint requires a shared key that is stored in a KeyVault to access it. Provide the shared key in a custom header `Saas-Configuration-Api-Key`.

This background job will calculate the folder sizes based on the files present and store the data in the MetaData of the Top Level Folder.


