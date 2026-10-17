[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain%20Craft%20Launcher%202/Images/icon.png" alt="Logo" width="80" height="80">

# PCL-HE

**Plain Craft Launcher - Harness Edition**

*An independently maintained third-party derivative of PCL*

[![Stars](https://img.shields.io/github/stars/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&color=eac54f&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI%2BPHBhdGggZmlsbD0iI2VhYzU0ZiIgZD0iTTEyIDJsMyA2IDYgLjktNC41IDQuMyAxLjEgNi4zTDEyIDE2LjZsLTUuNiAzIDEuMS02LjNMMyA4LjkgOWw4LTMiLz48L3N2Zz4%3D)](https://github.com/Sherlock-9522/PCL-HE/stargazers)
[![Issues](https://img.shields.io/github/issues/Sherlock-9522/PCL-HE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Sherlock-9522/PCL-HE/issues)
[![Last commit](https://img.shields.io/github/last-commit/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&logo=github)](https://github.com/Sherlock-9522/PCL-HE/commits)

</div>

PCL-HE is an independent third-party derivative work based on [Plain Craft Launcher](https://github.com/Meloong-Git/PCL), developed on top of [PCL Community Edition](https://github.com/PCL-Community/PCL-CE) as its direct upstream.

**This distribution is not affiliated with Chengdu Guapilong Technology Co., Ltd., the official PCL team, or the official PCL Community team, and does not represent or carry the endorsement of any of them.**

## ✨ Differences from upstream

| Area | Description |
|---|---|
| **Multiplayer** | The networking backend is [Terracotta](https://github.com/burningtnt/Terracotta), which runs as a separate local process and never goes through an intermediate server. Terracotta bundles EasyTier, so nothing extra needs downloading |
| **Privacy** | Telemetry is fully severed: no usage data is collected or uploaded. The Natayark account system and the upstream lobby service are disabled |
| **Updates** | As an independent distribution, it never fetches versions, announcements, or binaries from any upstream update service |
| **Visuals** | An original splash animation, a deepened main-page blue gradient, an independent opacity slider for the player profile panel, and a low-opacity mascot watermark |
| **Features** | Four direct Chunkbase entries on the saves page (seed map / stronghold / nether fortress / end city) and a schematics folder shortcut on the instance overview page |
| **Compliance** | The About page lists every upstream project that informed the design, along with their sponsorship channels |

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

The build output lands in `Plain Craft Launcher 2/bin/Release/net10.0-windows/`.

## 🛡️ "Windows protected your PC"

This distribution ships **without a code signing certificate**, so the `.exe` carries no Authenticode signature and SmartScreen reports the publisher as unknown before letting it start. That is **not** a malware warning — it is how Windows treats any unsigned binary, including one you built yourself from this source.

To get past it:

- **Built it yourself?** Right-click `PCL-HE.exe` → Properties → tick *Unblock* → OK. Or run `Unblock-File .\PCL-HE.exe` in PowerShell.
- **Downloaded from Releases?** Click *More info* → *Run anyway*.

Removing the prompt entirely requires buying a code signing certificate for the release artifacts. An ordinary OV certificate still reports "unknown publisher" at first until download volume builds up reputation; only an EV certificate clears it immediately.

## 🔒 License

- `Plain Craft Launcher 2/` uses a [custom license](Plain%20Craft%20Launcher%202/LICENCE), which contains the *PCL Distribution Limited License* and the *PCL Repository Fair Use Guidelines*
- `All other directories` use the [Apache License 2.0](LICENSE)

This distribution is never charged for. Both licenses above continue to apply to it.

## ❤️ Credits

- **[LTCat (龙腾猫跃)](https://github.com/Meloong-Git)** — original author of Plain Craft Launcher, [Afdian](https://ifdian.net/a/LTCat)
- **[PCL Community](https://github.com/PCL-Community)** — the PCL Community Edition team, and our direct upstream
- **[huanghongxun](https://github.com/huanghongxun)** — [Hello Minecraft! Launcher](https://hmcl.huangyuhui.net/), a reference for parts of our feature design and interaction

And every [contributor](https://github.com/PCL-Community/PCL-CE/graphs/contributors) to upstream PCL-CE. None of this would exist without their work.

## 🐛 Reporting issues

This is an independently maintained third-party project. **Please do not report issues with this distribution to the upstream PCL or PCL-CE repositories.**

Report problems in [this repository's issues](https://github.com/Sherlock-9522/PCL-HE/issues) and attach the log archive produced by Settings → View Log → Export Log.
