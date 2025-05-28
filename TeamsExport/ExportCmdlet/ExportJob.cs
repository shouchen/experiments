using System.Text.Json;

namespace ExportCmdlet
{
    public class ExportJob
    {
        public ExportJob(string? user, string? channel, CallType? callType, ArtifactType? artifactType, DateTime? startTime, DateTime? endTime)
        {
            User = user;
            Channel = channel;
            CallType = callType;
            ArtifactType = artifactType;
            StartTime = startTime;
            EndTime = endTime;
        }

        public void Start(string clientId, string tenantId, string clientSecret)
        {
            var handler = new AuthenticatedHttpClientHandler(clientId, tenantId, clientSecret)
            {
                InnerHandler = new HttpClientHandler()
            };

            client = new HttpClient(handler);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");

            System.Console.Write("[");
            VisitSharePointAsync(true).GetAwaiter().GetResult();
            VisitOneDriveAsync(true).GetAwaiter().GetResult();
            System.Console.WriteLine("\n]\n");
        }

        private void OutputArtifact(JsonElement item)
        {
            System.Console.WriteLine(firstObject ? "" : ",");
            firstObject = false;

            System.Console.WriteLine("  {");
            System.Console.WriteLine($"    \"name\" : \"{item.GetProperty("name").GetString()}\"");
            System.Console.WriteLine($"    \"mime-type\" : \"{item.GetProperty("file").GetProperty("mimeType").GetString()}\"");
            System.Console.WriteLine($"    \"downloadUrl\" : \"{item.GetProperty("@microsoft.graph.downloadUrl").GetString()}\"");
            System.Console.WriteLine($"    \"lastModifiedDateTime\" : \"{item.GetProperty("lastModifiedDateTime").GetString()}\"");
            System.Console.Write("  }");
        }

