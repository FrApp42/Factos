using Factos.Components;
using Factos.Data;
using Microsoft.EntityFrameworkCore;

namespace Factos.Domain;

/// Company logos, stored as files under `Data/uploads/logos` (upstream keeps them in its
/// media library on the public disk); `companies.logo` holds the file name. Served
/// publicly by unique hash, since the logo is printed on documents sent to customers.
public class Logos(AppDbFactory dbs, IWebHostEnvironment env, IConfiguration config)
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private string Folder => Path.Combine(env.ContentRootPath, config["Storage:UploadsPath"] ?? "Data/uploads", "logos");

    /// CompanyLogoRequest allows gif, jpg and png; the type is read from the content,
    /// never from the file name.
    public static string? DetectExtension(ReadOnlySpan<byte> head) => head switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => ".png",
        [0xFF, 0xD8, 0xFF, ..] => ".jpg",
        [0x47, 0x49, 0x46, 0x38, ..] => ".gif",
        _ => null,
    };

    public static string ContentType(string fileName) => Path.GetExtension(fileName) switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        _ => "image/jpeg",
    };

    public async Task SaveAsync(int companyId, byte[] content)
    {
        if (content.Length > MaxBytes)
            throw new SalesException(Loc.T("Le logo ne doit pas dépasser 2 Mo."));
        var extension = DetectExtension(content) ?? throw new SalesException(Loc.T("Le logo doit être une image PNG, JPEG ou GIF."));

        Directory.CreateDirectory(Folder);
        await RemoveFileAsync(companyId);
        var fileName = $"{companyId}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(Folder, fileName), content);

        await using var db = dbs.Create();
        await db.Companies.Where(c => c.Id == companyId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Logo, fileName).SetProperty(c => c.UpdatedAt, Data.Timestamp.Now()));
    }

    public async Task RemoveAsync(int companyId)
    {
        await RemoveFileAsync(companyId);
        await using var db = dbs.Create();
        await db.Companies.Where(c => c.Id == companyId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Logo, (string?)null).SetProperty(c => c.UpdatedAt, Data.Timestamp.Now()));
    }

    /// The logo's bytes, or null when the company has none (or the file is gone).
    public byte[]? Read(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName))
            return null;
        var path = Path.Combine(Folder, fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private async Task RemoveFileAsync(int companyId)
    {
        await using var db = dbs.Create();
        var current = await db.Companies.Where(c => c.Id == companyId).Select(c => c.Logo).FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(current) && current == Path.GetFileName(current))
            File.Delete(Path.Combine(Folder, current));
    }
}
