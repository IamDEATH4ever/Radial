using System.Runtime.InteropServices;
using WpfPoint = System.Windows.Point;

namespace Radial.Core;

public enum MouseButton { Left, Middle, Right, XButton1, XButton2 }

public sealed class InputManager : IDisposable
{
    private const int WhKeyboardLl = 13, WhMouseLl = 14, WmMouseMove = 0x0200, WmLButtonDown = 0x0201, WmLButtonUp = 0x0202, WmMouseWheel = 0x020A;
    private const int WmKeyDown = 0x0100, WmSysKeyDown = 0x0104, WmKeyUp = 0x0101, WmSysKeyUp = 0x0105;
    private const int WmMButtonDown = 0x0207, WmMButtonUp = 0x0208, WmRButtonDown = 0x0204, WmRButtonUp = 0x0205;
    private const int WmXButtonDown = 0x020B, WmXButtonUp = 0x020C;
    private readonly MouseButton _primary, _secondary;
    private readonly LowLevelMouseProc _callback;
    private readonly LowLevelKeyboardProc _keyboardCallback;
    private const bool MouseHookDiagnostics = true;
    private IntPtr _hook, _keyboardHook;
    private bool _isM4Down, _isRmbDown, _isRadialOpen;
    private readonly bool[] _downConsumedByRadial = new bool[5];
    private readonly object _stateGate = new();
    public event Action<WpfPoint>? GestureStarted;
    public event Action<WpfPoint>? CursorMoved;
    public event Action? GestureEnded;
    public event Action<int>? MouseWheel;

    public InputManager(MouseButton primary, MouseButton secondary) { _primary = primary; _secondary = secondary; _callback = HookCallback; _keyboardCallback = KeyboardHookCallback; }
    public void Start()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hook = SetWindowsHookEx(WhMouseLl, _callback, GetModuleHandle(module.ModuleName), 0);
        if (_hook == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Unable to install the global mouse hook.");
        _keyboardHook = SetWindowsHookExKeyboard(WhKeyboardLl, _keyboardCallback, GetModuleHandle(module.ModuleName), 0);
        if (_keyboardHook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            throw new System.ComponentModel.Win32Exception(error, "Unable to install the global keyboard hook.");
        }
    }
    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        var consume = false;
        try
        {
            if (code >= 0)
            {
                var data = Marshal.PtrToStructure<MouseHookData>(lParam);
                var message = wParam.ToInt32();
                if (message == WmMouseMove)
                {
                    consume = IsRadialOpen();
                    if (consume) CursorMoved?.Invoke(new WpfPoint(data.Point.X, data.Point.Y));
                }
                else if (message == WmMouseWheel && IsRadialOpen())
                {
                    consume = true;
                    MouseWheel?.Invoke((short)(data.MouseData >> 16));
                }
                else
                {
                    var button = MessageButton(message, data.MouseData >> 16);
                    if (button.HasValue)
                    {
                        var down = message is WmLButtonDown or WmMButtonDown or WmRButtonDown or WmXButtonDown;
                        var radialOpenBefore = IsRadialOpen();
                        SetState(button.Value, down);
                        var downConsumedByRadial = false;
                        lock (_stateGate)
                        {
                            var index = (int)button.Value;
                            if (down)
                            {
                                consume = _isRadialOpen;
                                if ((uint)index < (uint)_downConsumedByRadial.Length)
                                    _downConsumedByRadial[index] = consume;
                                downConsumedByRadial = consume;
                            }
                            else
                            {
                                downConsumedByRadial = (uint)index < (uint)_downConsumedByRadial.Length && _downConsumedByRadial[index];
                                consume = downConsumedByRadial;
                                if ((uint)index < (uint)_downConsumedByRadial.Length)
                                    _downConsumedByRadial[index] = false;
                            }
                        }
                        if (MouseHookDiagnostics)
                            System.Diagnostics.Debug.WriteLine($"[MouseHook] message=0x{message:X4} button={button.Value} {(down ? "down" : "up")} RadialOpen before={radialOpenBefore} after={IsRadialOpen()} consumed={consume} downConsumedByRadial={downConsumedByRadial}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ResetRadialInputState();
            System.Diagnostics.Debug.WriteLine($"[MouseHook] callback failed; state reset; event passed through: {ex}");
            consume = false;
        }
        if (consume) return new IntPtr(1);
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0 && IsRadialOpen() && (wParam.ToInt32() is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp))
                return new IntPtr(1);
        }
        catch (Exception ex)
        {
            ResetRadialInputState();
            System.Diagnostics.Debug.WriteLine($"[KeyboardHook] callback failed; state reset; event passed through: {ex}");
        }
        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }
    private bool IsRadialOpen() { lock (_stateGate) return _isRadialOpen; }
    private void SetState(MouseButton button, bool down)
    {
        bool open = false, close = false;
        lock (_stateGate)
        {
            if (button == MouseButton.XButton1) _isM4Down = down;
            if (button == MouseButton.Right) _isRmbDown = down;
            if (!_isRadialOpen && _isM4Down && _isRmbDown) { _isRadialOpen = true; open = true; }
            else if (_isRadialOpen && !_isM4Down && !_isRmbDown) { _isRadialOpen = false; close = true; }
        }
        if (open)
        {
            GetCursorPos(out var p);
            GestureStarted?.Invoke(new WpfPoint(p.X, p.Y));
        }
        else if (close) GestureEnded?.Invoke();
    }
    private static MouseButton? MessageButton(int message, uint xData) => message switch
    {
        WmLButtonDown or WmLButtonUp => MouseButton.Left, WmMButtonDown or WmMButtonUp => MouseButton.Middle,
        WmRButtonDown or WmRButtonUp => MouseButton.Right, WmXButtonDown or WmXButtonUp => ((xData & 1) == 1 ? MouseButton.XButton1 : MouseButton.XButton2), _ => null
    };
    public void ResetRadialInputState()
    {
        lock (_stateGate)
        {
            _isM4Down = false;
            _isRmbDown = false;
            _isRadialOpen = false;
            Array.Clear(_downConsumedByRadial);
        }
    }
    public void Dispose()
    {
        ResetRadialInputState();
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
    }
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct MouseHookData { public POINT Point; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, LowLevelMouseProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] private static extern IntPtr SetWindowsHookExKeyboard(int id, LowLevelKeyboardProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
}
