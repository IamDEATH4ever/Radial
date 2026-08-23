using System.Runtime.InteropServices;
using WpfPoint = System.Windows.Point;

namespace Radial.Core;

public enum MouseButton { Left, Middle, Right, XButton1, XButton2 }

public sealed class InputManager : IDisposable
{
    private const int WhMouseLl = 14, WmMouseMove = 0x0200, WmLButtonDown = 0x0201, WmLButtonUp = 0x0202;
    private const int WmMButtonDown = 0x0207, WmMButtonUp = 0x0208, WmRButtonDown = 0x0204, WmRButtonUp = 0x0205;
    private const int WmXButtonDown = 0x020B, WmXButtonUp = 0x020C;
    private readonly MouseButton _primary, _secondary;
    private readonly LowLevelMouseProc _callback;
    private IntPtr _hook;
    private bool _primaryDown, _secondaryDown, _active;
    public event Action<WpfPoint>? GestureStarted;
    public event Action<WpfPoint>? CursorMoved;
    public event Action? GestureEnded;

    public InputManager(MouseButton primary, MouseButton secondary) { _primary = primary; _secondary = secondary; _callback = HookCallback; }
    public void Start()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hook = SetWindowsHookEx(WhMouseLl, _callback, GetModuleHandle(module.ModuleName), 0);
        if (_hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Unable to install the global mouse hook.");
    }
    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<MouseHookData>(lParam);
            var message = wParam.ToInt32();
            if (message == WmMouseMove) CursorMoved?.Invoke(new WpfPoint(data.Point.X, data.Point.Y));
            var button = MessageButton(message, data.MouseData >> 16);
            if (button.HasValue) { var down = message is WmLButtonDown or WmMButtonDown or WmRButtonDown or WmXButtonDown; SetState(button.Value, down); }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }
    private void SetState(MouseButton button, bool down)
    {
        if (button == _primary) _primaryDown = down;
        if (button == _secondary) _secondaryDown = down;
        var combined = _primaryDown && _secondaryDown;
        if (combined && !_active) { _active = true; GetCursorPos(out var p); GestureStarted?.Invoke(new WpfPoint(p.X, p.Y)); }
        else if (_active && !combined) { _active = false; GestureEnded?.Invoke(); }
    }
    private static MouseButton? MessageButton(int message, uint xData) => message switch
    {
        WmLButtonDown or WmLButtonUp => MouseButton.Left, WmMButtonDown or WmMButtonUp => MouseButton.Middle,
        WmRButtonDown or WmRButtonUp => MouseButton.Right, WmXButtonDown or WmXButtonUp => ((xData & 1) == 1 ? MouseButton.XButton1 : MouseButton.XButton2), _ => null
    };
    public void Dispose() { if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; } }
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData { public POINT Point; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, LowLevelMouseProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
}
