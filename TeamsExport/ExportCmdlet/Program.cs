using System.Management.Automation;

// Import-Module ExportCmdlet when running in PowerShell

namespace ExportCmdlet
{
    public enum CallType
    {
        Channel,
        NonChannelScheduled,
        Calls
    }

    public enum ArtifactType
    {
        RecordingTranscript,
        Notes,
        Whiteboard
    }


    [Cmdlet(VerbsCommon.Get, "TeamsArtifacts")]
    public class GetTeamsArtifactsCommand : PSCmdlet
    {
        [Parameter()]
        public string? User { get; set; }

        [Parameter()]
        public string? Channel { get; set; }
        [Parameter()]

        public CallType? CallType { get; set; }
        [Parameter()]

        public ArtifactType? ArtifactType { get; set; }
        [Parameter()]

        public DateTime? StartTime { get; set; }
        [Parameter()]

        public DateTime? EndTime { get; set; }

        [Parameter()]
        public SwitchParameter Full { get; set; }

        [Parameter()]
        public SwitchParameter Usage { get; set; }

        void ShowUsage()
        {
            Console.WriteLine("Get-TeamsArtifacts");
            Console.WriteLine("    -Usage");
            Console.WriteLine();
            Console.WriteLine("Get-TeamsArtifacts");
            Console.WriteLine("    -User foo@contoso.com");
            Console.WriteLine("    -Channel \"Contoso Channel\"");
            Console.WriteLine("    -CallType { Channel | NonChannelScheduled | Calls }");
            Console.WriteLine("    -ArtifactType { RecordingTranscript | Notes | Whiteboard }");
            Console.WriteLine("    -StartTime \"2025-04-29\"");
            Console.WriteLine("    -EndTime \"2025-04-29\"");
            Console.WriteLine("    -Full");
            Console.WriteLine();
            Console.WriteLine("All parameters are optional. If no parameters are specified, all standard SharePoint locations are");
            Console.WriteLine("searched and any artifact metadata is returned. The actual artifacts can be downloaded from the URLs");
            Console.WriteLine("contained in the response. The -Full switch will also search SharePoint outside the standard locations");
            Console.WriteLine("and will also find any artifacts that have been moved. The other parameters filter the result. User");
            Console.WriteLine("specifies the organizer or initiator, Channel exports the artifacts from the specificed channel.");
            Console.WriteLine("CallType and Artifacts are further filters. StartTime omits artifact metadata that is prior to this");
            Console.WriteLine("date and time. EndTime omits artifact metadata that is after this date and time.");
            Console.WriteLine();
        }

        protected override void ProcessRecord()
        {
            if (User != null)
                throw new InvalidOperationException("-User parameter not implemented yet.");
            if (Channel != null)
                throw new InvalidOperationException("-Channel parameter not implemented yet.");
            if (CallType != null)
                throw new InvalidOperationException("-CallType parameter not implemented yet.");
            if (ArtifactType != null)
                throw new InvalidOperationException("-ArtifactType parameter not implemented yet.");

            if (Usage)
            {
                if (this.MyInvocation.BoundParameters.Count > 1)
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException("Can't combine Usage with other parameters"),
                        "UsageError",
                        ErrorCategory.InvalidArgument,
                        null));
                    return;
                }

                ShowUsage();
                return;
            }

            var clientId = Environment.GetEnvironmentVariable("EXPORT_TOOL.CLIENT_ID");
            var tenantId = Environment.GetEnvironmentVariable("EXPORT_TOOL.TENANT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("EXPORT_TOOL.CLIENT_SECRET");

            if (string.IsNullOrEmpty(clientId))
                throw new InvalidOperationException("EXPORT_TOOL.CLIENT_ID environment variable not set.");
            if (string.IsNullOrEmpty(tenantId))
                throw new InvalidOperationException("EXPORT_TOOL.TENANT_ID environment variable not set.");
            if (string.IsNullOrEmpty(clientSecret))
                throw new InvalidOperationException("EXPORT_TOOL.CLIENT_SECRET environment variable not set.");

            StartTime = StartTime?.ToUniversalTime();
            EndTime = EndTime?.ToUniversalTime();

            var job = new ExportJob(User, Channel, CallType, ArtifactType, StartTime, EndTime);
            job.Start(clientId, tenantId, clientSecret);
        }
    }
}
