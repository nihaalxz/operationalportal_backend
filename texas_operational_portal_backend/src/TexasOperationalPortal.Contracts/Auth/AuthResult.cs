namespace TexasOperationalPortal.Contracts.Auth;

public sealed record AuthResult(
    LoginResponse Response,
    string RefreshToken
);
