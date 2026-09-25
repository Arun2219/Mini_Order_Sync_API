namespace Mini_Order_Sync_API.Configuration;

public class ErpSettings
{
    public const string SectionName = "ErpSettings";

    public int FailureRatePercentage { get; set; } = 20;
    public int MaxRetryAttempts { get; set; } = 3;
    public int WorkerPollingIntervalSeconds { get; set; } = 10;
}
