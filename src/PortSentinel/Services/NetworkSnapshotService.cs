#pragma warning disable CS0649
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class NetworkSnapshotService
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;
    private const int ErrorInsufficientBuffer = 122;

    private readonly ProcessMetadataService _processes = new();

    public Task<IReadOnlyList<NetworkEntry>> CaptureAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<NetworkEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new List<NetworkEntry>();
            ReadTcp4(result);
            ReadTcp6(result);
            ReadUdp4(result);
            ReadUdp6(result);

            return result
                .OrderByDescending(static item => item.IsListener)
                .ThenBy(static item => item.Protocol, StringComparer.Ordinal)
                .ThenBy(static item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.LocalPort)
                .ToArray();
        }, cancellationToken);
    }

    private void ReadTcp4(List<NetworkEntry> destination)
    {
        ReadTable(
            (IntPtr buffer, ref int size) => GetExtendedTcpTable(
                buffer,
                ref size,
                true,
                AfInet,
                TcpTableClass.OwnerPidAll,
                0),
            row =>
            {
                var value = Marshal.PtrToStructure<MibTcpRowOwnerPid>(row);
                ProcessMetadata process = _processes.Get(unchecked((int)value.OwningPid));
                string local = new IPAddress(BitConverter.GetBytes(value.LocalAddr)).ToString();
                string remote = new IPAddress(BitConverter.GetBytes(value.RemoteAddr)).ToString();
                string state = TcpStateName(value.State);
                bool listener = value.State == 2;

                destination.Add(new NetworkEntry(
                    "TCP",
                    local,
                    DecodePort(value.LocalPort),
                    listener ? string.Empty : remote,
                    listener ? 0 : DecodePort(value.RemotePort),
                    state,
                    process.ProcessId,
                    process.Name,
                    process.Path,
                    listener,
                    !listener && IsExternal(remote)));
            },
            Marshal.SizeOf<MibTcpRowOwnerPid>());
    }

    private void ReadTcp6(List<NetworkEntry> destination)
    {
        ReadTable(
            (IntPtr buffer, ref int size) => GetExtendedTcpTable(
                buffer,
                ref size,
                true,
                AfInet6,
                TcpTableClass.OwnerPidAll,
                0),
            row =>
            {
                var value = Marshal.PtrToStructure<MibTcp6RowOwnerPid>(row);
                ProcessMetadata process = _processes.Get(unchecked((int)value.OwningPid));
                string local = new IPAddress(value.LocalAddr, value.LocalScopeId).ToString();
                string remote = new IPAddress(value.RemoteAddr, value.RemoteScopeId).ToString();
                string state = TcpStateName(value.State);
                bool listener = value.State == 2;

                destination.Add(new NetworkEntry(
                    "TCP6",
                    local,
                    DecodePort(value.LocalPort),
                    listener ? string.Empty : remote,
                    listener ? 0 : DecodePort(value.RemotePort),
                    state,
                    process.ProcessId,
                    process.Name,
                    process.Path,
                    listener,
                    !listener && IsExternal(remote)));
            },
            Marshal.SizeOf<MibTcp6RowOwnerPid>());
    }

    private void ReadUdp4(List<NetworkEntry> destination)
    {
        ReadTable(
            (IntPtr buffer, ref int size) => GetExtendedUdpTable(
                buffer,
                ref size,
                true,
                AfInet,
                UdpTableClass.OwnerPid,
                0),
            row =>
            {
                var value = Marshal.PtrToStructure<MibUdpRowOwnerPid>(row);
                ProcessMetadata process = _processes.Get(unchecked((int)value.OwningPid));
                string local = new IPAddress(BitConverter.GetBytes(value.LocalAddr)).ToString();

                destination.Add(new NetworkEntry(
                    "UDP",
                    local,
                    DecodePort(value.LocalPort),
                    string.Empty,
                    0,
                    "BOUND",
                    process.ProcessId,
                    process.Name,
                    process.Path,
                    true,
                    false));
            },
            Marshal.SizeOf<MibUdpRowOwnerPid>());
    }

    private void ReadUdp6(List<NetworkEntry> destination)
    {
        ReadTable(
            (IntPtr buffer, ref int size) => GetExtendedUdpTable(
                buffer,
                ref size,
                true,
                AfInet6,
                UdpTableClass.OwnerPid,
                0),
            row =>
            {
                var value = Marshal.PtrToStructure<MibUdp6RowOwnerPid>(row);
                ProcessMetadata process = _processes.Get(unchecked((int)value.OwningPid));
                string local = new IPAddress(value.LocalAddr, value.LocalScopeId).ToString();

                destination.Add(new NetworkEntry(
                    "UDP6",
                    local,
                    DecodePort(value.LocalPort),
                    string.Empty,
                    0,
                    "BOUND",
                    process.ProcessId,
                    process.Name,
                    process.Path,
                    true,
                    false));
            },
            Marshal.SizeOf<MibUdp6RowOwnerPid>());
    }

    private static void ReadTable(
        NativeTableReader reader,
        Action<IntPtr> consumeRow,
        int rowSize)
    {
        int size = 0;
        uint result = reader(IntPtr.Zero, ref size);
        if (result != ErrorInsufficientBuffer && result != 0)
        {
            throw new Win32Exception(unchecked((int)result));
        }

        if (size <= sizeof(uint))
        {
            return;
        }

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = reader(buffer, ref size);
            if (result != 0)
            {
                throw new Win32Exception(unchecked((int)result));
            }

            int count = Marshal.ReadInt32(buffer);
            IntPtr row = IntPtr.Add(buffer, sizeof(uint));

            for (int index = 0; index < count; index++)
            {
                consumeRow(IntPtr.Add(row, index * rowSize));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int DecodePort(uint value)
    {
        return unchecked((ushort)IPAddress.NetworkToHostOrder((short)(value & 0xFFFF)));
    }

    private static bool IsExternal(string address)
    {
        if (!IPAddress.TryParse(address, out IPAddress? ip))
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        byte[] bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return !(bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254));
        }

        return !(ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (bytes[0] & 0xFE) == 0xFC);
    }

    private static string TcpStateName(uint state) => state switch
    {
        1 => "CLOSED",
        2 => "LISTENING",
        3 => "SYN_SENT",
        4 => "SYN_RECEIVED",
        5 => "ESTABLISHED",
        6 => "FIN_WAIT_1",
        7 => "FIN_WAIT_2",
        8 => "CLOSE_WAIT",
        9 => "CLOSING",
        10 => "LAST_ACK",
        11 => "TIME_WAIT",
        12 => "DELETE_TCB",
        _ => $"STATE_{state}"
    };

    private delegate uint NativeTableReader(IntPtr buffer, ref int size);

    private enum TcpTableClass
    {
        OwnerPidAll = 5
    }

    private enum UdpTableClass
    {
        OwnerPid = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteAddr;
        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdpRowOwnerPid
    {
        public uint LocalAddr;
        public uint LocalPort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        bool order,
        int ipVersion,
        TcpTableClass tableClass,
        uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr udpTable,
        ref int size,
        bool order,
        int ipVersion,
        UdpTableClass tableClass,
        uint reserved);
}
#pragma warning restore CS0649
