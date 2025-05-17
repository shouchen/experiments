################################################################################
# Microsoft Teams Artifact Export Tool
################################################################################

param(
    [string]$User,
    [string]$Channel,
    [string]$CallType,
    [string]$ArtifactType,
    [string]$StartDate,
    [string]$EndDate,
    [switch]$Full,
    [switch]$Usage
)

enum CallType {
    Channel
    NonChannelScheduled
    Calls
    All
}

enum ArtifactType {
    RecordingTranscript
    Notes
    Whiteboard
    All
}

$output
$firstObject = $true

function ValidateParameters {
    param(
        [hashtable]$params
    )

    if ($Usage -and ($params.Count -ne 1)) {
        Write-Error "Can't combine Usage with other parameters" -ErrorAction Stop
    }

    if ($CallType -ne "" -and $CallType -ne "") {
        if ($CallType -in [Enum]::GetNames([CallType])) {
            $CallType = [CallType]::$CallType
        } else {
            Write-Error "Invalid CallType value." -ErrorAction Stop
        }
    }

    if ($ArtifactType -ne "" -and $ArtifactType -ne "") {
        if ($ArtifactType -in [Enum]::GetNames([ArtifactType])) {
            $ArtifactType = [ArtifactType]::$ArtifactType
        } else {
            Write-Error "Invalid ArtifactType value." -ErrorAction Stop
        }
    }

    if ($null -ne $StartDate -and $StartDate -ne "") {
        [datetime]$temp = [DateTime]::Now

        if ([datetime]::TryParse($StartDate, [ref]$temp)) {
            $StartDate = $temp;
        } else {
            Write-Error "Invalid StartDate." -ErrorAction Stop
        }
    }

    if ($null -ne $EndDate -and $EndDate -ne "") {
        [datetime]$temp = [DateTime]::Now

        if ([datetime]::TryParse($EndDate, [ref]$temp)) {
            $EndDate = $temp;
        } else {
            Write-Error "Invalid EndDate." -ErrorAction Stop
        }
    }
}

function Show-Usage {
    Write-Host 'Get-TeamsArtifacts'
    Write-Host '    -Usage'
    Write-Host 
    Write-Host 'Get-TeamsArtifacts'
    Write-Host '    -User foo@contoso.com'
    Write-Host '    -Channel "Contoso Channel"'
    Write-Host '    -CallType { Channel | NonChannelScheduled | Calls }'
    Write-Host '    -ArtifactType { RecordingTranscript | Notes | Whiteboard }'
    Write-Host '    -StartDate "2025-04-29"'
    Write-Host '    -EndDate "2025-04-29"'
    Write-Host '    -Full'
    Write-Host
    Write-Host 'All parameters are optional. If no parameters are specified, all standard SharePoint locations are'
    Write-Host 'searched and any artifact metadata is returned. The actual artifacts can be downloaded from the URLs'
    Write-Host 'contained in the response. The -Full switch will also search SharePoint outside the standard locations'
    Write-Host 'and will also find any artifacts that have been moved. The other parameters filter the result. User'
    Write-Host 'specifies the organizer or initiator, Channel exports the artifacts from the specificed channel.'
    Write-Host 'CallType and Artifacts are further filters. StartDate omits artifact metadata that is prior to this'
    Write-Host 'date. EndDate omits artifact metadata that is after this date.'
    Write-Host
    Exit 0
}

function Get-AuthHeaders {
    $clientId = ${Env:EXPORT_TOOL.CLIENT_ID}
    $tenantId = ${Env:EXPORT_TOOL.TENANT_ID}
    $clientSecret = ${Env:EXPORT_TOOL.CLIENT_SECRET}

    if ($null -eq $clientId) {
        Write-Error "EXPORT_TOOL.CLIENT_ID environment variable not set." -ErrorAction Stop
    }

    if ($null -eq $tenantId) {
        Write-Error "EXPORT_TOOL.TENANT_ID environment variable not set" -ErrorAction Stop
    }

    if ($null -eq $clientSecret) {
        Write-Error "EXPORT_TOOL.CLIENT_SECRET environment variable not set" -ErrorAction Stop
    }

    $body = @{
        client_id     = $clientId
        client_secret = $clientSecret
        scope         = "https://graph.microsoft.com/.default"
        grant_type    = "client_credentials"
    }

    $tokenResponse = Invoke-RestMethod -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"
    $accessToken = $tokenResponse.access_token
    # Note that tokenResponse.expires_in and ext_expires_in is in seconds
    
    $localHeaders = @{
        "Authorization" = "Bearer $accessToken"
        "Content-Type"  = "application/json"
    }

    # TODO: Handle expirations as this is 1 hour.
    private async Task VisitSharePointAsync(
        bool full,
        Dictionary<string, string> headers,
        Func<JsonElement, Task> outputArtifact)
    {
        using var client = new HttpClient();
        foreach (var header in headers)
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

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
                        await VisitFileSystemFolderAsync(
                            driveId,
                            rootId ?? "",
                            full,
                            headers,
                            outputArtifact);
                    }
                }
            }
            else
            {
                // TODO: Handle subset logic if needed
            }
        }
    }
    return $localHeaders
}

