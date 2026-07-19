using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WindowsTaskbarMonitor.App.Tray;

internal sealed class TrayIconService : IDisposable
{
    private const uint CallbackMessage = 0x8001;
    private const uint WindowMessageContextMenu = 0x007B;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private const uint WindowMessageRightButtonUp = 0x0205;
    private const uint NotifyIconSelect = 0x0400;
    private const uint NotifyIconKeySelect = 0x0401;
    private const uint NotifyIconAdd = 0x00000000;
    private const uint NotifyIconModify = 0x00000001;
    private const uint NotifyIconDelete = 0x00000002;
    private const uint NotifyIconSetVersion = 0x00000004;
    private const uint NotifyIconMessage = 0x00000001;
    private const uint NotifyIconIcon = 0x00000002;
    private const uint NotifyIconTip = 0x00000004;
    private const uint NotifyIconGuid = 0x00000020;
    private const uint NotifyIconVersion4 = 4;
    private const uint MenuString = 0x00000000;
    private const uint MenuSeparator = 0x00000800;
    private const uint TrackRightButton = 0x0002;
    private const uint TrackReturnCommand = 0x0100;
    private const uint OpenCommand = 1;
    private const uint ExitCommand = 2;
    private static readonly Guid IconGuid = new("7B20B497-0B7E-4E1E-AF67-307D9945541B");

    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClass = $"TaskbarMonitor.{Guid.NewGuid():N}";
    private readonly uint _taskbarCreatedMessage;
    private IntPtr _windowHandle;
    private IntPtr _iconHandle;
    private bool _started;

    public TrayIconService()
    {
        _windowProcedure = ProcessWindowMessage;
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
        CreateMessageWindow();
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _iconHandle = TrayIconRenderer.Render("--");
        AddIcon("Taskbar Monitor");
        _started = true;
    }

    public void Update(string label, string tooltip)
    {
        if (!_started)
        {
            return;
        }

        var replacement = TrayIconRenderer.Render(label);
        var data = CreateNotifyIconData(tooltip, replacement);
        if (Shell_NotifyIconW(NotifyIconModify, ref data))
        {
            DestroyIcon(_iconHandle);
            _iconHandle = replacement;
        }
        else
        {
            DestroyIcon(replacement);
        }
    }

    public TrayBounds GetBounds()
    {
        var identifier = new NotifyIconIdentifier
        {
            Size = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            WindowHandle = _windowHandle,
            Identifier = 1,
            Guid = IconGuid
        };

        if (Shell_NotifyIconGetRect(ref identifier, out var rectangle) >= 0)
        {
            return new TrayBounds(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
        }

        GetCursorPos(out var point);
        return new TrayBounds(point.X - 8, point.Y - 8, point.X + 8, point.Y + 8);
    }

    public void Dispose()
    {
        if (_started)
        {
            var data = CreateNotifyIconData(string.Empty, _iconHandle);
            Shell_NotifyIconW(NotifyIconDelete, ref data);
            _started = false;
        }

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        UnregisterClassW(_windowClass, GetModuleHandleW(null));
    }

    private void CreateMessageWindow()
    {
        var module = GetModuleHandleW(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            WindowProcedure = _windowProcedure,
            Instance = module,
            ClassName = _windowClass
        };

        if (RegisterClassExW(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _windowHandle = CreateWindowExW(
            0,
            _windowClass,
            "Taskbar Monitor messages",
            0,
            0,
            0,
            0,
            0,
            new IntPtr(-3),
            IntPtr.Zero,
            module,
            IntPtr.Zero);

        if (_windowHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private void AddIcon(string tooltip)
    {
        var data = CreateNotifyIconData(tooltip, _iconHandle);
        if (!Shell_NotifyIconW(NotifyIconAdd, ref data))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        data.TimeoutOrVersion = NotifyIconVersion4;
        Shell_NotifyIconW(NotifyIconSetVersion, ref data);
    }

    private NotifyIconData CreateNotifyIconData(string tooltip, IntPtr icon) => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        WindowHandle = _windowHandle,
        Identifier = 1,
        Flags = NotifyIconMessage | NotifyIconIcon | NotifyIconTip | NotifyIconGuid,
        CallbackMessage = CallbackMessage,
        IconHandle = icon,
        Tip = tooltip.Length <= 127 ? tooltip : tooltip[..127],
        Info = string.Empty,
        InfoTitle = string.Empty,
        Guid = IconGuid
    };

    private IntPtr ProcessWindowMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == _taskbarCreatedMessage && _started)
        {
            AddIcon("Taskbar Monitor");
            return IntPtr.Zero;
        }

        if (message == CallbackMessage)
        {
            var notification = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (notification)
            {
                case WindowMessageLeftButtonUp:
                case NotifyIconSelect:
                case NotifyIconKeySelect:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    return IntPtr.Zero;
                case WindowMessageRightButtonUp:
                case WindowMessageContextMenu:
                    ShowContextMenu();
                    return IntPtr.Zero;
            }
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenuW(menu, MenuString, OpenCommand, "Open Taskbar Monitor");
            AppendMenuW(menu, MenuSeparator, 0, null);
            AppendMenuW(menu, MenuString, ExitCommand, "Exit");
            GetCursorPos(out var point);
            SetForegroundWindow(_windowHandle);

            var command = TrackPopupMenuEx(
                menu,
                TrackRightButton | TrackReturnCommand,
                point.X,
                point.Y,
                _windowHandle,
                IntPtr.Zero);

            if (command == OpenCommand)
            {
                OpenRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (command == ExitCommand)
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string message);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRectangle iconLocation);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, uint identifier, string? text);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr window,
        IntPtr parameters);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WindowProcedure? WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string? ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Identifier;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint Size;
        public IntPtr WindowHandle;
        public uint Identifier;
        public Guid Guid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRectangle
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }
}
