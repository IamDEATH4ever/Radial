using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Radial.Models;
using ShortcutModel = Radial.Models.Shortcut;

namespace Radial.Core;

public sealed class MacroPlayer
{
    public async Task PlayAsync(Macro macro, CancellationToken cancellationToken = default) => await PlayAsync(macro, null, cancellationToken);
    public async Task PlayAsync(Macro macro, IntPtr? targetOverride, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = targetOverride.GetValueOrDefault();
        if (target == IntPtr.Zero) target = FindTarget(macro.TargetApplication);
        if (target == IntPtr.Zero && macro.TargetApplication is { ExecutablePath.Length: > 0 } metadata && File.Exists(metadata.ExecutablePath))
        {
            using var launched = Process.Start(new ProcessStartInfo(metadata.ExecutablePath) { UseShellExecute = true });
            if (launched is not null) for (var i = 0; i < 50 && target == IntPtr.Zero; i++) { cancellationToken.ThrowIfCancellationRequested(); await Task.Delay(100, cancellationToken); launched.Refresh(); target = launched.MainWindowHandle; }
        }
        if (target == IntPtr.Zero) throw new InvalidOperationException($"Target application '{macro.TargetApplication?.DisplayName ?? "unknown"}' is not running and could not be started.");
        if (IsIconic(target)) ShowWindow(target, 9); // restore only; normal/maximized states are preserved
        if (!SetForegroundWindow(target)) throw new InvalidOperationException("Unable to focus the target application.");
        SendShortcut(macro.Shortcut);
        await Task.CompletedTask;
    }
    private static IntPtr FindTarget(TargetApplicationMetadata? target)
    {
        if (target is null) return IntPtr.Zero;
        try
        {
            if (target.ProcessId > 0)
            {
                using var process = Process.GetProcessById(target.ProcessId);
                if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
            }
            var processName = Path.GetFileNameWithoutExtension(target.ProcessName);
            var running = Process.GetProcessesByName(processName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            if (running is not null) return running.MainWindowHandle;
        }
        catch { }
        return target.WindowHandle != 0 && IsWindow((IntPtr)target.WindowHandle) ? (IntPtr)target.WindowHandle : IntPtr.Zero;
    }
    private static void SendShortcut(ShortcutModel shortcut)
    {
        if (shortcut.Key == 0) throw new InvalidOperationException("The macro has no primary key.");
        var modifiers = new List<byte>(); if (shortcut.Modifiers.HasFlag(ModifierKeys.Control)) modifiers.Add(0x11); if (shortcut.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers.Add(0x10); if (shortcut.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers.Add(0x12); if (shortcut.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers.Add(0x5B);
        var inputs = modifiers.Select(k => KeyInput(k, false)).Append(KeyInput((byte)shortcut.Key, false)).Append(KeyInput((byte)shortcut.Key, true)).Concat(modifiers.AsEnumerable().Reverse().Select(k => KeyInput(k, true))).ToArray();
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"SendInput rejected shortcut {shortcut.DisplayText}: sent={sent}/{inputs.Length}, lastError={error}");
            throw new InvalidOperationException($"Windows rejected the keyboard shortcut input (sent {sent}/{inputs.Length}, Win32 error {error}).");
        }
    }
    private static INPUT KeyInput(byte key, bool up) => new() { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBDINPUT { Vk = key, Flags = up ? 2u : 0u } } };
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public INPUTUNION Data; }
    // Keep the native INPUT union size (MOUSEINPUT is required for the x64 cbSize),
    // but only ever populate the keyboard member.
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT Mouse; [FieldOffset(0)] public KEYBDINPUT Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int X, Y; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
}
