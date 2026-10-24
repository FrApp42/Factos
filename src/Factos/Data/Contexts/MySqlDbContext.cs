using Microsoft.EntityFrameworkCore;

namespace Factos.Data.Contexts;

/// Provider-specific context: exists only to own its own migration set.
public class MySqlDbContext(DbContextOptions<MySqlDbContext> options) : ApplicationDbContext(options);
