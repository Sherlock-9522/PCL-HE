# PCL-HE 项目历史记录

这份文档记录 PCL-HE 从 PCL-CE 基线到 `v0.1.1` 之间所有重要的改动、缺陷修复与技术决策。
它面向「接手这个仓库的下一个会话」，目的不是复述 commit message，而是**保留那些只存在于
当时对话里的根因分析、踩过的坑、以及为什么选择这个方案而不是另一个**。

## 怎么用这份文档

文档按主题分章，**每章自包含**。如果你只需要处理某个方向的工作，读对应那一章即可，不必通读。

| 你要做的事 | 读哪一章 |
| --- | --- |
| 了解项目为什么叫这个名字、为什么去掉某些词 | 第 0 章 |
| 改品牌、图标、窗口标题、关于页 | 第 1 章 |
| 改界面布局、窗口尺寸、缩放行为、主题配色 | 第 2 章 |
| 改下载逻辑、加镜像源、修网络超时 | 第 3 章 |
| 改登录（微软 / 第三方 / 离线） | 第 4 章 |
| 改联机（陶瓦 / EasyTier / 大厅） | 第 5 章 |
| 改内存相关功能 | 第 6 章 |
| 改构建、CI、发布流程、部署 | 第 7 章 |
| 查「这个问题以前修过吗」「当时为什么这么写」 | 第 8 章（审计修复汇总） |
| 改 README、许可证声明、合规相关内容 | 第 9 章 |
| 想知道某个 commit 属于哪个主题 | 附录 A |

**给分段执行者的提示**：本文件的章节之间没有前置依赖。若任务要求你「按历史顺序理解项目」，
读第 0 章 → 附录 A（提交时间线）即可，其余章节按需查阅。

---

## 第 0 章　项目由来与命名

### 命名演变

| 阶段 | 名称 | 说明 |
| --- | --- | --- |
| 基线 | PCL-CE | 上游为 PCL Community Edition，直接继承自 PCL2 |
| 中期 | PCL-DSH | 初版衍生名，含企业商标缩写 |
| 当前 | **PCL-HE** | Plain Craft Launcher - Harness Edition |

改名到 `PCL-HE` 的原因是**规避数字商标风险**：原名里的缩写对应某 AI 企业的品牌标识。
新名保留 `Harness`（马具 / 驾驭）这一通用词汇，用来纪念「人与 AI 协作开发」这件事本身。
缩写 `HE` 读作「he」，简洁且不与上游撞名。

仓库路径同期从 `PCL-DSH` 改为 `PCL-HE`（GitHub 侧的 301 重定向至今有效）。

### 展示名与元数据

- 完整展示名：`Plain Craft Launcher (PCL) - Harness Edition`
- 短名 / 可执行文件名：`PCL-HE`，产物为 `PCL-HE.exe`
- 公司字段：`PCL-HE Project`
- 版权字段保留 `Copyright © 龙腾猫跃 2016.` 前缀，后缀追加 `Modified by the PCL-HE Project`
- 版本号从 `0.1.0.0` 起步，`v0.1.1` 为当前 Tag

### 开发模式（写进 README 的那段）

项目采用「人类架构与合规审查 + AI 辅助编程」：发起人负责产品设计、开源协议合规性审查、
测试反馈与核心思路；核心代码重构、功能实现与排错主要由 AI 辅助完成。
这一段同时出现在三份 README 里，措辞略有差异但语义一致。

---

## 第 1 章　品牌替换

### 一次性批量替换

`logo_design/rebrand.py` 做了一次 57 文件 / 719 处的字符串替换，策略是**长字符串优先**，
并保留原文件的 BOM 与换行符。替换范围包括产品名、公司名、仓库地址、文档链接。

**教训**：批量替换脚本必须按「长串在前」排序，否则 `PCL Community Edition` 会被
`PCL` 之类短串先切碎。

### 会静默出错的三个地方

这三个地方替换错了不会编译报错，只会在运行时炸掉：

1. **WPF pack URI 内嵌程序集名**。`<AssemblyName>` 改成 `PCL-HE` 后，所有
   `/Plain Craft Launcher 2;component/...` 必须同步成 `/PCL-HE;component/...`（20 处 / 8 文件）。
2. **C# 字符串里的 `clr-namespace:PCL;assembly=...`**。这些写在 `.xaml.cs` 的字符串常量里，
   XAML 编译器看不见。漏改会在页面首次打开时抛
   `XamlParseException: 无法创建未知类型 "{clr-namespace:PCL;assembly=Plain Craft Launcher 2}MyCard"`。
   实际漏了 7 处：`PageSpeedLeft.xaml.cs:202/216/233/275`、`PageLaunchRight.xaml.cs:482/846`、
   `PageHelpDetail.xaml.cs:56`。用 `logo_design/fix_assembly_refs.py` 统一修掉。
3. **IPC 管道名**。`PCL.Core/App/Essentials/RpcService.cs:78` 的 `PipePrefix` 与
   `PromoteService.cs:48` 的 `PCLHE_PM@{processId}`，改漏会导致新旧版本互相抢单例锁。

### 图标与字标

- `Images/icon.png` / `icon.ico`：六边形 + Arial Bold 的 `HE`
- `Images/logo_he.png`：开屏动画用的大图
- `Images/mascot.png`：页面水印

**字标居中的坑**：早期用字体 advance 宽度做居中，成品里 `H` 比 `E` 占位宽，整体看起来偏左。
根因是 **Microsoft YaHei 是 CJK 优先字体，拉丁字形本身偏窄**（H/E 墨迹比 1.44），换成
Arial Bold 后降到 1.05。最终改用 `PIL` 的 `getbbox()` 按**墨迹包围盒**居中，而不是按字体度量。

### 鲸鱼图标的处理

上游 PCL 的鲸鱼吉祥物可能涉及数字资产授权，全部移除：`whale.png` / `whale_blue.png` /
`whale_dark.png` 删除，相关引用改指向新 logo。

后来发现 `Images/Heads/Logo-HE.png` 只是**改了文件名、内容是旧鲸鱼图**，在「关于 → 软件信息」
里露了出来。已用 `logo_he.png` 覆盖，并把裁剪形状从 `EllipseGeometry` 改成
`RectangleGeometry Rect="0,0,34,34" RadiusX="7" RadiusY="7"`——圆形裁剪会削掉六边形的上下尖角。

**这个文件里有 9 处相同的 `EllipseGeometry` 片段**（9 个圆形头像），替换时必须带上
`Source="/PCL-HE;component/Images/Heads/Logo-HE.png"` 前缀做唯一匹配，否则会误改其他头像。

### 用户可见输出的品牌清理

`1099a57` 清掉最后三处：`PageSetupAbout.xaml` 的引用、`CrashReportExporter.cs`、
`CrashLogPreparer.cs`、`app.manifest` 的 `assemblyIdentity`。

---

## 第 2 章　界面、窗口与主题

### 2.1 启动时窗口莫名放大

**现象**：每次启动窗口都比上次大一圈，偶尔直接铺满屏幕。

**根因**：`FormMain_SizeChanged` 无条件把当前 `Height`/`Width` 写进 `States.System`。
窗口被最大化时，写入的是**屏幕尺寸**；下次启动按这个尺寸恢复，就变成了近似全屏。
再叠加另一处不一致：`FormMain.xaml` 设计尺寸是 `850x500`，而 `PCL.Core/App/States.cs`
的默认值是 `1024x640`。

**修法**（`bdcdd0f`）：
- `States.cs` 默认值改成与 XAML 一致（`500` / `850`）
- `FormMain.xaml.cs:101-115` 恢复尺寸时校验：`double.IsNaN`、`<= 0`、超出工作区一律退回设计尺寸
- `FormMain_SizeChanged` 只在非最大化时持久化

**实机验证**：DPI 125% 下，首次运行 → `850.4x500` DIP；保存 `1520x856` → 原样恢复；
故意写入 `4000x3000`（超出工作区）→ 回退到 `850.4x500`。

### 2.2 边框与缩放热区对不上

**现象**：鼠标移到窗口边框上看不出能拖动，光标一直是箭头。

这条追了三层：

1. **热区判定的坐标系不对**。原来用 `GetWindowRect` 算边界，但 DWM 会向外扩一圈阴影，
   热区落在了不可见区域。改用 `WindowInterop.GetVisibleWindowRectangle(hWnd)`，
   内部走 `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`。
2. **`canResize` 恒为 false**。诊断日志显示每次 `WM_NCHITTEST` 都在
   `FormMain.xaml.cs:431` 的 `if (!canResize) return HTCLIENT;` 被短路。
   根因是 `Config.Preference.LockWindowSize` 默认 `true`（**且这一项没有显式指定
   `ConfigSource`，落到了 `Shared` 全局配置**，写在 `%APPDATA%\PCLHE\config.v1.json`
   而不是实例配置里），启动时走 `RemoveResizer()`。
3. **光标被 WPF 覆盖**。即使返回了 `HTLEFT` 等命中码，`WM_NCHITTEST` 里调用的
   `SetCursor` 也会被随后的 `WM_SETCURSOR` 重置回自定义光标。

**最终方案**（`39c35fc`）：在 `_SizeWndProc` 里同时接管 `WM_SETCURSOR`
（`const int WM_SETCURSOR = 0x20;`），把 `lParam` 低 16 位的命中码映射回
`ResizeEdge` 再 `WindowInterop.SetResizeCursor(edge)`。八处实测全部正确。

`WindowInterop` 里为此新增了 `ResizeEdge` 枚举与 `LoadCursorW` / `SetCursor` 的
`LibraryImport`。**注意 `LoadCursor` 第二参数是 `IntPtr`，`IDC_*` 常量是 `int`，
C# 不会隐式转换，必须 `new IntPtr(cursorId)`。**

### 2.3 锁定窗口大小时外观不一致

勾选「锁定启动器大小」后，边框看起来能拖、实际一律 `HTCLIENT`，这就是用户说的
「边框和实际区域对不上」。

