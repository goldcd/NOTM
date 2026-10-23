using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NOTM.Core.Audio;

/// <summary>
/// Keeps specific HDMI audio devices (soundbars, AV receivers, DACs) awake by maintaining
/// an active, shared-mode inaudible WASAPI audio stream targeted specifically to each device.
///
/// Prevents the soundbar from entering power-saving sleep mode and eliminates the
/// annoying 1-2 second audio delay/clip when sound starts playing.
///
/// When the target soundbar is powered off, NOTM stops streaming and waits for it without
/// ever keeping alive any other audio output (such as headphones or PC speakers).
///
/// Concept and default 'Fluctuate' stream implementation inspired by SoundKeeper
/// by VEG (vrubleg): https://github.com/vrubleg/soundkeeper
/// </summary>
public class AudioKeepAliveService : IDisposable
{
    private readonly object _lock = new();
    private bool _isEnabled = true;
    private bool _isDisposed;
    private readonly HashSet<string> _targetEndpointIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EndpointSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled
    {
        get
        {
            lock (_lock) return _isEnabled;
        }
        set
        {
            lock (_lock)
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (_isEnabled)
                    {
                        SyncSessionsLocked();
                    }
                    else
                    {
                        StopAllSessionsLocked();
                    }
                }
            }
        }
    }

    public AudioKeepAliveService(bool startEnabled = true)
    {
        _isEnabled = startEnabled;
    }

    /// <summary>
    /// Updates the collection of target audio endpoint IDs that should be kept awake.
    /// Only the specified endpoints are kept alive. Any device that is powered off or
    /// disconnected will be gracefully waited on without affecting any other devices.
    /// </summary>
    public void SetTargetEndpoints(IEnumerable<string> endpointIds)
    {
        lock (_lock)
        {
            _targetEndpointIds.Clear();
            foreach (var id in endpointIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _targetEndpointIds.Add(id);
                }
            }

            if (_isEnabled)
            {
                SyncSessionsLocked();
            }
        }
    }

    private void SyncSessionsLocked()
    {
        // Stop sessions that are no longer targeted
        var toRemove = _sessions.Keys.Where(k => !_targetEndpointIds.Contains(k)).ToList();
        foreach (var id in toRemove)
        {
            if (_sessions.TryGetValue(id, out var session))
            {
                session.Dispose();
                _sessions.Remove(id);
            }
        }

        // Start sessions for newly targeted endpoints
        foreach (var id in _targetEndpointIds)
        {
            if (!_sessions.ContainsKey(id))
            {
                var session = new EndpointSession(id);
                _sessions[id] = session;
                session.Start();
            }
        }
    }

    private void StopAllSessionsLocked()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _sessions.Clear();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                StopAllSessionsLocked();
            }
        }
    }

    /// <summary>
    /// Represents an active, low-overhead WASAPI keep-alive session dedicated to a single
    /// audio endpoint ID (e.g. Panasonic SoundSlayer).
    /// </summary>
    private sealed class EndpointSession : IDisposable
    {
        private readonly string _endpointId;
        private CancellationTokenSource? _cts;
        private Task? _workerTask;

        public EndpointSession(string endpointId)
        {
            _endpointId = endpointId;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _workerTask = Task.Run(() => WorkerLoop(token), token);
        }

        private void WorkerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                IntPtr pFormat = IntPtr.Zero;
                Wasapi.IMMDevice? device = null;
                Wasapi.IAudioClient? audioClient = null;
                Wasapi.IAudioRenderClient? renderClient = null;

                try
                {
                    var enumerator = (Wasapi.IMMDeviceEnumerator)new Wasapi.MMDeviceEnumeratorCom();
                    int hr = enumerator.GetDevice(_endpointId, out device);
                    if (hr != 0 || device == null)
                    {
                        // Device not plugged in or turned off (e.g. soundbar powered off)
                        token.WaitHandle.WaitOne(3000);
                        continue;
                    }

                    hr = device.GetState(out int state);
                    if (hr != 0 || state != Wasapi.DEVICE_STATE_ACTIVE)
                    {
                        // Soundbar is off or in standby
                        token.WaitHandle.WaitOne(3000);
                        continue;
                    }

                    var iidAudioClient = Wasapi.IID_IAudioClient;
                    hr = device.Activate(ref iidAudioClient, Wasapi.CLSCTX_ALL, IntPtr.Zero, out object clientObj);
                    if (hr != 0 || clientObj == null)
                    {
                        token.WaitHandle.WaitOne(3000);
                        continue;
                    }

                    audioClient = (Wasapi.IAudioClient)clientObj;

                    // Get native device mix format
                    hr = audioClient.GetMixFormat(out pFormat);
                    if (hr != 0 || pFormat == IntPtr.Zero)
                    {
                        token.WaitHandle.WaitOne(3000);
                        continue;
                    }

                    // Read WAVEFORMATEX header:
                    // Offset 2: nChannels (ushort)
                    // Offset 12: nBlockAlign (ushort)
                    // Offset 14: wBitsPerSample (ushort)
                    ushort channels = (ushort)Marshal.ReadInt16(pFormat, 2);
                    ushort blockAlign = (ushort)Marshal.ReadInt16(pFormat, 12);
                    ushort bitsPerSample = (ushort)Marshal.ReadInt16(pFormat, 14);

                    if (channels == 0) channels = 2;
                    if (blockAlign == 0) blockAlign = (ushort)(channels * (bitsPerSample / 8));

                    // Initialize shared-mode stream with 1 second buffer (10,000,000 hns)
                    var sessionGuid = Guid.Empty;
                    hr = audioClient.Initialize(
                        Wasapi.AUDCLNT_SHAREMODE_SHARED,
                        0,
                        10000000,
                        0,
                        pFormat,
                        ref sessionGuid);

                    if (hr != 0)
                    {
                        token.WaitHandle.WaitOne(3000);
                        continue;
                    }

                    audioClient.GetBufferSize(out uint bufferSize);

                    var iidRenderClient = Wasapi.IID_IAudioRenderClient;
                    hr = audioClient.GetService(ref iidRenderClient, out object renderObj);
                    if (hr != 0 || renderObj == null)
                    {
                        token.WaitHandle.WaitOne(3000);
                        continue;
                    }

                    renderClient = (Wasapi.IAudioRenderClient)renderObj;

                    // Pre-fill initial buffer with silence
                    if (renderClient.GetBuffer(bufferSize, out IntPtr initialBuffer) == 0 && initialBuffer != IntPtr.Zero)
                    {
                        Wasapi.ZeroMemory(initialBuffer, (IntPtr)(bufferSize * blockAlign));
                        renderClient.ReleaseBuffer(bufferSize, 0);
                    }

                    hr = audioClient.Start();
                    if (hr != 0)
                    {
                        token.WaitHandle.WaitOne(3000);
                        continue;
                    }

                    var lastFluctuateTime = DateTime.UtcNow;
                    bool fluctuatePolarity = false;

                    // Streaming Loop: feeds minimal buffer padding every 250ms with ~0% CPU
                    while (!token.IsCancellationRequested)
                    {
                        hr = audioClient.GetCurrentPadding(out uint paddingFrames);
                        if (hr != 0)
                        {
                            // Audio device turned off or disconnected
                            break;
                        }

                        uint framesToWrite = (bufferSize > paddingFrames) ? (bufferSize - paddingFrames) : 0;

                        if (framesToWrite > 0)
                        {
                            hr = renderClient.GetBuffer(framesToWrite, out IntPtr pData);
                            if (hr == 0 && pData != IntPtr.Zero)
                            {
                                int byteCount = (int)(framesToWrite * blockAlign);
                                Wasapi.ZeroMemory(pData, (IntPtr)byteCount);

                                // SoundKeeper 'Fluctuate' mode (exact clone of CSoundSession.cpp):
                                // Minimal 24-bit/16-bit deviation from 0, negating each odd time to avoid DC bias:
                                // 0x34000001 = 1.192093E-7 = 1.0/8388607 (minimal float deviation)
                                // 0x38000100 = 3.051851E-5 = 1.0/32767 (minimal 16-bit deviation)
                                if ((DateTime.UtcNow - lastFluctuateTime).TotalSeconds >= 1.0)
                                {
                                    lastFluctuateTime = DateTime.UtcNow;
                                    fluctuatePolarity = !fluctuatePolarity;

                                    if (bitsPerSample == 32)
                                    {
                                        uint sample = fluctuatePolarity ? 0x34000001U : 0xB4000001U;
                                        for (int ch = 0; ch < channels; ch++)
                                        {
                                            Marshal.WriteInt32(pData + (ch * sizeof(float)), unchecked((int)sample));
                                        }
                                    }
                                    else if (bitsPerSample == 16)
                                    {
                                        short sample = fluctuatePolarity ? (short)1 : (short)-1;
                                        for (int ch = 0; ch < channels; ch++)
                                        {
                                            Marshal.WriteInt16(pData + (ch * sizeof(short)), sample);
                                        }
                                    }
                                }

                                renderClient.ReleaseBuffer(framesToWrite, 0);
                            }
                        }

                        token.WaitHandle.WaitOne(250);
                    }

                    audioClient?.Stop();
                }
                catch
                {
                    // Silently handle device disconnection / standby
                    token.WaitHandle.WaitOne(2500);
                }
                finally
                {
                    if (pFormat != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(pFormat);
                        pFormat = IntPtr.Zero;
                    }

                    if (renderClient != null)
                    {
                        Marshal.ReleaseComObject(renderClient);
                        renderClient = null;
                    }

                    if (audioClient != null)
                    {
                        Marshal.ReleaseComObject(audioClient);
                        audioClient = null;
                    }

                    if (device != null)
                    {
                        Marshal.ReleaseComObject(device);
                        device = null;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                    _workerTask?.Wait(1500);
                }
                catch
                {
                    // Ignore task cancellation exceptions
                }
                finally
                {
                    _cts.Dispose();
                    _cts = null;
                    _workerTask = null;
                }
            }
        }
    }
}
