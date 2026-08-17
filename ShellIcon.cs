using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace DesktopFolder;

internal static class ShellIcon
{
    private static readonly ConcurrentDictionary<string, BitmapSource> IconCache = new();

    public static BitmapSource? GetLargeIcon(string path, bool isFolder)
    {
        var key = (isFolder ? "D|" : "F|") + path;
        if (IconCache.TryGetValue(key, out var cached)) return cached;

        var icon = GetShellItemImage(path, 256)
            ?? GetShellIconFallback(path, isFolder, false)
            ?? GetShellIconFallback(path, isFolder, true);

        if (icon != null) IconCache[key] = icon;
        return icon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            SIZE size,
            uint flags,
            out IntPtr phbm);
    }

    private static readonly Guid ClsidShellItemImageFactory = new("461BB70F-3A36-4E0D-9D87-822EE1CB8C39");

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SHELLICONSIZE = 0x4;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    private const int SIIGBF_BIGGERSIZEOK = 0x1;
    private const int SIIGBF_SCALEUP = 0x100;

    private static BitmapSource? GetShellItemImage(string path, int size)
    {
        try
        {
            var type = Type.GetTypeFromCLSID(ClsidShellItemImageFactory);
            if (type == null || Activator.CreateInstance(type) is not IShellItemImageFactory factory) return null;
            var sz = new SIZE { cx = size, cy = size };
            if (factory.GetImage(path, sz, SIIGBF_BIGGERSIZEOK | SIIGBF_SCALEUP, out var hbm) == 0 && hbm != IntPtr.Zero)
            {
                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    if (bmp.CanFreeze) bmp.Freeze();
                    return bmp;
                }
                finally { DeleteObject(hbm); }
            }
        }
        catch { }
        return null;
    }

    private static BitmapSource? GetShellIconFallback(string path, bool isFolder, bool useFileAttributes)
    {
        try
        {
            var fi = new SHFILEINFO();
            var flags = SHGFI_ICON | SHGFI_SHELLICONSIZE;
            if (useFileAttributes) flags |= SHGFI_USEFILEATTRIBUTES;
            var h = SHGetFileInfo(path, isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL,
                ref fi, (uint)Marshal.SizeOf(fi), flags);
            if (h == IntPtr.Zero || fi.hIcon == IntPtr.Zero) return null;
            try
            {
                var bmp = Imaging.CreateBitmapSourceFromHIcon(fi.hIcon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                if (bmp.CanFreeze) bmp.Freeze();
                return bmp;
            }
            finally { DestroyIcon(fi.hIcon); }
        }
        catch { return null; }
    }

    // ---- 鑳屾櫙鐗规晥:閫愬儚绱犻€忔槑 + 姣涚幓鐠?(SetWindowCompositionAttribute) ----
    // 澶辨晥鏃朵粎淇濈暀绾€忔槑,缁濅笉浼氬嚭鐜伴粦杈广€?
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttribData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    private const int WCA_ACCENT_POLICY = 19;

    public static void SetupBackdrop(System.Windows.Window window)
    {
        ApplyAccent(new WindowInteropHelper(window).Handle, 0, 0);
    }

    public static void DisableBackdrop(IntPtr hwnd)
    {
        ApplyAccent(hwnd, 0, 0);
    }

    public static void EnableBackdrop(IntPtr hwnd)
    {
        ApplyAccent(hwnd, 0, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static (int X, int Y) GetCursorPosScreen()
    {
        GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    public static (int Left, int Top, int Right, int Bottom) GetWindowRectPx(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var r);
        return (r.Left, r.Top, r.Right, r.Bottom);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOSENDCHANGING = 0x0400;

    public static void PositionWindow(IntPtr hwnd, int x, int y)
    {
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    public static void ResizeWindow(IntPtr hwnd, int x, int y, int w, int h)
    {
        SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSENDCHANGING);
    }

    public static void PruneCache(HashSet<string> keepKeys)
    {
        foreach (var k in IconCache.Keys)
            if (!keepKeys.Contains(k))
                IconCache.TryRemove(k, out _);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static bool IsForegroundSameProcess()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        GetWindowThreadProcessId(fg, out var pid);
        return pid == Environment.ProcessId;
    }

    // ---- 鎸傞潬鍒版闈㈠眰(WorkerW):鍙瓨鍦ㄤ簬妗岄潰,鏅€氱獥鍙ｆ案杩滅洊鍦ㄤ笂闈?----

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    public static void AttachToDesktop(IntPtr hwnd)
    {
        try
        {
            var progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return;
            SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), 0x0002, 1000, out _);
            IntPtr workerw = IntPtr.Zero;
            EnumWindows((h, l) =>
            {
                var sb = new System.Text.StringBuilder(256);
                GetClassName(h, sb, sb.Capacity);
                if (sb.ToString() == "WorkerW" && FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    workerw = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (workerw != IntPtr.Zero) SetParent(hwnd, workerw);
        }
        catch { }
    }

    private static void ApplyAccent(IntPtr hwnd, int accentState, int gradientColor)
    {
        var accent = new AccentPolicy
        {
            AccentState = accentState,
            GradientColor = gradientColor
        };
        int size = Marshal.SizeOf(accent);
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttribData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = ptr,
                SizeOfData = size
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
}