**修法**：把 `ResizeMode` 与 `canResize` 同步——`LockWindowSize` 为真时
`ResizeMode = NoResize`，外观上直接去掉缩放边框。

### 2.4 拖动窗口时抽搐

**现象**：往下拖下边框时窗口反复跳动，逻辑尺寸震荡。

**根因**：`FormMain_SizeChanged` 末尾有一段上游的 `#1938` 修复：

```csharp
if (WindowState == WindowState.Maximized)
    WindowState = WindowState.Normal;
```

往下拖边框时 Windows 会短暂进入最大化状态，这行立刻改回普通 → 窗口缩小 → 继续拖又最大化，
逐帧震荡。副作用是**运行时根本无法最大化窗口**。

**修法**（`ac2a6e7`）：条件改成 `!isSizeSaveable && WindowState == WindowState.Maximized`。
`isSizeSaveable` 在 `FormMain_Loaded` 期间为 `false`，正好覆盖上游原本想修的那个初始化场景，
而用户主动拖动时不再触发。

### 2.5 字体列表里的方框

**现象**：字体选择下拉里有两项显示成方块。

**根因有两层**：
1. 列表项的 DataTemplate 是 `<TextBlock Text="{Binding Name}" FontFamily="{Binding Font}" />`，
   即**用字体自己渲染自己的名字**。图标字体（Wingdings 一类）没有字母字形，必然画成方框。
2. 判断「这个字体能不能渲染自己的名字」不能只看有没有字母 `A`。
   `MT Extra`、`MS Outlook`、`Kingsoft UE` 都有 `A`，但缺名字里的其他字符。

**修法**（`83f6e8e`）：`FontSelector.xaml.cs` 新增 `BuildPreviewFontFamily`，逐个 typeface 取
`TryGetGlyphTypeface`，用 `font.Name.All(ch => glyph.CharacterToGlyphMap.ContainsKey(ch))`
判定能否渲染**整个名字**，不能则回退到 `LocalizationFontService.BuildLaunchFontFamily()`。

**关键事实**：`CharacterToGlyphMap.ContainsKey` 对缺字返回 `false`，**不是**映射到 `.notdef`。
本机 209 个字体里只有 6 个走回退：`Segoe Fluent Icons`、`Segoe MDL2 Assets`、`MT Extra`、
`MS Outlook`、`Kingsoft UE`、`Marlett`。

**试过但无效的做法**：`FontFamily` 的逗号回退链（`"Segoe Fluent Icons, Microsoft YaHei UI"`）
——`GetTypefaces()` 不会合并回退字体，字符级回退在这条路径上不生效。

### 2.6 深色标题栏与默认深色主题

需求演进：先要「标题栏和边框染黑、HE 徽标反色、有点层次感」，随后改为
「**默认深色主题、背景黑白渐变（仿浅色主题），但保留切回浅色的能力**」，最后补充
「浅色主题保持原来的蓝白渐变，留一点原汁原味」。

**标题栏**（`7697db2`）：
- `Application.xaml` 新增四个固定色（不参与 `ThemeService` 计算）：`ColorBrushTitleBar` `#20242C`、
  `ColorBrushTitleBarEdge` `#3A4049`、`ColorBrushTitleBadge` `#F5FFFFFF`、`ColorBrushTitleBadgeText` `#20242C`
- `FormMain.xaml` 的 `PanTitle` 背景从 `ColorBrush3` 改为 `ColorBrushTitleBar`
- 原来「顶部高光 + 底部暗线」两块换成四层：顶部受光细线（1px `#26FFFFFF`）、纵向压暗渐变、
  底部亮线（1px `#3DFFFFFF`）、底部阴影（2px `#40000000`）
- `CELogo` 反色：白底黑字

**默认深色**：`PCL.Core/App/Config.cs:266` 的 `ColorMode` 默认值 `System` → `Dark`。
**注意这一项存在用户配置文件里**，已有用户需要手动改 `%APPDATA%\PCLHE\config.v1.json` 的
`"UiDarkMode"` 才能看到新的默认外观。

**黑白渐变**：改 `ToneProfileConfig.cs` 的 `DefaultDark`，关键在于把 `C5` / `C7` 归零
（让 `L5`/`L7` 决定的背景渐变变中性灰），同时保持 `C2`~`C4` 的色相，按钮和高亮才不会一起变灰。

最终参数：
```csharp
L1: 0.96, L2: 0.75, L3: 0.6, L4: 0.65,
L5: 0.55, L6: 0.3, L7: 0.3, L8: 0.22,
LBackground: 0.3, LForeground: 1, LWhite: 0.275,
C5: 0, C7: 0
```

调参过程：初版 `L5:0.45, L6:0.25, L7:0.225, L8:0.2` 渐变太弱 → 提 `L5` 到 `0.55` 拉开反差 →
再提 `L6`/`L7` 到 `0.3`，把「黑黑渐变」变成「灰黑渐变」。

**PrintWindow 条纹伪影**：给 `PanBack` 加 `Background="Transparent"` 后截图出现密集横向条纹。
原因是 WPF 走了透明渲染路径。移除该属性（Border 默认就是 null）后恢复正常。
**教训：不要为了「显式」给 Border 补 `Background="Transparent"`。**

### 2.7 其他样式调整

- 窗口圆角改成与 PCL2 一致的 `6px`（`9027bf5`），同时放大默认窗口
- 卡片标题换成微软雅黑 + 更大字号（`a0c049f`）
- 主操作按钮加方向投影增加层次（`fcd4ef4`）：`MyExtraTextButton.xaml` 的 `PanColor`
  Border 加 `DropShadowEffect ShadowDepth>0` + 白色 2px 描边 + 字体加粗
- 按钮文字颜色试过改黑，用户反馈「还是白色好了」，已回退

### 2.8 各种「你知道吗」提示

`PCL-Harness Edition/Resources/hints/zh-CN.txt` 与 `en-US.txt` 各 1050 条。
`9437031` 清理了其中 24 条带 `PCL CE` 的：17 条功能描述直接改名，
7 条描述上游组织关系的逐条重写（例如「PCL CE 是由 PCL-Community 开发的社区衍生版」→
「PCL-HE 是由第三方独立维护的 PCL 衍生版本」；「支持 Mirror 酱加速更新」→
「源码与问题反馈都在 GitHub 仓库」，因为 HE 的 `UpdateManager` 源列表是空的）。

### 2.9 壁纸自适应方式

设置 → 个性化 → 背景图片的「自适应方式」原本只有 9 项（智能 / 居中 / 适应 / 拉伸 / 平铺 /
四个角），与 PCL2 新版的双向九宫格不是一回事。本次按 PCL2 的设定补齐：

- 索引 `0..8` 的含义保持原样（老配置 `UiBackgroundSuit` 的取值不变义）。
- 新增 `9..17` 共 9 项「保持长宽比」：`9` 居中，`10..17` 依次为
  左上 / 左中 / 左下 / 中上 / 中下 / 右上 / 右中 / 右下。
  实现是 `Stretch.Uniform`（完整显示、不裁切）加 `ImageBrush.AlignmentX/AlignmentY` 定位，
  窗口尺寸变化时由 WPF 按比例重算，未占满的一侧留背景色。
- 与「适应」（`Stretch.UniformToFill`）的区别：适应会铺满并裁掉超出部分，保持长宽比不裁。
- 进入函数时除了原有的 `TileMode/Viewport` 复位，还补了 `AlignmentX/AlignmentY = Center`，
  否则从「左上」切到「适应」时位置会残留。

顺带修掉一个从 VB.NET 移植到 C# 时就带进来的 bug，上游 `PCL-CE` 至今仍在。
VB 版加载壁纸后跟的是 `Setup.Load("UiBackgroundSuit", True)`（重新加载并应用该设置），
翻成 C# 时变成了：

```csharp
FrmMain.ImgBack.Background = new MyBitmap(Address);
_ = Config.Preference.Background.WallpaperSuitMode; // 纯粹的空读，没有副作用
```

`MyBitmap` 的隐式 `ImageBrush` 转换新建的 brush，`Stretch` 是 WPF 默认的 `Fill`；
启动时 `ModSetup.ApplyAll()` 那次 `UiBackgroundSuit` 又因为 `ImgBack.Background` 还是
`null` 而提前 return。结果就是**用户选什么模式都没用，壁纸永远是拉伸填满**——
这正是「壁纸不随窗口变化保持长宽比」的根因。修复是把那行空读换成
`ModSetup.UiBackgroundSuit(Config.Preference.Background.WallpaperSuitMode);` 。

本地配置系统 `ConfigService` 没有 `Setup.Load` 那种「重新应用」的等价 API，
所以直接调用 `ModSetup.UiBackgroundSuit` 是正确修法。

验证：临时在函数里把运行时属性写进文件（`Stretch` / `AlignmentX/Y` /
`ImgBack.ActualWidth` / `VisualTreeHelper.GetDpi`），实测 `suit=9` 得到
`Uniform + Center/Center`、`suit=10` 得到 `Uniform + Left/Top`，
内容区随窗口变化（`1062x635` 逻辑像素，系统缩放 1.25）。验证完即删掉诊断代码。

---

## 第 3 章　下载链路

### 3.1 版本列表卡在官方源上

**现象**：点开下载页，等十几秒后弹 `All connection attempts failed for maven.neoforged.net`，
期间窗口卡到无法操作。

**日志时间线**：
```
15:32:53.188  DlNeoForgeList Bmclapi 状态改变：Loading → Finished   ← 镜像源 0.2 秒成功
15:32:55.249  Connected to files.minecraftforge.net via 172.67.161.211
15:33:09.073  [WARN] All connection attempts failed for maven.neoforged.net
15:33:09.118  [INFO] [UI] 弹出提示：All connection attempts failed for maven.neoforged.net
```

**根因**：`Config.cs:84` 的 `VersionListSource` 默认值 `1`，对应
`ModDownload.cs:1312-1315` 的 `{ 官方源 5, 镜像源 35 }` —— 官方源先启动，镜像源要 3.5 秒后才作备选。
本机到 `maven.neoforged.net` 的 IPv6 与 IPv4 都超时。

