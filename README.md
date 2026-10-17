**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain%20Craft%20Launcher%202/Images/icon.png" alt="Logo" width="80" height="80">

# PCL-HE

**Plain Craft Launcher - Harness Edition**

*一个由第三方独立维护的 PCL 衍生版*

[![Stars](https://img.shields.io/github/stars/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&color=eac54f&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI%2BPHBhdGggZmlsbD0iI2VhYzU0ZiIgZD0iTTEyIDJsMyA2IDYgLjktNC41IDQuMyAxLjEgNi4zTDEyIDE2LjZsLTUuNiAzIDEuMS02LjNMMyA4LjkgOWw4LTMiLz48L3N2Zz4%3D)](https://github.com/Sherlock-9522/PCL-HE/stargazers)
[![Issues](https://img.shields.io/github/issues/Sherlock-9522/PCL-HE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Sherlock-9522/PCL-HE/issues)
[![Last commit](https://img.shields.io/github/last-commit/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&logo=github)](https://github.com/Sherlock-9522/PCL-HE/commits)

</div>

PCL-HE 是第三方基于 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 独立进行二次创作的产物，以 [PCL Community Edition](https://github.com/PCL-Community/PCL-CE) 作为直接的上游基础继续开发。

**本发行版与成都瓜皮龙科技有限公司、PCL 官方团队、PCL Community 官方团队之间不存在隶属关系，也不代表其立场或受到其背书。**

## ✨ 与上游的区别

PCL-HE 在上游基础上做了这些事：

| 方向 | 说明 |
|---|---|
| **联机** | 联机底座换成 [陶瓦（Terracotta）](https://github.com/burningtnt/Terracotta)，在本机以独立进程方式提供，不经过任何中间服务器；陶瓦自带 EasyTier，无需另行下载 |
| **隐私** | 全面切断遥测：不收集、不上报任何使用数据；已停用 Natayark 账户体系与上游联机大厅服务 |
| **更新** | 作为独立发行版，不从任何上游更新服务拉取版本、公告或本体 |
| **视觉** | 原创开屏动画、深化的主界面蓝渐变、档案区独立不透明度滑块、低透明度吉祥物水印 |
| **功能** | 存档页四个 Chunkbase 直达入口（种子地图 / 要塞 / 下界要塞 / 末地城）、实例概览页的蓝图文件夹快捷方式 |
| **合规** | 关于页完整列出「开发参考」的上游项目与赞助渠道 |

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

构建产物位于 `Plain Craft Launcher 2/bin/Release/net10.0-windows/`。

## 🛡️ Windows 提示「已保护你的电脑」怎么办

本发行版**没有代码签名证书**，`.exe` 不含 Authenticode 签名，因此 SmartScreen 会把「发布者」显示为未知并拦截启动。这**不是**病毒报警，只是 Windows 对任何未签名程序的默认态度；你自己从源码构建出来的产物同样会被这样拦。

解除方式：

- **自己构建**的产物：右键 `PCL-HE.exe` → 属性 → 勾选「解除锁定」→ 确定；或在 PowerShell 里执行 `Unblock-File .\PCL-HE.exe`
- **从 Releases 下载**的产物：点「更多信息」→「仍要运行」

需要彻底消除的话，只能为发布产物购买代码签名证书。普通 OV 证书签好后，一开始仍会报「发布者未知」，要等下载量累积出信誉；能立刻通过的是 EV 证书。

## 🔒 许可证

- `Plain Craft Launcher 2/` 使用 [自定义许可证](Plain%20Craft%20Launcher%202/LICENCE)，其中包含《PCL 分发有限许可》与《PCL 存储库合理使用指南》
- `其余所有目录` 使用 [Apache License 2.0](LICENSE)

本发行版不以任何形式向接收者收费。上文两份许可继续适用于本发行版。

## ❤️ 致谢

- **[龙腾猫跃](https://github.com/Meloong-Git)** —— Plain Craft Launcher 原作者，[爱发电](https://ifdian.net/a/LTCat)
- **[PCL Community](https://github.com/PCL-Community)** —— PCL Community Edition 开发团队，本发行版的直接上游
- **[huanghongxun](https://github.com/huanghongxun)** —— [Hello Minecraft! Launcher](https://hmcl.huangyuhui.net/)，部分功能设计与交互思路的参考来源

以及上游 PCL-CE 的[全部贡献者](https://github.com/PCL-Community/PCL-CE/graphs/contributors)。没有他们的工作，就没有这个项目。

## 🐛 反馈

本项目为第三方独立维护，**请不要向上游 PCL 或 PCL-CE 仓库反馈本发行版的问题**。

遇到问题请提到[本仓库的 Issues](https://github.com/Sherlock-9522/PCL-HE/issues)，并附上「设置 → 查看日志 → 导出日志」生成的日志压缩包。
