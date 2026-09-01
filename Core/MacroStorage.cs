using System.Text.Json;
using System.IO;
using Radial.Models;

namespace Radial.Core;

public sealed class MacroStorage
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public MacroStorage()
    {
        _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Radial", "Macros");
        Directory.CreateDirectory(_directory);
    }
    public IReadOnlyList<Macro> LoadAll() => Directory.EnumerateFiles(_directory, "*.json").Select(Load).Where(m => m is { Shortcut.Key: > 0 }).Cast<Macro>().OrderBy(m => m.Name).ToList();
    public void Save(Macro macro) => File.WriteAllText(Path.Combine(_directory, $"{macro.Id:N}.json"), JsonSerializer.Serialize(macro, _options));
    public void Delete(Macro macro) { var path = Path.Combine(_directory, $"{macro.Id:N}.json"); if (File.Exists(path)) File.Delete(path); }
    private Macro? Load(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var macro = JsonSerializer.Deserialize<Macro>(document.RootElement.GetRawText(), _options);
            if (macro is not null && macro.Shortcut.Key == 0 && document.RootElement.TryGetProperty("Shortcut", out var shortcut) && shortcut.TryGetProperty("PrimaryKey", out var oldKey)) macro.Shortcut.Key = oldKey.GetInt32();
            return macro;
        }
        catch { return null; }
    }
}
