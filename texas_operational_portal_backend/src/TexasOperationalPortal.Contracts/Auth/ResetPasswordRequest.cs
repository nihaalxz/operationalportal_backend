namespace TexasOperationalPortal.Contracts.Auth;

public sealed record ResetPasswordRequest(string CurrentPassword, string NewPassword);
