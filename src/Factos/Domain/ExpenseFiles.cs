using Factos.Components;

namespace Factos.Domain;

/// Expense attachments (Factos-only, upstream has none), stored as files under
/// `Data/uploads/expenses/{expenseId}/` — no table, so no migration. Back up with the database.
public class ExpenseFiles(IWebHostEnvironment env, IConfiguration config)
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private string Folder(long expenseId) => Path.Combine(env.ContentRootPath, config["Storage:UploadsPath"] ?? "Data/uploads", "expenses", expenseId.ToString());

    public List<(string Name, long Size)> List(long expenseId)
        => Directory.Exists(Folder(expenseId))
            ? new DirectoryInfo(Folder(expenseId)).GetFiles().OrderBy(f => f.CreationTimeUtc).Select(f => (f.Name, f.Length)).ToList()
            : [];

    public async Task SaveAsync(long expenseId, string name, byte[] content)
    {
        if (content.Length > MaxBytes)
            throw new SalesException(Loc.F("« {0} » dépasse 10 Mo.", name));
        // Path.GetFileName drops any directory part sent by the client.
        var clean = string.Concat(Path.GetFileName(name).Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (clean.Length == 0)
            clean = "file";
        Directory.CreateDirectory(Folder(expenseId));
        var path = Path.Combine(Folder(expenseId), clean);
        for (var n = 2; File.Exists(path); n++)
            path = Path.Combine(Folder(expenseId), $"{Path.GetFileNameWithoutExtension(clean)} ({n}){Path.GetExtension(clean)}");
        await File.WriteAllBytesAsync(path, content);
    }

    /// Full path of an existing attachment, or null (also for any name that is not a bare file name).
    public string? Find(long expenseId, string name)
    {
        if (name != Path.GetFileName(name))
            return null;
        var path = Path.Combine(Folder(expenseId), name);
        return File.Exists(path) ? path : null;
    }

    public void Remove(long expenseId, string name)
    {
        if (Find(expenseId, name) is { } path)
            File.Delete(path);
    }

    public void RemoveAll(long expenseId)
    {
        if (Directory.Exists(Folder(expenseId)))
            Directory.Delete(Folder(expenseId), true);
    }
}
