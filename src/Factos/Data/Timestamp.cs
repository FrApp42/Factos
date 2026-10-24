namespace Factos.Data;

public static class Timestamp
{
    /// Laravel's now() as stored by a TIMESTAMP column: UTC, whole seconds. Keeping the
    /// fraction would leak into every JSON date (".000000Z" upstream).
    public static DateTime Now()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }
}
