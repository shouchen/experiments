using ExportCmdlet;

// This test app shows how a service (such as Tags Service) could invoke the same busines logic that
// the cmdlet uses. It is not a replacement for the cmdlet, but rather a demonstration of how to use the
// ExportJob class directly in a C# application.

public class Program
{
    public static void Main(string[] args)
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

        string? user = null, channel = null;
        CallType? callType = null;
        ArtifactType? artifactType = null;
        DateTime startTime = DateTime.MinValue, endTime = DateTime.MaxValue;

        var job = new ExportJob(user, channel, callType, artifactType, startTime, endTime);
        job.Start(clientId, tenantId, clientSecret);
    }
}
