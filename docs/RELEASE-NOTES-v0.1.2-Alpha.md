# PCL-HE v0.1.2-Alpha

> 早期 Alpha 版本，可能存在导致启动失败或数据异常的 Bug。遇到问题请到 [Issues](https://github.com/Sherlock-9522/PCL-HE/issues) 反馈，并附上「设置 → 查看日志 → 导出日志」生成的日志压缩包。

## 下载哪个包？ / Which file to download?

> **如果你不知道 .NET Desktop Runtime 是什么，请直接下载文件名带 `self-contained` 的那个包，别的不用管。**

本次一共四个包，`win-x64` 与 `win-arm64` 各两个：

- **`PCL-HE_v0.1.2-Alpha_win-x64_self-contained.zip`** —— 已内置 .NET 运行时，解压即用，**绝大多数人请选这个**。体积较大（约 80 MB），但不会缺任何东西。
- **`PCL-HE_v0.1.2-Alpha_win-x64_framework-dependent.zip`** —— 体积小得多（约 24 MB），但**必须自己先装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)**。没装的话双击会弹出系统提示要求下载运行时（少数环境下可能一闪而过或直接没反应），装好后重试即可。
- **带 `win-arm64` 的两个包**：只给 Windows on ARM 设备（例如骁龙 X 系列笔记本）。不确定自己是不是，就选 `win-x64`，绝大多数场景都能跑。

> **If you do not know what .NET Desktop Runtime is, just download the file whose name contains `self-contained` and ignore the rest.**

Four packages this time, two per architecture (`win-x64`, `win-arm64`):

- **`PCL-HE_v0.1.2-Alpha_win-x64_self-contained.zip`** — bundles the .NET runtime, unzip and run. **Most players should pick this one.** Larger (about 80 MB), but nothing is missing.
- **`PCL-HE_v0.1.2-Alpha_win-x64_framework-dependent.zip`** — much smaller (about 24 MB) but **requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)**. Without it, launching shows a system prompt to download the runtime (on a few systems it may flash by or appear to do nothing) — install it and retry.
- **The two `win-arm64` packages** are only for Windows on ARM devices (e.g. Snapdragon X laptops). If you are unsure, take `win-x64` — it runs in almost every case.

## 简体中文

### 内存优化重做（本次重点）

这是本次更新的主要工作。「工具 → 百宝箱」里的内存优化被整个重写成一个独立子项目 `PCL.MemoryOptimizer`，随启动器一同发布，也可以单独维护（子项目里有自己的 README 与设计说明）。核心原则只有一条：**只把暂时用不到的物理内存页换出去，绝不结束、也不挂起任何进程**。

重写成三档，按「会不会动到游戏」区分：

- **轻量护航**（免提权，零风险）：只整理启动器自身的工作集。启动器常驻几百 MB 到 1 GB 多，切回启动器时重新渲染一次就好。
- **游戏内护航**（免提权，游戏运行中推荐）：**把游戏完整排除在外**，只整理浏览器、聊天软件、编辑器这类后台程序的工作集，同时把游戏进程的内存优先级提到最高。游戏一个字节都不动，所以游戏跑着的时候也能随时点——这是之前缺的那一档。
- **深度清理**（需要一次 UAC 授权）：由临时提权进程调用系统内核接口，一次性整理全系统工作集。实测耗时约 0.7 秒，可用内存增加约 2.4 GB，进程数前后完全不变。代价是被换出的程序切回前台时会有短暂迟滞，所以**游戏运行中请不要用它**，界面也会先弹一次确认。

配套改动：
- 删掉旧的「遍历所有进程强行修剪工作集」实现——它会波及你正在使用的程序，而且统计口径不可靠。
- 「设置 → 游戏内存」可勾选**启动游戏前自动优化**；实例设置里可以单独选「跟随全局 / 开启 / 关闭」。
- 本次运行里已经点过深度清理并授权过时，启动游戏会直接走深度清理、不再多问一遍；还没授权则只跑轻量护航，并在左下角提示去百宝箱点一次即可。

### 便携模式

