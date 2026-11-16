namespace TexasOperationalPortal.Contracts.Notifications;

public static class SignalRGroups
{
    public const string Accounts = "role:accounts";
    public const string Sales = "role:sales";
    public const string Operations = "role:operations";
    public const string GlobalAll = "global:all";

    public static string User(string userId) => $"user:{userId}";
    public static string Role(string roleName) => $"role:{roleName.ToLowerInvariant()}";
    public static string CustomerLedger(long headId) => $"ledger:customer:{headId}";
    public static string Wallet(long headId) => $"wallet:{headId}";
    public static string Booking(long bookingId) => $"booking:{bookingId}";
    public static string Dashboard(string portal) => $"dashboard:{portal.ToLowerInvariant()}";
}
