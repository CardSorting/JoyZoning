namespace JoyZoning.Domain.Configuration;

public class HermesOptions
{
    public const string SectionName = "Hermes";

    public string InstallRoot { get; set; } = "/Users/bozoegg/Downloads/diet-hermes-main-master";
    public string Profile { get; set; } = "joyzoning";
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:8642";
    public string DashboardBaseUrl { get; set; } = "http://127.0.0.1:9119";
    public bool AutoStartGateway { get; set; } = true;
    public string? ApiKey { get; set; }
}

public class ControlPlaneOptions
{
    public const string SectionName = "ControlPlane";

    public string ListenUrl { get; set; } = "http://127.0.0.1:9470";
}
