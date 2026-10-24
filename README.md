**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL-Harness%20Edition/Images/icon.png" alt="Logo" width="80" height="80">

# PCL-HE

**Plain Craft Launcher (PCL) - Harness Edition**

*一个由第三方独立维护的 PCL 衍生版，也属于 PCL-CE 的另一种分支版本*

[![Stars](https://img.shields.io/github/stars/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&color=eac54f&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI%2BPHBhdGggZmlsbD0iI2VhYzU0ZiIgZD0iTTEyIDJsMyA2IDYgLjktNC41IDQuMyAxLjEgNi4zTDEyIDE2LjZsLTUuNiAzIDEuMS02LjNMMyA4LjkgOWw4LTMiLz48L3N2Zz4%3D)](https://github.com/Sherlock-9522/PCL-HE/stargazers)
[![Issues](https://img.shields.io/github/issues/Sherlock-9522/PCL-HE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Sherlock-9522/PCL-HE/issues)
[![Last commit](https://img.shields.io/github/last-commit/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&logo=github)](https://github.com/Sherlock-9522/PCL-HE/commits)

</div>

> [!WARNING]
> 当前为 **v0.1.2-Alpha** 早期测试版本，可能存在导致启动失败、数据异常甚至更严重问题的缺陷。使用前请自行备份 `.minecraft` 与实例配置。遇到问题请到 [Issues](https://github.com/Sherlock-9522/PCL-HE/issues) 反馈并附上日志；**请不要向上游 PCL 或 PCL-CE 仓库反馈本发行版的问题**。

PCL-HE 是第三方基于 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 独立进行二次创作的产物，以 [PCL Community Edition](https://github.com/PCL-Community/PCL-CE) 作为直接的上游基础继续开发。

**本发行版与成都瓜皮龙科技有限公司、PCL 官方团队、PCL Community 官方团队之间不存在隶属关系，也不代表其立场或受到其背书。**


## ✨ 与上游的区别

PCL-HE 在上游基础上做了这些事：

| 方向 | 说明 |
|---|---|
| **联机** | 联机底座换成了 [陶瓦（Terracotta）联机](https://github.com/burningtnt/Terracotta)，在本机以独立进程方式提供，不经过任何中间服务器；陶瓦自带 EasyTier，无需另行下载 |
| **隐私** | 全面切断遥测：不收集、不上报任何使用数据；已停用 Natayark 账户体系与上游联机大厅服务 |
| **更新** | 作为独立发行版，不从任何上游更新服务拉取版本、公告或本体 |
| **视觉** | 原创开屏动画、深化的主界面蓝渐变、档案区独立不透明度滑块、低透明度吉祥物水印 |
| **内存** | 内置独立子项目 [PCL.MemoryOptimizer](PCL.MemoryOptimizer/README.md)：轻量护航、游戏内护航（免提权，绝不动游戏）与深度清理（经授权调用系统内核接口）三档，不结束任何进程 |
| **功能** | 存档页四个 Chunkbase 直达入口（种子地图 / 要塞 / 下界要塞 / 末地城）、实例概览页的蓝图文件夹快捷方式 |
| **合规** | 关于页完整列出「开发参考」的上游项目与赞助渠道 |


## 🧠 内存优化

「工具 → 百宝箱」里的内存优化已经重做成一个独立子项目 [PCL.MemoryOptimizer](PCL.MemoryOptimizer/README.md)，随启动器一同发布，也可以单独维护。它分三档：

- **轻量护航**：不需要管理员权限，只整理启动器自身的工作集，收益小但绝对安全；
- **游戏内护航**：不需要管理员权限，把游戏完整排除在外，只整理浏览器、聊天软件这些后台程序的工作集，同时把游戏进程的内存优先级提到最高。游戏一个字节都不动，所以**游戏跑着的时候也能随时点**；
- **深度清理**：点按钮时弹一次 UAC，由临时提权进程调用系统内核接口，一次性整理全系统工作集。实测可用内存增加约 2.4 GB，进程一个不少。

也可以在「设置 → 游戏内存」勾选**启动游戏前自动优化**。如果本次运行里已经点过深度清理并授权过，启动时会直接走深度清理，不再多问一遍。

> ⚠️ 深度清理会把你正在用的程序的内存页换到页面文件里，切回这些程序时会有一点点卡顿；游戏正在运行时不要用它。它是「缓解当下内存压力」的手段，不是让程序少占内存的办法。


## 🌱 项目缘起与开发方式

2026 年 10 月 20 日，上游 PCL2 与若干主流启动器因政策原因陆续停止了「直接下载游戏本体」的供应，玩家想玩原版就得自己另找安装器、自己拼 jar 与资源文件。PCL-HE 因此立项：在 PCL-CE 的基础上继续维护一套**仍然能完整下载并安装游戏本体**的启动器，离线登录、第三方验证与正版登录三条入口保持可用。

开发方式上，这个项目和通常的开源项目不太一样，所以有必要写清楚：

- **架构、需求与验收由人类维护者（也就是我本人）负责** —— 决定做成什么样、哪种实现可以接受、什么才算修好了；
- **而具体实现、代码巡检与验证由 AI 承担** —— 由 [DeepSeek Harness](https://www.deepseek.com/harness) 驱动与开发，逐轮产出补丁、跑构建，并在真实实例里复现问题、确认修复。（某些情况下允许其自由发挥）

换句话说，这里不是「AI 生成、我照单全收」，而是人类守住方向与质量关口，AI 大模型负责把想法落成能跑的代码。好处是迭代快、覆盖面广；代价是每个改动都必须有实机验证兜底，所以仓库里大量提交都附带复现步骤与日志证据，而不是只改几行代码就宣布修好，工作量大。

**感谢 DeepSeek Harness 提供模型与工具支持，同时也感谢 DeepSeek Harness 社区的插件大佬们所提供的工具插件** —— 没有它与工具插件，这个项目只会残存在我的脑中，而不会以这样的速度成型。同时也有必要说清楚：PCL-HE 由社区独立维护，与任何模型厂商都不存在隶属或背书关系。


## 💻 支持平台

| 操作系统 | 支持情况 | 环境要求 |
|---|---|---|
| Windows 10 1809 (17763) 或更高 | ✅ 完整支持 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ 理论能跑，酌情提供支持 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 或更低版本 | ❌ 不支持 | / |
| macOS / Linux / 其他操作系统 | ⚠️ 仅交叉编译开发支持 | [.NET 10 SDK](https://get.dot.net/10) |


## 🔨 从源码构建

```bash
git clone https://github.com/Sherlock-9522/PCL-HE.git
cd PCL-HE
dotnet build "Plain Craft Launcher 2.slnx" -c Release
```

构建产物位于 `PCL-Harness Edition/bin/Release/net10.0-windows/`。

想要**便携模式**，在 `PCL-HE.exe` 同目录建一个 `CONFIG` 文件夹即可：所有配置都会写进那里，不再向 `%APPDATA%` 写任何东西；首次启用会把已有配置迁移过去，删掉 `CONFIG` 就退回原来的行为。


## 🛡️ Windows 提示「已保护你的电脑」怎么办

本发行版**没有代码签名证书**，`.exe` 不含 Authenticode 签名，因此 SmartScreen 会把「发布者」显示为未知并拦截启动。这**不是**病毒报警，只是 Windows 对任何未签名程序的默认态度；你自己从源码构建出来的产物同样会被这样拦。

解除方式：

- **自己构建**的产物：右键 `PCL-HE.exe` → 属性 → 勾选「解除锁定」→ 确定；或在 PowerShell 里执行 `Unblock-File .\PCL-HE.exe`
- **从 Releases 下载**的产物：点「更多信息」→「仍要运行」
- **只在本机跑**：仓库里还带了 `setup-signing.ps1` 与 `deploy.ps1`——前者创建一份自签名证书并装进当前用户的受信任存储，后者用它给部署产物签名。做过一次之后，本机启动 PCL-HE 就不再被拦。这份证书只在本机受信任，换到别人的电脑上依然会显示「发布者未知」。

需要彻底消除的话，只能为发布产物购买代码签名证书。普通 OV 证书签好后，一开始仍会报「发布者未知」，要等下载量累积出信誉；能立刻通过的是 EV 证书。


## 🔐 关于正版登录目前的状况

正版登录走的是 **Microsoft 设备代码流**（Device Code Flow）：启动器显示一串 8 位验证码，你在 <https://www.microsoft.com/link> 输入并授权，全程不需要在启动器里填密码。

目前内置的 Azure 应用是 `Plain Craft Launcher (PCL) - Harness Edition`，客户端 ID 为 `fe6a35b0-86b0-4e23-b234-e80a01b0b5fc`。

**已知限制**：用这个应用登录时，Microsoft OAuth 与 Xbox Live 两步都能通过，但最后一步 `login_with_xbox` 会返回 **HTTP 403**，服务端原文是：

```json
{
  "path": "/authentication/login_with_xbox",
  "errorMessage": "Invalid app registration, see https://aka.ms/AppRegInfo for more information"
}
```

即 Minecraft 服务端认为这个 Azure 应用**没有在 Minecraft 侧完成登记**。这一步与你的账号无关，也与客户端实现无关——同一份代码、同一套协议换成已登记的应用 ID 就能登录（可对照 [HMCL](https://github.com/HMCL-dev/HMCL) 的实现，逐字段一致）。

如果你需要正版登录，可以：

1. 在 Azure 上自行注册一个应用（「个人 Microsoft 账户」类型，并开启「允许公共客户端流」）；
2. 设置环境变量 `PCL_MS_CLIENT_ID` 为你的应用 ID，再启动启动器。

环境变量优先于内置值，改完重启即生效，不需要重新编译。

**离线登录**（随便填个名字）与**第三方登录**（LittleSkin 等 authlib-injector 服务）不受以上限制，开箱即可用。


## 🔒 许可证

- `PCL-Harness Edition/` 使用 [自定义许可证](PCL-Harness%20Edition/LICENCE)，其中包含《PCL 分发有限许可》与《PCL 存储库合理使用指南》
- `其余所有目录` 使用 [Apache License 2.0](LICENSE)

本发行版不以任何形式向接收者收费。上文两份许可继续适用于本发行版。


## ❤️ 致谢

- **[龙腾猫跃](https://github.com/Meloong-Git)** —— Plain Craft Launcher 原作者，[爱发电](https://ifdian.net/a/LTCat)
- **[PCL Community](https://github.com/PCL-Community)** —— PCL Community Edition 开发团队，本发行版的直接上游
- **[huanghongxun](https://github.com/huanghongxun)** —— [Hello Minecraft! Launcher](https://hmcl.huangyuhui.net/)，部分功能设计与交互思路的参考来源

以及上游 PCL-CE 的[全部贡献者](https://github.com/PCL-Community/PCL-CE/graphs/contributors)。没有他们的工作，就没有这个项目。


## 📝 反馈

本项目为第三方独立维护，**请不要向上游 PCL 或 PCL-CE 仓库反馈本发行版的问题**。

遇到问题请提到[本仓库的 Issues](https://github.com/Sherlock-9522/PCL-HE/issues)，并附上「设置 → 查看日志 → 导出日志」生成的日志压缩包。

> 日志里的 Minecraft 访问令牌（`--accessToken`）会由启动器自动打码，但截图、配置文件以及你自己填写的密钥不会被处理。上传前请自行检查，别把账号密码或私有密钥贴进公开 Issue。