**修法**（`ced2cc3`）：默认值改 `0`，即镜像优先（`{ 镜像源 30, 官方源 90 }`）。

### 3.2 为什么 `[WARN]` 会弹对话框

排查这条时发现一个反直觉的机制：`PCL.Core/Logging/LogLevel.cs` 在 `TRACE` 宏下把
`Warning` 映射成 `300 + ActionLevel.HintErr`，而 `ActionLevel.Hint` 在 `ModBase.cs:3355-3402`
里会先 `LogWrapper.Warn` 再 `HintService.Hint(...)`。所以 **Debug 构建下 Warn 级别就会弹 Toast，
Release 不会**（`49620b1` 只加了注释说明，没改行为）。

**教训**：判断「这条日志会不会弹窗」必须先确认构建配置，不能只看日志级别名。

### 3.3 库文件的镜像

`ModDownloadLib.cs:2036` 与 `:1647` 附近已有逻辑把 `maven.neoforged.net/releases` 替换成
`bmclapi2.bangbang93.com/maven`。`d3d0040` 把 `maven.neoforged.net` / `maven.minecraftforge.net`
也纳入 `DlSourceLibraryGet` 的镜像映射。

**BMCLAPI 同时镜像了 `maven.fabricmc.net`** —— 早期判断「Fabric 没有镜像」是错的。

### 3.4 三轮审计发现的下载侧缺陷

细节见第 8 章，这里只列与下载直接相关的：

- **镜像地址与原始地址相同时抛空消息异常**（`ModDownload.cs:1985-2016` 与 `:2033`）：
  `urls` 只在 `if ((mcimUrl ?? "") != (url ?? ""))` 分支内填充，`DlSourceModGet` 回落到同一 URL 时
  `urls` 为空，`foreach` 零次执行后 `throw new Exception("")`，一个请求都没发。
  修法：`urls.Count == 0` 时回退为原始 URL。
- **UVMC 缓存写完即锁死**（`ModDownload.cs:348-369` / `:434-449`）：
  直接 `File.WriteAllText` 覆盖目标文件，刷新条件只有 `if (!File.Exists(...))`，写坏后永久不再刷新。
  修法：新增 `WriteCacheAtomically(path, content)`，先写 `.tmp` 再 `File.Move(tempPath, path, true)`。
- **等待原版下载的轮询没有失败出口**（`ModDownloadLib.cs:965-969`）：循环条件只排除 `Finished`，
  `Failed` / `Aborted` 的 loader 永远留在列表里。
- **安装器进程等待无超时**（`ModDownloadLib.cs:857-868`）：`while (!process.HasExited) Thread.Sleep(10);`
  没有超时，取消只挂在输出回调里，安装器不说话时回调永不触发。已补 30 分钟上限。
- **HTTP 缓存把 304 的空 body 写进缓存**（`HttpCacheHandler.cs:66-79`）：
  304 分支未命中正文缓存时没有 `return`，直接落到 `:79` 写入空 body。
  同一文件 `:117-123` 的 `_DeserializeResponse` 固定返回 `HttpStatusCode.OK`，meta 里的
  `StatusCode` 从不读回——24 小时缓存内的 404/500 会以 200 返回，`EnsureSuccessStatusCode()` 检查形同虚设。
- **连接取消时 socket 不释放**（`HostConnectionHandler.cs:196-210`）：
  `catch (Exception ex) when (ex is not OperationCanceledException)` 在取消分支既不 dispose 也不包装抛出，
  而 `:80` 的 `cancellationSource.Cancel()` 在每次连接成功时都会取消其余并发尝试，取消是常态。
- **连接缓存排序比较器二次查字典**（`HostConnectionHandler.cs:118-132`）：
  `cachedAddresses.Sort((a, b) => addressCache![b].CompareTo(addressCache[a]))` 与 `:169-175`
  的 `TryRemove` 构成竞态，抛 `KeyNotFoundException` 被包装成
  `InvalidOperationException (Failed to compare two elements)`，表现为随机「连接失败」。

### 3.5 下载链路的端到端验证

**只用代码推断不算验证。** 实际跑过一次完整下载：日志 14986 行，
`顺序下载成功` 4734 条、`开始下载` 4808 条，全程无模态错误框。

---

## 第 4 章　登录与账号

### 4.1 微软登录：设备代码流本来就已经实现

排查「无法正版登录」时先读了实现，结论是**设备代码流早就写好了**：
- `MicrosoftProvider.cs` 的 `DeviceEndpoint` 是 `login.microsoftonline.com/consumers/oauth2/v2.0/devicecode`
- `GetDeviceCodeAsync` 提交 `client_id` + `scope=XboxLive.signin offline_access`
- `PollDeviceCodeAsync` 用 `grant_type=urn:ietf:params:oauth:grant-type:device_code`

**真正的断点**是 `PCL.Core/App/Secrets.cs` 的 `MSOAuthClientId` 只从环境变量读，
而 `EnvironmentInterop.GetSecret(..., readEnvDebugOnly: true)` 在 `#if !DEBUG` 下**根本不读环境变量**，
所以 Release 构建永远拿不到 client id，构造函数直接抛 `InvalidOperationException`。

**修法**（`488392a`）：把 client id 内置为默认值，环境变量仍可覆盖。

```csharp
public const string MSOAuthApplicationName = "Plain Craft Launcher (PCL) - Harness Edition";
private const string BuiltInMSOAuthClientId = "fe6a35b0-86b0-4e23-b234-e80a01b0b5fc";
```

### 4.2 `AADSTS70002` 与 Azure 侧的开关

设备码请求最初返回 `AADSTS70002: must be marked as 'mobile'`。
这份 Azure 应用清单里**没有 `allowPublicClient` 字段**，`publicClient.redirectUris` 已经配了
`https://login.microsoftonline.com/common/oauth2/nativeclient`，也不存在任何字符串字段被误填成布尔值。

真正要开的是 Azure Portal 的「**身份验证 → 高级设置 → 允许公共客户端流**」。
打开后 devicecode 请求直接返回 200。

### 4.3 403 `Invalid app registration`

拿到 token 后打通了前三步（设备码、Xbox Live 用户认证、XSTS），最后一步
`POST https://api.minecraftservices.com/authentication/login_with_xbox` 返回 403。
服务端原文：

```json
{
  "path": "/authentication/login_with_xbox",
  "errorMessage": "Invalid app registration, see https://aka.ms/AppRegInfo for more information"
}
```

**这不是「白名单」问题**，是 Minecraft 侧认为这个 Azure 应用没有完成登记。
`aka.ms/AppRegInfo` 指向 `help.minecraft.net` 的一篇 Zendesk 文章（正文由 SPA 渲染，抓不到）。

**协议实现本身逐字段核对过与 HMCL 一致**：`RpsTicket` 的 `d=` 前缀、XSTS 的
`RelyingParty = "rp://api.minecraftservices.com/"`、`uhs` 取自 `DisplayClaims.Xui[0].UserHash`、
`SandboxId = "RETAIL"`、`AuthMethod = "RPS"`、`SiteName = "user.auth.xboxlive.com"`。
所以差异只能在应用注册属性上。

**对照实验**：用假 token 直接打该端点返回 **401**（端点可达，token 无效给 401），
而真实 token 得到 403——说明请求被拒在应用层。

三份 README 现在都写了这一节的完整说明与自建应用的方法（环境变量 `PCL_MS_CLIENT_ID` 覆盖）。

### 4.4 第三方登录：LittleSkin 的 OAuth 应用

`YggdrasilConnectProvider.cs:26-30` 的 `_BuiltInClientIds` 里 `["littleskin.cn"] = "1514"`
是**上游 PCL-CE 注册的应用**，所以授权页显示「登录至 PCL CE」。

已在 LittleSkin 建了独立应用：客户端 ID **1637**、应用名
`Plain Craft Launcher (PCL) - Harness Edition`、回调 `https://open.littleskin.cn/oauth/callback`。
但**白名单还没申请**（要发邮件到 `support@littleskin.in`），所以 `_BuiltInClientIds`
暂时仍指向 `1514`——换掉会直接登不上。

**注意**：LittleSkin 文档明确 `Yggdrasil.PlayerProfiles.Read` 与 `.Select` **不可同时申请**，
同时用会在设备代码流里报 `invalid_scope`。当前 `_Scopes` 是
`openid` / `offline_access` / `Yggdrasil.PlayerProfiles.Select` / `Yggdrasil.Server.Join`，**没有 `Read`**，不冲突。

`6413ee7` 加了环境变量覆盖机制：`PCL_OAUTH_CLIENT_ID_<主机名大写、点与连字符换下划线>`，
例如 `PCL_OAUTH_CLIENT_ID_LITTLESKIN_CN`。三个调用点语义一致：
`PageLoginAuth.xaml.cs:337`（`IsOAuthSupportedAsync` 短路判定）、`:370`（`_GetClientId` 取值）、
`YggdrasilConnectProvider.cs:54`（构造函数）。

### 4.5 `expired_token` 的友好提示

第三方登录长期未授权后刷新会报 `IdentityModelAuthenticationException: 认证失败：expired_token`，
原本整段堆栈糊进弹窗。`2cbf5c8` 新增 `catch (IdentityModelAuthenticationException ex)` 分支，
按 `ex.Error` 映射到 `Launch.Account.Auth.Expired` / `.Denied` / `.InvalidScope` / `.Rejected`
四个语言 key，走 `HintService.Hint(...)` 而不是模态框。

### 4.6 解除「必须先正版验证」

`ProfileUi.cs` 的 `CanCreateOtherProfile()` 原本在 Release 下要求「已有微软档案 或 区域特性解锁
或 无网络」，首次使用且零档案时三项全不满足，于是只能选正版。

用户要求离线与第三方能被单独选择（因为正版登录还在等 Minecraft 侧登记），
已改为直接 `return true;`，并隐藏提示条 `HintMicrosoft`。

---

## 第 5 章　联机（陶瓦 / EasyTier）

