// [소스 안내] 실행한 프로세스의 자식 프로세스를 찾는 Windows 보조 코드입니다. 배치 파일 아래의 Java를 추적하는 데 사용합니다.
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NeoForgeServerManager;

// Toolhelp avoids WMI and extra NuGet packages. Only descendants of our own cmd are inspected.
// 기존 run.bat 방식에서 배치 프로세스 아래에 실행된 Java를 찾아 CPU/RAM을 측정합니다.
internal static class ProcessTree
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Entry
    {
        public uint Size, Usage, Id;
        public IntPtr Heap;
        public uint Module, Threads, Parent;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snapshot, ref Entry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    public static Process? FindJava(int parent)
    {
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return null;
        var entries = new List<Entry>();
        try
        {
            var e = new Entry { Size = (uint)Marshal.SizeOf<Entry>(), Name = "" };
            if (Process32FirstW(snapshot, ref e)) do { entries.Add(e); } while (Process32NextW(snapshot, ref e));
        }
        finally { CloseHandle(snapshot); }
        var ids = new HashSet<uint> { (uint)parent };
        bool changed;
        do
        {
            changed = false;
            foreach (var e in entries)
                if (ids.Contains(e.Parent) && ids.Add(e.Id)) changed = true;
        } while (changed);
        foreach (var e in entries)
            if (ids.Contains(e.Id) && (e.Name.Equals("java.exe", StringComparison.OrdinalIgnoreCase) || e.Name.Equals("javaw.exe", StringComparison.OrdinalIgnoreCase)))
                try { return Process.GetProcessById((int)e.Id); } catch (ArgumentException) { }
        return null;
    }
}