function OutputArtifact {
    param (
        $item
    )

    Write-Host $item.Name

    if ($firstObject) {
        $global:firstObject = $false
        $output.WriteLine()
    } else {
        $output.WriteLine(",")
    }

    $output.WriteLine("  {")
    $output.WriteLine("    ""name"" : ""$($item.name)""")
    $output.WriteLine("    ""mime-type"" : ""$($item.file.mimeType)""")
    $output.WriteLine("    ""downloadUrl"" : ""$($item.'@microsoft.graph.downloadUrl')""")
    $output.Write("  }")
}

function VisitFileSystemFolder {
    param (
        [string]$driveID,
        [string]$driveItemID,
        [bool]$recurse
    )

    # This is called with a folder ID, so get all the items in this folder.
    $uri = "https://graph.microsoft.com/v1.0/drives/$($driveID)/items/$($driveItemID)/children"
    $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
    $items = $response.value

    foreach ($item in $items) {
        if ($item.file) {
            # If file qualifies, output it.
            OutputArtifact $item
        } elseif ($item.folder.childCount -gt 0) {
            # Visit all non-empty subfolders
            VisitFileSystemFolder $driveID $item.Id $recurse
        }
    }
}

function VisitOneDrive {
    param($Full)

    $response = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/users" -Headers $headers -Method Get
    $users = $response.value
    
    foreach ($user in $users) {
        #Write-Host "$($user.displayName)"

        $uri = "https://graph.microsoft.com/v1.0/users/$($user.id)/drive/root"
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        $root = $response.id
        
        if ($Full) {
            if ($response.folder.childCount -gt 0) {
                VisitFileSystemFolder "NEED DRIVE ID" $root $true
            }
        } else {
            # Transcript/Recordings = Recordings folder, *.mp4 (or check metadata)
            $response = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/users/$($user.id)/drive/root:/Recordings"
            if (response.folder.childCount -gt 0) {
                VisitFileSystemFolder "NEED DRIVE ID" $response.id $false
            }   
    
            # Notes = Meetings folder, *.loop (or check metadata)
            $response = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/me/drive/root:/Meetings"
            if (response.folder.childCount -gt 0) {
                VisitFileSystemFolder "NEED DRIVE ID" $response.id $false
            }   
    
            # WB = Whiteboards folder, *.whiteboard (or check metadata)
            $response = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/me/drive/root:/Whiteboards"
            if (response.folder.childCount -gt 0) {
                VisitFileSystemFolder "NEED DRIVE ID" $response.id $false
            }   
        }
    }
}

function VisitSharePoint {
    param($Full)

    # Get all the sites across all collections
    $response = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/sites?search=*" -Headers $headers -Method Get
    $sites = $response.value

    foreach ($site in $sites) {
        # Get all the drives (document libraries) in the site
        $response = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/sites/$($site.id)/drives" -Headers $headers -Method Get
        $drives = $response.value

        if ($Full) {
            foreach ($drive in $drives) {
                # Get the root folder of the drive
                $uri = "https://graph.microsoft.com/v1.0/drives/$($drive.id)/root"
                $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get

                if ($response.folder.childCount -gt 0) {
                    VisitFileSystemFolder $drive.id $response.id $Full
                }   
            }
        } else {
            # TODO: Handle subset
        }
    }
}

################################################################################
# Main Program
################################################################################

ValidateParameters $PSBoundParameters
Write-Host 

if ($Usage) {
    Show-Usage
    Exit 0
}

$headers = Get-AuthHeaders

$output = [System.IO.StreamWriter]::new("D:/github/experiments/PowerShellExportScript/TeamsArtifacts.json", $false)
$output.Write("[")

VisitSharePoint $true
VisitOneDrive $true

$output.WriteLine()
$output.WriteLine("]")
$output.Close()

Write-Host