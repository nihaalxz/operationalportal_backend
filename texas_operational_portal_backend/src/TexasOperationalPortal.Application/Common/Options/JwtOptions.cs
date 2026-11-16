using System;
using System.ComponentModel.DataAnnotations;

namespace TexasOperationalPortal.Application.Common.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:Key is required.")]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters long.")]
    public string Key { get; set; } = null!;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:Issuer is required.")]
    public string Issuer { get; set; } = null!;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:Audience is required.")]
    public string Audience { get; set; } = null!;

    [Range(1, 168, ErrorMessage = "Jwt:ExpiryHours must be between 1 and 168 hours (7 days).")]
    public int ExpiryHours { get; set; } = 8;
}
