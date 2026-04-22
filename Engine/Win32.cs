using System.Runtime.InteropServices;
using System.Text;

namespace LiveWallpaper.Engine;

internal static class Win32
{
    // ── Window styles ────────────────────────────────────────────────────────
    public const int  GWL_EXSTYLE       = -20;
    public const uint WS_EX_TOOLWINDOW  = 0x00000080;
    public const uint WS_EX_NOACTIVATE  = 0x08000000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const uint WS_EX_LAYERED     = 0x00080000;
    public const uint WS_POPUP          = 0x80000000;
    public const uint WS_VISIBLE        = 0x10000000;

    // ── SetWindowPos flags ───────────────────────────────────────────────────
    public const uint SWP_NOSIZE     = 0x0001;
    public const uint SWP_NOMOVE     = 0x0002;
    public const uint SWP_NOZORDER   = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public static readonly IntPtr HWND_BOTTOM  = new(1);
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    // ── SendMessageTimeout ───────────────────────────────────────────────────
    public const uint SMTO_NORMAL = 0x0000;

    // ── ShowWindow ───────────────────────────────────────────────────────────
    public const int SW_SHOW = 5;

    // ── System metrics ───────────────────────────────────────────────────────
    public const int SM_CXSCREEN       = 0;
    public const int SM_CYSCREEN       = 1;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    // ── RedrawWindow flags ───────────────────────────────────────────────────
    public const uint RDW_INVALIDATE  = 0x0001;
    public const uint RDW_ALLCHILDREN = 0x0080;
    public const uint RDW_UPDATENOW   = 0x0100;

    // ── Monitor flags ────────────────────────────────────────────────────────
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const uint MONITORINFOF_PRIMARY     = 0x00000001;

    // ── Delegates ────────────────────────────────────────────────────────────
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    // ── Structs ──────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
        public int Width  => right  - left;
        public int Height => bottom - top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;   // full monitor area (virtual screen coords)
        public RECT rcWork;      // monitor work area (excludes taskbar)
        public uint dwFlags;     // MONITORINFOF_PRIMARY if primary
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint    cbSize;
        public uint    style;
        public IntPtr  lpfnWndProc;
        public int     cbClsExtra;
        public int     cbWndExtra;
        public IntPtr  hInstance;
        public IntPtr  hIcon;
        public IntPtr  hCursor;
        public IntPtr  hbrBackground;
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string  lpszClassName;
        public IntPtr  hIconSm;
    }

    // ── Window finding & enumeration ─────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(
        IntPtr hwndParent, IntPtr hwndChildAfter,
        string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsZoomed(IntPtr hWnd);   // returns true if maximized

    // ── Window position & style ──────────────────────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg,
        IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    public static extern bool RedrawWindow(
        IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [DllImport("user32.dll")]
    public static extern long SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

    [DllImport("user32.dll")]
    public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

    // ── Window class registration ────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string? lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern IntPtr DefWindowProc(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // ── Monitor APIs ─────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    // ── Process memory trim ──────────────────────────────────────────────────

    /// <summary>
    /// Pass (-1, -1) to trim the working set to minimum.
    /// This reduces Task Manager "Memory" while process is idle.
    /// Pages are not freed — they go to standby and are re-faulted on access.
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern bool SetProcessWorkingSetSize(
        IntPtr hProcess,
        IntPtr dwMinimumWorkingSetSize,
        IntPtr dwMaximumWorkingSetSize);
}
