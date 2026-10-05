namespace HealthApp.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string Provider { get; set; } = "Local";
    public string Container { get; set; } = "healthapp-media";
    public string ConnectionString { get; set; } = "";
    public string AccountUrl { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public string LocalRoot { get; set; } = "wwwroot/uploads";
    public string PrivateLocalRoot { get; set; } = "App_Data/private-uploads";
}
