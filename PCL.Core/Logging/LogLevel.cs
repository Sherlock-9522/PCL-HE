using System.Collections.Generic;

namespace PCL.Core.Logging;

/// <summary>
/// 日志等级。将十进制值 <c>% 100</c> 并显式转换可得到该等级默认对应的 <see cref="ActionLevel"/>
/// </summary>
public enum LogLevel
{
    Trace = 000 + ActionLevel.TraceLog,
    Debug = 100 + ActionLevel.NormalLog,
    Info = 200 + ActionLevel.NormalLog,
    // TRACE 构建（含 Debug）把 Warning 升级为弹提示、Error 升级为弹对话框，便于开发时立刻看到；
    // 但日志行仍打印为 [WARN] / [ERR!]，因为 RealLevel() 按区间判定。
    // 因此用 Debug 构建验收界面时，一条"只该记日志"的 Warn 会弹出红色 Toast，Release / Beta 下不会。
#if TRACE
    Warning = 300 + ActionLevel.HintErr,
    Error = 400 + ActionLevel.MsgBoxErr,
#else
    Warning = 300 + ActionLevel.NormalLog,
    Error = 400 + ActionLevel.HintErr,
#endif
    Fatal = 500 + ActionLevel.MsgBoxFatal,
}

public static class LogLevelExtensions
{
    private static readonly Dictionary<LogLevel, string> _LevelNameMap = new()
    {
        [LogLevel.Trace] = "TRA",
        [LogLevel.Debug] = "DBG",
        [LogLevel.Info] = "INFO",
        [LogLevel.Warning] = "WARN",
        [LogLevel.Error] = "ERR!",
        [LogLevel.Fatal] = "FTL!"
    };

    extension(LogLevel level)
    {
        public string PrintName() => _LevelNameMap[level];
        public ActionLevel DefaultActionLevel() => (ActionLevel)((int)level % 100);

        public LogLevel RealLevel() => (int)level switch
        {
            < 100 => LogLevel.Trace,
            < 200 => LogLevel.Debug,
            < 300 => LogLevel.Info,
            < 400 => LogLevel.Warning,
            < 500 => LogLevel.Error,
            _ => LogLevel.Fatal,
        };

        public int Header() => (int)level / 100 * 100;
    }
}
