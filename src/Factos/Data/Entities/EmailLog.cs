using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `email_logs` table. Column names are part of the v1 API contract.
[Table("email_logs")]
public class EmailLog
{
    public long Id { get; set; }

    public string From { get; set; } = "";

    public string To { get; set; } = "";

    public string Subject { get; set; } = "";

    public string Body { get; set; } = "";

    public string MailableType { get; set; } = "";

    public string MailableId { get; set; } = "";

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Token { get; set; }

    /// Factos-only: first load of the tracking pixel (setting `mail_tracking`), null if never opened.
    public DateTime? OpenedAt { get; set; }
}
