using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Contexts;

/// Provider-specific context: exists only to own its own migration set.
public class SqliteDbContext(DbContextOptions<SqliteDbContext> options) : ApplicationDbContext(options);
