using System;
using System.ComponentModel.DataAnnotations;

namespace TexasOperationalPortal.Application.Common.Options;

public sealed class StaffSyncOptions
{
    public const string SectionName = "StaffSync";

    [Required(AllowEmptyStrings = false, ErrorMessage = "StaffSync:SyncKey is required.")]
    [MinLength(32, ErrorMessage = "StaffSync:SyncKey must be at least 32 characters long.")]
    public string SyncKey { get; set; } = null!;
}
