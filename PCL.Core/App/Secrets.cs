using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.App;

// ReSharper disable InconsistentNaming
public static class Secrets
{
    /// <summary>
    /// PCL-HE 自己的 Azure 应用注册名，仅用于文档说明与排查。
    /// </summary>
    public const string MSOAuthApplicationName = "Plain Craft Launcher (PCL) - Harness Edition";

    /// <summary>
    /// PCL-HE 内置的微软 OAuth Client ID。
    /// <para>
    /// 采用设备代码流（device code flow），既不需要客户端密钥，也不需要监听本地回调端口。
    /// 该值是本项目自行注册的 Azure 应用，与 PCL2、PCL CE 等上游项目无关。
    /// </para>
    /// <para>
    /// 注意：应用注册完成不等于登录可用。Azure 侧需开启「允许公共客户端流」，
    /// 且 Minecraft 服务侧需把该 Client ID 加入发行方白名单，否则换取 Xbox Live
    /// 令牌这一步会失败。白名单尚未提交，故当前版本的正版登录仍不可用，
    /// 离线登录与第三方（Yggdrasil）登录不受影响。
    /// </para>
    /// </summary>
    private const string BuiltInMSOAuthClientId = "fe6a35b0-86b0-4e23-b234-e80a01b0b5fc";

    /// <summary>
    /// 微软 OAuth 的 Client ID：优先取环境变量，方便使用者换成自己注册的应用；
    /// 未设置时回落到内置值，保证 Release 版开箱即可登录。
    /// <para>
    /// 刻意不走 readEnvDebugOnly：若只在 Debug 构建读取，Release 用户将永远无法覆盖默认值。
    /// </para>
    /// </summary>
    public static string MSOAuthClientId { get; } = EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: false).ReplaceNullOrEmpty() is { Length: > 0 } fromEnvironment ? fromEnvironment : BuiltInMSOAuthClientId;

    /// <summary>
    /// CurseForge API 的 Client ID
    /// </summary>
    public static string CurseForgeAPIKey { get; } = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 遥测密钥
    /// </summary>
    public static string TelemetryKey { get; } = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientId { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientSecret { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 联机根服务器
    /// </summary>
    public static string[] LinkServers { get; } = EnvironmentInterop.GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly: true).ReplaceNullOrEmpty().Split("|");

    /// <summary>
    /// 当前版本的 Git 提交 SHA
    /// </summary>
    public static string CommitHash { get; } = EnvironmentInterop.GetSecret("GITHUB_SHA", readEnvDebugOnly: true).ReplaceNullOrEmpty();
}
