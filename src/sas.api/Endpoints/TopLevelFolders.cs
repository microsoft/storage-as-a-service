using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using sas.api.Services;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace sas.api
{
    public static class TopLevelFolders
    {
        [FunctionName("TopLevelFoldersGET")]
        public static IActionResult TopLevelFoldersGET(
            [HttpTrigger(AuthorizationLevel.Function, "GET", Route = "TopLevelFolders/{account}/{filesystem}/{user?}")]
            HttpRequest req, string account, string filesystem, string user, ILogger log)
        {
            // Check for logged in user
            ClaimsPrincipal claimsPrincipal;

            try
            {
                claimsPrincipal = UserOperations.GetClaimsPrincipal(req);
                if (Extensions.AnyNull(claimsPrincipal, claimsPrincipal.Identity))
                    return new BadRequestErrorMessageResult("Call requires an authenticated user.");
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message);
                return new BadRequestErrorMessageResult("Unable to authenticate user.");
            }

            var authenticatedUser = claimsPrincipal.Identity.Name;

            // TODO: Review for security. This seems to allow any authenticated users to pass another user's UPN and retrieve the folders they have access to?
            // Perhaps acceptable if using an "admin" role
            if (user == null)
                user = authenticatedUser;

            // Find out user who is calling
            // TODO: Account for different clouds: don't hardcode the domain
            // TODO: Call helper function to form storage URI
            var storageUri = new Uri($"https://{account}.dfs.core.windows.net");
            var folderOperations = new FolderOperations(storageUri, filesystem, log);
            var folders = folderOperations.GetAccessibleFolders(user);
            return new OkObjectResult(folders.OrderBy(f => f.URI).ToList());
        }

        [FunctionName("TopLevelFoldersPOST")]
        public static async Task<IActionResult> TopLevelFoldersPOST(
                [HttpTrigger(AuthorizationLevel.Function, "POST", Route = "TopLevelFolders/{account}/{filesystem}")]
                HttpRequest req, string account, string filesystem, ILogger log)
        {
            //Extracting body object from the call and deserializing it.
            var tlfp = await GetTopLevelFolderParameters(req, log);
            if (tlfp == null)
                return new BadRequestErrorMessageResult($"{nameof(TopLevelFolderParameters)} is missing.");

            // Add Route Parameters
            tlfp.StorageAcount ??= account;
            tlfp.FileSystem ??= filesystem;

            // Check Parameters
            string error = null;
            if (Extensions.AnyNull(tlfp.FileSystem, tlfp.Folder, tlfp.FolderOwner, tlfp.FundCode, tlfp.StorageAcount))
                error = $"{nameof(TopLevelFolderParameters)} is malformed.";

            // Call each of the steps in order and error out if anytyhing fails
            var storageUri = new Uri($"https://{tlfp.StorageAcount}.dfs.core.windows.net");
            var fileSystemOperations = new FileSystemOperations(storageUri, log);
            var folderOperations = new FolderOperations(storageUri, tlfp.FileSystem, log);

            Result result = null;
            result = await fileSystemOperations.AddsFolderOwnerToContainerACLAsExecute(tlfp.FileSystem, tlfp.FolderOwner);
            if (!result.Success)
                return new BadRequestErrorMessageResult(result.Message);

			result = await folderOperations.CreateNewFolder(tlfp.Folder);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);
			result = await folderOperations.AddMetaData(tlfp.Folder, tlfp.FundCode, tlfp.FolderOwner);
			if (!result.Success)
				return new BadRequestErrorMessageResult(result.Message);

            result = await folderOperations.AssignFullRwx(tlfp.Folder, tlfp.FolderOwner);
            if (!result.Success)
                return new BadRequestErrorMessageResult(result.Message);

            var folderDetail = folderOperations.GetFolderDetail(tlfp.Folder);

            return new OkObjectResult(folderDetail);
        }

        internal static async Task<TopLevelFolderParameters> GetTopLevelFolderParameters(HttpRequest req, ILogger log)
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
            var bodyDeserialized = JsonConvert.DeserializeObject<TopLevelFolderParameters>(body);
            return bodyDeserialized;
        }

        internal class TopLevelFolderParameters
        {
            public string StorageAcount { get; set; }

            public string FileSystem { get; set; }

            public string Folder { get; set; }

            public string FundCode { get; set; }

            public string FolderOwner { get; set; }        // Probably will not stay as a string
        }
    }
}
