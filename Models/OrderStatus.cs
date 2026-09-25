namespace Mini_Order_Sync_API.Models;

public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Synced = "Synced";
    public const string Failed = "Failed";

    public static readonly string[] ValidStatuses = [Pending, Synced, Failed];

    public static bool IsValid(string status) =>
        ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
}
