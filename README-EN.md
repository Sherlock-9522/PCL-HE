[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL-Harness%20Edition/Images/icon.png" alt="Logo" width="80" height="80">

# PCL-HE

**Plain Craft Launcher (PCL) - Harness Edition**

*An independently maintained third-party derivative of PCL, and also an alternative branch of PCL-CE*

[![Stars](https://img.shields.io/github/stars/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&color=eac54f&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI%2BPHBhdGggZmlsbD0iI2VhYzU0ZiIgZD0iTTEyIDJsMyA2IDYgLjktNC41IDQuMyAxLjEgNi4zTDEyIDE2LjZsLTUuNiAzIDEuMS02LjNMMyA4LjkgOWw4LTMiLz48L3N2Zz4%3D)](https://github.com/Sherlock-9522/PCL-HE/stargazers)
[![Issues](https://img.shields.io/github/issues/Sherlock-9522/PCL-HE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Sherlock-9522/PCL-HE/issues)
[![Last commit](https://img.shields.io/github/last-commit/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&logo=github)](https://github.com/Sherlock-9522/PCL-HE/commits)

</div>

> [!WARNING]
> This is an early **v0.1.2-Alpha** testing release. It may contain defects that break launching, corrupt data, or cause worse problems. Back up your `.minecraft` and instance configuration first. Report problems in [Issues](https://github.com/Sherlock-9522/PCL-HE/issues) with logs attached; **do not report issues with this distribution to the upstream PCL or PCL-CE repositories**.

PCL-HE is an independent third-party derivative work based on [Plain Craft Launcher](https://github.com/Meloong-Git/PCL), developed on top of [PCL Community Edition](https://github.com/PCL-Community/PCL-CE) as its direct upstream.

**This distribution is not affiliated with Chengdu Guapilong Technology Co., Ltd., the official PCL team, or the official PCL Community team, and does not represent or carry the endorsement of any of them.**

## ✨ Differences from upstream

| Area | Description |
|---|---|
| **Multiplayer** | The networking backend has been switched to [Terracotta](https://github.com/burningtnt/Terracotta) multiplayer, which runs as a separate local process and never goes through an intermediate server. Terracotta bundles EasyTier, so nothing extra needs downloading |
| **Privacy** | Telemetry is fully severed: no usage data is collected or uploaded. The Natayark account system and the upstream lobby service are disabled |
| **Updates** | As an independent distribution, it never fetches versions, announcements, or binaries from any upstream update service |
| **Visuals** | An original splash animation, a deepened main-page blue gradient, an independent opacity slider for the player profile panel, and a low-opacity mascot watermark |
| **Memory** | Ships a standalone subproject, [PCL.MemoryOptimizer](PCL.MemoryOptimizer/README.md): light, in-game boost (no elevation, never touches the game) and a deep clean that calls a kernel interface after a single UAC prompt. No process is ever terminated |
| **Features** | Four direct Chunkbase entries on the saves page (seed map / stronghold / nether fortress / end city) and a schematics folder shortcut on the instance overview page |
| **Compliance** | The About page lists every upstream project that informed the design, along with their sponsorship channels |

## 🧠 Memory optimizer

The memory optimizer under Tools → Toolbox has been rebuilt as a standalone subproject, [PCL.MemoryOptimizer](PCL.MemoryOptimizer/README.md), shipped with the launcher and maintainable on its own. It has three modes:

- **Light** — no administrator rights needed; trims only the launcher's own working set. Small win, zero risk.
- **In-game boost** — no administrator rights needed; leaves the game out entirely, trims the working set of background programs (browser, chat, editor) and raises the game process memory priority to the highest level. Nothing of the game is touched, so it is safe to use **while the game is running**.
- **Deep clean** — one UAC prompt, after which a short-lived elevated process calls a kernel interface to trim working sets system-wide. Measured: available memory up by roughly 2.4 GB, with no process terminated.

You can also tick **optimize memory before launch** under Settings → Game memory. If deep clean was already authorised during this run, launching goes straight to it without asking again.

> ⚠️ Deep clean moves the memory pages of programs you are using into the page file, so switching back to them briefly stutters, and it should not be used while a game is running. It relieves current memory pressure; it does not make any program use less memory.


## 🌱 Origins and How This Is Built

On 20 October 2026, upstream PCL2 and several mainstream launchers stopped offering direct game downloads for policy reasons. Playing vanilla suddenly meant hunting for an installer and assembling jars and assets by hand. PCL-HE exists to keep a launcher that **can still download and install the game itself**, with offline login, third-party authentication and Microsoft login all left working.

The way this project is built differs from a typical open-source project, so it is worth stating plainly:

- **Architecture, requirements and acceptance belong to the human maintainer (myself)** — what gets built, which implementation is acceptable, and what counts as fixed.
- **Implementation, code audits and verification are done by AI** — driven and developed by [DeepSeek Harness](https://www.deepseek.com/harness), producing patches round by round, running builds, and reproducing issues in real instances before calling them fixed. (It is given a fair amount of latitude in some cases.)

This is not "AI generates, I rubber-stamp". A human holds the direction and the quality gate; the model turns intent into runnable code. The upside is speed and coverage. The cost is that every change needs real verification behind it, which is why so many commits here ship with reproduction steps and log evidence instead of a few changed lines and an announcement — and why the workload is considerable.

**Thanks to DeepSeek Harness for providing the model and tooling, and to the plugin authors in the DeepSeek Harness community for the tool plugins they contribute** — without them this project would have stayed in my head instead of taking shape at this pace. To be clear: PCL-HE is maintained independently by the community and has no affiliation with, or endorsement from, any model vendor.

## 💻 Supported Platforms

| Operating System | Support Status | Requirements |
|---|---|---|
| Windows 10 1809 (17763) or later | ✅ Full support | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ Should work, community support at our discretion | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 or earlier | ❌ Not supported | / |
| macOS / Linux / others | ⚠️ Cross-compilation only | [.NET 10 SDK](https://get.dot.net/10) |

## 🔨 Building from source

```bash
git clone https://github.com/Sherlock-9522/PCL-HE.git
cd PCL-HE
dotnet build "Plain Craft Launcher 2.slnx" -c Release
```

The build output lands in `PCL-Harness Edition/bin/Release/net10.0-windows/`.

## 🛡️ "Windows protected your PC"

This distribution ships **without a code signing certificate**, so the `.exe` carries no Authenticode signature and SmartScreen reports the publisher as unknown before letting it start. That is **not** a malware warning — it is how Windows treats any unsigned binary, including one you built yourself from this source.

To get past it:

- **Built it yourself?** Right-click `PCL-HE.exe` → Properties → tick *Unblock* → OK. Or run `Unblock-File .\PCL-HE.exe` in PowerShell.
- **Downloaded from Releases?** Click *More info* → *Run anyway*.
- **Just running it on your own machine?** The repo also ships `setup-signing.ps1` and `deploy.ps1`: the first creates a self-signed certificate and installs it into your current user's trusted stores, the second uses it to sign the deployed build. Do that once and local launches stop tripping the prompt. The certificate is trusted only on that machine — on anyone else's PC the publisher still shows as unknown.

Removing the prompt entirely requires buying a code signing certificate for the release artifacts. An ordinary OV certificate still reports "unknown publisher" at first until download volume builds up reputation; only an EV certificate clears it immediately.

## 🔐 About Microsoft login: current status

Signing in with a Microsoft account uses the **Device Code Flow**: the launcher shows an 8-character code, you enter it at <https://www.microsoft.com/link> and approve, and the launcher never asks for your password.

The built-in Azure application is `Plain Craft Launcher (PCL) - Harness Edition`, client ID `fe6a35b0-86b0-4e23-b234-e80a01b0b5fc`.

**Known limitation**: with this application, Microsoft OAuth and Xbox Live both succeed, but the final `login_with_xbox` call returns **HTTP 403**, and the server says:

```json
{
  "path": "/authentication/login_with_xbox",
  "errorMessage": "Invalid app registration, see https://aka.ms/AppRegInfo for more information"
}
```

In other words, the Minecraft backend does not consider this Azure application to be registered on the Minecraft side. This has nothing to do with your account and nothing to do with the client implementation: the same code and the same protocol work with a registered application ID (compare [HMCL](https://github.com/HMCL-dev/HMCL), which is field-for-field identical).

If you need Microsoft login, you can:

1. Register your own application on Azure (account type "Personal Microsoft accounts", with "Allow public client flows" enabled);
2. Set the environment variable `PCL_MS_CLIENT_ID` to your application ID, then start the launcher.

The environment variable takes precedence over the built-in value. A restart is enough; no rebuild is needed.

**Offline login** (any name you like) and **third-party login** (authlib-injector services such as LittleSkin) are unaffected and work out of the box.

## 🔒 License

- `PCL-Harness Edition/` uses a [custom license](PCL-Harness%20Edition/LICENCE), which contains the *PCL Distribution Limited License* and the *PCL Repository Fair Use Guidelines*
- `All other directories` use the [Apache License 2.0](LICENSE)

This distribution is never charged for. Both licenses above continue to apply to it.

## ❤️ Credits

- **[LTCat (龙腾猫跃)](https://github.com/Meloong-Git)** — original author of Plain Craft Launcher, [Afdian](https://ifdian.net/a/LTCat)
- **[PCL Community](https://github.com/PCL-Community)** — the PCL Community Edition team, and our direct upstream
- **[huanghongxun](https://github.com/huanghongxun)** — [Hello Minecraft! Launcher](https://hmcl.huangyuhui.net/), a reference for parts of our feature design and interaction

And every [contributor](https://github.com/PCL-Community/PCL-CE/graphs/contributors) to upstream PCL-CE. None of this would exist without their work.

## 📝 Reporting issues

This is an independently maintained third-party project. **Please do not report issues with this distribution to the upstream PCL or PCL-CE repositories.**

Report problems in [this repository's issues](https://github.com/Sherlock-9522/PCL-HE/issues) and attach the log archive produced by Settings → View Log → Export Log.

> Minecraft access tokens (`--accessToken`) in the log are masked automatically by the launcher, but screenshots, config files and any keys you entered yourself are not. Please check before uploading — do not paste account passwords or private keys into a public issue.
