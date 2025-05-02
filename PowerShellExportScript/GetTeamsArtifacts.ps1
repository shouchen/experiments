# # Install Microsoft Graph module if not already installed
# Install-Module Microsoft.Graph -Scope CurrentUser
#
# Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

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

function VisitSharePointFiles {
    param($example)

    # Connect to Microsoft Graph    Connect-MgGraph -Scopes "Sites.Read.All", "Files.Read.All"

    # Retrieve all sites    $sites = Get-MgSite -Search "*"
    foreach ($site in $sites) {        Write-Output "Site Name: $($site.DisplayName) - URL: $($site.WebUrl)"            # Get document libraries for the site        $drives = Get-MgSiteDrive -SiteId $site.Id            foreach ($drive in $drives) {            Write-Output "   Document Library: $($drive.Name)"                    # Get top-level items in the library            $items = Get-MgDriveRootChild -DriveId $drive.Id
            foreach ($item in $items) {                if ($item.Folder) {                    Write-Output "      Folder: $($item.Name)"                    Get-FilesAndFoldersRecursive -DriveId $drive.Id -ItemId $item.Id -Indent "         "                } else {                    Write-Output "      File: $($item.Name)"                }            }        }    }

    # Recursive function to retrieve all nested folders and files    function Get-FilesAndFoldersRecursive {        param (            [string]$DriveId,            [string]$ItemId,            [string]$Indent        )

        $subItems = Get-MgDriveItemChild -DriveId $DriveId -DriveItemId $ItemId            foreach ($subItem in $subItems) {            if ($subItem.Folder) {                Write-Output "$Indent Folder: $($subItem.Name)"                Get-FilesAndFoldersRecursive -DriveId $DriveId -ItemId $subItem.Id -Indent "$Indent   "            } else {                Write-Output "$Indent File: $($subItem.Name)"            }        }    }
}

function VisitOneDriveFiles {
    param($Full)

    # Connect to Microsoft Graph
    Connect-MgGraph -Scopes "User.Read.All", "Sites.Read.All", "Files.Read.All"

    # Function to recursively fetch folders
    function Get-FoldersRecursive {
        param (
            [string]$DriveId,
            [string]$ParentFolderId,
            [string]$Indent
        )

        $items = Get-MgDriveItemChild -DriveId $DriveId -DriveItemId $ParentFolderId

        foreach ($item in $items) {
            if ($item.Folder) { # If it's a folder
                Write-Output "$Indent Folder: $($item.Name)"
                Get-FoldersRecursive -DriveId $DriveId -ParentFolderId $item.Id -Indent "$Indent   "
            }
        }
    }

    # Get all users in the tenant
    #$users = Get-MgUser -All
    #
    #foreach ($user in $users) {
    #    $drive = Get-MgUserDrive -UserId $user.Id -ErrorAction SilentlyContinue
    #    if ($drive) {
    #        Write-Output "User: $($user.DisplayName) - OneDrive URL: $($drive.WebUrl)"
    #    
    #        # Get root folder and start recursion
    #        Get-FoldersRecursive -DriveId $drive.Id -ParentFolderId $drive.Id -Indent "   "
    #    }
    #}

    # Start with just myself (as a test)
    if ($Full) {
        Get-FoldersRecursive -DriveId "me" -ParentFolderId "root" -Indent "   "
    } else {
        # TODO: Go only through well-known folders
        # Get-MgDriveItemByPath -DriveId "me" -Path "Documents/Projects/MyFolder"

        $path = "Recordings"
        $recordingsFolder = Invoke-MgGraphRequest -Method GET -Uri "https://graph.microsoft.com/v1.0/me/drive/root:/$path"
        #$recordingsFolder = Get-MgDriveItemByPath -DriveId "me" -Path "Recordings"

        Get-FoldersRecursive -DriveId "me" -ParentFolderId $recordingsFolder -Indent "   "

        # Notes = Meetings folder, *.loop (or check metadata)
        # WB = Whiteboards folder, *.whiteboard (or check metadata)
        # Transcript/Recordings = Recordings folder, *.mp4 (or check metadata)
    }
}

################################################################################
# Validate parameters and re-assign correct type.
################################################################################

if ($Usage -and ($PSBoundParameters.Count -ne 1 -or $args.Count -ne 0)) {
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

if ($StartDate -ne $null -and $StartDate -ne "") {
    [datetime]$temp = [DateTime]::Now

    if ([datetime]::TryParse($StartDate, [ref]$temp)) {
        $StartDate = $temp;
    } else {
        Write-Error "Invalid StartDate." -ErrorAction Stop
    }
}

if ($EndDate -ne $null -and $EndDate -ne "") {
    [datetime]$temp = [DateTime]::Now

    if ([datetime]::TryParse($EndDate, [ref]$temp)) {
        $EndDate = $temp;
    } else {
        Write-Error "Invalid EndDate." -ErrorAction Stop
    }
}

################################################################################
# Parameters are OK. Display usage if that's what was asked for.
################################################################################

Write-Host 

if ($Usage) {
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

################################################################################
# Parameters are OK. Do the actual extraction.
################################################################################

# Import required modules

#Write-Host "Importing Microsoft Graph... (full graph may take ~5 minutes the first time)"
#Import-Module Microsoft.Graph

Write-Host "Importing Microsoft Graph... (may take a minute the first time)"
Import-Module Microsoft.Graph.Files
Import-Module Microsoft.Graph.Sites

Write-Host "Done Loading."
Write-Host 

#ConneVisitSharePointFiles $Full
VisitOneDriveFiles $Full