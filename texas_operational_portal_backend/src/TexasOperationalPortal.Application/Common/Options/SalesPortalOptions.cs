using System;
using System.ComponentModel.DataAnnotations;

namespace TexasOperationalPortal.Application.Common.Options;

public sealed class SalesPortalOptions
{
    public const string SectionName = "SalesPortal";

    [Required(AllowEmptyStrings = false, ErrorMessage = "SalesPortal:BaseAddress is required.")]
    [Url(ErrorMessage = "SalesPortal:BaseAddress must be a valid URL.")]
    public string BaseAddress { get; set; } = null!;

    [Required(AllowEmptyStrings = false, ErrorMessage = "SalesPortal:ApiKey is required.")]
    [MinLength(16, ErrorMessage = "SalesPortal:ApiKey must be at least 16 characters long for security.")]
    public string ApiKey { get; set; } = null!;
}
