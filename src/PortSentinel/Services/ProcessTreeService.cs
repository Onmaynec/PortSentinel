using System.ComponentModel;
using System.Runtime.InteropServices;
using PortSentinel.Models;

namespace PortSentinel.Services;

internal sealed class ProcessTreeService
{
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public IReadOnlyList<ProcessTreeNode> Capture(IReadOnlyList<NetworkEntry> entries)
    {
        Dictionary<int, ProcessRow> processes = ReadProcesses();
        Dictionary<int, int> networkCounts = entries
            .GroupBy(entry => entry.ProcessId)
            .ToDictionary(group => group.Key, group => group.Count());

        var included = new HashSet<int>();
        foreach (int processId in networkCounts.Keys)
        {
            int current = processId;
            var visited = new HashSet<int>();
            while (current > 0 && visited.Add(current) && processes.TryGetValue(current, out ProcessRow? row))
            {
                included.Add(current);
                current = row.ParentProcessId;
            }
        }

        var children = included
            .Select(processId => processes[processId])
            .GroupBy(row => included.Contains(row.ParentProcessId) ? row.ParentProcessId : 0)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray());

        var result = new List<ProcessTreeNode>();
        AppendChildren(0, 0, children, networkCounts, result, new HashSet<int>());
        return result;
    }

    private static void AppendChildren(
        int parentId,
        int depth,
        IReadOnlyDictionary<int, ProcessRow[]> children,
        IReadOnlyDictionary<int, int> networkCounts,
        ICollection<ProcessTreeNode> result,
        ISet<int> visited)
    {
        if (!children.TryGetValue(parentId, out ProcessRow[]? rows))
        {
            return;
        }

        foreach (ProcessRow row in rows)
        {
            if (!visited.Add(row.ProcessId))
            {
                continue;
            }

            bool active = networkCounts.TryGetValue(row.ProcessId, out int count);
            result.Add(new ProcessTreeNode(
                row.ProcessId,
                row.ParentProcessId,
                row.Name,
                depth,
                active,
                active ? count : 0));

            AppendChildren(row.ProcessId, depth + 1, children, networkCounts, result, visited);
        }
    }

    private static Dictionary<int, ProcessRow> ReadProcesses()
    {
        IntPtr snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var result = new Dictionary<int, ProcessRow>();
            var entry = new ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (!Process32First(snapshot, ref entry))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            do
            {
                int processId = unchecked((int)entry.ProcessId);
                int parentId = unchecked((int)entry.ParentProcessId);
                result[processId] = new ProcessRow(
                    processId,
                    parentId,
                    string.IsNullOrWhiteSpace(entry.ExecutableFile) ? $"PID {processId}" : entry.ExecutableFile);
                entry.Size = (uint)Marshal.SizeOf<ProcessEntry32>();
            }
            while (Process32Next(snapshot, ref entry));

            return result;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    private sealed record ProcessRow(int ProcessId, int ParentProcessId, string Name);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public UIntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExecutableFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "Process32FirstW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "Process32NextW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
