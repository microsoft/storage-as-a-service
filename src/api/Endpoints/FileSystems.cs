using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Authorization.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UsEduCsu.Saas.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Azure.Identity;

namespace Microsoft.UsEduCsu.Saas
{
	public static class FileSystems
    {
        public static ILogger logger;

        [FunctionName("FileSystems")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "POST", "GET", Route = "FileSystems/{account?}")]
            HttpRequest req, 
            ILogger log, String account)
        {

            if (req.Method == HttpMethods.Post)
                return await FileSystemsPOST(req, log, account);

            if (req.Method == HttpMethods.Get)
                return await FileSystemsGET(req, log, account);

            // TODO: If this is even possible (accepted methods are defined above?) return HTTP error code 405, response must include an Allow header with allowed methods
            return null;
        }

		private static async Task<IActionResult> FileSystemsGET(HttpRequest req, ILogger log, string account)
        {
            // Check for logged in user
            ClaimsPrincipal claimsPrincipal;
            try
            {
                claimsPrincipal = UserOperations.GetClaimsPrincipal(req);
                if (Services.Extensions.AnyNull(claimsPrincipal, claimsPrincipal.Identity))
                    return new BadRequestErrorMessageResult("Call requires an authenticated user.");
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestErrorMessageResult("Unable to authenticate user.");
            }

            // Calculate UPN
            var upn = claimsPrincipal.Identity.Name.ToLowerInvariant();
            var principalId = claimsPrincipal.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?
                .Value;

            // Get the Containers for a upn from each storage account
            var accounts = SasConfiguration.GetConfiguration().StorageAccounts;
            if (account != null)
                accounts = accounts.Where(a => a.ToLowerInvariant() == account).ToArray();

            // Define the return value
            var result = new List<FileSystemResult>();

			Parallel.ForEach(accounts, acct =>
			//foreach (var acct in accounts)
				{
					var containers = GetContainers(log, acct, upn, principalId);

					// Add the current account and the permissioned containers to the result set
					var fs = new FileSystemResult()
					{
						Name = acct,
						FileSystems = containers.Distinct().OrderBy(c => c).ToList()
					};
					if (fs.FileSystems.Any())
						result.Add(fs);
				}
			);

            
            log.LogTrace(JsonSerializer.Serialize(result));

            // Send back the Accounts and FileSystems
            return new OkObjectResult(result);
        }

        private static IList<string> GetContainers(ILogger log, string account, string upn, string principalId)
        {
            var containers = new List<string>();

			var serviceUri = SasConfiguration.GetStorageUri(account);
            var adls = new FileSystemOperations(log, new DefaultAzureCredential(), serviceUri);

			var fileSystems = adls.GetFilesystems();

			// Transition to User Credentials
			var userCred = CredentialHelper.GetUserCredentials(log, principalId);

			Parallel.ForEach(fileSystems, filesystem =>
			{
				var folderOps = new FolderOperations(log, userCred, serviceUri, filesystem.Name);
				var folders = folderOps.GetAccessibleFolders(checkForAny: true);
				if (folders.Count > 0)
					containers.Add(filesystem.Name);
			});

			//foreach(var filesystem in fileSystems)
			//{
			//	var folderOps = new FolderOperations(log, userCred, serviceUri, filesystem.Name);
			//	try
			//	{
			//		var folders = folderOps.GetAccessibleFolders();
			//		//var fd = folderOps.GetFolderDetail(string.Empty);        // User can at least read the root data
			//		//if (fd != null)
			//		if (folders.Count > 0)
			//			containers.Add(filesystem.Name);
			//	}
			//	catch (Exception ex)
			//	{
			//		log.LogTrace($"{principalId} does not have access to {filesystem.Name}");
			//	}
			//}

            return containers;
        }

        private static async Task<IActionResult> FileSystemsPOST(HttpRequest req, ILogger log, string account)
        {
            if (!SasConfiguration.ValidateSharedKey(req, SasConfiguration.ApiKey.FileSystems))
            {
                return new UnauthorizedResult();
            }

            // Extracting body object from the call and deserializing it.
            var tlfp = await GetFileSystemParameters(req, log);
            if (tlfp == null)
                return new BadRequestErrorMessageResult($"{nameof(FileSystemParameters)} is missing.");

            // Add Route Parameters
            tlfp.StorageAcount ??= account;

            // Check Parameters
            string error = null;
            if (Services.Extensions.AnyNull(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode, tlfp.StorageAcount))
                error = $"{nameof(FileSystemParameters)} is malformed.";
            if (tlfp.Owner.Contains("#EXT#"))
                error = "Guest accounts are not supported.";
            if (error != null)
                return new BadRequestErrorMessageResult(error);

            // Setup Azure Credential
            var tokenCredential = new DefaultAzureCredential();

            // Get Blob Owner
            var userOperations = new UserOperations(log, tokenCredential);
            var ownerId = await userOperations.GetObjectIdFromUPN(tlfp.Owner);
            if (ownerId == null)
                return new BadRequestErrorMessageResult("Owner identity not found. Please verify that the Owner is a valid member UPN and that the application has User.Read.All permission in the directory.");

            // Call each of the steps in order and error out if anytyhing fails
            var storageUri = SasConfiguration.GetStorageUri("tlfp.StorageAcount");
            var fileSystemOperations = new FileSystemOperations(log, tokenCredential, storageUri);

            // Create File System
            Result result = await fileSystemOperations.CreateFileSystem(tlfp.FileSystem, tlfp.Owner, tlfp.FundCode);
            if (!result.Success)
                return new BadRequestErrorMessageResult(result.Message);

            // Add Blob Owner
            var roleOperations = new RoleOperations(log, tokenCredential);
            roleOperations.AssignRoles(tlfp.StorageAcount, tlfp.FileSystem, ownerId);

            // Get Root Folder Details
            var folderOperations = new FolderOperations(log, tokenCredential, storageUri, tlfp.FileSystem);
            var folderDetail = folderOperations.GetFolderDetail(String.Empty);

            return new OkObjectResult(folderDetail);
        }

        internal static async Task<FileSystemParameters> GetFileSystemParameters(HttpRequest req, ILogger log)
        {
            string body = string.Empty;
            using (var reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
                if (string.IsNullOrEmpty(body))
                {
                    log.LogError("Body was empty coming from ReadToEndAsync");
                }
            }
            var bodyDeserialized = JsonSerializer.Deserialize<FileSystemParameters>(body);
            return bodyDeserialized;
        }

        private class FileSystemResult
        {
            //[{name: 'adlstorageaccountname', fileSystems: [{name: 'file system name'}]]
            public string Name { get; set; }
            public List<string> FileSystems { get; set; }
        }

        internal class FileSystemParameters
        {
            public string StorageAcount { get; set; }

            public string FileSystem { get; set; }

            public string FundCode { get; set; }

            public string Owner { get; set; }        // Probably will not stay as a string
        }
    }
}
