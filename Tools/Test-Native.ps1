$ErrorActionPreference = 'Stop'
$dll = (Resolve-Path (Join-Path $PSScriptRoot '..\Assets\Plugins\x86_64\echo_ggwave.dll')).Path
$env:PATH = (Split-Path $dll) + ';' + $env:PATH
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class NativeTest {
    [DllImport("echo_ggwave", CallingConvention=CallingConvention.Cdecl)] public static extern int echo_max_payload();
    [DllImport("echo_ggwave", CallingConvention=CallingConvention.Cdecl)] public static extern int echo_create(int a, int b, int mode);
    [DllImport("echo_ggwave", CallingConvention=CallingConvention.Cdecl)] public static extern void echo_destroy(int h);
    [DllImport("echo_ggwave", CallingConvention=CallingConvention.Cdecl)] public static extern int echo_encode(int h, byte[] p, int n, int protocol, int volume, [Out] float[] samples, int capacity);
    [DllImport("echo_ggwave", CallingConvention=CallingConvention.Cdecl, EntryPoint="echo_encode")] public static extern int echo_size(int h, byte[] p, int n, int protocol, int volume, IntPtr samples, int capacity);
    [DllImport("echo_ggwave", CallingConvention=CallingConvention.Cdecl)] public static extern int echo_decode(int h, float[] samples, int count, [Out] byte[] data, int capacity);
    public static void Test(string message) {
        int tx = echo_create(48000,48000,4), rx = echo_create(48000,48000,2);
        if (tx < 0 || rx < 0) throw new Exception("create failed");
        try {
            byte[] data = Encoding.UTF8.GetBytes(message);
            int count = echo_size(tx,data,data.Length,0,25,IntPtr.Zero,0);
            if (count <= 0) throw new Exception("size failed " + count);
            float[] samples = new float[count], frame = new float[1024];
            byte[] decoded = new byte[256];
            if (echo_encode(tx,data,data.Length,0,25,samples,count) != count) throw new Exception("encode failed");
            double sumSquares = 0, peak = 0;
            foreach (float sample in samples) { double magnitude = Math.Abs(sample); peak = Math.Max(peak, magnitude); sumSquares += sample * sample; }
            double rms = Math.Sqrt(sumSquares / count);
            if (peak < 0.001 || rms < 0.0001) throw new Exception("silent waveform");
            Console.WriteLine("PCM: " + count + " samples, " + (count / 48000.0).ToString("F2") + " s, peak " + peak.ToString("F4") + ", RMS " + rms.ToString("F4"));
            for (int start=0; start<count+48000; start+=1024) {
                Array.Clear(frame,0,frame.Length);
                if (start<count) Array.Copy(samples,start,frame,0,Math.Min(1024,count-start));
                int result=echo_decode(rx,frame,1024,decoded,decoded.Length);
                if (result>0) {
                    if (Encoding.UTF8.GetString(decoded,0,result)!=message) throw new Exception("mismatch");
                    return;
                }
            }
            throw new Exception("no packet decoded");
        } finally { echo_destroy(tx); echo_destroy(rx); }
    }
}
'@
if ([NativeTest]::echo_max_payload() -ne 140) { throw 'payload constant mismatch' }
foreach ($value in @('ECHO test','Hello ✓ 世界','repeat','repeat')) { [NativeTest]::Test($value); Write-Output "PASS $value" }
$tx = [NativeTest]::echo_create(48000,48000,4)
try {
    if ([NativeTest]::echo_size($tx,[byte[]]@(1),0,0,25,[IntPtr]::Zero,0) -ge 0) { throw 'empty accepted' }
    if ([NativeTest]::echo_size($tx,(New-Object byte[] 141),141,0,25,[IntPtr]::Zero,0) -ge 0) { throw 'oversized accepted' }
    Write-Output 'PASS empty and oversized rejection'
} finally { [NativeTest]::echo_destroy($tx) }
