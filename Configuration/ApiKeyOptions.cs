namespace Mini_Order_Sync_API.Configuration;

public class ApiKeyOptions
{
    public const string SectionName = "ApiKeySettings";

    public string HeaderName { get; set; } = "X-Api-Key";
    public string ApiKey { get; set; } = string.Empty;
}
