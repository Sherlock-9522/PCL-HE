using System;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace PCL.Core.Utils.OS;

public static partial class WindowInterop
{
    // ReSharper disable InconsistentNaming UnusedMember.Local

    // DWM 外边缘结构定义
    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int leftWidth, rightWidth, topHeight, bottomHeight; }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool pfEnabled);

    /// <summary>
    /// 获取窗口在屏幕上实际可见的边界（即排除 DWM 为你绘制的那圈不可见阴影）。
    /// </summary>
    private const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(
        IntPtr hWnd, uint dwAttribute, out RECT pvAttribute, uint cbAttribute);

    // Win32 矩形结构定义
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left; public int top; public int right; public int bottom; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // MONITOR_DPI_TYPE enum
    private enum MONITOR_DPI_TYPE {
        MDT_EFFECTIVE_DPI = 0,
        MDT_ANGULAR_DPI = 1,
        MDT_RAW_DPI = 2,
        MDT_DEFAULT = MDT_EFFECTIVE_DPI
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    // Get the primary monitor handle
    private const int MONITOR_DEFAULTTOPRIMARY = 1;

    [LibraryImport("shcore.dll", EntryPoint = "GetDpiForMonitor")]
    private static partial int GetDpiForMonitor(
        IntPtr hMonitor,
        MONITOR_DPI_TYPE dpiType,
        out uint dpiX,
        out uint dpiY
    );

    // ReSharper enable InconsistentNaming UnusedMember.Local

    /// <summary>
    /// 检测 DWM 组合是否可用
    /// </summary>
    public static bool IsCompositionEnabled()
    {
        var hResult = DwmIsCompositionEnabled(out var enabled);
        return hResult != 0 ? throw new Win32Exception(hResult, "Failed to check DWM status") : enabled;
    }

    /// <summary>
    /// 设置 DWM 窗口边框到客户区域的扩展大小
    /// </summary>
    public static void ExtendFrameIntoClientArea(
        IntPtr hWnd, int marginLeft, int marginTop, int marginRight, int marginBottom)
    {
        MARGINS margins = new()
        {
            leftWidth = marginLeft,
            rightWidth = marginRight,
            topHeight = marginTop,
            bottomHeight = marginBottom
        };
        if (!IsCompositionEnabled()) return;
        var hResult = DwmExtendFrameIntoClientArea(hWnd, ref margins);
        if (hResult != 0) throw new Win32Exception(hResult, "Failed to extend frame into client area");
    }

    /// <summary>
    /// See <see cref="ExtendFrameIntoClientArea(IntPtr, int, int, int, int)"/>
    /// </summary>
    public static void ExtendFrameIntoClientArea(IntPtr hWnd, int margin)
        => ExtendFrameIntoClientArea(hWnd, margin, margin, margin, margin);

    /// <summary>
    /// 获取 Win32 窗口矩形定义
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom) GetWindowRectangle(IntPtr hWnd)
    {
        var hResult = GetWindowRect(hWnd, out var rect);
        return hResult ? (rect.left, rect.top, rect.right, rect.bottom)
            : throw new Win32Exception("Failed to get window rectangle");
    }

    /// <summary>
    /// 获取窗口在屏幕上实际可见的边界。
    /// <para>
    /// <see cref="GetWindowRectangle"/> 返回的是包含 DWM 阴影边框的矩形，比用户看到的窗口
    /// 四周各外扩若干像素，因此不能直接用来判断鼠标落在哪条边上。这里改用
    /// DWMWA_EXTENDED_FRAME_BOUNDS 取真实可见区域；DWM 不可用时回退到窗口矩形。
    /// </para>
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom) GetVisibleWindowRectangle(IntPtr hWnd)
    {
        try
        {
            if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var visible,
                    (uint)Marshal.SizeOf<RECT>()) == 0
                && visible.right > visible.left && visible.bottom > visible.top)
                return (visible.left, visible.top, visible.right, visible.bottom);
        }
        catch (Exception)
        {
            // 取不到可见边界时退回窗口矩形，至少还能缩放
        }

        return GetWindowRectangle(hWnd);
    }

    #region 缩放热区光标

    /// <summary>
    /// 窗口缩放的边角。
    /// </summary>
    public enum ResizeEdge
    {
        None,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private const int IDC_SIZENWSE = 32642; // 左上-右下
    private const int IDC_SIZENESW = 32643; // 右上-左下
    private const int IDC_SIZEWE = 32644;   // 左右
    private const int IDC_SIZENS = 32645;   // 上下

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static partial IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetCursor(IntPtr hCursor);

    /// <summary>
    /// 为窗口缩放热区设置对应的系统缩放光标。
    /// <para>
    /// 系统是在处理 <c>WM_NCHITTEST</c> 时顺手把光标换成缩放形状的。我们自己接管了这条消息、
    /// 直接返回 <c>HTLEFT</c> 之类的命中码，就不再走 <c>DefWindowProc</c>，也就没人替我们设光标了。
    /// 结果是鼠标压在被判定为热区的边框上，光标仍是箭头，用户完全看不出这里能拖——
    /// 手感上就表现为「边框和实际操作区对不上」。
    /// </para>
    /// </summary>
    /// <param name="edge">当前命中的热区边角</param>
    public static void SetResizeCursor(ResizeEdge edge)
    {
        var cursorId = edge switch
        {
            ResizeEdge.Left or ResizeEdge.Right => IDC_SIZEWE,
            ResizeEdge.Top or ResizeEdge.Bottom => IDC_SIZENS,
            ResizeEdge.TopLeft or ResizeEdge.BottomRight => IDC_SIZENWSE,
            ResizeEdge.TopRight or ResizeEdge.BottomLeft => IDC_SIZENESW,
            _ => 0
        };

        if (cursorId == 0)
            return;

        SetCursor(LoadCursor(IntPtr.Zero, new IntPtr(cursorId)));
    }

    #endregion

    /// <summary>
    /// 获取 Win32 窗口位置与大小
    /// </summary>
    public static (int X, int Y, int Width, int Height) ToWindowBounds(
        this (int Left, int Top, int Right, int Bottom) rect)
    {
        var (l, t, r, b) = rect;
        var x = l;
        var y = t;
        var width = r - l;
        var height = b - t;
        return (x, y, width, height);
    }

    /// <summary>
    /// 获取指定屏幕的系统 DPI
    /// </summary>
    /// <param name="hWnd">位于指定屏幕上的任意窗口句柄，默认指定主屏</param>
    public static int GetSystemDpi(IntPtr hWnd = 0) {
        // Get the monitor handle
        var hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
        // 0 is S_OK
        var hr = GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _);
        if (hr == 0)
            return (int)dpiX;
        // fallback to default DPI (96)
        return 96;
    }
}