### 5.1 大厅与启动链路的挂起

`4e489a9` 修了一批会导致「永远转圈」的问题：

- **EasyTier 进程退出无人知晓**：`ETController` 的 `ETProcess.Start()` 后没有注册 `Exited` 事件，
  进程崩了界面还显示「运行中」。已补事件：`LogWrapper.Warn("Link", "EasyTier 进程已退出，联机可能已中断")`
  + `Status = ETState.Stopped` + `ETProcess = null`；并在 `EndProgramForce` 开头调
  `PCL.Core.Link.EasyTier.ETController.Exit()`。
- **等 `PlayerList` 无超时**（`LobbyController.cs:77`）：已补 15 秒上限 + `ct.ThrowIfCancellationRequested()`。
- **`TcpForwardWorker.cs:141` 的 `ConnectAsync` 无超时**：已补 `CancelAfter(10s)` 的 linked token。
- **`McForward` / `McBroadcast` 重建前没停旧的**：已在赋值前置 null 并 Stop，`CloseAsync` 同步置空。
- **`PageToolsGameLink` 的 `BtnJoin_Click` 可以重入**：已加 `Interlocked.CompareExchange`
  的单飞保护。
- **`TerracottaProcess` 的 `ResetClient()` 不持锁、`ShutdownAsync` 取 client 与置 null 不原子**：
  已用 `Gate.Wait()/Release()` 包裹，`ShutdownAsync` 改 `await Gate.WaitAsync(ct)`。

### 5.2 日志里的令牌泄漏

`ModLaunch.cs` 的 `McLaunchLog("ClientToken：" + ...)` 会明文打印 OAuth ClientToken
（打码函数不覆盖这个字段）。已整行改成注释。

### 5.3 创建大厅不依赖已发现的局域网世界

`c4025c9` 允许在没有发现 LAN 世界时也能创建大厅（端口手动指定）。

### 5.4 退出时关闭陶瓦

陶瓦是独立进程：`--hmcl` 拉起后父进程写端口即退出，真正驻留的是 detached 的子进程
（没有 `Process` 句柄）。`TerracottaProcess.ShutdownAsync()` 早就实现了
（`GET /panic?peaceful=true`），但**全仓库没有任何调用点**——它只在进联机页
`EnsureRunningAsync()` 时被拉起并记住 client。表现就是关掉启动器后陶瓦还在，
占着 EasyTier 的虚拟网卡与端口，下次启动还被当作已有实例复用。

修复是在 `FormMain.xaml.cs` 的 `EndProgramForce()` 里、`ETController.Exit()` 之后补上：

```csharp
using var terracottaCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
if (!TerracottaProcess.ShutdownAsync(terracottaCts.Token).Wait(TimeSpan.FromSeconds(4)))
    ModBase.Log("[Link] 关闭陶瓦超时，已放弃等待", ModBase.LogLevel.Debug);
```

只关闭「本次会话确认使用过」的实例（`_client` 非空），不主动去杀别人的：
陶瓦用 `%TEMP%\terracotta\terracotta.lock` 做单实例互斥，如果用户同时用 HMCL 联机，
两个启动器共享的是同一个陶瓦进程，无条件 panic 会顺手断掉 HMCL 的联机。

验证：手工用 `--hmcl` 拉起 `PCLHE/Terracotta/0.4.2/terracotta-*-windows-*.exe`，
等端口文件写出 `{"port":N}` 后 `GET /meta` 正常应答；再 `GET /panic?peaceful=true`，
4 秒内进程消失。注意 `/panic` 会先断开连接，PowerShell 侧会报「无法连接到远程服务器」，
但进程确已退出——所以 `ShutdownAsync` 里把异常吞掉当「已退出」是对的。

---

## 第 6 章　内存优化

### 6.1 功能定位

按用户要求，这一块被做成**独立于启动主流程**的工具，放在「工具 → 百宝箱」里，可手动触发。
它不是「减小内存占用」的魔法，而是把各进程暂时用不到的物理页换出到页面文件：

- 不减少任何进程的内存需求
- 被换出的页在下次访问时会重新读入，**收益是暂时的**
- 不影响数据正确性
- 系统进程与其他用户的进程无法处理，属预期

### 6.2 实现

`PCL.Core/Utils/OS/KernelInterop.cs` 新增：
- `public static (int Succeeded, int Failed) EmptyWorkingSets(IReadOnlySet<uint>? excludedProcessIds = null)`
- P/Invoke：`_EmptyWorkingSet`（psapi）、`_OpenProcess`、`_CloseHandle`（kernel32）
- 需要用到的访问权限：`PROCESS_QUERY_LIMITED_INFORMATION (0x1000) | PROCESS_SET_QUOTA (0x0100)`

UI 在 `Pages/PageTools/PageToolsTest.xaml` 与 `.xaml.cs`，包含「刷新」与「释放物理内存」两个按钮，
以及 `Tools.Memory.*` 系列语言 key（8 个，写进 `zh-CN` / `zh-TW` / `en-US` / `en-GB`）。

### 6.3 三个坑（都很容易重犯）

**坑一：`LibraryImport` 不写 `EntryPoint` 会用方法名当导出名。**

```csharp
[LibraryImport("kernel32.dll")]                       // ← 错误
private static partial nint _OpenProcess(uint, bool, uint);

[LibraryImport("kernel32.dll", EntryPoint = "OpenProcess", SetLastError = true)]  // ← 正确
```

源生成器在缺省时拿方法名本身去 DLL 里找，于是去找 `_OpenProcess`——kernel32 里没有这个名字，
抛 `EntryPointNotFoundException`。这个异常又被逐进程的 `catch { failed += 1; }` 吞掉，
于是**每个进程都记一笔「跳过」，功能一个进程都没处理过**，界面显示「成功 0 个」。
同一处 `_CloseHandle` 也漏了，导致每打开一个进程就泄漏一个句柄。

修复时同时把 `catch` 收窄：`Win32Exception`（权限不足）仍计入跳过，其余异常抛出来。
否则下一次写错入口名还是只能看到「全部跳过」。

**坑二：`ulong` 相减会下溢。**

```csharp
// 释放后可用内存变少时，这里会下溢成接近 2^64 的巨值（界面上出现过 17592186044416 MB）
double freedMb = (after.AvailablePhysical - before.AvailablePhysical) / 1024d / 1024d;
// 正确写法
double freedMb = ((double)after.AvailablePhysical - before.AvailablePhysical) / 1024d / 1024d;
```

**坑三：`MyButton.Click` 的委托签名是自定义的。**

`Controls/MyButton.xaml.cs:12` 声明 `public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);`
——不是 `RoutedEventHandler`。写成 `EventArgs` 会在运行时抛
`XamlParseException: 无法从文本 "BtnMemoryRefresh_Click" 创建 "Click". Cannot bind to the target method...`，
而且只在页面首次打开时爆，编译期完全看不出来。

**另外**：XAML 里写 `\n` 不会被解释成换行，多行文案要用 `&#10;`。

### 6.4 验证数据

把 `PCL.Core.dll` 单独加载出来调用同一个方法，部署前后对照：

| | 成功 | 失败 | 可用物理内存变化 |
| --- | --- | --- | --- |
| 修复前 | 0 | 311 | 1.6 MB |
| 修复后 | 142 | 161 | **+772 MB** |

那 161 个失败是系统进程（`csrss` / `lsass` / `services` 之类），普通用户权限打不开。
**管理员身份运行能处理更多**（需要 `SeDebugPrivilege`）。

### 6.5 后续打算

用户评价「有点效果，但相比龙猫纯血优化还不够」，要求**以后再做深度优化**，
并且希望内存优化作为仓库里的一个**独立项目**随启动器一同发布、独立维护，
在 GitHub 上更新「已实测可用 / 不保证完全无问题」之类的说明。这部分尚未开工。

---

## 第 7 章　构建、部署与发布

### 7.1 项目目录改名

主项目目录从 `Plain Craft Launcher 2` 改成 `PCL-Harness Edition`（`1be3b5a`，378 个 rename）。
**csproj 文件名仍是 `Plain Craft Launcher 2.csproj`**，只有目录名变了。

改名必须同步的地方：
- `Plain Craft Launcher 2.slnx:16` 的项目路径
- `.github/workflows/reusable-build.yml:44` 的 `dotnet publish "PCL-Harness Edition/Plain Craft Launcher 2.csproj"`
- 三份 README 里的构建命令与 LICENCE 链接
- `PCL-Harness Edition/metadata.json` 的 licence URL

漏掉 `slnx` 会报 `MSB3202: 未找到项目文件`。

### 7.2 数据目录隔离

`PCL.Core/App/Paths.cs` 的数据目录从上游的 `PCL` 改名为 `PCLHE`（Debug 版 `PCLHE_Debug`），
并保留 `oldName` 用于从 `PCLCE` 迁移。`ConfigService.cs:160-171` 的迁移逻辑同时识别
`config.v1.json`（上游的实际文件名）。

`EncryptHelper` 增加了 `_TryInheritKeyFromUpstream`，让新版本能复用旧版本的 DPAPI 密钥。
**注意 DPAPI salt 字符串 `"PCL CE Encryption Key"u8` 不能改**，改了会作废所有已加密数据。

### 7.3 本机构建与部署

```bash
# 构建（必须用完整 build，不能用 -t:Compile）
/c/Users/kingd/.dotnet/dotnet.exe build "Plain Craft Launcher 2.slnx" -c Release --nologo -v q

# 部署（同步到运行目录 + 重签 + 重建桌面快捷方式）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File deploy.ps1
```

**`-t:Compile` 不可用作编译验证**：它跳过 XAML 代码生成（`MarkupCompilePass`），
`obj` 里的 `.g.cs` 不全，会报出一堆假错误（`CS0115 没有找到适合的方法来重写`、
`CS0311 MyCompItem → FrameworkElement`）。

**`dotnet` 必须用绝对路径**：PATH 里那个没有 SDK，会报 `No .NET SDKs were found`。

