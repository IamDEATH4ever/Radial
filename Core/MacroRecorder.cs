using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Radial.Models;
using ShortcutModel = Radial.Models.Shortcut;

namespace Radial.Core;

public sealed class MacroRecorder : IDisposable
{
    private const int WhKeyboardLl = 13, WmKeyDown = 0x100, WmSysKeyDown = 0x104, WmKeyUp = 0x101, WmSysKeyUp = 0x105;
    private readonly LowLevelKeyboardProc _callback;
    private IntPtr _hook;
    private readonly HashSet<int> _down = new();
    public bool IsRecording { get; private set; }
    public event Action<ShortcutModel>? ShortcutDetected;
    public MacroRecorder() => _callback = HookCallback;
    public void Start() { if (IsRecording) throw new InvalidOperationException("Already recording."); using var p = Process.GetCurrentProcess(); using var m = p.MainModule!; _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(m.ModuleName), 0); if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to start shortcut recording."); IsRecording = true; _down.Clear(); }
    public void Stop() { IsRecording = false; _down.Clear(); if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && IsRecording && (wParam.ToInt32() is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp))
        {
            var data = Marshal.PtrToStructure<KeyboardData>(lParam); var key = (int)data.VkCode;
            var isDown = wParam.ToInt32() is WmKeyDown or WmSysKeyDown;
            if (isDown)
            {
                _down.Add(key);
                if (!IsModifier(key) && !IsInjected(data.Flags))
                    ShortcutDetected?.Invoke(new ShortcutModel { Modifiers = ReadModifiers(), Key = key });
            }
            else _down.Remove(key);
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }
    private ModifierKeys ReadModifiers() => (IsHeld(0x11) ? ModifierKeys.Control : 0) | (IsHeld(0x10) ? ModifierKeys.Shift : 0) | (IsHeld(0x12) ? ModifierKeys.Alt : 0) | (IsHeld(0x5B) || IsHeld(0x5C) ? ModifierKeys.Windows : 0);
    private bool IsHeld(int key) => _down.Contains(key) || (GetAsyncKeyState(key) & 0x8000) != 0;
    private static bool IsInjected(uint flags) => (flags & 0x10) != 0;
    private static bool IsModifier(int key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C;
    public void Dispose() => Stop();
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint VkCode, ScanCode, Flags, Time; public IntPtr ExtraInfo; }
    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
