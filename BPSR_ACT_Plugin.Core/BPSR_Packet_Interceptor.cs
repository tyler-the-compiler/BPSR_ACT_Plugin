using ACT_Plugin;
using PcapDotNet.Base;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.Ip;
using PcapDotNet.Packets.IpV4;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BPSR_ACT_Plugin.Core
{
    class BPSR_Packet_Interceptor
    {
        private DateTime cleanupLastTime;
        private const long FRAGMENT_TIMEOUT = 30000;
        private ICaptureDevice device;
        private string currentServer;
        private byte[] tcpPayloadData;
        private long tcpNextSeq = -1;
        private Dictionary<long, byte[]> tcpCache;
        private Dictionary<string, ipFragments> fragmentIPCache = new Dictionary<string, ipFragments>();
        private DateTime tcpLastTime;
        private object tcpLock = new object();
        private readonly byte[] sceneChangeSignature = new byte[] { 0x00, 0x63, 0x33, 0x53, 0x42, 0x00 };
        private readonly byte[] loginReturnSceneSignature = new byte[] { 0x00, 0x00, 0x00, 0x62, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, 0x11, 0x45, 0x14, 0x00, 0x00, 0x00, 0x00, 0x0a, 0x4e, 0x08, 0x01, 0x22, 0x24 };
        private BPSR_Packet_Processor packetProcessor;

        private struct ipFragments
        {
            public ipFragments Init()
            {
                Fragments = new List<IpDatagram> { };
                Timestamp = DateTime.Now;
                return this;
            }
            public List<IpDatagram> Fragments;
            public DateTime Timestamp;
        }

        private struct fragmentData
        {
            public int offset;
            public Datagram payload;
        }

        public BPSR_Packet_Interceptor()
        {
            packetProcessor = new BPSR_Packet_Processor();

            ClearTCPCache();
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
            device.OnPacketArrival += new PacketArrivalEventHandler(ProcessEthernetPacket);
            device.StartCapture();
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

        private void ClearTCPCache()
        {
            tcpPayloadData = new byte[] { };
            tcpNextSeq = -1;
            tcpLastTime = DateTime.MinValue;
            tcpCache = new Dictionary<long, byte[]>();
        }

        private void ProcessEthernetPacket(object sender, PacketCapture e)
        {
            var packet = new Packet(e.GetPacket().GetPacket().Bytes, DateTime.Now, DataLink.Ethernet);
            if (DateTime.Now - cleanupLastTime > TimeSpan.FromSeconds(10))
            {
                CleanUpExpiredFragments();
                cleanupLastTime = DateTime.Now;
            }

            var ethernetPacket = packet.Ethernet;
            if (ethernetPacket.EtherType != EthernetType.IpV4) { return; }

            var ipPacket = ethernetPacket.Ip;
            var sourceAddress = ((IpV4Datagram)ipPacket).Source.ToString();
            var destinationAddress = ((IpV4Datagram)ipPacket).Destination.ToString();

            var tcpBuffer = GetTCPBuffer(packet);
            if (tcpBuffer == null) { return; }

            var fullPacketBytes = new byte[ethernetPacket.HeaderLength + ((IpV4Datagram)ipPacket).RealHeaderLength + tcpBuffer.Length];
            ethernetPacket.ToArray().SubArray(0, ethernetPacket.HeaderLength).CopyTo(fullPacketBytes, 0);
            ipPacket.ToArray().SubArray(0, ((IpV4Datagram)ipPacket).RealHeaderLength).CopyTo(fullPacketBytes, ethernetPacket.HeaderLength);
            tcpBuffer.CopyTo(fullPacketBytes, ethernetPacket.HeaderLength + ((IpV4Datagram)ipPacket).RealHeaderLength);
            var reconstructedPacket = new Packet(fullPacketBytes, DateTime.Now, DataLinkKind.Ethernet);

            var tcpPacket = reconstructedPacket.Ethernet.Ip.Tcp;

            var buf = tcpBuffer.SubArray(tcpPacket.HeaderLength);
            var sourcePort = tcpPacket.SourcePort.ToString();
            var destinationPort = tcpPacket.DestinationPort.ToString();
            var sourceServer = $"{sourceAddress}:{sourcePort} -> {destinationAddress}:{destinationPort}";
            lock (tcpLock)
            {
                try
                {
                    if (currentServer != sourceServer)
                    {
                        try
                        {
                            if (buf.Length > 10 && buf[4] == 0)
                            {
                                var data = buf.SubArray(10);
                                if (data.Length > 0)
                                {
                                    using (var bReader = new BinaryReader(new MemoryStream(data)))
                                    {
                                        var data1 = new byte[0];
                                        do
                                        {
                                            var len_buf = bReader.ReadBytes(4);
                                            if (len_buf.Length == 0) { break; }
                                            var packetLength = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(len_buf, 0));
                                            if (packetLength > 0x100000 || packetLength < 4)
                                            {
                                                Debug.WriteLine($"Invalid Packet Length During Server Identification: {packetLength}. Discarding Buffer");
                                                bReader.Close();
                                                break;
                                            }
                                            data1 = bReader.ReadBytes((int)(packetLength - 4));
                                            if (data1.Length == 0) { break; }

                                            if (!data1.SubArray(5, sceneChangeSignature.Length).SequenceEqual(sceneChangeSignature))
                                            {
                                                break;
                                            }

                                            if (currentServer != sourceServer)
                                            {
                                                currentServer = sourceServer;
                                                ClearTCPCache();
                                                tcpNextSeq = tcpPacket.SequenceNumber + buf.Length;
                                                Debug.WriteLine($"Got Scene Server Address: {sourceServer}");
                                            }

                                        } while (bReader.BaseStream.Position < data.Length);
                                    }
                                }
                            }
                            if (buf.Length == 0x62)
                            {
                                if (buf.SubArray(0, 10).SequenceEqual(loginReturnSceneSignature.SubArray(0, 10)) && buf.SubArray(14, 6).SequenceEqual(loginReturnSceneSignature.SubArray(14, 6)))
                                {
                                    if (currentServer != sourceServer)
                                    {
                                        currentServer = sourceServer;
                                        ClearTCPCache();
                                        tcpNextSeq = tcpPacket.SequenceNumber + buf.Length;
                                        Debug.WriteLine($"Got Scene Server Address by Login Return Packet: {sourceServer}");
                                    }
                                }

                            }
                        }
                        catch (Exception ex) { throw ex; }
                        return;
                    }
                    if (tcpNextSeq == -1)
                    {
                        var bufAsInt = BitConverter.ToUInt32(buf, 0);
                        var bufAsIntReverseEndian = BinaryPrimitives.ReverseEndianness(bufAsInt);
                        if (buf.Length > 4 && bufAsIntReverseEndian < 0x0fffff)
                        {
                            tcpNextSeq = tcpPacket.SequenceNumber;
                        }
                        else
                        {
                            Debug.WriteLine("Unexpected TCP Capture Error, tcpNextSeq is -1");
                        }
                    }
                    if (tcpNextSeq - tcpPacket.SequenceNumber <= 0 || tcpNextSeq == -1)
                    {
                        if (!tcpCache.ContainsKey(tcpPacket.SequenceNumber))
                        {
                            tcpCache.Add(tcpPacket.SequenceNumber, buf);
                        }
                        else
                        {
                            tcpCache[tcpPacket.SequenceNumber] = buf;
                        }
                    }

                    while (true)
                    {
                        if (!tcpCache.ContainsKey(tcpNextSeq))
                        {
                            break;
                        }
                        var seq = tcpNextSeq;
                        var cachedTcpData = tcpCache[seq];
                        tcpPayloadData = tcpPayloadData.Length == 0 ? cachedTcpData : tcpPayloadData.Concat(cachedTcpData).ToArray();
                        tcpNextSeq = unchecked((long)((ulong)(seq + cachedTcpData.Length) >> 0));
                        tcpCache.Remove(seq);
                        tcpLastTime = DateTime.Now;
                    }

                    while (tcpPayloadData.Length > 4)
                    {
                        var packetSize = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(tcpPayloadData, 0));
                        if (tcpPayloadData.Length < packetSize)
                        {
                            break;
                        }
                        if (packetSize > 0x0fffff)
                        {
                            Debug.WriteLine("Invalid Length of Packet");
                            tcpPayloadData = new byte[0];
                            break;
                        }
                        if (tcpPayloadData.Length >= packetSize)
                        {
                            var pkt = tcpPayloadData.SubArray(0, (long)packetSize);
                            tcpPayloadData = tcpPayloadData.SubArray((long)packetSize);
                            packetProcessor.ProcessPacket(pkt);
                        }
                    }
                }
                catch (Exception ex) { throw ex; }
            }
        }

        private byte[] GetTCPBuffer(Packet packet)
        {
            lock (packet)
            {
                var ipPacket = packet.Ethernet.Ip;
                var ipId = ((IpV4Datagram)ipPacket).Identification.ToString();
                var isFragment = ((IpV4Datagram)ipPacket).Fragmentation.Options == IpV4FragmentationOptions.MoreFragments;
                var key = $"{ipId}-{((IpV4Datagram)ipPacket).Source.ToString()}-{((IpV4Datagram)ipPacket).Destination.ToString()}-{((IpV4Datagram)ipPacket).Protocol}";

                if (isFragment || ((IpV4Datagram)ipPacket).Fragmentation.Offset > 0)
                {
                    if (!fragmentIPCache.ContainsKey(key))
                    {
                        fragmentIPCache.Add(key, new ipFragments().Init());
                    }
                    var cacheEntry = fragmentIPCache[key];
                    cacheEntry.Fragments.Add(ipPacket);
                    cacheEntry.Timestamp = DateTime.Now;

                    if (isFragment)
                    {
                        return null;
                    }

                    var fragments = cacheEntry.Fragments;

                    var totalLength = 0;
                    var fragmentData = new List<fragmentData>();
                    foreach (var buffer in fragments)
                    {
                        var ip = buffer;
                        var fragmentOffset = ((IpV4Datagram)ip).Fragmentation.Offset / 8;
                        var payload = ip.Payload;

                        fragmentData.Add(new BPSR_Packet_Interceptor.fragmentData { offset = fragmentOffset, payload = payload });

                        var endOffset = fragmentOffset + payload.Length;

                        if (endOffset > totalLength) { totalLength = endOffset; }
                    }

                    var fullPayload = new byte[totalLength];
                    foreach (var fragment in fragmentData)
                    {
                        fragment.payload.ToArray().CopyTo(fullPayload, fragment.offset);
                    }
                    fragmentIPCache.Remove(key);
                    return fullPayload;
                }

                return ipPacket.Tcp.ToArray();
            }

        }

        private void CleanUpExpiredFragments()
        {
            var now = DateTime.Now;
            var fipc_copy = fragmentIPCache;
            var clearedFragments = 0;
            foreach (var cacheEntry in fipc_copy)
            {
                if (now - cacheEntry.Value.Timestamp > TimeSpan.FromSeconds(FRAGMENT_TIMEOUT))
                {
                    fragmentIPCache.Remove(cacheEntry.Key);
                    clearedFragments++;
                }
            }

            if (clearedFragments > 0)
            {
                Debug.WriteLine($"Cleared {clearedFragments} expired IP Fragment caches");
            }

            if (tcpLastTime != DateTime.MinValue && now - tcpLastTime > TimeSpan.FromSeconds(FRAGMENT_TIMEOUT))
            {
                Debug.WriteLine("Cannot capture next packet. Is the game closed or disconnected?");
                currentServer = "";
                ClearTCPCache();
            }
        }

    }
}
