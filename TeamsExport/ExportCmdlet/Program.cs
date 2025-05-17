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

        public DateTime? StartDate { get; set; }
        [Parameter()]

        public DateTime? EndDate { get; set; }

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
            Console.WriteLine("    -StartDate \"2025-04-29\"");
            Console.WriteLine("    -EndDate \"2025-04-29\"");
            Console.WriteLine("    -Full");
            Console.WriteLine();
            Console.WriteLine("All parameters are optional. If no parameters are specified, all standard SharePoint locations are");
            Console.WriteLine("searched and any artifact metadata is returned. The actual artifacts can be downloaded from the URLs");
            Console.WriteLine("contained in the response. The -Full switch will also search SharePoint outside the standard locations");
            Console.WriteLine("and will also find any artifacts that have been moved. The other parameters filter the result. User");
            Console.WriteLine("specifies the organizer or initiator, Channel exports the artifacts from the specificed channel.");
            Console.WriteLine("CallType and Artifacts are further filters. StartDate omits artifact metadata that is prior to this");
            Console.WriteLine("date. EndDate omits artifact metadata that is after this date.");
            Console.WriteLine();
        }

        protected override void ProcessRecord()
        {
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

            var job = new ExportJob(User, Channel, CallType, ArtifactType, StartDate, EndDate);
            job.Start();
        }
    }
}
