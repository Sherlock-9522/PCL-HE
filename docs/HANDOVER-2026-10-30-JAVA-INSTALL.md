# PCL-HE 自助安装 Java —— 交接文档

> 写于 2026-10-30 凌晨（会话上下文接近上限，据此续接）。
> 仓库：`C:\Users\kingd\Documents\DSH\DSH-4(XL)\PCL-HE`（解决方案 `Plain Craft Launcher 2.slnx`）
> 运行目录：`C:\Users\kingd\PCL-HE`（**必须在工作区之外**，否则会被套 AppContainer 导致写配置 ACCESS_DENIED 启动即崩）

## 0. 一句话现状

「工具 → 百宝箱 → 自助安装 Java」的**功能已经写完、编译通过、已部署、界面已实机截图验证**。
截至 2026-10-30 上午，§5 里除「机房真机五版本端到端实测」（本机不能测，用户机器已装好 8/17/21/25 且不愿被覆盖）之外**全部关闭**：
装完自动反馈、卸载入口、下载进度、四语 key 补齐、窄窗口换行确认、系统盘非 C 的路径确认、文档补章，
外加一并修掉的启动开屏卡顿（见 §10）。
**剩下必须真人做的**：找一台干净机器或虚拟机，把 8 / 11 / 17 / 21 / 25 各走一遍。

---

## 1. 构建 / 部署 / 验证的固定套路

### 构建（必须完整 build，不能 `-t:Compile`）
```bash
export DOTNET_ROOT="C:/Users/kingd/.dotnet"
cd "C:/Users/kingd/Documents/DSH/DSH-4(XL)/PCL-HE"
"/c/Users/kingd/.dotnet/dotnet.exe" build "Plain Craft Launcher 2.slnx" -c Release --nologo -v q
```
- 正常结果：`5391 个警告`、`0 个错误`（警告全是既有的 CS0414/CS8602 之类，可以无视）。
- 构建前**必须关掉正在运行的 PCL-HE.exe**，否则锁 `bin` 里的 `PCL.Core.dll` 会报 MSB3027/MSB3021：
  `powershell -Command "Get-Process PCL-HE -ErrorAction SilentlyContinue | Stop-Process -Force"`

