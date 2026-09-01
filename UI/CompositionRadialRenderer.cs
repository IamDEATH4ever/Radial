using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.UI.Composition;
using Windows.System;
using CompositionColor = Windows.UI.Color;

namespace Radial.UI;

public sealed class CompositionRadialRenderer : IRadialRenderer
{
    private const float Size = 360f;
    private const float Center = Size / 2f;
    private readonly Compositor _compositor;
    private readonly ICompositionTarget _target;
    private readonly ContainerVisual _root;
    private readonly SpriteVisual _background;
    private readonly SpriteVisual _selected;
    private readonly IDispatcherQueueController? _dispatcherQueueController;

    public CompositionRadialRenderer(IntPtr hwnd)
    {
        CompositionDiagnostics.Log("[Composition] Initialize started");
        CompositionDiagnostics.Log($"[Composition] HWND acquired: 0x{hwnd.ToInt64():X}, valid={CompositionDiagnostics.IsWindow(hwnd)}, thread={Environment.CurrentManagedThreadId}, apartment={Thread.CurrentThread.GetApartmentState()}");
        CompositionDiagnostics.Log("[Composition] Creating Compositor");
        try
        {
            _dispatcherQueueController = DispatcherQueueInterop.EnsureCurrentThreadQueue();
            _compositor = new Compositor();
            CompositionDiagnostics.Log("[Composition] Compositor created");
            CompositionDiagnostics.Log("[Composition] Creating desktop interop");
            _target = CompositionInterop.CreateDesktopWindowTarget(_compositor, hwnd, true);
            CompositionDiagnostics.Log("[Composition] Desktop interop created");
            CompositionDiagnostics.Log("[Composition] Creating root visual");
            _root = _compositor.CreateContainerVisual();
            CompositionDiagnostics.Log("[Composition] Root visual created");
            CompositionDiagnostics.Log("[Composition] Assigning root visual");
            _target.Root = _root;
            CompositionDiagnostics.Log("[Composition] Root visual assigned");
        }
        catch (Exception ex)
        {
            CompositionDiagnostics.LogException("[Composition] Initialization failed", ex);
            throw;
        }

        CompositionDiagnostics.Log("[Composition] Creating radial visual");
        _background = _compositor.CreateSpriteVisual();
        _background.Size = new System.Numerics.Vector2(Size, Size);
        _background.Brush = _compositor.CreateColorBrush(CompositionColor.FromArgb(150, 42, 46, 56));
        _root.Children.InsertAtTop(_background);
        CompositionDiagnostics.Log("[Composition] Radial visual created");

        _selected = _compositor.CreateSpriteVisual();
        _selected.Size = new System.Numerics.Vector2(140f, 140f);
        _selected.Offset = new System.Numerics.Vector3(Center - 70f, 0f, 1f);
        _selected.Brush = _compositor.CreateColorBrush(CompositionColor.FromArgb(235, 0, 120, 220));
        _root.Children.InsertAtTop(_selected);
        CompositionDiagnostics.Log("[Composition] Initialization complete");
    }

    public void Update(RadialRenderState state)
    {
        _selected.RotationAngleInDegrees = state.SelectedSector * (360f / Math.Max(1, state.SegmentCount));
        _selected.CenterPoint = new System.Numerics.Vector3(Center, Center, 0f);
    }

    public void Dispose()
    {
        _root.Children.RemoveAll();
        _target.Root = null;
        _selected.Dispose();
        _background.Dispose();
        _root.Dispose();
        Marshal.ReleaseComObject(_target);
        _compositor.Dispose();
        _dispatcherQueueController?.ShutdownQueueAsync();
    }

