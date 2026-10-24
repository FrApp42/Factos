using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Factos.Data;

// Identity's user plus InvoiceShelf's own `users` columns. int key: InvoiceShelf
// exposes integer ids in its API payloads, so the key type is part of the contract.
[Table("users")]
public class ApplicationUser : IdentityUser<int>
{
    [MaxLength(255)] public string Name { get; set; } = "";
    [MaxLength(255)] public string? Phone { get; set; }
    [MaxLength(255)] public string Role { get; set; } = "user";
    [MaxLength(255)] public string? FacebookId { get; set; }
    [MaxLength(255)] public string? GoogleId { get; set; }
    [MaxLength(255)] public string? GithubId { get; set; }
    [MaxLength(255)] public string? ContactName { get; set; }
    [MaxLength(255)] public string? CompanyName { get; set; }
    [MaxLength(255)] public string? Website { get; set; }
    public bool? EnablePortal { get; set; }
    public int? CurrencyId { get; set; }
    public int? CreatorId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
