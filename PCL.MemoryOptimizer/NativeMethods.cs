using System.Runtime.InteropServices;

namespace PCL.MemoryOptimizer;

/// <summary>
/// 内存优化器使用的 Win32 / NT 原生调用声明。本文件只声明调用与常量，不含业务逻辑。
/// </summary>
internal static partial class NativeMethods
{
    // -------- 权限掩码与常量 --------
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint PROCESS_SET_QUOTA = 0x0100;
    internal const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    internal const uint TOKEN_QUERY = 0x0008;
    internal const int SE_PRIVILEGE_ENABLED = 0x00000002;

    /// <summary>SYSTEM_INFORMATION_CLASS.SystemMemoryListInformation。</summary>
    internal const int SystemMemoryListInformation = 80;

    /// <summary>MEMORY_LIST_COMMAND.MemoryEmptyWorkingSets：清空全系统工作集。</summary>
    internal const int MemoryEmptyWorkingSets = 2;

    internal const string SeProfileSingleProcess = "SeProfileSingleProcessPrivilege";
    internal const string SeIncreaseQuota = "SeIncreaseQuotaPrivilege";

    // -------- 物理内存状态 --------
    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // -------- 逐进程工作集 --------
    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", EntryPoint = "OpenProcess", SetLastError = true)]
    internal static partial nint OpenProcess(uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("psapi.dll", EntryPoint = "EmptyWorkingSet", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EmptyWorkingSet(nint hProcess);

    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint hObject);

    // -------- 进程内存优先级 --------
    /// <summary>PROCESS_SET_INFORMATION：设置进程信息（内存优先级）所需的访问权限。</summary>
    internal const uint PROCESS_SET_INFORMATION = 0x0200;

    /// <summary>PROCESS_INFORMATION_CLASS.ProcessMemoryPriorityInformation。</summary>
    internal const int ProcessMemoryPriorityInformation = 0;

    /// <summary>MEMORY_PRIORITY_NORMAL：普通优先级，系统在内存紧张时会尽量最后换出它的页面。</summary>
    internal const uint MEMORY_PRIORITY_NORMAL = 5;

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORY_PRIORITY_INFORMATION
    {
        public uint MemoryPriority;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "SetProcessInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(nint hProcess, int processInformationClass,
        ref MEMORY_PRIORITY_INFORMATION processInformation, int processInformationSize);

    /// <summary>把进程页面的内存优先级设为指定值；调用方负责持有与关闭句柄。</summary>
    internal static bool SetMemoryPriority(nint processHandle, uint priority)
    {
        var info = new MEMORY_PRIORITY_INFORMATION { MemoryPriority = priority };
        return SetProcessInformation(processHandle, ProcessMemoryPriorityInformation, ref info, sizeof(uint));
    }

    // -------- 令牌与特权 --------
    [LibraryImport("advapi32.dll", EntryPoint = "OpenProcessToken", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW",
        StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool LookupPrivilegeValue(string? systemName, string name, out long luid);

    /// <summary>
    /// 单特权 TOKEN_PRIVILEGES。Pack = 4 是实测必需项：默认布局会让 LUID 错位，
    /// 导致 AdjustTokenPrivileges 返回 true 但实际未启用（GetLastError = 1300）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;
        public long Luid;
        public int Attributes;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AdjustTokenPrivileges(nint tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState, uint bufferLength, nint previousState, nint returnLength);

    // -------- 全系统清空工作集 --------
    /// <summary>
    /// 信息类 80 / 命令 2：把所有进程的工作集页移出物理内存。
    /// 该调用没有排除参数，因此绝不能在游戏运行中直接使用。
    /// </summary>
    [LibraryImport("ntdll.dll", EntryPoint = "NtSetSystemInformation")]
    internal static partial int NtSetSystemInformation(int systemInformationClass,
        ref int systemInformation, int systemInformationLength);
}
