using System.Reflection.PortableExecutable;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace ExportCmdlet
{
    public class ExportJob
    {
        public ExportJob(string? user, string? channel, CallType? callType, ArtifactType? artifactType, DateTime? startDate, DateTime? endDate)
        {
            User = user;
            Channel = channel;
            CallType = callType;
            ArtifactType = artifactType;
            StartDate = startDate;
            EndDate = endDate;
        }

        public void Start()
        {
            SetAuthHeadersAsync().GetAwaiter().GetResult();
            System.Console.Write("[");
            VisitSharePointAsync(true).GetAwaiter().GetResult();
            VisitOneDriveAsync(true).GetAwaiter().GetResult();
            System.Console.WriteLine("\n]\n");
        }

        private async Task SetAuthHeadersAsync()
        {
            var clientId = Environment.GetEnvironmentVariable("EXPORT_TOOL.CLIENT_ID");
            var tenantId = Environment.GetEnvironmentVariable("EXPORT_TOOL.TENANT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("EXPORT_TOOL.CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId))
                throw new InvalidOperationException("EXPORT_TOOL.CLIENT_ID environment variable not set.");
            if (string.IsNullOrEmpty(tenantId))
                throw new InvalidOperationException("EXPORT_TOOL.TENANT_ID environment variable not set.");
            if (string.IsNullOrEmpty(clientSecret))
                throw new InvalidOperationException("EXPORT_TOOL.CLIENT_SECRET environment variable not set.");

            var body = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "scope", "https://graph.microsoft.com/.default" },
                { "grant_type", "client_credentials" }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(body)
            };

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var tokenType = doc.RootElement.GetProperty("token_type").GetString();
            var accessToken = doc.RootElement.GetProperty("access_token").GetString();

            // Proactively refresh before this many seconds and/or do it when a request comes back with "invalid_token".
            // Simplest is the latter since token like is long. If I can't get a new token, I can do exponential backoff
            // (1s, 2s, 4s...) and then give up after some number of tries.
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"{tokenType} {accessToken}");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
        }

        private void OutputArtifact(JsonElement item)
        {
            System.Console.WriteLine(firstObject ? "" : ",");
            firstObject = false;

            System.Console.WriteLine("  {");
            System.Console.WriteLine($"    \"name\" : \"{item.GetProperty("name").GetString()}\"");
            System.Console.WriteLine($"    \"mime-type\" : \"{item.GetProperty("file").GetProperty("mimeType").GetString()}\"");
            System.Console.WriteLine($"    \"downloadUrl\" : \"{item.GetProperty("@microsoft.graph.downloadUrl").GetString()}\"");
            System.Console.Write("  }");
        }

        private async Task VisitFileSystemFolderAsync(
            string driveId,
            string driveItemId,
            bool recurse)
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
        private DateTime? StartDate { get; }
        private DateTime? EndDate { get; }

        private bool firstObject = true;
        private HttpClient client = new HttpClient();
    }
}