**构建前要关掉正在运行的 `PCL-HE.exe`**，否则会锁住 `bin` 里的 `PCL.Core.dll`，
报 `MSB3027` / `MSB3021`。

### 7.4 运行目录必须放在 workspace 之外

从 DSH workspace 内部启动的进程会被装进 AppContainer，只对 workspace 有写权限，
于是写 `%APPDATA%\PCLHE\instance.lock` 恒为 `ACCESS_DENIED`，启动即崩。

**结论**：运行目录固定为 `C:\Users\kingd\PCL-HE`，桌面快捷方式指向那里的 `PCL-HE.exe`。
从 `win_bash` 启动 GUI 时还要先把 `$env:TEMP` / `$env:TMP` 改回
`C:\Users\kingd\AppData\Local\Temp`，否则 `Paths` 静态构造会在沙箱临时目录上抛
`UnauthorizedAccessException`。

### 7.5 代码签名

`setup-signing.ps1` 生成自签名证书（`CN=PCL-HE Project`，10 年），`deploy.ps1` 自动签名。
`Get-AuthenticodeSignature` 显示 `Status: Valid`。

**这只解决「本机反复弹 SmartScreen」，不解决分发**：自签名证书不进微软信任库，
别人下载后仍会看到「发布者未知」。README 里写了这个提示的成因与自建产物解除锁定的方法。

### 7.6 发布流程的四个坑

目标：`release-publish.yml` 由 `v*` tag 或手动触发，构建 x64 + ARM64，签名（sigstore），
发 GitHub Release（Beta 配置自动标 prerelease）。

**坑一：workflow 文件解析失败时，run 的 `jobs` 是空的。**
`GET /actions/runs/{id}/jobs` 返回 `total_count: 0`，`/actions/workflows` 里的 name 退化成
文件路径（`.github/workflows/release-publish.yml` 而不是 `Release publish`）——这是
「GitHub 从未成功解析这个文件」的信号。

**坑二：GitHub Actions 表达式里没有 `replace()`。**
只有 `contains` / `startsWith` / `endsWith` / `format` / `join` / `toJSON` / `fromJSON` / `hashFiles`。
用了 `replace(newVersion, 'v', '')` 会得到：

```
Invalid workflow file: .github/workflows/release-publish.yml#L1
(Line: 58, Col: 14): Unrecognized function: 'replace'.
```

错误信息只在 run 的页面里，API 的 `jobs` 和 `logs` 都看不到。
最终方案：**版本变量直接取完整的 tag 名**（`github.event.inputs.version || github.ref_name`），
不再做字符串处理。

**坑三：sigstore 签名会把 workflow 自身的文件名写进证书 SAN。**
`actions/attest-build-provenance` 生成的证书里带 workflow 引用，如果后续提交改了这个
workflow 文件的路径/名字，再用同一个 tag 引用旧的 commit 就会对不上。
`8f4b932` 把校验基准改成 `github.ref`，并自建 tag。

**坑四（最要命）：`PublishSingleFile` + `--no-self-contained` = 下载后跑不起来。**
csproj 里 `<PublishSingleFile>true</PublishSingleFile>`，而 workflow 传了 `--no-self-contained`，
产物是一个 24 MB 的单文件 exe，**但目标机必须装 .NET 10 桌面运行时**。
第一次发出去的 `v0.1.1` 就是这个东西，朋友下载双击会直接看到「缺少框架」的对话框。

修法：改成 `--self-contained true`。单文件自包含产物 188 MB，
加 `-p:EnableCompressionInSingleFile=true` 后降到 84 MB，zip 出来 78 MB。

**判定方法**：下载发布的 zip，`unzip -l` 看内容。发布包只应该有 `PCL-HE.exe` 一个文件，
且体积在 80 MB 量级——如果是 24 MB，说明是 framework-dependent。

### 7.7 发布记录

- `v0.1.1`（2026-10-21）：首个 Release，prerelease，含 `PCL_HE_Beta_x64.zip`（78 MB）
  与 `PCL_HE_Beta_ARM64.zip`（73 MB），各带 `.sigstore.json`

### 7.8 推送 GitHub 的不稳定

本机到 `github.com:443` 常年不稳，`api.github.com` 反而正常。可用配方：

```bash
git -c credential.helper= \
    -c http.curloptResolve=github.com:443:<IP> \
    -c http.version=HTTP/1.1 \
    -c http.lowSpeedLimit=0 -c http.lowSpeedTime=30 \
    push "https://<user>:<token>@github.com/<owner>/<repo>.git" main:main
```

轮流试 `20.200.245.247` / `20.27.177.113` / `20.233.83.145` / `20.26.156.215`，
每次失败的 IP 都不一样，属瞬时抖动而非封锁。

**判定成功不能用管道返回值**：`if git push ... | tail -3` 恒为真（`tail` 返回 0）。
必须 `grep -qE "main -> main|Everything up-to-date"`，或事后用 `ls-remote` 校验 SHA。

### 7.9 源码目录的换行符与 BOM 不统一

改文件前必须探测并原样写回，否则 diff 会变成整篇重写：

```python
raw = open(path, "rb").read()
bom = raw.startswith(b"\xef\xbb\xbf")
crlf = raw.count(b"\r\n") > 0
t = raw.decode("utf-8-sig" if bom else "utf-8").replace("\r\n", "\n")
# ... 处理 ...
if crlf: t = t.replace("\n", "\r\n")
data = t.encode("utf-8")
if bom: data = b"\xef\xbb\xbf" + data
open(path, "wb").write(data)
```

已知样例：`MicrosoftProvider.cs`、`PageLoginProfile.xaml.cs`、`PageToolsTest.xaml.cs` 是 **LF + 有 BOM**；
`ModDownload.cs` 是 **LF 无 BOM**；`Secrets.cs`、`KernelInterop.cs`、`reusable-build.yml` 是 **CRLF 无 BOM**。

**从工作树读字节上传到 GitHub API 会污染换行符**（工作树 CRLF、git 对象 LF）。
正确做法是 `git cat-file blob <commit>:<path>` 取字节。

---

## 第 8 章　审计修复汇总

### 8.1 审计方式

抽样读代码发现不了系统性问题，所以分三轮派出只读子代理做定向审查，每轮覆盖不同方向：

| 轮次 | 范围 | 产出 |
| --- | --- | --- |
| 第一轮 | 下载/网络 + UI | 8 条 + 6 条 |
| 第二轮 | 启动/实例链路 + 联机/登录链路 | 10 条 + 10 条 |
| 第三轮 | 本地化完整性 + 静态资源引用 | 各若干 |

另做了一次**穷举扫描**（不是抽样）：用脚本按大括号配对提取全部 `async void` 方法体，
判断是否含 `try`。结果 22 个 `async void` 里有 8 个没有异常出口。

**为什么 `async void` 缺 catch 值得修**：`Application.xaml.cs:159` 有全局
`DispatcherUnhandledException` 且 `e.Handled = true`，所以不会崩进程，但会弹全局错误框；
更要紧的是两处会造成**永久不一致状态**——`BtnCreate_Click` 抛异常后按钮永远禁用，
`BtnAddServer_Click` 已经 `serverList.Add` + `PanServers.Children.Add` 但文件没写成。

### 8.2 值得单独记的几条

**`Thread.Sleep` 在 UI 线程上自旋**（`PageSetupUpdate.xaml.cs:156-162`）：
`BtnUpdate_Timer()` 用 `ModBase.RunInUi(...)` + `Thread.Sleep(200)` 循环，而调用点在
`async void CheckUpdate()` 的 UI 续体里（`await IsLatestAsync()` 没加 `ConfigureAwait(false)`），
直接把消息泵堵死，主窗口「未响应」。已改成 `async Task` + `await Task.Delay`。

**无锁计数器导致永久卡死**（`PageDownloadCompFavorites.xaml.cs:551/571/574`）：
`finishedTasks += 1` 在真线程池上非原子，丢一次更新后
`while (finishedTasks != ts.input.Count)` 永不相等。已改 `Interlocked.Increment` +
`Volatile.Read(...) < count` + 循环内 abort 检查。

**静态事件订阅用 lambda、退订用方法组**（`MyHint.xaml.cs:138` vs `:177`）：
两个不同的委托，`-=` 永远删不掉，且 `Loaded` 每次都再追加一个。
已改成方法组，并且退订后再订阅保证幂等。

**`ConfigService.cs:202` 把 `File.Exists(dir)` 当成 `File.Exists(configPath)`**：
原写法检查的是目录，恒为 false，导致旧的 `setup.ini` 会反复覆盖用户改动。

**`PageInstanceOverall` 的重命名先删后建，中途失败会丢目录**：
已改成先检查目标不存在、失败时回滚到原名。

**`PageInstanceExport` 的 `endedThreadCount` 同样是无锁计数**：
已 `Interlocked.Increment` + `Volatile.Read` + 120 秒上限 `break`。

**`ModWatcher` 主循环退出时没把 `State` 从 `Loading` 改掉**，
导致 `ModLaunch` 的 `while (watcher.State == Loading)` 死等：
已在退出前补 `if (State == MinecraftState.Loading) State = MinecraftState.Canceled;`。

**`async void` 缺异常出口**（`53e28d4`）：
用脚本按大括号配对提取全部 22 个 `async void` 方法体并判断是否含 `try`，其中 8 个没有。
`Application.xaml.cs:159` 有全局 `DispatcherUnhandledException` 且 `e.Handled = true`，
所以不会崩进程，但会弹全局错误框；更要紧的是两处会造成**永久不一致状态**——
`BtnCreate_Click` 抛异常后按钮永远禁用，`BtnAddServer_Click` 已经 `serverList.Add` +
`PanServers.Children.Add` 但文件没写成。

### 8.3 明确「不是缺陷」的几处

审计里这些被误报过，记下来避免重复排查：

- `MyButton.xaml.cs:54/57` 是两个不同重载，不是重复定义
- `PageSetupGameLink.xaml.cs:150`、`PageSetupJava.xaml.cs:39`、`ModDownloadLib.cs:1169` 的
  `GetAwaiter().GetResult()` 都在 `ModLoader.cs:632` 的 `Task.Run` 线程池内，不构成死锁
