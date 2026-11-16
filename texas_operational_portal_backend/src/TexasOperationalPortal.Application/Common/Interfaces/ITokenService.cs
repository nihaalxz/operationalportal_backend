using System.Collections.Generic;
using TexasOperationalPortal.Domain.Entities;

namespace TexasOperationalPortal.Application.Common.Interfaces;

public sealed record TokenResult(string AccessToken, string RefreshToken);

public interface ITokenService
{
    TokenResult GenerateTokens(OperationalUser user, List<string> permissions);
}
