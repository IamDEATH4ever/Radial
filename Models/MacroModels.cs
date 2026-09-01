namespace Radial.Models;

public sealed class Macro
{
    public int SchemaVersion { get; set; } = 3;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Macro";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public TargetApplicationMetadata? TargetApplication { get; set; }
    public Shortcut Shortcut { get; set; } = new();
}

public sealed class TargetApplicationMetadata
{
    public string DisplayName { get; set; } = "Unknown application";
    public string ProcessName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public int ProcessId { get; set; }
    public long WindowHandle { get; set; }
    public override string ToString() => $"{DisplayName} ({ProcessName})";
}

public sealed class Shortcut
{
    public ModifierKeys Modifiers { get; set; }
    public int Key { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int PrimaryKey { get => Key; set => Key = value; }
    public string DisplayText => string.Join(" + ", new[] { Modifiers.HasFlag(ModifierKeys.Control) ? "Ctrl" : null, Modifiers.HasFlag(ModifierKeys.Shift) ? "Shift" : null, Modifiers.HasFlag(ModifierKeys.Alt) ? "Alt" : null, Modifiers.HasFlag(ModifierKeys.Windows) ? "Windows" : null, Key == 0 ? null : ((System.Windows.Forms.Keys)Key).ToString() }.Where(s => s is not null));
}

[Flags]
public enum ModifierKeys { None = 0, Control = 1, Shift = 2, Alt = 4, Windows = 8 }

public sealed class RadialConfiguration
{
    public int SchemaVersion { get; set; } = 2;
    public List<ApplicationProfile> ApplicationProfiles { get; set; } = new();
}

public sealed class ApplicationProfile
{
    public string ApplicationId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public List<RadialWheel> Wheels { get; set; } = new() { new RadialWheel() };
}

public sealed class RadialWheel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Wheel 1";
    public List<Macro> Macros { get; set; } = new();
    [System.Text.Json.Serialization.JsonPropertyName("Actions")] public List<MacroActionReference> LegacyActions { get; set; } = new();
}

public sealed class MacroActionReference { public Guid MacroId { get; set; } public string Name { get; set; } = ""; }
