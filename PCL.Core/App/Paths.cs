using System;
using System.IO;
using PCL.Core.Utils.OS;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

/// <summary>
/// Application global paths.<br/>
/// <b>NOTE</b>: The behaviors of all path strings depends on <see cref="Path"/> API provided by
/// .NET standard library. You should use <see cref="Path"/> and other APIs relative to it to process
/// any path string from this service, rather than concat paths manually.
/// </summary>
public static class Paths
{
    /// <summary>
    /// The default directory used for relative path combining.
    /// <para>
    /// 刻意不取 <c>Basics.ExecutableDirectory</c>：<c>Basics</c> 的静态字段初始化会间接触发
    /// <c>Paths</c> 的静态构造，而这里若回头读 <c>Basics</c>，就构成静态初始化循环。
    /// 循环期间 CLR 不等待、直接返回默认值，取到的将是 null，
    /// 接着 Path.Combine 抛 ArgumentNullException，整个 Paths 类型报废
    /// （表现为启动即退出且不留任何日志）。此处自行计算，切断依赖。
    /// </para>
    /// </summary>
    public static string DefaultDirectory => _defaultDirectory ??= GetExecutableDirectory();

    private static string? _defaultDirectory;

    private static string GetExecutableDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var directory = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(directory)) return directory;
        }
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string _data;
    private static string _sharedData;
    private static string _sharedLocalData;
    private static string _temp;
    
    /// <summary>
    /// Per-instance data directory.
    /// </summary>
    public static string Data { get => _data; set => _data = value; }

    /// <summary>
    /// Shared synchronized data directory.
    /// </summary>
    public static string SharedData { get => _sharedData; set => _sharedData = value; }
    
    /// <summary>
    /// Shared synchronized data directory of old versions.<br/>
    /// Keep the value just for migration, DO NOT USE IT.
    /// </summary>
    public static string OldSharedData { get; set; }

    /// <summary>
    /// Shared local data directory, used to put some large files that can be released or downloaded back anytime.
    /// </summary>
    public static string SharedLocalData { get => _sharedLocalData; set => _sharedLocalData = value; }
    
    /// <summary>
    /// Temporary files directory (can be deleted anytime, except when the program is running).
    /// </summary>
    public static string Temp { get => _temp; set => _temp = value; }

    /// <summary>
    /// Get path string relative to a special folder.
    /// </summary>
    /// <param name="folder">the special folder</param>
    /// <param name="relative">the relative path</param>
    /// <returns>the path string relative to the special folder</returns>
    public static string GetSpecialPath(Special folder, string relative)
    {
        var folderPath = Environment.GetFolderPath(folder);
        // GetFolderPath 在用户配置未加载等异常环境下可能返回空值；直接交给 Path.Combine 会抛
        // ArgumentNullException，而这里处于静态构造函数中，一旦抛出整个 Paths 类型都不可用、
        // 启动器连第一屏都进不去（表现为启动即退出且不留日志）。退回到程序目录保证仍能启动。
        if (string.IsNullOrEmpty(folderPath))
        {
            Console.Error.WriteLine($"[Paths] GetFolderPath({folder}) 返回空值，已退回程序目录：{DefaultDirectory}");
            folderPath = DefaultDirectory;
        }
        return Path.Combine(folderPath, relative);
    }

    static Paths()
    {
        // PCL-HE 是独立发行版，共享数据目录必须与上游 PCL-CE 分开：
        // 单实例锁、EasyTier 运行时缓存都放在 SharedLocalData 下，共用会导致
        // 两者无法同时运行、且互相顶掉对方的运行时目录。
        // oldName 指向上游的目录名，用于首次启动时把既有配置与档案迁移过来。
#if DEBUG
        const string name = "PCLHE_Debug";
        const string oldName = "PCLCE_Debug";
#else
        const string name = "PCLHE";
        const string oldName = "PCLCE";
#endif
        // fill paths
        // DefaultDirectory 取自 Basics.ExecutableDirectory，但静态构造顺序并不保证 Basics 已就绪：
        // 一旦拿到的还是默认值 null，Path.Combine 会抛 ArgumentNullException，而这里在静态构造函数中，
        // 整个 Paths 类型会因此不可用，连锁导致 SingleInstanceService 等预加载服务实例化失败，
        // 表现为「启动即退出、且不留任何日志」。AppContext.BaseDirectory 不依赖任何托管初始化，用它兜底。
        _data = Path.Combine(DefaultDirectory, "PCL");
        _sharedData = GetSpecialPath(Special.ApplicationData, name);
        _sharedLocalData = GetSpecialPath(Special.LocalApplicationData, name);
        _temp = Path.Combine(Path.GetTempPath(), name);
        OldSharedData = GetSpecialPath(Special.ApplicationData, oldName);
#if DEBUG
        // read environment variables
        EnvironmentInterop.ReadVariable("PCL_PATH", ref _data);
        EnvironmentInterop.ReadVariable("PCL_PATH_SHARED", ref _sharedData);
        EnvironmentInterop.ReadVariable("PCL_PATH_LOCAL", ref _sharedLocalData);
        EnvironmentInterop.ReadVariable("PCL_PATH_TEMP", ref _temp);
#endif
        // create directories
        Directory.CreateDirectory(_data);
        Directory.CreateDirectory(_sharedData);
        Directory.CreateDirectory(_sharedLocalData);
        Directory.CreateDirectory(_temp);
    }
}
