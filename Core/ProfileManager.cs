using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using Radial.Models;

namespace Radial.Core;

public sealed class ProfileManager
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Radial", "radial-configuration.json");
    private readonly MacroStorage _macros = new();
    private RadialConfiguration _configuration = new();
    public RadialConfiguration Configuration => _configuration;
    public void Load()
    {
        if (File.Exists(_path)) { try { _configuration = System.Text.Json.JsonSerializer.Deserialize<RadialConfiguration>(File.ReadAllText(_path)) ?? new(); Normalize(); Save(); } catch { _configuration = new(); } return; }
        // One-time migration: retain existing macro files by grouping them using their stable target metadata.
        foreach (var macro in _macros.LoadAll().Where(m => m.TargetApplication is not null))
        {
            var target = macro.TargetApplication!;
            var profile = _configuration.ApplicationProfiles.FirstOrDefault(p => Same(p, target)) ?? new ApplicationProfile { ApplicationId = StableId(target), DisplayName = target.DisplayName, ProcessName = target.ProcessName, ExecutablePath = target.ExecutablePath };
            if (!_configuration.ApplicationProfiles.Contains(profile)) _configuration.ApplicationProfiles.Add(profile);
            profile.Wheels[0].Macros.Add(macro);
        }
        if (_configuration.ApplicationProfiles.Count > 0) Save();
    }
    public void Reload() => Load();
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, System.Text.Json.JsonSerializer.Serialize(_configuration, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); }
    public ApplicationProfile? Find(TargetApplicationMetadata? target) => target is null ? null : _configuration.ApplicationProfiles.FirstOrDefault(p => Same(p, target));
    public ApplicationProfile GetOrCreate(TargetApplicationMetadata target) { var profile = Find(target); if (profile is not null) return profile; profile = new ApplicationProfile { ApplicationId = StableId(target), DisplayName = target.DisplayName, ProcessName = target.ProcessName, ExecutablePath = target.ExecutablePath }; _configuration.ApplicationProfiles.Add(profile); Save(); return profile; }
    public static string StableId(TargetApplicationMetadata target) => !string.IsNullOrWhiteSpace(target.ExecutablePath) ? target.ExecutablePath.ToUpperInvariant() : target.ProcessName.ToUpperInvariant();
    private static bool Same(ApplicationProfile p, TargetApplicationMetadata t) => string.Equals(p.ExecutablePath, t.ExecutablePath, StringComparison.OrdinalIgnoreCase) && string.Equals(p.ProcessName, t.ProcessName, StringComparison.OrdinalIgnoreCase);
    public Macro? Resolve(Macro macro) => macro;
    private void Normalize()
    {
        _configuration.ApplicationProfiles ??= new();
        foreach (var profile in _configuration.ApplicationProfiles)
        {
            profile.Wheels ??= new();
            if (profile.Wheels.Count == 0) profile.Wheels.Add(new RadialWheel());
            foreach (var wheel in profile.Wheels)
            {
                wheel.Macros ??= new();
                if (wheel.Macros.Count == 0 && wheel.LegacyActions.Count > 0)
                {
                    var legacy = _macros.LoadAll().ToDictionary(m => m.Id);
                    foreach (var action in wheel.LegacyActions)
                        if (legacy.TryGetValue(action.MacroId, out var macro)) wheel.Macros.Add(macro);
                }
                wheel.LegacyActions.Clear();
            }
        }
    }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
