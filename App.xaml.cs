using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using Radial.Core;
using Radial.UI;

namespace Radial;

public partial class App : System.Windows.Application
{
    private InputManager? _input;
    private RadialWindow? _window;
    private Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _input = new InputManager(MouseButton.XButton1, MouseButton.Left);
        _input.GestureStarted += OnGestureStarted;
        _input.CursorMoved += OnCursorMoved;
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
        _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Shutdown());
    }

    private void OnGestureStarted(System.Windows.Point center)
    {
        Dispatcher.Invoke(() =>
        {
            _window?.Close();
            _window = new RadialWindow(center);
            _window.Show();
        });
    }

    private void OnCursorMoved(System.Windows.Point position)
    {
        Dispatcher.BeginInvoke(() => _window?.UpdateCursor(position));
    }

    private void OnGestureEnded()
    {
        Dispatcher.Invoke(() =>
        {
            if (_window is not null)
            {
                Console.WriteLine($"Selected sector: {_window.SelectedSector + 1}");
                _window.Close();
                _window = null;
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _input?.Dispose();
        if (_trayIcon is not null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
        _window?.Close();
        base.OnExit(e);
    }
}
