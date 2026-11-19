using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace QuizAssistant
{
    public static class AudioCapture
    {
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig] int Initialize(int ShareMode, int StreamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, ref Guid AudioSessionGuid);
            [PreserveSig] int GetBufferSize(out uint pNumBufferFrames);
            [PreserveSig] int GetStreamLatency(out long phnsLatency);
            [PreserveSig] int GetCurrentPadding(out uint pNumPaddingFrames);
            [PreserveSig] int IsFormatSupported(int ShareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
            [PreserveSig] int GetMixFormat(out IntPtr ppDeviceFormat);
            [PreserveSig] int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr eventHandle);
            [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out int pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
            [PreserveSig] int ReleaseBuffer(uint NumFramesRead);
            [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        private const int TARGET_SAMPLE_RATE = 16000;
        private const int MAX_BUFFER_SECONDS = 120; // 2 minutes
        private const int BUFFER_SIZE_BYTES = TARGET_SAMPLE_RATE * 2 * MAX_BUFFER_SECONDS; // 16kHz * 2 bytes * 120s = 3.84 MB

        private static byte[] ringBuffer = new byte[BUFFER_SIZE_BYTES];
        private static int writePos = 0;
        private static int totalWritten = 0;
        private static readonly object bufferLock = new object();

        private static Thread captureThread;
        private static volatile bool isRunning = false;
        private static DateTime lastActivityTime = DateTime.MinValue;

        public static void Start()
        {
            if (isRunning) return;
            isRunning = true;
            captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "WasapiAudioCaptureThread"
            };
            captureThread.Start();
        }

        public static void Stop()
        {
            isRunning = false;
        }

        public static bool HasRecentAudio(int withinSeconds = 30)
        {
            return (DateTime.UtcNow - lastActivityTime).TotalSeconds <= withinSeconds;
        }

        public static byte[] GetLastAudioWavBytes(int maxSeconds = 90)
        {
            lock (bufferLock)
            {
                int reqBytes = Math.Min(BUFFER_SIZE_BYTES, TARGET_SAMPLE_RATE * 2 * maxSeconds);
                int availableBytes = Math.Min(totalWritten, reqBytes);
                if (availableBytes <= 0) return null;

                byte[] pcmData = new byte[availableBytes];
                int startPos = (writePos - availableBytes + BUFFER_SIZE_BYTES) % BUFFER_SIZE_BYTES;

                for (int i = 0; i < availableBytes; i++)
                {
                    pcmData[i] = ringBuffer[(startPos + i) % BUFFER_SIZE_BYTES];
                }

                return CreateWavHeaderAndData(pcmData, TARGET_SAMPLE_RATE, 1, 16);
            }
        }

        private static byte[] CreateWavHeaderAndData(byte[] pcmData, int sampleRate, short channels, short bitsPerSample)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                short blockAlign = (short)(channels * (bitsPerSample / 8));

                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + pcmData.Length);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write(channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write(blockAlign);
                bw.Write(bitsPerSample);
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(pcmData.Length);
                bw.Write(pcmData);

                return ms.ToArray();
            }
        }

        private static void CaptureLoop()
        {
            while (isRunning)
            {
                IMMDevice device = null;
                IAudioClient client = null;
                IAudioCaptureClient capture = null;
                IntPtr pFormat = IntPtr.Zero;

                try
                {
                    IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    int hr = enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out device);
                    if (hr != 0 || device == null)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
                    object clientObj;
                    hr = device.Activate(ref IID_IAudioClient, 1, IntPtr.Zero, out clientObj);
                    if (hr != 0 || clientObj == null)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    client = (IAudioClient)clientObj;
                    hr = client.GetMixFormat(out pFormat);
                    if (hr != 0 || pFormat == IntPtr.Zero)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    short wFormatTag = Marshal.ReadInt16(pFormat, 0);
                    short nChannels = Marshal.ReadInt16(pFormat, 2);
                    int nSamplesPerSec = Marshal.ReadInt32(pFormat, 4);
                    short wBitsPerSample = Marshal.ReadInt16(pFormat, 14);

                    long bufferDuration = 10000000; // 1 second
                    Guid emptyGuid = Guid.Empty;
                    hr = client.Initialize(0 /* SHARED */, 0x00020000 /* LOOPBACK */, bufferDuration, 0, pFormat, ref emptyGuid);
                    if (hr != 0)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
                    object captureObj;
                    hr = client.GetService(ref IID_IAudioCaptureClient, out captureObj);
                    if (hr != 0 || captureObj == null)
                    {
                        Thread.Sleep(2000);
                        continue;
                    }

                    capture = (IAudioCaptureClient)captureObj;
                    client.Start();

                    // Resampling variables
                    double resampleRatio = (double)nSamplesPerSec / TARGET_SAMPLE_RATE;
                    double resamplePos = 0.0;

                    while (isRunning)
                    {
                        uint packetFrames;
                        hr = capture.GetNextPacketSize(out packetFrames);
                        if (hr != 0 || packetFrames == 0)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        IntPtr pData;
                        uint numFramesRead;
                        int flags;
                        ulong devPos, qpcPos;
                        hr = capture.GetBuffer(out pData, out numFramesRead, out flags, out devPos, out qpcPos);
                        if (hr == 0 && numFramesRead > 0 && pData != IntPtr.Zero)
                        {
                            bool isSilent = (flags & 0x00000001) != 0; // AUDCLNT_BUFFERFLAGS_SILENT

                            if (!isSilent)
                            {
                                ProcessAudioFrames(pData, numFramesRead, nChannels, wBitsPerSample, resampleRatio, ref resamplePos);
                            }

                            capture.ReleaseBuffer(numFramesRead);
                        }
                    }

                    client.Stop();
                }
                catch
                {
                    Thread.Sleep(1500);
                }
                finally
                {
                    if (pFormat != IntPtr.Zero)
                    {
                        try { Marshal.FreeCoTaskMem(pFormat); } catch { }
                    }
                }
            }
        }

        private static void ProcessAudioFrames(IntPtr pData, uint numFrames, int channels, int bitsPerSample, double resampleRatio, ref double resamplePos)
        {
            // Windows loopback is almost always 32-bit IEEE float (channels = 2)
            bool isFloat = (bitsPerSample == 32);
            float sumSquares = 0f;
            int countedSamples = 0;

            int bytesPerFrame = channels * (bitsPerSample / 8);
            byte[] frameBytes = new byte[numFrames * bytesPerFrame];
            Marshal.Copy(pData, frameBytes, 0, frameBytes.Length);

            lock (bufferLock)
            {
                for (int f = 0; f < numFrames; f++)
                {
                    resamplePos += 1.0;
                    if (resamplePos >= resampleRatio)
                    {
                        resamplePos -= resampleRatio;

                        float monoSample = 0f;
                        int offset = f * bytesPerFrame;

                        if (isFloat)
                        {
                            float ch1 = BitConverter.ToSingle(frameBytes, offset);
                            float ch2 = (channels > 1) ? BitConverter.ToSingle(frameBytes, offset + 4) : ch1;
                            monoSample = (ch1 + ch2) * 0.5f;
                        }
                        else if (bitsPerSample == 16)
                        {
                            short s1 = BitConverter.ToInt16(frameBytes, offset);
                            short s2 = (channels > 1) ? BitConverter.ToInt16(frameBytes, offset + 2) : s1;
                            monoSample = ((float)(s1 + s2)) / 65536.0f;
                        }

                        // Clamp sample [-1.0, 1.0]
                        if (monoSample > 1.0f) monoSample = 1.0f;
                        if (monoSample < -1.0f) monoSample = -1.0f;

                        sumSquares += (monoSample * monoSample);
                        countedSamples++;

                        short pcmShort = (short)(monoSample * 32767f);
                        byte low = (byte)(pcmShort & 0xFF);
                        byte high = (byte)((pcmShort >> 8) & 0xFF);

                        ringBuffer[writePos] = low;
                        writePos = (writePos + 1) % BUFFER_SIZE_BYTES;
                        ringBuffer[writePos] = high;
                        writePos = (writePos + 1) % BUFFER_SIZE_BYTES;

                        if (totalWritten < BUFFER_SIZE_BYTES) totalWritten += 2;
                    }
                }
            }

            if (countedSamples > 0)
            {
                float rms = (float)Math.Sqrt(sumSquares / countedSamples);
                if (rms > 0.003f) // Auditory activity threshold
                {
                    lastActivityTime = DateTime.UtcNow;
                }
            }
        }
    }
}