        private async Task VisitFileSystemFolderAsync(string driveId, string driveItemId, bool recurse)
        {
            var uri = $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{driveItemId}/children";
            var response = await client.GetAsync(uri);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("value");

            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("file", out _))
                {
                    item.TryGetProperty("lastModifiedDateTime", out var value);
                    if (value.TryGetDateTime(out var lastModifiedDateTime))
                    {
                        if ((StartTime.HasValue && lastModifiedDateTime < StartTime) ||
                            (EndTime.HasValue && lastModifiedDateTime > EndTime))
                        {
                            continue;
                        }
                    }

                    OutputArtifact(item);
                }
                else if (item.TryGetProperty("folder", out var folder) &&
                         folder.TryGetProperty("childCount", out var childCount) &&
                         childCount.GetInt32() > 0 && recurse)
                {
                    // Recurse into non-empty subfolders
                    var subfolderId = item.GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(subfolderId))
                    {
                        await VisitFileSystemFolderAsync(driveId, subfolderId, recurse);
                    }
                }
            }
        }
        private async Task VisitOneDriveAsync(bool full)
        {
            // Get all users
            var usersResponse = await client.GetAsync("https://graph.microsoft.com/v1.0/users");
            usersResponse.EnsureSuccessStatusCode();

            var usersJson = await usersResponse.Content.ReadAsStringAsync();
            using var usersDoc = JsonDocument.Parse(usersJson);
            var users = usersDoc.RootElement.GetProperty("value");

            foreach (var user in users.EnumerateArray())
            {
                var userId = user.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(userId))
                    continue;

                // Get the user's OneDrive root
                var rootResponse = await client.GetAsync($"https://graph.microsoft.com/v1.0/users/{userId}/drive/root");
                rootResponse.EnsureSuccessStatusCode();
                var rootJson = await rootResponse.Content.ReadAsStringAsync();
                using var rootDoc = JsonDocument.Parse(rootJson);
                var root = rootDoc.RootElement;
                var rootId = root.GetProperty("id").GetString();

                if (full)
                {
                    if (root.TryGetProperty("folder", out var folder) &&
                        folder.TryGetProperty("childCount", out var childCount) &&
                        childCount.GetInt32() > 0)
                    {
                        // Recursively visit all folders in the user's drive
                        await VisitFileSystemFolderAsync(
                            root.GetProperty("parentReference").GetProperty("driveId").GetString() ?? "",
                            rootId ?? "",
                            true);
                    }
                }
                else
                {
                    // Visit specific artifact folders if they exist
                    var artifactFolders = new[] { "Recordings", "Meetings", "Whiteboards" };
                    foreach (var folderName in artifactFolders)
                    {
                        var folderResponse = await client.GetAsync(
                            $"https://graph.microsoft.com/v1.0/users/{userId}/drive/root:/{folderName}");
                        if (!folderResponse.IsSuccessStatusCode)
                            continue;

                        var folderJson = await folderResponse.Content.ReadAsStringAsync();
                        using var folderDoc = JsonDocument.Parse(folderJson);
                        var folderRoot = folderDoc.RootElement;
                        if (folderRoot.TryGetProperty("folder", out var subFolder) &&
                            subFolder.TryGetProperty("childCount", out var subChildCount) &&
                            subChildCount.GetInt32() > 0)
                        {
                            await VisitFileSystemFolderAsync(
                                folderRoot.GetProperty("parentReference").GetProperty("driveId").GetString() ?? "",
                                folderRoot.GetProperty("id").GetString() ?? "",
                                false);
                        }
                    }
                }
            }
        }
        private async Task VisitSharePointAsync(bool full)
        {
            // Get all sites across all collections
            var sitesResponse = await client.GetAsync("https://graph.microsoft.com/v1.0/sites?search=*");
            sitesResponse.EnsureSuccessStatusCode();
            var sitesJson = await sitesResponse.Content.ReadAsStringAsync();
            using var sitesDoc = JsonDocument.Parse(sitesJson);
            var sites = sitesDoc.RootElement.GetProperty("value");

            foreach (var site in sites.EnumerateArray())
            {
                var siteId = site.GetProperty("id").GetString();
                if (string.IsNullOrEmpty(siteId))
                    continue;

                // Get all drives (document libraries) in the site
                var drivesResponse = await client.GetAsync($"https://graph.microsoft.com/v1.0/sites/{siteId}/drives");
                drivesResponse.EnsureSuccessStatusCode();
                var drivesJson = await drivesResponse.Content.ReadAsStringAsync();
                using var drivesDoc = JsonDocument.Parse(drivesJson);
                var drives = drivesDoc.RootElement.GetProperty("value");

                if (full)
                {
                    foreach (var drive in drives.EnumerateArray())
                    {
                        var driveId = drive.GetProperty("id").GetString();
                        if (string.IsNullOrEmpty(driveId))
                            continue;

                        // Get the root folder of the drive
                        var rootResponse = await client.GetAsync($"https://graph.microsoft.com/v1.0/drives/{driveId}/root");
                        rootResponse.EnsureSuccessStatusCode();
                        var rootJson = await rootResponse.Content.ReadAsStringAsync();
                        using var rootDoc = JsonDocument.Parse(rootJson);
                        var root = rootDoc.RootElement;
                        var rootId = root.GetProperty("id").GetString();

                        if (root.TryGetProperty("folder", out var folder) &&
                            folder.TryGetProperty("childCount", out var childCount) &&
                            childCount.GetInt32() > 0)
                        {
                            await VisitFileSystemFolderAsync(driveId, rootId ?? "", full);
                        }
                    }
                }
                else
                {
                    // TODO: Handle subset logic if needed
                }
            }
        }

        private string? User { get; }
        private string? Channel { get; }
        private CallType? CallType { get; }
        private ArtifactType? ArtifactType { get; }
        private DateTime? StartTime { get; }
        private DateTime? EndTime { get; }

        private bool firstObject = true;
        private HttpClient client = new HttpClient();
    }
}