- `PCL-HE.exe` 同目录若存在 `CONFIG` 文件夹，全部配置改写到这里，不再向 `%APPDATA%` 写任何东西；首次启用会自动迁移已有配置。删掉 `CONFIG` 即退回原来的行为。

### 与上游同目录共存

- 配置文件名改为 `PCL-HE.ini` 与 `config.pclhe.v1.yml`，不再读写上游同名的 `PCL.ini`、`config.v1.yml`。

### 修复

- 启动时报「解压文件失败」：启动阶段链中间插入了一步类型不匹配的环节，导致后续步骤拿到空输入而崩溃。
- 离线启动的「下次一定」每次都会再弹一次；现在只提示一次。
- 深色主题下启动进度条仍是蓝色、右下角电源按钮蓝底黑符，与深色界面不协调。
- 「新建档案」按钮只有中间一小块能点，点击边缘无响应。
- 深色主题下右下角电源按钮的图标跟着主题变成深灰，几乎看不见；现已固定为白色。
- 操作按钮上的「开始下载」等文字用了幼圆并叠加合成加粗，笔画糊在一起；已恢复为系统默认字体与常规字重。

### 合规清理

- 移除上游沿用的加密熵值、第三方账号（Natayark）网页授权入口，以及崩溃提示里的上游指向文案。
- 上游项目、贡献者、开发参考与许可证署名**全部保留**。

---

## English

### Memory optimizer rebuilt (the focus of this release)

This is the main work of this release. The memory optimizer under Tools → Toolbox has been rewritten from scratch as a standalone subproject, `PCL.MemoryOptimizer`, shipped with the launcher and maintainable on its own (it has its own README and design notes). One rule underpins all of it: **only idle physical pages are moved out — no process is ever terminated or suspended.**

It now has three modes, separated by whether they can touch the game:

- **Light** (no elevation, zero risk): trims only the launcher's own working set. The launcher routinely holds several hundred MB to over 1 GB; switching back to it just re-renders once.
- **In-game boost** (no elevation, recommended while playing): leaves the game out entirely, trims the working set of background programs such as browsers, chat clients and editors, and raises the game process memory priority to the highest level. Nothing of the game is touched, so it is safe to use while the game is running — the mode that was previously missing.
- **Deep clean** (one UAC prompt): a short-lived elevated process calls a kernel interface to trim working sets system-wide. Measured: ~0.7 s, about 2.4 GB more available memory, with the process count unchanged before and after. The trade-off is a brief stutter when switching back to a trimmed program, so **do not use it while a game is running** — the UI asks for confirmation first.

Supporting changes:
- The old "walk every process and force-trim" implementation is gone: it hit programs you were actively using, and its accounting was unreliable.
- Tick **optimize memory before launch** under Settings → Game memory; per-instance settings let you choose follow-global / on / off.
- If deep clean was already authorised during this session, launching goes straight to it without asking again; otherwise only the light mode runs and a hint in the bottom-left corner points to the Toolbox.

### Portable mode

- If a `CONFIG` folder sits next to `PCL-HE.exe`, all configuration is written there and nothing goes to `%APPDATA%`; existing config is migrated on first use. Delete `CONFIG` to fall back to the previous behaviour.

### Coexisting with upstream installs

- Config files are now `PCL-HE.ini` and `config.pclhe.v1.yml`; the upstream `PCL.ini` and `config.v1.yml` are no longer read or written.

### Fixes

- "Failed to extract files" at launch: a stage with a mismatched generic type sat in the middle of the launch chain, leaving the next step with a null input.
- The offline "maybe later" prompt reappeared on every launch; it now appears once.
- In dark theme the launch progress bar stayed blue and the power button had black glyphs on a blue disc.
- The "new profile" button only responded near its centre; clicks on the edges did nothing.
- In dark theme the power button glyph followed the theme into dark grey and was barely visible; it is now fixed to white.
- Action-button labels such as "start download" used a font with a single weight plus synthetic bold, smearing the strokes together; they are back to the system default font at normal weight.

### Compliance cleanup

- Removed the inherited encryption entropy value, the third-party (Natayark) web authorisation entry, and upstream-referencing wording in crash dialogs.
- Upstream projects, contributors, development references and licence attributions are **all retained**.
