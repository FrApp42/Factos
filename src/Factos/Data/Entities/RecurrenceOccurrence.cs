using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Entities;

/// Mirrors InvoiceShelf's `recurrence_occurrences` table: one row per generated run, so a
/// schedule never produces the same occurrence twice.
[Table("recurrence_occurrences")]
[Index(nameof(ScheduleType), nameof(ScheduleId), nameof(ScheduledAt), IsUnique = true, Name = "recurrence_occurrence_unique")]
[Index(nameof(ScheduleType), nameof(ScheduleId), nameof(ScheduledFor), Name = "recurrence_occurrence_day")]
[Index(nameof(RecordType), nameof(RecordId))]
public class RecurrenceOccurrence
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    [MaxLength(255)]
    public string ScheduleType { get; set; } = "";

    public long ScheduleId { get; set; }

    public DateOnly ScheduledFor { get; set; }

    public DateTime ScheduledAt { get; set; }

    [MaxLength(255)]
    public string RecordType { get; set; } = "";

    public long RecordId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