- `Lang.Number<T>(T value, string? format)` 的第二参数是 .NET 数字格式串（`"N0"` / `"P0"`），
  不查语言 key
- 其他语言缺 key 时**回退到 zh-CN**（`LocalizationService.cs:31` `DefaultLanguageCode = "zh-CN"`），
  只有 zh-CN 也缺才露原始 key
- `PageDownloadCompFavorites.xaml.cs:186-187` 无参取 `Title` 模板是故意的，
  由 `:161` 的 `string.Format(newItem.Title, 0)` 消费

### 8.4 本地化修复

- zh-CN 缺 3 个 key：`Minecraft.Download.Error.InstallerTimeout`、`VanillaDownloadTimeout`、
  `VanillaDownloadFailed`（`ModDownloadLib.cs:876/1002/1009` 引用）
- `Download.Install.State.GetVersionListFailed` 的文案含 `{0}`，但有 5 处无参拼接：
  `PageDownloadInstall.xaml.cs:1134/1855/1962/2205`、`PageInstanceInstall.xaml.cs:1379`
- `Instance.Resource.Item.Schematic.FileType` 的文案不含占位符，但有 3 处传参
  （`PageInstanceCompResource.xaml.cs:2942/2964/2997`），参数被静默丢弃
- `ModLaunch.cs:2901/2906/2911` 的窗口标题替换硬编码了中文「离线」「正版」「Authlib-Injector」，
  已改成 `Minecraft.Launch.Login.Type.*` 三个 key
- `InstallerFailed` / `InstallerLogUnreadable` / `InstallerNoLogOutput` 三个 key 在代码里**零引用**，
  属于孤儿条目（真正接线的是 `InstallerFailedLastLine`）

### 8.5 静态资源

- `PageSetupUpdate.xaml:141` 引用的 `Images/Icons/Tahoe.png` 不存在。
  实测 `XamlReader.Parse` **不抛异常**（`Image.Source` 为 null），属静默缺图，从基线起就悬空。
- 孤儿资源：`Heads/Logo-CE.png`（品牌残留）、`Heads/Logo.png`、
  `Skins/{Ari,Efe,Kai,Makena,Noor,Sunny,Zuri}.png`

---

## 第 9 章　文档、合规与许可证

### 9.1 必须保留的内容（不要动）

- 「关于」页对上游的署名与头像（龙腾猫跃 / PCL Community）
- `metadata.json` 的 source / licence 字段
- `.github/ISSUE_TEMPLATE/config.yml` 里的上游入口
- `.github/FUNDING.yml`（龙腾猫跃 爱发电）
- 数据目录名 `PCL`（`PCL.Core/App/Paths.cs`）
- 语言文件里的 `PCL Community Edition` 致谢
- `EncryptHelper` 的 DPAPI salt（见 7.2）

### 9.2 已清理的内容

- 13+2 个文件里的 AI 模型署名注释（保留了两条合法的第三方署名：
  `en-US.xaml:3189` / `en-GB.xaml:3133` 的 "Author: pynickle, partially AI-generated"）
- `.disabled` 后缀的 issue template 也曾漏过品牌替换

### 9.3 三份 README

`README.md`（简中）、`README-EN.md`（英）、`README-ZH_TW.md`（繁中）内容需保持同步。
已有的专门章节：

- 「关于正版登录目前的状况」：设备码流程、内置应用信息、403 限制、自建应用的方法
- 「Windows 提示『已保护你的电脑』怎么办」：自签名不解决分发、如何解除锁定
- 「项目缘起与开发方式」：人类架构与合规审查 + AI 辅助编程的分工说明

**排版坑**：用 `(?<!\n\n)\n## ` 加空行的正则，在已经是「空行 + 标题」的情况下仍会匹配，
产生 `\n\n\n##`。修法是随后 `t.replace("\n\n\n## ", "\n\n## ")`。

### 9.4 LICENSE 与署名

`PCL-Harness Edition/LICENCE` 保留上游 PCL 的自定义许可证原文。产品名改动后，
`metadata.json` 里的 licence URL 同步指向新仓库路径（`(PCL-Harness%20Edition/LICENCE)`）。

---

## 附录 A　提交时间线

按时间正序，简略。完整信息用 `git log` 查看。基线导入共 102 个提交。

### 2026-10-17　基线导入与品牌改造

```
02d8248  chore: baseline import of PCL-CE (dev branch snapshot) as PCL-DSH starting point
0c1d9af  docs(legal): state third-party derivative status and drop dead legal links
7abbe8a  feat(about): credit the developer and drop links to severed services
8caf7ed  chore: rebrand CI, issue templates and assembly metadata for PCL-DSH
476a762  fix(window): remove the dark frame and mismatched corner radii
96dc63a  style(window): give the title bar a subtle vertical gradient
7282dfb  feat(launch): state third-party status in the launch notice
94929d2  chore(i18n): drop the localization keys for the removed org-name prompt
fe50043  chore: initial public release of PCL-DSH
43749cf  docs: fix the README logo path so it renders on GitHub
13cc873  fix(update): correct brand prefix and stop reporting spurious update errors
f8c6ef0  refactor(update): drop the unused MirrorChyan CDK row and fix the changelog link
a0c049f  style(card): use Microsoft YaHei at a larger size for card titles
9027bf5  style(window): match PCL2's 6px corner radius and enlarge the default window
15f4a32  feat(memory): add read-only memory diagnostics
2d4186f  feat(launch): check memory before starting the game
c4025c9  fix(link): allow creating a lobby without a discovered LAN world
4cf1f93  chore(i18n): rebrand the 100-luck easter egg for PCL-DSH
9382579  ci: add GitHub Actions build and release workflows
18a6739  refactor(core): move XAML namespace URIs off the upstream domain
83a0b37  fix(identity): stop sending requests as the upstream edition
52a1180  refactor(link): drop the upstream telemetry implementation and repoint help links
89cd9bc  fix(identity): rename the IPC pipes off the upstream prefix
0648d8b  feat(branding): rebrand to PCL-HE
39732ae  fix(branding): balance the HE wordmark and center it by ink bounds
d2aba7b  chore(branding): drop the orphaned whale assets
3cd1790  fix(compliance): catch the .disabled issue template in the rebrand
57f7f59  docs: correct the watermark description in all three READMEs
f80febd  docs: stop pointing contributors and release notes at the upstream repo
2ba3c7d  fix(update): drop the last references to the upstream executable
9c240b1  fix(paths): isolate shared data dirs from the upstream launcher
8e556d1  fix(secret): skip key inheritance when the upstream path is uninitialized
59fb2f5  feat(branding): rename the executable to PCL-HE and give it its own version
cef4fb5  fix(start): drop the upstream 521 hardcoded homepage-reset threshold
a66f6a0  chore(cleanup): drop AI model credits from the source tree
4581d44  fix(xaml): restore the runtime type resolution broken by the assembly rename
f6d46bc  fix(ui): align the resize hot zone with the visible border
fcd4ef4  style(ui): give the action button depth with a directional shadow
d3d0040  fix(download): mirror the NeoForge and Forge maven hosts in library downloads
83f6e8e  fix(ui): stop the font list rendering icon fonts as boxes
ff09877  fix(auth): tell users how to enable Microsoft login
6413ee7  feat(auth): let users override the built-in OAuth client id
ced2cc3  perf(download): try the mirror before the official source for version lists
49620b1  docs(logging): explain the TRACE-only level upgrade
bdcdd0f  fix(ui): keep the main window at its designed size on startup
39c35fc  fix(ui): keep the resize cursor visible on every window edge
a7a8ecd  docs: explain the SmartScreen prompt and how to get past it
```

### 2026-10-20　目录改名、崩溃修复、审计

```
1be3b5a  refactor: rename the main project directory to PCL-Harness Edition
7ca648b  chore: add a deploy script that syncs the build to a runnable directory
ab16306  build: sign the deployed executable with a local self-signed certificate
1099a57  refactor: drop the last brand traces from user-visible output
c593d3b  fix: start the launcher even when the single-instance lock cannot be written
5289129  fix: close the hangs and leaks the audit reports turned up
0896264  fix(download): stop losing the mirrored request and the version cache
4e489a9  fix: make the lobby and launch links fail instead of hanging
```

### 2026-10-21　功能、主题、发布

```
488392a  feat(auth): ship a built-in Microsoft OAuth client id for device code login
6077ddf  docs: explain where PCL-HE comes from and how it is built
b206491  docs(auth): record the two external gates Microsoft login still needs
f492841  fix: show real text where the UI printed keys and placeholders
01b938f  fix(launch): stop hardcoding Chinese login type names in the window title
255d2dc  fix(auth): explain the Minecraft 403 instead of leaking an HttpRequestException
1d9d99b  docs: state the Microsoft login limitation and how to supply your own app id
1f55248  docs: quote the server's own words about the login failure
278d3dd  (GitHub 网页提交) 更新 README.md
8b5c2fb  (GitHub 网页提交) 更新 README.md
038eccd  (GitHub 网页提交) 更新 README.md
9c9acf3  docs: bring the English and Traditional Chinese READMEs in line
53e28d4  fix(ui): give every async handler an exception path
ac2a6e7  fix(ui): stop the window from snapping back while being resized
333b0c2  feat(tools): add a memory optimization panel to the toolbox
7697db2  feat(ui): dark title bar, dark theme by default, new brand mark
9437031  docs(tips): rebrand the loading tips and drop the ones that no longer apply
2cbf5c8  fix(auth): report protocol errors as hints instead of stack traces
73c354e  fix(memory): give the new P/Invokes an explicit entry point
36e3fcf  chore(release): publish the whole output and bump to 0.1.1
11f0356  ci(release): let a v* tag trigger the publish workflow
80f1af2  ci(release): use github.event.inputs for the manual path
e2ebb58  ci(release): inline the release variables
47e53a4  ci(release): stop using replace(), which Actions does not have
8f4b932  ci(release): verify the signature against the ref that actually ran
b439f00  build(ci): publish a self-contained binary so the download just runs
```

