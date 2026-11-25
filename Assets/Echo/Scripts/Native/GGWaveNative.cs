using System.Runtime.InteropServices;

namespace Echo {
internal static class GGWaveNative {
    const string Library = "echo_ggwave";
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int echo_max_payload();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int echo_create(int inputRate, int outputRate, int mode);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void echo_destroy(int handle);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int echo_encode(int handle, byte[] payload, int length, int protocol, int volume, [Out] float[] output, int capacitySamples);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "echo_encode")] internal static extern int echo_encode_size(int handle, byte[] payload, int length, int protocol, int volume, System.IntPtr output, int capacitySamples);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int echo_decode(int handle, float[] samples, int count, [Out] byte[] payload, int capacity);
}
}
