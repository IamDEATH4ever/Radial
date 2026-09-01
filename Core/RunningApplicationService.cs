using System.Diagnostics;
using System.Runtime.InteropServices;
using Radial.Models;

namespace Radial.Core;

public sealed class RunningApplicationService
{
    public IReadOnlyList<TargetApplicationMetadata> GetVisibleApplications()
    {
        var result = new List<TargetApplicationMetadata>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || GetWindowTextLength(handle) == 0 || GetWindow(handle, 4) != IntPtr.Zero) return true;
            GetWindowThreadProcessId(handle, out var pid);
            try
            {
                using var process = Process.GetProcessById((int)pid);
                result.Add(new TargetApplicationMetadata { DisplayName = GetWindowTitle(handle), ProcessName = process.ProcessName, ExecutablePath = SafePath(process), ProcessId = process.Id, WindowHandle = handle.ToInt64() });
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return result.GroupBy(a => a.ProcessId).Select(g => g.First()).OrderBy(a => a.DisplayName).ToList();
    }
    private static string SafePath(Process p) { try { return p.MainModule?.FileName ?? ""; } catch { return ""; } }
    private static string GetWindowTitle(IntPtr h) { var sb = new System.Text.StringBuilder(256); GetWindowText(h, sb, sb.Capacity); return sb.ToString(); }
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
