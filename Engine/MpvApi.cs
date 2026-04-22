using System.Runtime.InteropServices;

namespace LiveWallpaper.Engine;

/// <summary>
/// Thin P/Invoke wrapper around libmpv-2.dll.
/// Updated for compatibility with mpv v2 API.
/// </summary>
internal static class MpvApi
{
    private const string Dll = "libmpv-2.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_option_string(IntPtr ctx, string name, string data);

    // mpv_format: 1 = string, 4 = int64, 5 = double, 6 = flag
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_option(IntPtr ctx, string name, int format, ref long data);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_property_string(IntPtr ctx, string name, string data);

    // Di libmpv-2.dll, mpv_set_property_long sudah dihapus. 
    // Kita gunakan mpv_set_property dengan dua overload (double dan long).
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "mpv_set_property")]
    public static extern int mpv_set_property_double(IntPtr ctx, string name, int format, ref double data);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "mpv_set_property")]
    public static extern int mpv_set_property_int64(IntPtr ctx, string name, int format, ref long data);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(IntPtr ctx, IntPtr args);

    /// <summary>Sends a command using a null-terminated array of UTF-8 strings.</summary>
    public static int Command(IntPtr ctx, params string[] args)
    {
        var ptrs = new IntPtr[args.Length + 1];
        for (int i = 0; i < args.Length; i++)
            ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
        ptrs[args.Length] = IntPtr.Zero;

        var handle = GCHandle.Alloc(ptrs, GCHandleType.Pinned);
        int result = mpv_command(ctx, handle.AddrOfPinnedObject());
        handle.Free();

        foreach (var p in ptrs)
            if (p != IntPtr.Zero) Marshal.FreeCoTaskMem(p);

        return result;
    }

    public const int MPV_FORMAT_NONE   = 0;
    public const int MPV_FORMAT_STRING = 1;
    public const int MPV_FORMAT_FLAG   = 3;
    public const int MPV_FORMAT_INT64  = 4;
    public const int MPV_FORMAT_DOUBLE = 5;

    /// <summary>Set a double property (e.g. speed, brightness).</summary>
    public static void SetDouble(IntPtr ctx, string name, double value)
    {
        mpv_set_property_double(ctx, name, MPV_FORMAT_DOUBLE, ref value);
    }

    /// <summary>Set an int64 property.</summary>
    public static void SetInt64(IntPtr ctx, string name, long value)
    {
        // Memanggil fungsi yang sudah diarahkan ke mpv_set_property di dalam DLL
        mpv_set_property_int64(ctx, name, MPV_FORMAT_INT64, ref value);
    }

    /// <summary>Convenience: set wid option (HWND as int64).</summary>
    public static int SetWid(IntPtr ctx, IntPtr hwnd)
    {
        long wid = hwnd.ToInt64();
        return mpv_set_option(ctx, "wid", MPV_FORMAT_INT64, ref wid);
    }
}
