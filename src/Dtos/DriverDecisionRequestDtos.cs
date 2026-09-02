using System.ComponentModel.DataAnnotations;

namespace SRC.Dtos;

public class ApproveDriverRequestDto
{
    /// <summary>Optional note recorded with the approval (e.g. "Documents verified against DMT").</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}

public class RejectDriverRequestDto
{
    /// <summary>Why the registration is rejected; shown to the driver so they can fix it.</summary>
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = null!;
}
