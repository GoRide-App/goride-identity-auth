using System.ComponentModel.DataAnnotations;
using SRC.Enums;

namespace SRC.Entities;

/// <summary>
/// Local mirror of an Identity Server account, keyed by the opaque WSO2 user id (the
/// <c>sub</c> claim). Deactivation is a soft delete: the row stays so historical trip
/// records that reference <see cref="UserId"/> keep their referential integrity.
/// </summary>
public class UserAccount
{
    [Key]
    public string UserId { get; set; } = null!;
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public DateTime? DeactivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
