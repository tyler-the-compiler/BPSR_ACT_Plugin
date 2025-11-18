using Newtonsoft.Json.Linq;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BPSR_ACT_Plugin.Core
{
    public class BPSR_Packet_InterceptorV2
    {
        public string CurrentServer { get; set; } = string.Empty;

        private readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10);
        private readonly TimeSpan GapTimeout = TimeSpan.FromSeconds(2);
        private DateTime LastAnyPacketAt = DateTime.MinValue;
        private DateTime? WaitingGapSince;
        private readonly byte[] ServerSignature = { 0x00, 0x63, 0x33, 0x53, 0x42, 0x00 };
        private readonly byte[] LoginReturnSignature = { 0x00, 0x00, 0x00, 0x62, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x11, 0x45, 0x14, 0x00, 0x00, 0x00, 0x00, 0x0a, 0x4e, 0x08, 0x01, 0x22, 0x24 };
        private uint? TcpNextSeq { get; set; }
        private ConcurrentDictionary<uint, byte[]> TcpCache { get; } = new ConcurrentDictionary<uint, byte[]>();
        private DateTime TcpLastTime { get; set; } = DateTime.MinValue;
        private object TcpLock { get; } = new object();
        private static readonly object padlock = new object();
        private MemoryStream TcpStream { get; } = new MemoryStream();
        private ConcurrentDictionary<uint, DateTime> TcpCacheTime { get; } = new ConcurrentDictionary<uint, DateTime>();
        private BPSR_Packet_Processor packetProcessor;
        private ICaptureDevice device;
        private static BPSR_Packet_InterceptorV2 instance;
        public static BPSR_Packet_InterceptorV2 Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new BPSR_Packet_InterceptorV2();
                    }
                    return instance;
                }
            }
        }

        private BPSR_Packet_InterceptorV2()
        {
            packetProcessor = new BPSR_Packet_Processor();
        }

        public void Start()
        {
            device = AutoFindNetworkDevice();
            device.Open(new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                Immediate = true,
                ReadTimeout = 1000,
                BufferSize = 1024 * 1024 * 4
            });
            device.Filter = "ip and tcp";
            device.OnPacketArrival += new PacketArrivalEventHandler(HandlePacket);
            device.StartCapture();
        }

        public void Stop()
        {

        }

        private void HandlePacket(object sender, PacketCapture e)
        {
            try
            {
                var raw = e.GetPacket();
                var ret = TryEnlistData(raw);
                if (!ret)
                {
                    Debug.WriteLine("Packet enlist failed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Packet enlist failed");
#if DEBUG
                throw;
#endif
            }
        }

        private bool TryEnlistData(RawCapture data)
        {
            var ret = StartNewAnalyzer(null, data);
            ret.ConfigureAwait(false);
            return ret.Result;
        }

        public Task EnlistDataAsync(RawCapture data, CancellationToken token = default)
        {
            return StartNewAnalyzer(null, data);
        }

        private Task<bool> StartNewAnalyzer(ICaptureDevice device, RawCapture raw)
        {
            return Task.Run(() =>
            {
                try
                {
                    HandleRaw(device, raw);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Packet interceptor crashed");
                    return false;
                }
            });
        }

        private ICaptureDevice AutoFindNetworkDevice()
        {
            Debug.WriteLine("Auto detecting network interface");
            var allDevices = CaptureDeviceList.Instance;
            if (allDevices.Count == 0)
            {
                Debug.WriteLine("No Devices Found");
                return null;
            }
            var routePrintString = ExecuteRouteCommand();
            try
            {
                var trimmedOption = routePrintString
                .Split('\n')
                .FirstOrDefault(l => l.Trim().StartsWith("0.0.0.0"))
                .Trim();
                var defaultInterface = Regex.Split(trimmedOption, @"\s+")[3];

                var iface = allDevices.FirstOrDefault(d => ((LibPcapLiveDevice)d).Addresses.FirstOrDefault(a => a.Addr.ToString().Trim().Equals(defaultInterface)) != null);
                Debug.WriteLine($"Using network interface: {iface.Description}");
                return iface;
            }
            catch { return null; }
        }

        private string ExecuteRouteCommand()
        {
            var psi = new ProcessStartInfo("CMD.exe", @"/C route print 0.0.0.0")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            var exitCode = proc.ExitCode;
            proc.Close();
            return output;
        }

        internal void ProcessInline(RawCapture raw)
        {
            HandleRaw(device: null, raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForceReconnect(string reason)
        {
            ResetCaptureState();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ForceResyncTo(uint seq)
        {
            TcpCache.Clear();
            TcpStream.Position = 0;
            TcpStream.SetLength(0);
            TcpNextSeq = seq;
            WaitingGapSince = null;
            TcpLastTime = DateTime.Now;
        }

        private void ClearTcpCache()
        {
            TcpNextSeq = null;
            TcpLastTime = DateTime.MinValue;
            TcpCache.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SeqCmp(uint a, uint b)
        {
            return unchecked((int)(a - b));
        }

        private void HandleRaw(ICaptureDevice device, RawCapture raw)
        {
            try
            {
                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);
                var tcpPacket = packet.Extract<TcpPacket>();
                if (tcpPacket == null)
                {
                    return;
                }

                var ipv4Packet = packet.Extract<IPv4Packet>();
                if (ipv4Packet == null)
                {
                    return;
                }

                var payload = tcpPacket.PayloadData;
                if (payload == null || payload.Length == 0)
                {
                    return;
                }

                var srcServer = $"{ipv4Packet.SourceAddress}:{tcpPacket.SourcePort} -> {ipv4Packet.DestinationAddress}:{tcpPacket.DestinationPort}";
                var revServer = $"{ipv4Packet.DestinationAddress}:{tcpPacket.DestinationPort} -> {ipv4Packet.SourceAddress}:{tcpPacket.SourcePort}";

                var now = DateTime.Now;
                lock (TcpLock)
                {
                    if (!String.IsNullOrEmpty(CurrentServer))
                    {
                        if (CurrentServer == srcServer || CurrentServer == revServer)
                        {
                            LastAnyPacketAt = now;
                        }
                        if (LastAnyPacketAt != DateTime.MinValue && now - LastAnyPacketAt > IdleTimeout)
                        {
                            ForceReconnect("idle timeout");
                        }
                    }

                    if (CurrentServer != srcServer)
                    {
                        try
                        {
                            if (payload.Length > 10 && payload[4] == 0)
                            {
                                var data = payload.AsSpan(10);
                                if (data.Length > 0)
                                {
                                    using (var payloadMs = new MemoryStream(data.ToArray()))
                                    {
                                        byte[] tmp;
                                        do
                                        {
                                            var lenBuffer = new byte[4];
                                            if (payloadMs.Read(lenBuffer, 0, 4) != 4)
                                            {
                                                break;
                                            }
                                            var len = lenBuffer.ReadInt32BigEndian();
                                            if (len < 4 || len > payloadMs.Length - 4)
                                            {
                                                Debug.WriteLine($"Invalid Packet Length During Server Identification: {len}. Discarding Buffer");
                                                payloadMs.Close();
                                                break;
                                            }

                                            tmp = new byte[len - 4];

                                            if (payloadMs.Read(tmp, 0, tmp.Length) != tmp.Length)
                                            {
                                                break;
                                            }

                                            if (!tmp.Skip(5).Take(ServerSignature.Length).SequenceEqual(ServerSignature))
                                            {
                                                break;
                                            }

                                            try
                                            {
                                                if (CurrentServer != srcServer)
                                                {
                                                    var prevServer = CurrentServer;
                                                    CurrentServer = srcServer;

                                                    ClearTcpCache();

                                                    TcpNextSeq = tcpPacket.SequenceNumber + (uint)payload.Length;

                                                    Debug.WriteLine($"Got Scene Server Address: {srcServer}");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Debug.WriteLine("Error while detecting scene server");
                                            }
                                        } while (tmp.Length > 0);
                                    }
                                }


                            }
                            if (payload.Length == 0x62)
                            {
                                if (payload.AsSpan(0, 10).SequenceEqual(LoginReturnSignature.AsSpan(0, 10)) && payload.AsSpan(14, 6).SequenceEqual(LoginReturnSignature.AsSpan(14, 6)))
                                {
                                    if (CurrentServer != srcServer)
                                    {
                                        var prevServer = CurrentServer;
                                        CurrentServer = srcServer;

                                        ClearTcpCache();

                                        TcpNextSeq = tcpPacket.SequenceNumber + (uint)payload.Length;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Error in server detection phase");
                        }

                        return;
                    }

                    if (TcpNextSeq == null)
                    {
                        if (payload.Length > 4 && BinaryPrimitives.ReadUInt32BigEndian(payload) < 0x0fffff)
                        {
                            TcpNextSeq = tcpPacket.SequenceNumber;
                        }
                    }

                    if (TcpNextSeq != null)
                    {
                        var cmp = SeqCmp(tcpPacket.SequenceNumber, TcpNextSeq.Value);
                        if (cmp > 0)
                        {
                            WaitingGapSince = WaitingGapSince ?? now;
                            if (now - WaitingGapSince.Value > GapTimeout)
                            {
                                ForceResyncTo(tcpPacket.SequenceNumber);
                            }
                        }
                        else if (cmp == 0)
                        {
                            WaitingGapSince = null;
                        }
                    }

                    if (TcpNextSeq == null || SeqCmp(tcpPacket.SequenceNumber, TcpNextSeq.Value) >= 0)
                    {
                        TcpCache[tcpPacket.SequenceNumber] = payload.ToArray();
                    }

                    using (var messageMs = new MemoryStream(4096))
                    {
                        while (TcpNextSeq != null && TcpCache.TryRemove(TcpNextSeq.Value, out var cachedTcpData))
                        {
                            messageMs.Write(cachedTcpData, 0, cachedTcpData.Length);
                            unchecked
                            {
                                TcpNextSeq += (uint)cachedTcpData.Length;
                            }

                            TcpLastTime = now;
                            LastAnyPacketAt = now;
                        }

                        if (messageMs.Length > 0)
                        {
                            var endPos = TcpStream.Length;
                            TcpStream.Position = endPos;
                            messageMs.Position = 0;
                            messageMs.CopyTo(TcpStream);
                        }
                    }

                    TcpStream.Position = 0;

                    var lenBuf = new byte[4];
                    while (true)
                    {
                        var start = TcpStream.Position;
                        if (TcpStream.Length - start < 4) { break; }

                        var n = TcpStream.Read(lenBuf, 0, 4);
                        if (n < 4)
                        {
                            TcpStream.Position = start;
                            break;
                        }

                        var packetSize = BinaryPrimitives.ReadInt32BigEndian(lenBuf);

                        if (packetSize <= 4 || packetSize > 0x0FFFFF)
                        {
                            TcpStream.Position = start;
                            break;
                        }

                        if (TcpStream.Length - start < packetSize)
                        {
                            TcpStream.Position = start;
                            break;
                        }

                        TcpStream.Position = start;
                        var messagePacket = new byte[packetSize];
                        var read = TcpStream.Read(messagePacket, 0, packetSize);
                        if (read != packetSize)
                        {
                            TcpStream.Position = start;
                            break;
                        }

                        packetProcessor.ProcessPacket(messagePacket);
                    }

                    if (TcpStream.Position > 0)
                    {
                        var remain = TcpStream.Length - TcpStream.Position;
                        if (remain > 0)
                        {
                            var buffer = TcpStream.GetBuffer();
                            Buffer.BlockCopy(buffer, (int)TcpStream.Position, buffer, 0, (int)remain);
                            TcpStream.Position = 0;
                            TcpStream.SetLength(remain);
                        }
                        else
                        {
                            TcpStream.SetLength(0);
                            TcpStream.Position = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        public void ResetCaptureState()
        {
            lock (TcpLock)
            {
                CurrentServer = string.Empty;
                TcpNextSeq = null;
                TcpLastTime = DateTime.MinValue;

                TcpCache.Clear();
                TcpCacheTime.Clear();

                if (TcpStream.Capacity > 1 << 20)
                {
                    TcpStream.Position = 0;
                    TcpStream.SetLength(0);
                    try
                    {
                        TcpStream.Dispose();
                    }
                    catch { }
                }
                else
                {
                    TcpStream.Position = 0;
                    TcpStream.SetLength(0);
                }
            }
        }

    }
}
