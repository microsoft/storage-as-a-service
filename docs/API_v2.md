# API 'V2'

## Endpoints

```
StorageAccounts [GET]
FileSystems [POST]
```

### StorageAccounts

`GET /api/StorageAccounts[?refresh=true]`

Retrieves the list of storage accounts the signed in user can access.

#### Response Body

```json
[
 "accountname1",
 "accountname2"
]
```

`GET /api/StorageAccounts/{accountName}`

Retrieves the list of containers and container details the signed in user can access.

#### Response Body

```json
[
 {
  "containerName": "containerA",
  "storageExplorerDirectLink": "storageexplorer://?v=2&...",
  "metaData": [
   {
    "key": "metadata key 1",
    "value": "metadata value 1"
   },
   {
    "key": "metadata key 1",
    "value": "metadata value 1"
   }
  ],
  "access": [
  {
   "roleName": "Reader",
   "principalName": "UPN or group name",
   "principalId": "guid"
  },
  {
   "roleName": "Contributor",
   "principalName": "UPN or group name",
   "principalId": "guid"
  },
  {
   "roleName": "Owner",
   "principalName": "UPN or group name",
   "principalId": "guid"
  }
  ]
 },
 {
  "containerName": "containerB",
  ...
 }
]
```

### FileSystems

`POST /api/FileSystems`

Creates a new container.

#### Request Body

```json
{
 "containerName": "containerC",
 "readAccess": [
  "groupNameOrUPN",
  "groupNameOrUPN"
 ],
 "writeAccess": [
  "groupNameOrUPN",
  "groupNameOrUPN"
 ]
}
```

#### Response Body

See response format of `/api/StorageAccounts/{container}`.

### CalculateAllFolderSizes

See v1.