    private static class DispatcherQueueInterop
    {
        public static IDispatcherQueueController? EnsureCurrentThreadQueue()
        {
            CompositionDiagnostics.Log("[Composition] Checking current-thread DispatcherQueue");
            var existing = DispatcherQueue.GetForCurrentThread();
            if (existing is not null)
            {
                CompositionDiagnostics.Log("[Composition] DispatcherQueue already exists");
                return null;
            }

            CompositionDiagnostics.Log("[Composition] Creating current-thread DispatcherQueue");
            var options = new DispatcherQueueOptions
            {
                DwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
                ThreadType = 2,
                ApartmentType = 1
            };
            var hr = CreateDispatcherQueueController(options, out var rawController);
            if (hr < 0)
            {
                CompositionDiagnostics.Log($"[Composition] CreateDispatcherQueueController failed: 0x{hr:X8}");
                Marshal.ThrowExceptionForHR(hr);
            }

            if (rawController == IntPtr.Zero)
            {
                throw new COMException("CreateDispatcherQueueController returned a null controller.", unchecked((int)0x80004003));
            }

            try
            {
                var controller = (IDispatcherQueueController)Marshal.GetObjectForIUnknown(rawController);
                CompositionDiagnostics.Log("[Composition] DispatcherQueue created");
                return controller;
            }
            finally
            {
                Marshal.Release(rawController);
            }
        }

        [DllImport("coremessaging.dll", ExactSpelling = true)]
        private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr dispatcherQueueController);

        [StructLayout(LayoutKind.Sequential)]
        private struct DispatcherQueueOptions
        {
            public int DwSize;
            public int ThreadType;
            public int ApartmentType;
        }
    }

    private static class CompositionInterop
    {
        public static ICompositionTarget CreateDesktopWindowTarget(Compositor compositor, IntPtr hwnd, bool topmost)
        {
            CompositionDiagnostics.Log("[Composition] Casting compositor to desktop interop");
            var interop = (ICompositorDesktopInterop)(object)compositor;
            CompositionDiagnostics.Log("[Composition] Calling CreateDesktopWindowTarget");
            interop.CreateDesktopWindowTarget(hwnd, topmost, out var rawTarget);
            CompositionDiagnostics.Log($"[Composition] Desktop target returned: 0x{rawTarget.ToInt64():X}");
            if (rawTarget == IntPtr.Zero)
            {
                throw new COMException("CreateDesktopWindowTarget returned a null target.", unchecked((int)0x80004003));
            }
            try
            {
                CompositionDiagnostics.Log("[Composition] Converting desktop target COM object");
                return (ICompositionTarget)Marshal.GetObjectForIUnknown(rawTarget);
            }
            finally
            {
                Marshal.Release(rawTarget);
            }
        }
    }

    public static class CompositionDiagnostics
    {
        public static string LogPath { get; } = Path.Combine(Path.GetTempPath(), "Radial-composition.log");

        public static void Log(string message) => File.AppendAllText(LogPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");

        public static void LogException(string context, Exception exception)
        {
            var hresult = exception.HResult;
            var win32 = exception is Win32Exception win32Exception ? win32Exception.NativeErrorCode : 0;
            Log($"{context}: {exception.GetType().FullName}: {exception.Message}; HResult=0x{hresult:X8}; Win32=0x{win32:X8}; Inner={exception.InnerException}");
            Log(exception.StackTrace ?? "<no stack trace>");
        }

        public static bool IsWindow(IntPtr hwnd) => IsWindowNative(hwnd);

        [DllImport("user32.dll", EntryPoint = "IsWindow")]
        private static extern bool IsWindowNative(IntPtr hwnd);
    }

    [ComImport]
    [Guid("22F34E66-50DB-4E36-A98D-61C01B384D20")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface IDispatcherQueueController
    {
        DispatcherQueue DispatcherQueue { get; }
        Windows.Foundation.IAsyncAction ShutdownQueueAsync();
    }

    [ComImport]
    [Guid("A1BEA8BA-D726-4663-8129-6B5E7927FFA6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface ICompositionTarget
    {
        Visual? Root { get; set; }
    }

    [ComImport]
    [Guid("29E691FA-4567-4DCA-B319-D0F207EB6807")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(IntPtr hwndTarget, bool isTopmost, out IntPtr result);
    }
}