### 部署
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "C:/Users/kingd/Documents/DSH/DSH-4(XL)/PCL-HE/deploy.ps1"
```
输出「签名：Valid / 目标：C:\Users\kingd\PCL-HE / 入口：…\PCL-HE.exe」即成功。

### 日志
`C:/Users/kingd/PCL-HE/PCL/Log/*.log`（扩展名是 `.log`，不是 `.txt`）
最有用的两行：`[Control] 按下按钮：xxx`（确认点到了）、`[Control] 普通弹窗：`（弹窗创建）。
查最新的：`tail -n 40 "$(ls -t /c/Users/kingd/PCL-HE/PCL/Log/*.log | head -1)"`

### 截图 / 合成点击（无头验证用）
PowerShell + `SetWindowPos(h,-1,0,0,1247,771,0x0040)` 置顶 → `SetForegroundWindow` →
`SetCursorPos` + `mouse_event(0x0002/0x0004)` 点击 → `CopyFromScreen(0,0)` 存 PNG。
常用坐标（窗口 1247×771、位于 0,0 时）：顶部「工具」(760,26)、左侧「百宝箱」(60,191)、
百宝箱内 Java 卡「下载并安装」(245,536)、「静默安装」(438,536)、「重新扫描」(594,536)、「查询最新版本」(743,536)。
**坑**：`MyButton` 要求先有 `MouseLeftButtonDown` 才响应 `Button_MouseUp`，合成点击经常第一次被吃掉 —— 先点一下非按钮区域再点按钮，并以日志里出现「按下按钮」为准。

---

## 2. 需求全量（按用户原话整理，按时间顺序）

1. **m05264**：百宝箱单独做「自助安装 Java」——下 Adoptium OpenJDK 的 **JRE**（8/11/17/21/25，Windows `.msi`），
   界面自己设计（「AI 味不能太强」）。动机：现有「缺 Java 时按需静默下载」有时失效或太慢
   （根因：走 Mojang `piston-meta` 清单逐个文件下上百个请求，见 `Modules/Minecraft/ModJava.cs:435 JavaFileList()`）。
   要说明「手动安装」与「后台静默安装」两种方式，附简易教程。
2. **m05293**：
   - 下载源镜像由我定。
   - **手动安装**：弹窗一次性讲完整教程，右下角「确定」**带倒计时（初版 3 分钟，后改 60 秒）**，倒计时结束前不可点；
     正文说明**为什么选 JRE、MSI 比 JDK/压缩包好在哪**；然后自动唤起 Adoptium 的 `.msi` 安装程序。
   - **静默安装**：先弹**红色警告风格**的授权须知，用户授权后给进度。
   - 「已装版本 + 卸载入口」如果必要可以做，「记得备份」。
3. **m05471**：「我们先在这边研究功能并排查可能出现问题的地方，到时候我直接去机房的机子上测试——
   毕竟我这台机已经安装 adoptium 的 8，11，21，25 了（位于 D 盘），无法覆盖安装」。
   → 本机**不能真装**，验证只做到「查询 → 下载 → sha256 校验 → 唤起安装器（不点下一步）」。
4. **m05501**：静默安装**前**要问安装路径。
   - 只有一个盘的用户：默认装 C 盘（如 `C:\Program Files (x86)` 之类）。
   - 有多盘的（HE 放在 D 盘）：弹窗询问「装 C 盘默认路径」还是「自己选路径」，**不能强制装 C 盘**（不然要被提 issue）。
   - 手动安装若选了 D 盘，还可以给选项：「装到指定路径」还是「装到 PCL-HE 的 `PCL` 文件夹」——
     后者在 `PCL` 下建 `Adoptium JRE` 文件夹（参考用户给的 PCL2 例子 `E:\PCL2\PCL\Adoptium JRE`）。
5. **m05548**：自定义路径控件参考「下载自定义文件」的样式（`TextDownloadFolder` + 「选择」按钮 +
   `SystemDialogs.SelectFolder()`）。
6. **m05996**：把「查询最新版本」按钮从 Grid 右侧移到「重新扫描」旁边；首页再加一条「关于自助下载安装 Java」的独立卡片。
7. **m06049**：教程弹窗倒计时缩到 **1 分钟**；每段末尾的句号删掉（「总带着句号有点累赘」）。
8. **m06275**（七条界面反馈）：
   1) 两个功能的介绍字体再大一点；「开游戏」改成「运行游戏」；
   2) 关闭/取消弹窗后**会再弹一次**；
   3) 扫描路径过期：E 盘的 jre8 早挪到 D 盘了，界面还显示 `E:\jre8`；
   4) Java 17 现在在 `D:\java\jre17\bin\java.exe`，不是旧的 `D:\java8,21,25\`；
   5) 「内存优化」独立成一张卡片，别和 HE 介绍混一起；
   6) 路径弹窗太窄，「选择」框也窄，弹窗要加宽。
9. **m06315**：启动器里所有红字颜色再加深一点，现在偏橙不够醒目。

---

## 3. 代码地图（当前状态）

### 3.1 数据/逻辑层
**`PCL-Harness Edition/Modules/Minecraft/ModJavaInstall.cs`**（约 305 行，静态类，全部核心逻辑在这里）
| 行 | 成员 | 作用 |
|---|---|---|
| 22 | `public static readonly int[] SupportedFeatures = [8, 11, 17, 21, 25]` | 支持的版本 |
| 24 | `private const string ApiTemplate` | `https://api.adoptium.net/v3/assets/latest/{0}/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse` |
| 31 | `private static readonly string[] MirrorRoots` | 清华 `https://mirrors.tuna.tsinghua.edu.cn/Adoptium` + 南大 `https://mirror.nju.edu.cn/Adoptium` |
| 38 | `public sealed class JreRelease` | `Feature / Version / FileName / Sha256 / Size / Urls` |
| 58 | `GetDownloadFolder()` | `Paths.Temp/JavaInstaller` |
| 64 | `FetchLatest(int feature)` | 调 API 取 `[0].binary.installer.{name,link,size,checksum}`，版本用 `NormalizeVersion` 归一 |
| 104 | `CreateDownloadTask(JreRelease)` | 造 `DownloadFile`，带 `ModBase.FileChecker(size, sha256)` |
| 118 | `CreateLoader(release, taskName)` | 造 `LoaderDownload` |
| 128 | `public sealed class InstallPathContext` | `Feature / Version / SystemDirectory / LauncherDirectory / RecommendSystem / SelectedDirectory` |
| 142 | `CreatePathContext(release)` | `RecommendSystem = !HasOtherDrive()`（**单盘推荐系统目录，多盘推荐启动器目录**） |
| 152 | `public sealed record InstalledJre(int Feature, string Version, string DisplayName, string Location)` | 带 `LocationExists` 计算属性 |
| 159 | `private const string ArpPrefix = "Eclipse Temurin JRE"` | 卸载登记表 DisplayName 前缀 |
| 168 | `GetInstalledJres()` | 读 `SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` 与 `WOW6432Node` 同名键，取 `InstallLocation` |
| 203 | `FindInstalled(int feature)` | 查某主版本是否已装 |
| 207 | `GetSystemInstallDir(release)` | `ProgramFiles\Eclipse Adoptium\jre-{ver}-hotspot` |
| 214 | `GetLauncherInstallDir(int feature)` | `ModBase.exePath\PCL\Adoptium JRE\jre-{feature}` |
| 218 | `NormalizeVersion(raw)` | 去 `jdk-` / `jdk` 前缀（8 是 `jdk8u504-b01`，21 是 `jdk-21.0.13+9`） |
| 227 | `DirectoryName(release)` | `+` → `.` 再拼 `-hotspot` |
| 233 | `HasOtherDrive()` | `DriveInfo` 枚举 Fixed+IsReady 且非系统盘 |
| 250 | `ParseFeature(displayName, version)` | 从登记信息推主版本 |
| 269 | `RegisterInstalledDirectory(string installDirectory)` | 装完把 `bin\java.exe` 用 `ModJava.Javas.AddOrGet` 登记并 `IsEnabled = true` + `SaveConfig()` |
| 296 | `SizeText(long)` | 人读字节数 |

**`PCL.Core/Utils/OS/JavaInstallPromote.cs`**（提权侧）
- :27 `[PromoteOperation("java-install-msi")] public static string? InstallMsi(string? arg)`：
  arg 形如 `安装包完整路径|安装目录`（目录可省）；执行
  `msiexec /i "<msi>" /qn /norestart [INSTALLDIR="<dir>"]`，`WaitForExit(10min)`，
  返回单行 JSON `{ok, code, rebootRequired?, message?}`，`3010` 视为成功。
- :87 `public sealed record InstallResult(bool Ok, int Code, bool RebootRequired, string? Message)`
- :99 `public static InstallResult? RunInstallViaPromote(string msiPath, string? installDirectory = null, TimeSpan? timeout = null)`：
  `PromoteService.Append("java-install-msi " + msiPath[+"|"+dir], callback)` + `PromoteService.Activate()`，
  **返回 null 表示用户取消了 UAC**，默认等 5 分钟。

### 3.2 界面层
**`PCL-Harness Edition/Pages/PageTools/PageToolsTest.xaml`**
- :10 `MyCard Tools.Memory.Title`（内存优化卡，三档：轻量护航 / 深度清理 / 游戏内护航）
- :57 红色 `Danger.Note`（颜色现在走全局 `ColorBrushRedLight`）
- :65 `MyCard Tools.Java.Title`（自助安装 Java 卡）
  - 描述 `Tools.Java.Description`（FontSize 13.5）
  - `ComboJavaFeature`（Java 版本，160 宽、`SelectionChanged`）+ `TextJavaRelease`（查询结果）+ 「查询最新版本」已移到 WrapPanel
  - 按钮行：下载并安装(160) / 静默安装(160) / 重新扫描(130) / 查询最新版本(130)
  - :100 `TextJavaStatus`（13.5）
  - 小字 `Tools.Java.Note`（13）

**`PCL-Harness Edition/Pages/PageTools/PageToolsTest.xaml.cs`**
| 行 | 成员 |
|---|---|
| 323 | `private int SelectedJavaFeature`（读 `ComboJavaFeature.SelectedIndex` → `SupportedFeatures[i]`） |
| 344 | `SetJavaBusy(bool)`（禁用/恢复四个按钮 + 状态文本） |
| 357 | `RefreshJavaInstalled()`（开头有 `if (!Dispatcher.CheckAccess()) { ModBase.RunInUi(...); return; }`，**这是修过的跨线程 bug**） |
| 412 | `BtnJavaQuery_Click` → `FetchLatest` → 显示「最新版本 x，安装包 y MB」 |
| 497 | `ConfirmSameFeatureInstall(int feature)` → 已装同主版本时红色确认弹窗（MSI 会卸载旧版并删掉旧目录） |
| 512 | `AskInstallDirectory(release)` → 弹 `MyMsgJavaPath`，返回选中的目录或 null |
| 525 | `BtnJavaInstall_Click`（手动）：教程弹窗(60s 倒计时) → 升级确认 → 路径弹窗 → 下载+校验 → `msiexec /i "<msi>" INSTALLDIR="<dir>"`（**带 UI，UseShellExecute=true**） |
| 571 | `BtnJavaSilent_Click`（静默）：红色授权 → 升级确认 → 路径弹窗 → 下载+校验 → `RunInstallViaPromote` → `RegisterInstalledDirectory` → `RefreshJavaInstalled` |
| 632 | `BtnJavaRescan_Click` → `ModJava.Javas.ScanJavaAsync(force: true)` |

**`PCL-Harness Edition/Controls/MyMsg/MyMsgJavaGuide.xaml(.cs)`**（教程弹窗）
- :22 `ConfirmDelay = TimeSpan.FromSeconds(60)`；:27 `remainingSeconds`；`DispatcherTimer` 每秒刷 `Btn1.Text = "我已阅读，继续（n）"` 且 `IsEnabled=false`
- :142 `Btn1_Click` 里再判 `!Btn1.IsEnabled → return`
- 正文四段：顶部四条要点（`Tools.Java.Guide.Body`）+ 三个小节（`Reason.Title/Body` 为什么 JRE 不是 JDK、
  `Msi.Title/Body` 为什么 MSI 不是压缩包、`Next.Title/Body` 接下来会发生什么）+ `Source` 免责小字
- 文案**全部去掉行尾句号**（用户要求）

**`PCL-Harness Edition/Controls/MyMsg/MyMsgJavaPath.xaml(.cs)`**（安装位置弹窗）
- 顶层 `MinWidth="640" MaxWidth="820"`（:5，原本 560/700，用户嫌窄已加宽）
- 三个 `RadioButton`：`RadioSystem`（系统默认位置）/ `RadioLauncher`（放进启动器文件夹）/ `RadioCustom`（自定义文件夹）
- `TextCustom`（MyTextBox）+ `BtnBrowse`（MyButton「选择」，`MinWidth="76"`）
- 末尾红色警告：`<Border HorizontalAlignment="Stretch">` 包 `TextBlock`（:64-66，**原来宽度塌成 45px 竖条，已修**）
- code-behind：`Radio_Checked` / `BtnBrowse_Click`（`SystemDialogs.SelectFolder`）/ `Btn1_Click`（`ValidateDirectory` 校验：空、非法字符、盘符不存在、直接选盘符根 都拒绝）/ `Btn2_Click`

**`PCL-Harness Edition/Pages/PageLaunch/PageLaunchRight.xaml`**
- :8 `PanHint`（PCL-HE 提示，只剩来历与免责）
- :20 `PanMemoryOpt`（**内存优化独立卡片**，标题 `Launch.Right.MemoryOpt.Title`，自带 × → `BtnMemoryOptClose_Click`）
- :37 `PanJavaHint`（Java 提示卡，`BtnJavaHintClose`）
- :52 `PanLog`

### 3.3 接入点
- `PCL-Harness Edition/Modules/ModMain.cs`
  - `enum MyMsgBoxType`：Text / Select / Input / AuthServer / Login / Markdown / **JavaGuide** / **JavaPath**
  - `MyMsgBox(...)` 新增可选参数 `MyMsgBoxType type = MyMsgBoxType.Text` 与 `object content = null`
  - `MyMsgBoxTick()` 里按 type 创建弹窗；**早退判断后新增了 `while (WaitingMyMsgBox.Count > 0 && WaitingMyMsgBox[0].IsExited) WaitingMyMsgBox.RemoveAt(0);`（修「关掉又弹一次」）**
- `PCL.Core/App/States.cs`：`[ConfigItem<bool>("UiLauncherMemoryOptHint", true)] MemoryOptimizeHint`、`[ConfigItem<bool>("UiLauncherJavaInstallHint", true)] JavaInstallHint`
- `PCL.Core/App/Config.cs`：`LaunchMemoryOptimizeBeforeLaunch`、`VersionMemoryOptimize`、`LaunchPurchaseHintDismissed`
- 语言文件真实路径：**`PCL.Core/App/Localization/Languages/{zh-CN,zh-TW,en-US,en-GB}.xaml`**（不是 `PCL-Harness Edition/Languages/`），UTF-8 无 BOM、CRLF

### 3.4 本次新增/使用的语言 key（四语都有）
`Tools.Java.Title/Description/Note`、`Tools.Java.Query.*`、`Tools.Java.Guide.Title/Body/Confirm`、
`Tools.Java.Guide.Reason.Title/Body`、`Tools.Java.Guide.Msi.Title/Body`、`Tools.Java.Guide.Next.Title/Body`、`Tools.Java.Guide.Source`、
`Tools.Java.Path.Title/Body/System/Launcher/Custom/Browse/BrowseTitle/Empty/Invalid/NoDrive/RootNotAllowed/Admin`、
`Tools.Java.Silent.Title/Body/Ok`、`Tools.Java.Upgrade.Title/Body/Confirm`、
`Tools.Java.StatusInstalled/StatusStale/StatusMissing`、`Tools.Java.Manual.Opened`、`Tools.Java.Install.Failed`、`Tools.Java.Silent.*`、
`Launch.Right.JavaHint.Title/Body/Where/Disclaimer`、`Launch.Right.MemoryOpt.*`

---

## 4. 已实机验证过的部分（截图在 `C:\Users\kingd\AppData\Local\Temp`）

| 项 | 证据 |
|---|---|
| 百宝箱 Java 卡四个按钮一行、查询显示「最新版本 8u504-b01，安装包 30.5 MB」 | `java8.png` / `java_q*.png` |
| 教程弹窗（60 秒倒计时、四段无句号、三段小标题） | `java_guide6.png` |
| 静默授权红色弹窗 | `java_silent.png` / `java_silent11.png` |
| 升级确认红色弹窗（「已经装过 Java 8 了」） | `java_upgrade2.png` |
| 安装位置弹窗（三选一 + 默认选中「放进启动器文件夹」） | `java_pathdlg2.png` |
| 首页三张独立卡片（PCL-HE 提示 / 内存优化 / Java 提示）+ 红字变深 + 字号变大 | `v2_home.png` |
| 下载 + sha256 校验（真下过 jre21.msi 35004416 字节，校验通过） | `C:/Users/kingd/AppData/Local/Temp/jre21.msi` |
| 已装 Adoptium JRE 列表（4 条，路径已不存在的那条自动调淡）+ 每条右侧「卸载」 | `v1030_uninstall.png` / `v1030_state.png` |
| 卸载红色确认框（标题「卸载 Java 21」、两段正文、左「卸载」右「取消」） | `v1030_unconfirm2.png` |
| 窄窗口（780 宽）四按钮排成 3+1 两行、左对齐无截断 | `narrow6.png` |
| 开屏静态预览生效、约 1 s 时半透明淡出 | `splash_a..e.png` / `splash_d.png` |

**没验证过的**：MSI 真正安装（本机不允许，原因见 §6「用户机器现状」）、静默提权安装全流程、
装完的自动登记与重扫、卸载的实际执行（只验证到确认框弹出，没点下去）。

---

## 5. 待办清单

> 2026-10-30 上午更新：除第 1 条「机房真机实测」外全部关闭。下面保留原编号。

### 已完成（本轮）

2. ~~手动路径装完没有任何提示~~：`BtnJavaInstall_Click` 现在拿住 `Process.Start` 的返回值，交给新增的
   `WatchManualInstall(feature, version, installDir, process)`（后台线程，30 分钟期限，每 1.5 秒查 `<installDir>\bin\java.exe`）。
   一旦出现就 `OnManualInstallDone` → `RegisterInstalledDirectory` + `ScanJavaAsync(force: true)` + `RefreshJavaInstalled()` + 提示 `Tools.Java.Manual.Done`；
   若 msiexec 已退出、再复查一次仍没有，则退回 `ModJavaInstall.FindInstalled(feature) is { LocationExists: true }`（用户可能改过路径），
   最后才提示 `Tools.Java.Manual.Cancelled`。
3. ~~卸载入口没做~~：`InstalledJre` 扩成 `(Feature, Version, DisplayName, Location, ProductCode)`，新增 `LocationExists` / `CanUninstall` 两个计算属性；
   `GetInstalledJres()` 把 ARP 键名（形如 `{4925953F-…}`）存进 `ProductCode`。Java 卡新增「已安装的 Adoptium JRE」区块，
   每条右侧「卸载」→ 红色确认框 → 提权 `msiexec /x {ProductCode} /qn /norestart`。
   提权复用 `[PromoteOperation("java-uninstall-msi")] UninstallMsi`（`PCL.Core/Utils/OS/JavaInstallPromote.cs`）+
   `RunUninstallViaPromote(productCode, timeout)`，3010 视为成功；完成后自动重扫并刷新列表，失败按返回码分别提示。
4. ~~下载没有进度~~：`DownloadInstaller` 在 `combo.Start()` 之后起一个 400 ms 间隔的后台线程，
   读 `download.files.FirstOrDefault()` 的 `DownloadedBytes` / `TotalSize`，把 `TextJavaStatus` 刷成
   「正在下载 Java 21：12.4 MB / 33.4 MB（37%）」；下载结束 `progressStop.Cancel()` + `Join(2000)`。
6. ~~缺 key 兜底~~：四语（zh-CN / zh-TW / en-US / en-GB）各 3257 个 key 已用脚本逐一比对，完全一致；
   `Tools.Java.*` 共 67 个。
7. ~~窄窗口换行~~：实机把窗口窄到 780 宽，四个按钮排成 3+1 两行（第一行「下载并安装 / 静默安装 / 重新扫描」，
   第二行「查询最新版本」），左对齐、无重叠无截断 —— `WrapPanel` 的默认行为可接受，**不改代码**。
8. ~~系统盘非 C 的路径小字~~：`GetSystemInstallDir` 用的是 `Environment.SpecialFolder.ProgramFiles`，
   `HasOtherDrive()` 拿 Windows 目录所在盘符与所有 `DriveType.Fixed` 比较，本来就没有硬编码 `C:\`，**不需要改**。
9. ~~补文档~~：`docs/PROJECT-HISTORY.md` 已补「第 10 章　自助安装 Java」与「第 11 章　界面字号规范」，
   开头「怎么用这份文档」的导航表也加了对应两行。
10. ~~下载源只能靠隐式镜像回落~~：Java 卡在「Java 版本」下方新增一行「下载源」下拉（`ComboJavaSource`），
    两项：「镜像源（推荐）」与「官方源」；持久化到 `States.Hint.JavaInstallerSource`
    （`PCL.Core/App/States.cs` 的 `[ConfigItem<int>("JavaInstallerSource", 0)]`，0=镜像源、1=官方源）。
    `ModJavaInstall.FetchLatest(int feature, bool officialOnly = false)`：镜像源时 urls = 清华 → 南大 → 官方直链（3 个），
    官方源时只有 Adoptium 直链（1 个）。切换后清空已查询结果，强制重新查询。
    说明文字明确写「只决定 Java 安装包从哪个站下载，和『设置 → 下载』里 Minecraft 资源的官方源、镜像源不是一回事」，
    避免两个同名概念混淆。日志可核对：`…镜像源优先，3 个下载源` / `…官方源，1 个下载源`。

### 仍待办

1. **机房真机实测**（只能真人做）：8 / 11 / 17 / 21 / 25 各走一遍
   - 手动路径：教程 → 路径 → 下载 → msiexec 向导 → 装完**不必再点「重新扫描」**，应自动登记并提示
   - 静默路径：授权 → UAC → 装完是否自动登记并重扫到
   - 卸载：点「卸载」→ UAC → 安装目录与卸载登记项是否都消失、列表是否自动刷新
   - 重点看：`INSTALLDIR` 覆盖是否真的生效、装到 `PCL\Adoptium JRE\jre-11` 这种非 Program Files 目录能否跑起来
5. **镜像回落只在 404 时生效**：清华/南大镜像版本滞后一版（21 是 `21.0.12.1_1`，API 是 `21.0.13_9`），
   按 API 文件名拼 URL 会 404，由下载器换源。**没验证过换源是否真的顺滑**，机房弱网时重点看。
   现已提供「下载源」下拉：镜像滞后或超时，可临时切到「官方源」只走 Adoptium 直链。

---

## 6. 关键技术事实（已核实，别重复踩）

### Adoptium API
`GET https://api.adoptium.net/v3/assets/latest/{feature}/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse`
- 返回**数组**，取 `[0].binary.installer.{name,link,size,checksum}`，`checksum` 就是 sha256，版本在 `[0].release_name` / `[0].version.semver`
- 实测（2026-10-30）：8u504+1 30.5MB、11.0.32+101 30.1MB、17.0.20+101 30.2MB、21.0.13+9 33.4MB、25.0.5+8 40.8MB
- **必须带 User-Agent 头**，否则 python urllib 会 403

### MSI 内部结构（用 MsiOpenDatabase 只读查过 jre21.msi）
- `ProductCode={4925953F-5097-4BC9-9446-6CD020E25719}`、`ProductVersion=21.0.13.9`、
  `UpgradeCode={E1ED1384-86C6-6900-91B2-CBA0DB8D3E2E}`、`Manufacturer=Eclipse Adoptium`
- `INSTALLDIR` 在 Directory 表里是**普通 public property**，命令行 `INSTALLDIR="..."` 可覆盖；
  默认值 `ProgramFiles64Folder\Eclipse Adoptium\jre-21.0.13.9-hotspot`；per-machine 安装（需管理员）
- Upgrade 表存在 → **装新版会 RemoveExistingProducts 卸旧版，并删掉旧 INSTALLDIR 整棵树**
  （所以路径弹窗里必须警告「请选一个空文件夹」，`ConfirmSameFeatureInstall` 也要先确认）

### 注册表
- Adoptium MSI **不写** `HKLM\SOFTWARE\JavaSoft\Java Runtime Environment`，也不写 `HKLM\SOFTWARE\Eclipse Adoptium`
- 只写卸载登记：`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{ProductCode}`（32 位在 `WOW6432Node`），
  有 `DisplayName`（`Eclipse Temurin JRE with Hotspot 21.0.13+9 (x64)`）、`DisplayVersion`（`21.0.13.101`）、`InstallLocation`
- → `PCL.Core/Minecraft/Java/Scanner/RegistryJavaScanner.cs`（只读 JavaSoft/Azul/Liberica）**扫不到 Adoptium 装的 JRE**，
  所以装完必须 `RegisterInstalledDirectory` 自己登记
- `InstallLocation` 是安装时写死的，用户手动挪目录后不会更新 → 界面要优先用真实扫描结果，路径不存在时给 `Tools.Java.StatusStale` 提示

### 用户机器现状（测试时的基线）
- 装了 Adoptium JRE **8 / 17 / 21 / 25**（用户以为还装了 11，其实没有）
  - 8 → `E:\jre8`（已挪走，登记过期）、17 → `D:\java8,21,25\jre17`（已挪到 `D:\java\jre17`）、21 → `E:\jre21`、25 → `E:\jre25`
- 磁盘：C（系统）、D、E 都在，所以 `HasOtherDrive()` 为 true → 路径弹窗默认选「放进启动器文件夹」
- 物理内存 15.72 GB，非管理员运行

---

## 7. 踩过的坑（都别重复）

1. **`edit` 工具会丢 BOM / 改换行** → 语言文件与 PowerShell 脚本改动一律用 python 按字节读写保持原样。
2. **`edit` 若同一段文本出现两处会直接抛错**（`remoteResult = MyMsgBox(...)` 在 ModMain.cs 里有两处）→ 按行号用 python 定点回退。
3. **python 改过的文件再 `edit` 会报 `file changed since it was read`** → 先 `read` 一次再改。
4. **bash heredoc 写 PowerShell 脚本会吃反斜杠**（`HKLM:\SOFTWARE\…` 变成 `HKLM:SOFTWAREMicrosoft…`）→ 用 `write` 工具写 .ps1 再 `-File` 执行。
5. **bash 里双引号包 PowerShell 会展开 `$` 变量** → 整段用单引号或写文件。
6. **`.NET 程序集里的字符串是 UTF-16**，用 ASCII grep `.dll` 一律 0 命中 → 按 `utf-16-le` 搜。
7. **`grep` 传不存在的路径会让整个 run_code 程序抛 ToolCallError 并中断后续调用** → 先用 glob 定位，且把可能失败的查询包 try/catch。
8. **XAML 里换行要写 `&#10;`**，写成 `&amp;#10;` 会显示字面量（我犯过，四语各 15 处已修）。
9. **重复的属性会让 XAML 编译直接失败**（我给同一个 TextBlock 加了两个 `FontSize`，构建时报错，已删）。
10. **`MyIconButton.Click` 是 `(object, EventArgs)`，`MyButton.Click` 是 `(object, MouseButtonEventArgs)`**，别混。
11. **后台线程不能读 `ComboBox.SelectedIndex`** → UI 读取一律先 `Dispatcher.CheckAccess()` 切回 UI 线程。
12. **`ModMain.MyMsgBox` 只返回 int**，需要传对象进出时用新增的 `content` 参数（`MyMsgBoxConverter.Content`）。

---

## 8. 其他挂起事务（不属于这次 Java 功能，但别丢）

1. **内存优化**：三档（轻量护航 / 深度清理 / 游戏内护航）+ 启动前自动优化已实现、已部署；
   等维护者实机复验（启动不再报错、开关位置、实际释放量）。用户还要求把它做成**仓库内独立子项目**并补 GitHub 说明（`PCL-HE/PCL.MemoryOptimizer/` 已建，README/DESIGN 待补全）。
2. **LittleSkin 登录**：OAuth 客户端 `client_id=1637` 已获白名单（2026-10-26 邮件），但处于**测试模式**（只有应用创建者本人能授权）；实测待回传日志。
3. **git**：`origin/main` 有 3 个未推送 commit（`bcf310e` / `89b9a53` / `668c4d1`）＋本次改动未提交；是否 push 等用户点头。
4. **发版**：计划 `v0.1.3-Alpha`（metadata base 0.1.2→0.1.3、suffix Alpha、code 3→4），CI 四产物（x64/ARM64 × selfcontained/slim）需要 push tag 才真验证。
5. **未跟踪文件** `docs/PROJECT-HISTORY.md.bak-20261024`（以及 `.bak-20261030`）待用户决定去留。
6. **子代理 99ce7240 交付的 main 说明草稿**：`C:\Users\kingd\AppData\Local\Temp\pclhe_main_preview.md`（8052 字节，仓库零改动），用户曾提议让它接手后续发布提交。

---

## 9. 建议的续接方式

新会话里第一件事：
1. 读本文件 + `docs/PROJECT-HISTORY.md`（尤其第 7 章构建部署、附录 C 交接）。
2. `git status` + `git log --oneline -5` 确认工作区状态。本文件写完时（HEAD = `668c4d1`）的未提交清单：
   - 已修改：`PCL-Harness Edition/Application.xaml`、`Modules/ModMain.cs`、`Pages/PageLaunch/PageLaunchRight.xaml(.cs)`、`Pages/PageTools/PageToolsTest.xaml(.cs)`、`PCL.Core/App/States.cs`、`PCL.Core/App/Localization/Languages/{zh-CN,zh-TW,en-US,en-GB}.xaml`、`docs/PROJECT-HISTORY.md`
   - 新增（未跟踪）：`Controls/MyMsg/MyMsgJavaGuide.xaml(.cs)`、`Controls/MyMsg/MyMsgJavaPath.xaml(.cs)`、`Modules/Minecraft/ModJavaInstall.cs`、`PCL.Core/Utils/OS/JavaInstallPromote.cs`、`docs/HANDOVER-2026-10-30-JAVA-INSTALL.md`
   - 已提交但未 push 的三个 commit：`668c4d1`（docs: 主题/图标/交接）、`89b9a53`（Lapis Blue 配色）、`bcf310e`（新建档案图标与内存单选框对齐）
3. 复现构建 → 部署 → 截图，确认与本文档描述一致，再动手做 §5 的 P0。

---

## 10. 启动开屏卡顿（2026-10-30 上午一并修了）

### 10.1 现象与根因
用户反馈启动第一秒像「卡一下」，开屏那张图几乎看不到内容。

实测启动链本身只要约 600 ms：BeforeLoading 254 ms / Loading 135 ms / Loaded 596 ms /
WindowCreating 226 ms / WindowCreated 14 ms（第一阶段 422 ms、第二阶段 156 ms）。

真正的原因在时序，不在启动链：
- `LifecycleService._StartStateFlow` 是同步 for 循环 + `.GetAwaiter().GetResult()`，一口气跑完 Loading→Loaded→WindowCreating，中间不让出消息泵；
- 开屏窗口的 `Loaded` 要等消息泵空闲才触发 `PlayIntro()`，而那时 `FormMain` 已经在往屏幕上画；
- 结果入场动画被压扁，整窗基本只剩一帧。

次要因素（都已顺手改掉）：`AllowsTransparency=True` 让窗口只能整窗软件光栅化、
`DropShadowEffect BlurRadius=26` 每帧卷积、512×512 的 logo 每帧按 HighQuality 重采样到 84。

### 10.2 改了什么
- `PCL-Harness Edition/FormSplash.xaml`：删 `DropShadowEffect`，换成静态 `RadialGradientBrush` 辉光 + `Ellipse`；
  logo 加 `DecodePixelWidth="168"`；`RootGrid` 加 `RenderTransformOrigin="0.5,0.5"`，`ScaleTransform` 去掉硬编码 `CenterX/CenterY`。
- `FormSplash.xaml.cs`：新增 `ShowStaticPreview()`（RootGrid / LogoBox / LabTitle / LabSub / ProgressTrack 的 Opacity 全置 1，
  RootScale=1、LogoShift.Y=0）与 `StartProgressSweep(speed, delayMs)`；`PlayIntro()` 开头用 `_createdUtc` 算滞后，
  超过 400 ms 就直接静态呈现并记日志；整套节奏压到 680 ms 内收尾；
  `BeginFadeOut()` 先停光带，并把淡出从窗口 `Opacity` 挪到 `RootGrid`（视觉等价，但能被 `RenderTargetBitmap` 诊断到）。
- `PCL-Harness Edition/Application.xaml.cs`：`frmStart.Show()` 之后立刻 `ShowStaticPreview()` 并做一次
  `DispatcherPriority.Render` 让步；`Config.Debug.AnimationSpeed` 改为提前调 `ModSetup.SystemDebugAnim(...)`，
  否则开屏永远用 1 倍速（`ApplyAll` 要到主窗体 Loaded 才跑）。
- `PCL-Harness Edition/FormMain.xaml.cs`：`FormMain_Loaded` 里 `PageSetupUI.BackgroundRefresh`、
  `ModMusic.MusicRefreshPlay`、`ThemeManager.ThemeRefresh`、`ModSetup.ApplyAll` 与四个 Blur 资源赋值，
  全部挪到 `isSizeSaveable = true; ShowWindowToTop();` 之后，用 `DispatcherPriority.ApplicationIdle` 排队执行。

### 10.3 验证与遗留
- 设环境变量 `HE_SPLASH_SHOT=<目录>` 启动会导出 props。实测首行是
  `0ms root=1.000 logo=1.000 title=1.000 sub=1.000 track=0.984`，root 从 560 ms 才开始下降 → 静态预览确实先生效了。
- 连拍 `splash_a..e.png`：1020 ms 能看到开屏窗口半透明淡出，2600 ms 主界面完全正常。
- 遗留：快机器上 `lag < 400 ms` 的入场动画分支，本机从未执行过（本机每次都超过 400 ms）。


---

## 11. 2026-10-30 04:00 收尾（第二轮）

### 11.1 本轮又改了什么

- 「下载源」下拉：见 §5 已完成第 10 条。
- 全局文案去段尾句号：`zh-CN.xaml` 480 个 key 删 638 个段尾「。」（389470→387556 字节），`zh-TW.xaml` 480 个 key 删 640 个（391249→389329）；字节减少量恰好等于「。」个数 ×3，证明没有别的字节被改动。可见长度 < 12 的短提示、句子中间的句号、以 ？/！/…… 结尾的段落一律保留。副作用：`Setup.About.UpstreamLegal.*` / `Setup.About.Legal.*` / `Setup.About.EasterEgg.*` 共 10 条法律与彩蛋文案也去掉了段尾句号——原文各段是否本来带句号已不可逆，如需还原要逐条人工核对。
- 字号 100% 基线：取值与理由写进 `docs/PROJECT-HISTORY.md:386` 的 2.13 小节。注意第 11 章 11.2 表格里「卡片标题 16」与代码 `PCL-Harness Edition/Controls/MyCard.cs:126` 的 `FontSize = 15d` 仍不一致，**以代码为准**。
- 弹窗只弹一次：`PCL-Harness Edition/Modules/ModMain.cs` 的 `MyMsgBox` / `MyMsgBoxMarkdown` 原先在非 UI 线程分支里先 `WaitingMyMsgBox.Add(converter)` 再 `ModBase.RunInUiWait(() => remoteResult = MyMsgBox(...))`，那个 converter 永远不会被移除，UI 线程又加一个，于是用户关掉后还会再弹一次；`MyMsgBoxTick()` 里加「队首已退出就移除」的清理后消失。
- 新装 JRE 的登记：`RegisterInstalledDirectory` 仍要显式调用（Adoptium MSI 不写 `HKLM\SOFTWARE\JavaSoft`），手动路径由 `WatchManualInstall` 负责，静默路径由 `BtnJavaSilent_Click` 负责。

### 11.2 验证记录

- 切官方源：`C:\Users\kingd\PCL-HE\CONFIG\config.v1.json` 里 `"JavaInstallerSource": 1`，界面显示「官方源」并清空版本信息；点「查询最新版本」后日志 `Launch-2026-10-30-034339424.log:208` 记 `[Java] 最新 JRE 8：8u504-b01，30.5 MB，官方源，1 个下载源`；切回镜像源后同一文件 `:156` 记 `…镜像源优先，3 个下载源`。
- 重启后下拉默认回到「镜像源（推荐）」，`"JavaInstallerSource": 0` 没有被改回。
- 截图（`C:\Users\kingd\AppData\Local\Temp`）：`v6_java.png`（Java 卡完整状态：下载源行、四个按钮、已安装列表与卸载按钮）、`v5_d2.png`（官方源选中）、`v6_tools.png`（去句号后的内存优化卡）、`v4_g30.png`（教程弹窗 30 秒倒计时）。
- **合成点击的坑**：窗口失去焦点时 `SetWindowPos` + `mouse_event` 会静默失效（点击落在别的窗口上，截图却以为点到了）。每次动作前先 `WScript.Shell.AppActivate($pid)`，再用 `GetWindowRect` 确认窗口真的在 `0,0 @ 1247x771`；点击后取截图文件哈希比对，哈希没变就说明这次点击没生效。

### 11.3 仍没做的

- §5 仍待办第 1 条（机房真机实测 8/11/17/21/25）与第 5 条（弱网下镜像回落的顺滑度）不变，只能真人做。
- 手动安装后的自动反馈只在本机逻辑层验证过（本机已装 8/11/21/25，MSI 不能真装），真机需确认 `<installDir>\bin\java.exe` 出现后能自动登记并提示。
- 卸载入口同理：本机点「卸载」能走到红色确认框，但**没有真的执行**（取消了），真机需确认目录与卸载登记项都消失。

## 12. 2026-10-30 04:15 界面微调（第三轮）

### 12.1 本轮改动

1. **Java 卡行序**：现在是「下载源 → Java 版本 → 四个按钮」，两个下拉同为 160×26，两侧说明同字号 13.5。
2. **下载源说明断行**：`Tools.Java.Source.Note` 在「；」后插 `&#10;`，四语同步。
3. **内存优化三块重排**：轻量（绿）→ 游戏内（蓝）→ 深度（红），每块改成「左文字 + 右按钮」两列 Grid，按钮 `VerticalAlignment=Center`。
4. **「刷新」右置**：`TextMemoryStatus` 与 `BtnMemoryRefresh` 合并成一行（`*` + `Auto`）。
5. **小节介绍断行**：`Tools.Memory.Light/Game/Deep.Description` 按「。」「；」插 `&#10;`，四语同步。
6. **`MyButton` 新增两档配色**：`ColorState.Green = 4`、`ColorState.Blue = 5`，新增 `GetHoverBackgroundResourceKey()`；
   `Application.xaml` 补 `ColorBrushGreen/Blue` × Back/Light/Dark 六个资源。
7. **法律/隐私/彩蛋句号回滚**：上一轮全局删句号时误伤这 6 条，已从 `git show HEAD:` 取回原值（zh-CN 与 zh-TW 各 6 条）。

### 12.2 验证

* 构建：5391 个警告、0 个错误、42.61 秒。
* 部署：签名 Valid，目标 `C:/Users/kingd/PCL-HE`。
* 截图：`C:/Users/kingd/AppData/Local/Temp/v7_top5.png`（内存卡完整，绿/蓝/红三色按钮就位、刷新在统计行右侧）、
  `v7_mem.png`（Java 卡：下载源在上、两个下拉等宽、说明两行）。

### 12.3 又踩到的坑

* **合成滚轮**：`mouse_event(0x0800, …, 240)` 在有动画的 `MyScrollViewer` 上会被吞掉。
  可靠做法是 `SendMessage(hwnd, 0x020A, (IntPtr)(120 << 16), (IntPtr)(x + (y << 16)))`，每次一个齿、间隔 160 ms。
* **合成点击**：点完必须比对截图哈希确认真的生效——`AppActivate` + `SetForegroundWindow` 之后仍可能只激活窗口而不触发按钮。


-
-
-




#
#
 
1
3
.
 
行
高
与
列
宽
对
齐
（
2
0
2
6
-
1
0
-
3
0
 
下
午
，
用
户
 
m
0
1
5
8
1
 
/
 
m
0
1
5
8
8
）




#
#
#
 
1
3
.
1
 
内
存
卡
行
高
拉
大




用
户
反
馈
三
块
太
窄
。
~
~
P
a
g
e
T
o
o
l
s
T
e
s
t
.
x
a
m
l
~
~
 
内
存
卡
统
一
调
整
：




|
 
位
置
 
|
 
改
动
 
|


|
 
-
-
-
 
|
 
-
-
-
 
|


|
 
卡
片
描
述
 
/
 
三
块
描
述
 
/
 
危
险
提
示
 
/
 
底
部
提
示
 
|
 
加
 
~
~
L
i
n
e
H
e
i
g
h
t
=
"
2
1
"
 
L
i
n
e
S
t
a
c
k
i
n
g
S
t
r
a
t
e
g
y
=
"
B
l
o
c
k
L
i
n
e
H
e
i
g
h
t
"
~
~
（
1
3
.
5
 
号
字
约
 
1
.
5
5
 
倍
行
距
）
 
|


|
 
三
块
之
间
 
|
 
~
~
M
a
r
g
i
n
~
~
 
~
~
0
,
1
8
~
~
 
→
 
~
~
0
,
2
6
~
~
 
|


|
 
小
节
标
题
与
描
述
 
|
 
~
~
0
,
6
~
~
 
→
 
~
~
0
,
8
~
~
 
|


|
 
危
险
提
示
上
边
距
 
|
 
~
~
0
,
1
6
~
~
 
→
 
~
~
0
,
2
2
~
~
 
|


|
 
状
态
行
 
/
 
底
部
提
示
 
|
 
~
~
0
,
8
~
~
 
→
 
~
~
0
,
1
0
~
~
 
|




#
#
#
 
1
3
.
2
 
J
a
v
a
 
卡
两
行
下
拉
对
齐




~
~
下
载
源
~
~
 
与
 
~
~
J
a
v
a
 
版
本
~
~
 
标
签
文
字
宽
度
不
同
，
两
个
独
立
 
~
~
G
r
i
d
~
~
 
的
 
~
~
A
u
t
o
~
~
 
列
各
算
各
的
，


两
个
下
拉
起
点
差
约
 
2
2
 
p
x
。
做
法
：
两
个
 
~
~
G
r
i
d
~
~
 
的
第
 
1
 
列
都
加
 
~
~
S
h
a
r
e
d
S
i
z
e
G
r
o
u
p
=
"
J
a
v
a
R
o
w
L
a
b
e
l
"
~
~
、


第
 
2
 
列
加
 
~
~
S
h
a
r
e
d
S
i
z
e
G
r
o
u
p
=
"
J
a
v
a
R
o
w
V
a
l
u
e
"
~
~
，
靠
 
~
~
P
a
n
M
a
i
n
~
~
 
上
已
有
的
 
~
~
G
r
i
d
.
I
s
S
h
a
r
e
d
S
i
z
e
S
c
o
p
e
=
"
T
r
u
e
"
~
~


（
~
~
P
a
g
e
T
o
o
l
s
T
e
s
t
.
x
a
m
l
:
9
~
~
）
生
效
；
版
本
行
上
边
距
 
~
~
0
,
1
2
~
~
 
→
 
~
~
0
,
1
4
~
~
。




*
*
踩
坑
*
*
：
第
一
次
只
给
「
下
载
源
」
那
一
行
加
了
组
名
，
界
面
依
旧
不
齐
—
—
~
~
S
h
a
r
e
d
S
i
z
e
G
r
o
u
p
~
~
 
必
须
*
*
两
边
都
声
明
同
一
组
名
*
*


才
会
互
相
取
最
大
值
，
单
边
声
明
不
报
错
、
静
默
无
效
。




#
#
#
 
1
3
.
3
 
验
证




*
 
构
建
：
5
3
8
8
 
个
警
告
、
0
 
个
错
误
、
1
2
.
4
4
 
秒
；
部
署
签
名
 
V
a
l
i
d
。


*
 
截
图
 
~
~
C
:
/
U
s
e
r
s
/
k
i
n
g
d
/
A
p
p
D
a
t
a
/
L
o
c
a
l
/
T
e
m
p
/
v
9
_
m
e
m
t
o
p
.
p
n
g
~
~
：
内
存
卡
行
距
舒
展
，
三
块
间
距
均
等
。


*
 
截
图
 
~
~
C
:
/
U
s
e
r
s
/
k
i
n
g
d
/
A
p
p
D
a
t
a
/
L
o
c
a
l
/
T
e
m
p
/
v
9
_
m
e
m
.
p
n
g
~
~
：
两
组
下
拉
左
边
缘
同
在
 
x
≈
2
5
7
，
说
明
文
字
同
在
 
x
≈
4
4
7
。




-
-
-




#
#
 
1
4
.
 
默
认
窗
口
尺
寸
改
成
每
次
启
动
回
默
认
（
2
0
2
6
-
1
0
-
3
0
 
下
午
）




#
#
#
 
1
4
.
1
 
决
策




用
户
先
问
「
为
什
么
窗
口
没
恢
复
默
认
大
小
」
，
确
认
现
行
为
「
记
住
上
次
尺
寸
」
后
，


指
定
默
认
尺
寸
为
 
1
6
:
9
、
不
能
太
大
也
不
能
太
小
、
跟
随
系
统
缩
放
。
屏
幕
 
1
9
2
0
x
1
0
8
0
 
@
1
0
0
%
，


最
终
取
 
*
*
1
2
8
0
x
7
2
0
*
*
（
2
/
3
 
宽
，
留
白
充
足
，
卡
片
与
四
个
按
钮
单
行
放
得
下
）
。




#
#
#
 
1
4
.
2
 
改
动




*
 
~
~
P
C
L
-
H
a
r
n
e
s
s
 
E
d
i
t
i
o
n
/
F
o
r
m
M
a
i
n
.
x
a
m
l
~
~
：
~
~
W
i
d
t
h
=
"
8
5
0
"
 
H
e
i
g
h
t
=
"
5
0
0
"
~
~
 
→
 
~
~
W
i
d
t
h
=
"
1
2
8
0
"
 
H
e
i
g
h
t
=
"
7
2
0
"
~
~
，
设
计
器
尺
寸
同
步
。


*
 
~
~
P
C
L
-
H
a
r
n
e
s
s
 
E
d
i
t
i
o
n
/
F
o
r
m
M
a
i
n
.
x
a
m
l
.
c
s
~
~
（
原
 
:
1
0
3
-
1
2
4
）
：
删
掉
读
 
~
~
S
t
a
t
e
s
.
U
I
.
W
i
n
d
o
w
W
i
d
t
h
/
H
e
i
g
h
t
~
~
 
的
逻
辑
，


 
 
改
为
取
 
X
A
M
L
 
设
计
值
 
+
 
工
作
区
 
9
2
%
 
等
比
收
缩
（
先
压
宽
再
压
高
，
保
持
 
1
6
:
9
）
。


*
 
~
~
P
C
L
.
C
o
r
e
/
A
p
p
/
S
t
a
t
e
s
.
c
s
~
~
：
~
~
W
i
n
d
o
w
H
e
i
g
h
t
~
~
 
默
认
 
~
~
5
0
0
~
~
→
~
~
7
2
0
~
~
、
~
~
W
i
n
d
o
w
W
i
d
t
h
~
~
 
默
认
 
~
~
8
5
0
~
~
→
~
~
1
2
8
0
~
~
（
兜
底
用
）
。


*
 
~
~
F
o
r
m
M
a
i
n
_
S
i
z
e
C
h
a
n
g
e
d
~
~
 
保
持
写
回
配
置
（
不
再
被
读
，
留
着
以
后
加
开
关
）
。




#
#
#
 
1
4
.
3
 
验
证




*
 
启
动
后
 
~
~
G
e
t
W
i
n
d
o
w
R
e
c
t
~
~
 
=
 
~
~
1
2
8
0
x
7
2
0
~
~
，
位
置
 
~
~
(
2
6
0
,
1
1
4
)
~
~
（
居
中
）
。


*
 
配
置
里
强
行
写
 
~
~
W
i
n
d
o
w
W
i
d
t
h
:
 
9
0
0
 
/
 
W
i
n
d
o
w
H
e
i
g
h
t
:
 
6
0
0
~
~
 
再
启
动
，
窗
口
仍
是
 
~
~
1
2
8
0
x
7
2
0
~
~
。


*
 
截
图
 
~
~
C
:
/
U
s
e
r
s
/
k
i
n
g
d
/
A
p
p
D
a
t
a
/
L
o
c
a
l
/
T
e
m
p
/
v
1
0
_
f
r
o
n
t
.
p
n
g
~
~
：
1
2
8
0
 
宽
下
左
栏
、
卡
片
、
四
按
钮
一
行
全
部
正
常
。

---

## 15. 内存卡按钮加高与启动过渡调整（2026-10-30 傍晚）

### 15.1 内存卡四个按钮加高

用户反馈内存卡的四个按钮（刷新 / 执行轻量护航 / 执行游戏内护航 / 执行深度清理）比 Java 卡的按钮扁。
原因是 Java 卡的按钮落在 `WrapPanel ItemHeight="50"` 里、被行高拉伸到约 35px，
而内存卡的按钮在普通 `Grid` 里由内容撑开，只有 22px 左右。

改法：给这四个 `MyButton` 各加 `Height="36"`（`PageToolsTest.xaml:22/38/54/72`），与 Java 卡按钮视觉齐平。

截图：`C:/Users/kingd/AppData/Local/Temp/v11_mem3.png`

### 15.2 启动过渡的三处调整

1. `FormSplash.xaml.cs:22` 的 `MinVisibleMs` 900 → 700（开屏少占 200ms）。
2. `FormMain.xaml.cs` 的 `ShowWindowToTop()` 之后新增：先 `UpdateLayout()` +
   `Dispatcher.Invoke(DispatcherPriority.Render, ...)` 把主窗体首帧渲染跑完，再开始淡入，
   避免淡入前几帧现算布局导致「开屏刚淡出、窗口卡一下」。
3. 开屏淡出 400 → 280ms；主窗体淡入 delay 100 → 40ms、时长 250 → 300ms（重叠更连贯）。

### 15.3 启动耗时测量（澄清「更卡顿」）

日志（`C:/Users/kingd/PCL-HE/PCL/Log/Launch-2026-10-30-042418997.log`）：

| 阶段 | 用时 |
| --- | --- |
| BeforeLoading → Loading | 247 ms |
| Loading（配置/缓存/网络） | 110 ms |
| 第一阶段 | 422 ms |
| 第二阶段（主窗体构造） | 141 ms |
| 第三阶段 | 15 ms |

进程启动到主窗体就绪约 1.2 秒，**没有变慢**。
用户感受到的「顿一下才弹出窗口」来自：自定义开屏是 WPF 窗口，要等 WPF 容器与 Dispatcher 就绪（约 1 秒）
才能画出第一帧，这段时间屏幕没有任何反馈。这是把原生 `SplashScreen` 换成 WPF 开屏窗口带来的固有代价。
若想更快看到主窗体，可在「设置 → 界面」关闭 `UiLauncherLogo`（`ShowStartupLogo`），
启动路径会直接跳过开屏。

### 15.4 自动截图的新坑

窗口在 (320,180) 时，`WindowFromPoint(400,300)` 返回桌面 `FolderView`、合成点击全部失效；
用 `SetWindowPos(h, HWND_TOPMOST, 0, 0, 1280, 720, SWP_SHOWWINDOW)` 把窗口挪回 (0,0) 后命中恢复正常。
所以自动化点击前必须先确认 `WindowFromPoint` 命中的是 PCL 窗口本身，不能只看截图。