### 2026-10-22　壁纸自适应与陶瓦退出

`d08c1a7` 里两件事一起做：

- **壁纸自适应方式**补进 PCL2 新版的双向九宫格（详见 2.9）：索引 `9..17` 保持长宽比，
  并修掉「启动加载壁纸后从未应用模式」的旧 bug（VB 到 C# 移植时把 `Setup.Load` 翻成了空读）。
  验证方式是用临时诊断输出打运行时属性（`suit=9` 得到 `Uniform + Center/Center`，
  `suit=10` 得到 `Uniform + Left/Top`），比截图取色可靠得多。
- **退出时关闭陶瓦**（详见 5.4）：补上 `ShutdownAsync()` 的调用点，避免 detached 的
  `terracotta --hmcl2` 子进程在关闭启动器后继续占着 EasyTier 网卡与端口。

翻译债的补齐（`c78264b`，见附录 B）也是同一天提交的。

### 2026-10-23　窗口描边、标题栏层次与下载按钮实心态

这一天的三笔提交都属界面打磨，起因是维护者反馈「窗口四角没被边框框住，像漏风」「下载按钮的配色和 PCL2 不一样」「开始下载那几个字太细」以及「窗口缺少层次感」。

- **壁纸铺满**（`48284dd`）：九宫格 9~17 档（保持长宽比系列）由 `Stretch.Uniform` 改为
  `Stretch.UniformToFill`，窗口比例与原图不一致时改为裁切而不是留黑边；四种语言的提示文案
  同步改写为「铺满窗口……超出部分被裁切」。
- **窗口描边与层次**（`fefc3b9`）：新增内缩 1px 的 `PanEdge` 描边层——原先贴外缘的描边弧会被
  DWM 的系统圆角削掉半像素，看起来就是四角断线；内缩之后角弧圆心与系统裁剪圆心同心。同时把
  `FormMain.xaml.cs` 的裁剪矩形由 `(0,0,w,h)` 改成 `(1,1,w-2,h-2)`，两者才真正对齐。
  标题栏下沿另加一道 `#26000000 → #00000000` 的渐隐投影，用来分开标题栏与内容区。
- **下载按钮实心态**（`13640a6`）：`MyExtraTextButton` 新增 `SolidTheme` 实心态——浅色模式
  `#0b5bcb` 蓝底白字、深色模式黑底白字（初版取 `#121216`，2026-10-24 改为标题栏色 `#20242C`），悬停各自变亮，并经
  `ThemeService.ColorModeChanged` 跟随启动器深浅模式切换；按钮文字改为幼圆加粗。

描边一项的验证方式值得单独记下来：先按 DPI 感知分别取「窗口自绘内容」与「屏幕合成图」，逐像素
比对四角，证明原描边其实已经闭合（两者差异仅 81 像素，总计约 129 万），据此判定问题出在观感
（`#3A4049` 在深色标题栏上对比度低、1.25 倍缩放下 1px 弧线不显眼）而不是漏画，这才转向
「内缩重画 + 加层次」这条路。
### 2026-10-24　浮钮深色配色、去 PCL-CE 化与首轮缺陷清扫

这一天分三段：界面收尾、上游切割核验、以及按审查报告做的缺陷修复。

- **浮钮深色配色**（`d12ef93`）：`MyExtraTextButton` 的实心态此前只有浅色一套（`#0b5bcb` 蓝底白字），
  其余四处浮钮甚至没标 `SolidTheme`，走的是不参与深浅切换的主题色组 `ColorBrush3`（`#1370f3`），
  所以在深色窗口下仍是蓝底——这才是「深色模式没生效」的根因。现在深色底直接取窗口标题栏色：
  静止 `#20242C`（等于 `ColorBrushTitleBar`）、悬停 `#3A4049`（等于 `ColorBrushTitleBarEdge`），
  浅色组保持不变。同时去掉模板里那圈白色渐变描边，做法是只清 `PanColor.BorderBrush`、
  不动 `BorderThickness`——Border 的厚度参与布局，直接设 0 会让图标与文字整体位移 2px。
- **DNS 换成阿里公共 DoH**（`3ae8a3f`）：`DnsQuery.cs` 的竞速列表里原先有一个 PCL-CE 的端点
  `doh.pysio.online`，换成 `dns.alidns.com`。同一次核验还确认了另外三处早已是不可达的死代码：
  EasyTier 下载地址、Natayark 数据拉取、Natayark OAuth 入口（分别被 `#pragma warning disable CS0162`
  与方法开头的 `return;` 挡住）；CI 也不再注入 CurseForge Key 与 Sentry DSN，所以遥测在 Release
  构建里直接走「未找到 Sentry DSN」返回，完全不上报。关于页「特别鸣谢」里的 Pysio 条目属署名，保留。
- **首轮缺陷清扫**（`92fd5b1` `3b1c2e1` `ff44ebc` `c5945d3` `0af653f` `ad69a87` `11a4a6a` `9e5c20b`）：
  按审查报告修了 19 处。最值的一组都在崩溃链上，而且它们的后果是同一个——**崩溃弹窗根本弹不出来**：
  `ModWatcher.cs:729` 的 `CurrentLogFiles.Last()` 位于 `analyzer.Output(...)` 的实参求值阶段，
  集合为空时异常发生在调用之前，弹窗永远不显示（改用 `LastOrDefault() ?? ""`）；
  `CrashLogPreparer` 的 `All` 用链式 `??` 拼接，`game` 非空时整段短路掉 `debug`，
  只出现在 debug 日志里的线索被系统性忽略。此外还有若干处：实例重命名现在会迁移 23 项
  以实例路径为键的状态（此前改名即丢失描述、收藏、卡片类型）；`PromoteTempFile` 的覆盖语义
  （原先「目标已存在」的守卫与 `File.Move(..., true)` 的覆盖意图互相打架，导致新下载的文件
  永远替换不掉旧文件）；崩溃报告导出改为先写临时文件再原子替换；崩溃日志导入补上 tar/tgz；
  `--gpu` 缺参数时不再无声退出；游戏日志页的 `frmLogRight` 判空写错等。

审查里另有 4 条刻意未改，都属于「改对不难、改错代价大」的类型：`LoaderDownload` 的单文件失败
策略、`ModLoader` 回调在 UI 线程做重活、`McInstance.PathIndie` getter 的副作用、`ModDownloadLib`
里硬编码的「安装」长度 3。它们与此前挂着的 `ModLaunch.cs:1056` 锁内等待同型，等能跑通场景再动。

---

## 附录 B　已知限制与待办

### 外部依赖，代码侧无法推进

- **微软正版登录**：Minecraft 服务端返回 `Invalid app registration`。
  需要在 Minecraft 侧完成应用登记，或用已获授权的客户端 ID。
- **LittleSkin 第三方登录**：独立 OAuth 应用（客户端 ID `1637`）已在 LittleSkin 建好，
  但**设备代码流白名单尚未申请**（需发邮件到 `support@littleskin.in`）。
  在此之前 `_BuiltInClientIds` 仍指向上游的 `1514`，换掉会直接登不上。

### 已明确延后的工作

- **内存优化的深度改进**：用户要求以后再做，并希望它作为仓库内的**独立项目**维护，
  在 GitHub 上补充「已实测可用 / 不保证完全无问题」之类的说明。
- **字体选择界面的配色与按钮字体微调**：用户表示「感觉也可以了」。
- **首轮巡检里唯一未修的一条**：`lock (ModJava.javaLock)` 内等待 Java 下载并弹模态框
  （`ModLaunch.cs:1056`，锁内 1102-1130）。要改得同时动 `ModDownloadLib.cs:716` 与 `:1740`
  两个调用方的取值时机，属结构性调整，未跑通场景前风险大于收益。

### 翻译债（已清完）

补全前的实际缺口比早先的记录更大：`zh-TW` / `en-GB` 各缺 **76** 个 key、`en-US` 缺 **20** 个
（早先统计漏了 `Minecraft.Download.Error.*` 6 个与 `Tools.GameLink.*` 14 个这两组）。
缺口集中在 `Instance.Resource.Mod.JarInJar.*` 35 个、`Tools.GameLink.*` 14 个、
`Crash.Report.JarInJarMod.*` 9 个、`Setup.GameManage.Download.*` 9 个、
`Minecraft.Download.Error.*` 6 个、`Launch.Skin.Cape.Name.*` 3 个。

本次补齐方式：

- `en-US` 20 条全新英译；`en-GB` 的 76 条里，56 条直接沿用 `en-US` 并做英式化
  （`analyse` / `behaviour` / `colour` 等），其余 20 条与 `en-US` 新译同步；
  `zh-TW` 76 条按既有译法手译，术语沿用文件内的用词统计：
  模組 / 依賴 / 前置 / 內嵌 / 安裝器 / 逾時 / 停用（非「禁用」）/ 連線 / 大廳 /
  日誌 / 協議 / 資料夾 / 實例 / 區域網 / 建立者（房主）/ 預設 / 網路。
- 插入按「注释分组 + 组内字母序」定位：语言文件整体并非严格单调（`en-US` 124 处、
  `en-GB` 96 处、`zh-TW` 99 处逆序），全局排序会打乱分组。
- 校验：四语言 key 数对齐到 **3126**、XML 可解析、无重复 key、`{0}`/`{1}`/`{2}`
  占位符与 `zh-CN` 逐条一致、UTF-8 无 BOM + CRLF 原样保留；
  `dotnet build -c Release` 0 错误。
- `fr-FR` / `ja-JP` / `es-ES` 仍只有零星条目（`zh-CN` 回退），不在本次范围。

### 版本状态

- 最新 Tag：`v0.1.1`（prerelease）
- 可用包：`PCL_HE_Beta_x64.zip`（78 MB，自包含单文件）、`PCL_HE_Beta_ARM64.zip`（73 MB）
- 代码签名为自签名，分发后仍会触发 SmartScreen
- `main` 上另有三笔未发布的界面打磨提交（见附录 A 的 2026-10-23 一节），尚未打 tag

