using System.Runtime.InteropServices;
using Avalonia;

namespace Clawd;

/// <summary>Windows-only helpers: taskbar rect, keep-on-top, hide from Alt+Tab. No-ops on Linux.</summary>
static class Win
{
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string cls, string? title);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int idx, int val);

    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO p);

    /// <summary>Seconds since the last keyboard / mouse input anywhere in Windows.</summary>
    public static double IdleSeconds()
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var i = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref i)) return 0;
        return unchecked((uint)Environment.TickCount - i.dwTime) / 1000.0;
    }

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);

    public static bool TryGetCursor(out int x, out int y)
    {
        x = y = 0;
        if (!OperatingSystem.IsWindows() || !GetCursorPos(out var p)) return false;
        x = p.X; y = p.Y;
        return true;
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

    public static bool IsAltDown() => OperatingSystem.IsWindows() && (GetAsyncKeyState(0x12) & 0x8000) != 0;

    /// <summary>WS_EX_TRANSPARENT | WS_EX_LAYERED: mouse clicks fall through the window to whatever is below.</summary>
    public static void SetClickThrough(IntPtr hwnd, bool on)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero) return;
        int ex = GetWindowLong(hwnd, -20);
        int want = on ? ex | 0x20 | 0x80000 : ex & ~0x20;
        if (want != ex) SetWindowLong(hwnd, -20, want);
    }

    public static bool TryGetTaskbar(out PixelRect r)
    {
        r = default;
        if (!OperatingSystem.IsWindows()) return false;
        var h = FindWindowW("Shell_TrayWnd", null);
        if (h == IntPtr.Zero || !GetWindowRect(h, out var q)) return false;
        r = new PixelRect(q.Left, q.Top, q.Right - q.Left, q.Bottom - q.Top);
        return true;
    }

    // The taskbar is topmost too, so we re-raise ourselves periodically.
    public static void Raise(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero) return;
        // HWND_TOPMOST, NOSIZE | NOMOVE | NOACTIVATE | SHOWWINDOW
        SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
    }

    public static void MakeToolWindow(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero) return;
        int ex = GetWindowLong(hwnd, -20);                       // GWL_EXSTYLE
        SetWindowLong(hwnd, -20, ex | 0x00000080 | 0x08000000);  // TOOLWINDOW | NOACTIVATE
    }
}
