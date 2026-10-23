using System;
using System.Runtime.InteropServices;

namespace NOTM.Core.Audio;

/// <summary>
/// Native WASAPI (Windows Audio Session API) COM interfaces and definitions.
/// Used for zero-dependency, low-overhead audio keep-alive streaming.
/// Inspired by SoundKeeper (https://github.com/vrubleg/soundkeeper).
/// </summary>
internal static class Wasapi
{
    public const int eRender = 0;
    public const int eMultimedia = 1;

    public const int DEVICE_STATE_ACTIVE = 0x00000001;
    public const int STGM_READ = 0x00000000;

    public const int AUDCLNT_SHAREMODE_SHARED = 0;
    public const int AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    public const int AUDCLNT_BUFFERFLAGS_SILENT = 0x00000002;

    public const int CLSCTX_ALL = 23;

    public static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    public static readonly Guid IID_IAudioRenderClient = new("F294AC80-3186-4283-9280-E0A6037F4C4B");

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorCom
    {
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        int GetCount(out uint pcDevices);
        int Item(uint nDevice, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        int GetState(out int pdwState);
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out IntPtr pkey);
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pwszVal;
        [FieldOffset(8)] public uint uintVal;
    }

    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, ref Guid audioSessionGuid);
        int GetBufferSize(out uint pNumBufferFrames);
        int GetStreamLatency(out long phnsLatency);
        int GetCurrentPadding(out uint pNumPaddingFrames);
        int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
        int GetMixFormat(out IntPtr ppDeviceFormat);
        int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr eventHandle);
        int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    [Guid("F294AC80-3186-4283-9280-E0A6037F4C4B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient
    {
        int GetBuffer(uint numFramesRequested, out IntPtr ppData);
        int ReleaseBuffer(uint numFramesWritten, int dwFlags);
    }

    [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
    public static extern void ZeroMemory(IntPtr dest, IntPtr size);

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PROPVARIANT pvar);

    public record AudioEndpointInfo(string Id, string FriendlyName);

    /// <summary>
    /// Enumerates all currently active audio render endpoints in Windows.
    /// </summary>
    public static System.Collections.Generic.List<AudioEndpointInfo> GetActiveAudioEndpoints()
    {
        var list = new System.Collections.Generic.List<AudioEndpointInfo>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            int hr = enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out IMMDeviceCollection coll);
            if (hr == 0 && coll != null)
            {
                coll.GetCount(out uint count);
                var pkeyFriendlyName = new PROPERTYKEY
                {
                    fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                    pid = 14
                };

                for (uint i = 0; i < count; i++)
                {
                    if (coll.Item(i, out IMMDevice dev) == 0 && dev != null)
                    {
                        try
                        {
                            dev.GetId(out string id);
                            string friendlyName = string.Empty;
                            if (dev.OpenPropertyStore(STGM_READ, out IPropertyStore store) == 0 && store != null)
                            {
                                try
                                {
                                    if (store.GetValue(ref pkeyFriendlyName, out PROPVARIANT pv) == 0)
                                    {
                                        if (pv.vt == 31 && pv.pwszVal != IntPtr.Zero) // VT_LPWSTR
                                        {
                                            friendlyName = Marshal.PtrToStringUni(pv.pwszVal) ?? string.Empty;
                                        }
                                        PropVariantClear(ref pv);
                                    }
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(store);
                                }
                            }

                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(friendlyName))
                            {
                                list.Add(new AudioEndpointInfo(id, friendlyName));
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(dev);
                        }
                    }
                }
                Marshal.ReleaseComObject(coll);
            }
        }
        catch
        {
            // Fallback gracefully on systems with restricted audio service
        }
        return list;
    }
}
