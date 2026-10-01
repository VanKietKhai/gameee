using System.Runtime.InteropServices;

namespace ConanServerControl.Infrastructure.ProcessManagement;

/// <summary>
/// Parent/child process relationships from a Toolhelp32 snapshot. Windows keeps a child's parent
/// PID after the parent exits, and the PID cannot be reused while a handle to the parent is open,
/// so descendants stay findable after the launcher process has exited.
/// </summary>
internal static class ProcessTree
{
    private const uint SnapProcess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public static IReadOnlyList<int> GetDescendantIds(int rootPid)
    {
        var pairs = Snapshot();
        var result = new List<int>();
        var seen = new HashSet<int> { rootPid };
        var frontier = new Queue<int>();
        frontier.Enqueue(rootPid);
        while (frontier.Count > 0)
        {
            var parent = frontier.Dequeue();
            foreach (var (pid, parentPid) in pairs)
            {
                if (parentPid == parent && seen.Add(pid))
                {
                    result.Add(pid);
                    frontier.Enqueue(pid);
                }
            }
        }

        return result;
    }

    private static List<(int Pid, int ParentPid)> Snapshot()
    {
        var list = new List<(int, int)>();
        if (!OperatingSystem.IsWindows())
        {
            return list;
        }

        var snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandleValue)
        {
            return list;
        }

        try
        {
            var entry = new ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (!Process32FirstW(snapshot, ref entry))
            {
                return list;
            }

            do
            {
                list.Add(((int)entry.th32ProcessID, (int)entry.th32ParentProcessID));
            }
            while (Process32NextW(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return list;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