---

## 附录 C　会话交接记录（2026-10-24 凌晨）

本节由当班会话在交接时写下，供后续会话直接接手，不必重读代码或重新 grep。

### C.1　启动即崩溃的根治（维护者报「PCL-HE 启动不了」）

**现象**：进程起后 6~16 秒自行退出，`PCL/Log` 下不产生任何新日志，退出码 `-532462766`（`0xE0434352`，CLR 未处理异常）。

**定位手法（可复用）**：
- 用 `.ps1` 脚本把 exe 的 stdout/stderr 重定向到文件再读，**bash 内联 PowerShell 会把 `$` 变量吃掉，必须写成脚本文件执行**。
- Windows 事件日志的 `.NET Runtime`（ID 1026）能拿到完整托管堆栈；WER 的 `Report.wer` 只有签名、没有堆栈。

堆栈指向 `Lifecycle._CreateService` → `LifecycleContext.Fatal` → `_FatalExit` → `ForceShutdown` → `Shutdown` 抛 `InvalidOperationException`；真正的错在更早：`SingleInstanceService` 实例化失败，底层是 `Paths..cctor()` 抛 `ArgumentNullException: Value cannot be null. (Parameter 'path1')`。

**根因一（真正的错）**：`Basics` 与 `Paths` 构成静态初始化循环 —— `Basics` 的静态字段初始化会间接触发 `Paths` 的静态构造，而 `Paths.DefaultDirectory => Basics.ExecutableDirectory` 又回头读 `Basics`；同一线程重入时 CLR 不等构造完成、直接返回默认值，于是拿到 `null`，`Path.Combine(null, "PCL")` 抛异常；因为它发生在静态构造函数里，整个 `Paths` 类型报废。

**根因二（把错误掩盖了）**：`PCL.Core/App/IoC/LifecycleFlow.cs:181` 的 `Shutdown` 第一句是 `if (CurrentState == LifecycleState.BeforeLoading) throw new InvalidOperationException();`，而 `PCL.Core/App/IoC/LifecycleContext.cs:40` 的 `if (item.ActionLevel == ActionLevel.MsgBoxFatal) _FatalExit();` 恰恰在 BeforeLoading 阶段触发 —— 于是「记录致命错误」这个动作本身把进程抛死，原始错误一行日志都没留下。同一个 lambda 的另两条回调（`:44`、`:64`）都带 `CurrentState != LifecycleState.BeforeLoading` 守卫，只有这条漏了。

**修法**（`30973b5 fix(core): break the static init cycle that killed startup`，3 文件）：
- `PCL.Core/App/Paths.cs`：`DefaultDirectory` 改为自算，彻底切断对 `Basics` 的依赖 —— `public static string DefaultDirectory => _defaultDirectory ??= GetExecutableDirectory();`，新增 `private static string? _defaultDirectory;` 与 `private static string GetExecutableDirectory()`（先取 `Environment.ProcessPath` 的目录，失败退到 `AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)`）。
- `PCL.Core/App/IoC/LifecycleFlow.cs` 的 `_FatalExit()`：改为 `if (CurrentState == LifecycleState.BeforeLoading) _Exit(-1); else ForceShutdown(-1);`。
- `PCL.Core/App/IoC/LifecycleService.cs` 的 `_CreateService` catch：补 `Console.Error.WriteLine` 诊断。

**实测**：部署后 12 秒 `EXITED=False`，stderr 完全干净。

### C.2　缺陷清扫总账（两轮共 20 处，全部编译 0 错误并推送）

首轮 19 处见 2026-10-24 一节；第二轮新增 1 处：
- `PCL-Harness Edition/Pages/PageInstance/PageInstanceInstall.xaml.cs`（`90c8ad2`）：隔离警告原先按全局档位 `Config.Launch.IndieSolutionV2 == 0 || == 2` 判断，而该档位有 5 档（默认 4），1/3 档下会漏警告、2 档下对快照版实例会多警告。改为按 `McInstance.PathIndie` 与 `PathInstance` 的实际比对判断。
- 判定链（重要）：`PathIndie`（`PCL-Harness Edition/Modules/Minecraft/McInstance.cs:65-115`）按三级优先算 —— ①老 `IndieV1` 迁移；②实例目录下 `mods` 有文件或 `saves` 有子目录则自动隔离；③才轮到全局档位 switch（0=关闭 / 1=仅可装 Mod / 2=仅非正式版 / 3=前两者 / _=全部）。**该 getter 有副作用**：`:110 Config.Instance.IndieV2[PathInstance] = ShouldBeIndie();` 会把结果写进缓存；**返回值语义是反的** —— 被隔离时返回 `PathInstance`，未隔离时返回 `ModFolder.mcFolderSelected`。
- 刻意未动：`PCL-Harness Edition/Pages/PageDownload/PageDownloadInstall.xaml.cs:2330-2340`（新建实例）仍按全局档位判断 —— 那时实例目录还没创建，不该替它写缓存。

### C.3　待办（按优先级）

1. **发版 v0.1.2-Alpha**（维护者定在 10-24 中午或下午）。步骤：改 `PCL-Harness Edition/metadata.json` 的 version 对象（`base` 由 `0.1.1` 升 `0.1.2`、`suffix` 填 `Alpha`、**`code` 由 `2` 递增到 `3`**）→ 提交 → `git tag v0.1.2-Alpha` → `git push origin v0.1.2-Alpha`。`code` 即 `ModBase.versionCode`（`PCL-Harness Edition/Modules/Base/ModBase.cs:47`），被 `FormMain.xaml.cs:70` 与 `UpgradeSub`/`DowngradeSub` 用来判断版本升降，**不递增老用户升级后收不到提示**。CI 由 `.github/workflows/release-publish.yml` 监听 `push tags 'v*'`，Beta 配置发 prerelease，x64 与 ARM64 双矩阵。
2. 实机测试：正版登录取 XSTS/accessToken（微软白名单尚未提交，**当前不可用属预期**）、LittleSkin 设备代码流、Modrinth 与 CurseForge 各下一个模组验证速度与安装。
3. 实机验收浮钮深色配色（深色底 `#20242C`、悬停 `#3A4049`、已去描边）与幼圆加粗字体。
4. 剩余审查项（都涉并发时序，**发版后再单独排一轮**）：`PCL-Harness Edition/Modules/Network/Loaders/LoaderDownload.cs:77-83`（单文件失败即取消整批）、`PCL-Harness Edition/Modules/Base/ModLoader.cs:294`（安装回调在 UI 线程做重活）、`PCL-Harness Edition/Pages/PageDownload/ModDownloadLib.cs:716-746` 与 `:1740-1769`（在 `lock (ModJava.javaLock)` 内等 Java 下载并可能弹模态框）、`PCL-Harness Edition/Modules/Minecraft/ModLocalComp.cs:2244-2275`（属性 getter 内同步阻塞等哈希）、`PCL-Harness Edition/Modules/Network/Management/NetManager.cs` 与 `Models/DownloadFile.cs`（下载登记字段无锁）。
5. `PCL-Harness Edition/Pages/PageLaunch/PageLaunchRight.xaml.cs:173-229` 的第三方主页预设（属 PCL2 社区功能、非 PCL-CE 残留），**待维护者裁定是否切割**。
6. `PCL-Harness Edition/Modules/Minecraft/ModDownloadLib.cs:3698-3699` 的 `versionName.Remove(versionName.Length - 3, 3)` 与 `:3809` 的 `private const string mcInstallDefaultType = "安装"` 长度不一致（中文 2 字符 vs 砍 3 字符），属系统性中文硬编码，需先摸清 `loader.name` 完整拼接链再改。

### C.4　环境与操作要点

- 编译：`export DOTNET_ROOT="C:\\Users\\kingd\\.dotnet"` 后执行 `/c/Users/kingd/.dotnet/dotnet.exe build "Plain Craft Launcher 2.slnx" -c Release`（workdir 为 `PCL-HE`）。**PATH 里的 `/c/Program Files/dotnet` 没有 SDK，必须显式用 `.dotnet` 那个**。
- 部署：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File deploy.ps1`（workdir `PCL-HE`）。运行目录必须在 workspace 之外（`C:\\Users\\kingd\\PCL-HE`），否则 DSH 沙箱会给进程套 AppContainer 限制导致启动即退。
- 拉起：`powershell.exe -NoProfile -Command 'Start-Process -FilePath "C:\\Users\\kingd\\PCL-HE\\PCL-HE.exe" -WorkingDirectory "C:\\Users\\kingd\\PCL-HE"'`（bash 里不能用双引号包 PowerShell，`$env` 会被吞）。
- `git push` 报 `Internal Server Error` 或 `Failed to connect to github.com:443 after 21xxx ms` 都是**临时网络故障，重试即可，不是凭证问题**。
- 提交风格：Conventional Commits + 英文标题。
- 本机 `secrets.json` 位于 `~/AppData/Local/PCLHE/secrets.json`，只有一个键 `CURSEFORGE_API_KEY`（Key 不进仓库、不进 CI）。
- 界面改动后必须**重新编译 + deploy.ps1 部署**才可见，源文件改动不等于运行中的程序已变。
- `glob` 工具对含空格路径（如 `PCL-Harness Edition/LICEN*`）与 `.github/workflows/*` 返回空，需用 bash `ls`。

### C.5　仓库状态（交接时刻）

- `origin/main` 的 `HEAD` = `90c8ad2`，未推送计数 0，工作区干净。
- 仓库现有 tag：`ci-workflows`、`v0.1.1`；当前 `git describe` 为 `v0.1.1-28-g90c8ad2`。
- `PCL-HE/SECURITY.md` 已新建（中英双语，报告走 GitHub 私密漏洞通道，处理方式写明「尽力而为、非 SLA」）。
- 三版 README 已补「本机自签名可免 SmartScreen」的第三条解除方式。
- v0.1.1 的 GitHub release notes 已改写为中英双语精简版（中文在上，已通过 API 写回）。
