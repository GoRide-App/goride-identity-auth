using System.ComponentModel.DataAnnotations;
using SRC.Enums;

namespace SRC.Dtos;

public class UpdateDriverStatusRequestDto
{
    [Required]
    public DriverStatus Status { get; set; }

    [Required]
    public string Reason { get; set; } = null!;
}
