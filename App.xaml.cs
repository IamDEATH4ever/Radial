using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using Radial.Core;
using Radial.Models;
using Radial.UI;

namespace Radial;

public partial class App : System.Windows.Application
{
    private InputManager? _input;
    private RadialWindow? _window;
    private IntPtr _radialTarget;
    private MacroManagerWindow? _macroManager;
    private readonly ProfileManager _profiles = new();
    private Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => CompositionRadialRenderer.CompositionDiagnostics.LogException("[CLR] UnhandledException", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        DispatcherUnhandledException += (_, args) => CompositionRadialRenderer.CompositionDiagnostics.LogException("[WPF] DispatcherUnhandledException", args.Exception);
        base.OnStartup(e);
        _profiles.Load();
        _input = new InputManager(MouseButton.XButton1, MouseButton.Right);
        _input.GestureStarted += OnGestureStarted;
        _input.CursorMoved += OnCursorMoved;
        _input.MouseWheel += OnMouseWheel;
        _input.GestureEnded += OnGestureEnded;
        try
        {
            _input.Start();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Radial", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Radial",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("Macro Manager", null, (_, _) => OpenMacroManager());
        _trayIcon.ContextMenuStrip.Items.Add("Settings", null, (_, _) => System.Windows.MessageBox.Show("Settings will be available in a future release.", "Radial"));
        _trayIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Shutdown());
    }

    private void OpenMacroManager() { Dispatcher.Invoke(() => { if (_macroManager is null) { _macroManager = new MacroManagerWindow(); _macroManager.Closed += (_, _) => _macroManager = null; } _macroManager.Show(); _macroManager.Activate(); }); }

    private void OnGestureStarted(System.Windows.Point center)
    {
        _radialTarget = ProfileManager.GetForegroundWindow();
        CompositionRadialRenderer.CompositionDiagnostics.Log($"[Overlay] GestureStarted center={center}; dispatcher-thread={Environment.CurrentManagedThreadId}");
        Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    _profiles.Reload();
                    _window?.Close();
                    var target = new RunningApplicationService().GetVisibleApplications().FirstOrDefault(a => a.WindowHandle == _radialTarget.ToInt64());
                    var profile = _profiles.Find(target);
                    var radialWindow = new RadialWindow(center, profile, _profiles);
                    radialWindow.Closed += (_, _) => { _input?.ResetRadialInputState(); if (ReferenceEquals(_window, radialWindow)) _window = null; };
                    _window = radialWindow;
                    _window.Show();
                }
                catch (Exception ex)
                {
                    _input?.ResetRadialInputState();
                    _window?.Close(); _window = null;
                    CompositionRadialRenderer.CompositionDiagnostics.LogException("[Overlay] Open failed", ex);
                }
        });
    }

    private void OnCursorMoved(System.Windows.Point position)
    {
        Dispatcher.BeginInvoke(() => _window?.UpdateCursor(position));
    }
    private void OnMouseWheel(int delta) => Dispatcher.BeginInvoke(() => { if (_window is not null) _window.SwitchWheel(delta < 0 ? 1 : -1); });

    private void OnGestureEnded()
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var window = _window;
                var macro = window?.SelectedMacro;
                var target = _radialTarget;
                _input?.ResetRadialInputState();
                if (window is not null) window.Close();
                _window = null;
                if (macro is not null) _ = ExecuteMacroAfterCloseAsync(macro, target);
            }
            catch (Exception ex)
            {
                _input?.ResetRadialInputState();
                CompositionRadialRenderer.CompositionDiagnostics.LogException("[Overlay] Close failed", ex);
            }
        });
    }

    private static async Task ExecuteMacroAfterCloseAsync(Macro macro, IntPtr target)
    {
        try { await new MacroPlayer().PlayAsync(macro, target); }
        catch (Exception ex) { CompositionRadialRenderer.CompositionDiagnostics.LogException("[Macro] Playback failed", ex); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _input?.ResetRadialInputState();
        _input?.Dispose();
        if (_trayIcon is not null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
        _window?.Close();
        base.OnExit(e);
    }
}
