using System.Text.RegularExpressions;
using PCL.Core.Logging;

namespace PCL;

internal static class CrashRegex
{
    /// <summary>
    ///     单次匹配的时间上限。
    /// </summary>
    /// <remarks>
    ///     崩溃日志里有相当一部分模式带嵌套量词（例如 [\n\r]+\t(.*) 外面再套一层 +），
    ///     一旦日志内容与预期结构不符就会指数级回溯，把分析线程整整卡住。
    ///     .NET 只有显式传入 matchTimeout 才启用超时保护，静态重载
    ///     Regex.Match / Regex.Matches / Regex.IsMatch(text, pattern, options) 不会带默认超时，
    ///     所以三个包装必须都把超时传下去。
    /// </remarks>
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    public static string? First(
        string text,
        string pattern,
        RegexOptions options = RegexOptions.None)
    {
        try
        {
            var value = Regex.Match(text, pattern, options, MatchTimeout).Value;
            return string.IsNullOrEmpty(value)
                ? null
                : value;
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则匹配超时，已跳过该项");
            return null;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则匹配第一项出错");
            return null;
        }
    }

    public static List<string> All(
        string text,
        string pattern,
        RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.Matches(text, pattern, options, MatchTimeout)
                .Select(match => match.Value)
                .ToList();
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则匹配超时，已跳过该项");
            return [];
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则匹配全部项出错");
            return [];
        }
    }

    public static bool IsMatch(
        string text,
        string pattern,
        RegexOptions options = RegexOptions.None)
    {
        try
        {
            return Regex.IsMatch(text, pattern, options, MatchTimeout);
        }
        catch (RegexMatchTimeoutException ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则检查超时，按未匹配处理");
            return false;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "正则检查出错");
            return false;
        }
    }
}
