[简体中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="PCL-Harness%20Edition/Images/icon.png" alt="Logo" width="80" height="80">

# PCL-HE

**Plain Craft Launcher - Harness Edition**

*一個由第三方獨立維護的 PCL 衍生版*

[![Stars](https://img.shields.io/github/stars/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&color=eac54f&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI%2BPHBhdGggZmlsbD0iI2VhYzU0ZiIgZD0iTTEyIDJsMyA2IDYgLjktNC41IDQuMyAxLjEgNi4zTDEyIDE2LjZsLTUuNiAzIDEuMS02LjNMMyA4LjkgOWw4LTMiLz48L3N2Zz4%3D)](https://github.com/Sherlock-9522/PCL-HE/stargazers)
[![Issues](https://img.shields.io/github/issues/Sherlock-9522/PCL-HE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Sherlock-9522/PCL-HE/issues)
[![Last commit](https://img.shields.io/github/last-commit/Sherlock-9522/PCL-HE?style=for-the-badge&labelColor=444444&logo=github)](https://github.com/Sherlock-9522/PCL-HE/commits)

</div>

PCL-HE 是第三方基於 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 獨立進行二次創作的產物，以 [PCL Community Edition](https://github.com/PCL-Community/PCL-CE) 作為直接的上游基礎繼續開發。

**本發行版與成都瓜皮龍科技有限公司、PCL 官方團隊、PCL Community 官方團隊之間不存在隸屬關係，也不代表其立場或受到其背書。**

## ✨ 與上游的區別

PCL-HE 在上游基礎上做了這些事：

| 方向 | 說明 |
|---|---|
| **連線** | 連線底座換成 [陶瓦（Terracotta）](https://github.com/burningtnt/Terracotta)，在本機以獨立行程方式提供，不經過任何中間伺服器；陶瓦自帶 EasyTier，無需另行下載 |
| **隱私** | 全面切斷遙測：不收集、不上報任何使用資料；已停用 Natayark 帳戶體系與上游連線大廳服務 |
| **更新** | 作為獨立發行版，不從任何上游更新服務拉取版本、公告或本體 |
| **視覺** | 原創開屏動畫、深化的主介面藍漸層、檔案區獨立不透明度滑桿、低透明度吉祥物浮水印 |
| **功能** | 存檔頁四個 Chunkbase 直達入口（種子地圖／要塞／地獄要塞／終界城）、實例概覽頁的藍圖資料夾捷徑 |
| **合規** | 關於頁完整列出「開發參考」的上游專案與贊助管道 |

## 🌱 專案緣起與開發方式

2026 年 10 月，上游 PCL2 與若干主流啟動器陸續停止了「直接下載遊戲本體」的供應，玩家想玩原版就得自己另找安裝器、自己拼 jar 與資源檔。PCL-HE 因此立項：在 PCL-CE 的基礎上繼續維護一套**仍然能完整下載並安裝遊戲本體**的啟動器，離線登入、第三方驗證與正版登入三條入口保持可用。

開發方式上，這個專案和通常的開源專案不太一樣，所以有必要寫清楚：

- **架構、需求與驗收由人類維護者負責** —— 決定做成什麼樣、哪種實作可以接受、什麼才算修好了；
- **具體實作、程式碼巡檢與驗證由 AI 承擔** —— 由 [DeepSeek](https://www.deepseek.com/) 驅動，逐輪產出補丁、跑建置，並在真實實例裡重現問題、確認修復。

換句話說，這裡不是「AI 產生、人類照單全收」，而是人類守住方向與品質關口，AI 負責把想法落成能跑的程式碼。好處是迭代快、覆蓋面廣；代價是每個改動都必須有實機驗證兜底，所以倉庫裡大量提交都附帶重現步驟與日誌證據，而不是只改幾行就宣布修好。

**感謝 DeepSeek 提供模型與工具支援** —— 沒有它，這個專案不會以這樣的速度成形。同時也要說清楚：PCL-HE 由社群獨立維護，與任何模型廠商都不存在隸屬或背書關係。

## 💻 支援平台

| 作業系統 | 支援情況 | 環境要求 |
|---|---|---|
| Windows 10 1809 (17763) 或更高 | ✅ 完整支援 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ 理論能跑，酌情提供支援 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 或更低版本 | ❌ 不支援 | / |
| macOS / Linux / 其他作業系統 | ⚠️ 僅交叉編譯開發支援 | [.NET 10 SDK](https://get.dot.net/10) |

## 🔨 從原始碼建置

```bash
git clone https://github.com/Sherlock-9522/PCL-HE.git
cd PCL-HE
dotnet build "Plain Craft Launcher 2.slnx" -c Release
```

建置產物位於 `PCL-Harness Edition/bin/Release/net10.0-windows/`。

## 🛡️ Windows 提示「已保護您的電腦」怎麼辦

本發行版**沒有程式碼簽章憑證**，`.exe` 不含 Authenticode 簽章，因此 SmartScreen 會把「發行者」顯示為未知並攔截啟動。這**不是**病毒警告，只是 Windows 對任何未簽章程式的預設態度；您自己從原始碼建置出來的產物同樣會被這樣攔。

解除方式：

- **自己建置**的產物：右鍵 `PCL-HE.exe` → 內容 → 勾選「解除封鎖」→ 確定；或在 PowerShell 裡執行 `Unblock-File .\PCL-HE.exe`
- **從 Releases 下載**的產物：點「更多資訊」→「仍要執行」

需要徹底消除的話，只能為發佈產物購買程式碼簽章憑證。一般 OV 憑證簽好後，一開始仍會回報「發行者未知」，要等下載量累積出信譽；能立刻通過的是 EV 憑證。

## 🔒 授權條款

- `PCL-Harness Edition/` 使用 [自訂授權條款](PCL-Harness%20Edition/LICENCE)，其中包含《PCL 分發有限許可》與《PCL 儲存庫合理使用指南》
- `其餘所有目錄` 使用 [Apache License 2.0](LICENSE)

本發行版不以任何形式向接收者收費。上文兩份授權繼續適用於本發行版。

## ❤️ 致謝

- **[龍騰貓躍](https://github.com/Meloong-Git)** —— Plain Craft Launcher 原作者，[愛發電](https://ifdian.net/a/LTCat)
- **[PCL Community](https://github.com/PCL-Community)** —— PCL Community Edition 開發團隊，本發行版的直接上游
- **[huanghongxun](https://github.com/huanghongxun)** —— [Hello Minecraft! Launcher](https://hmcl.huangyuhui.net/)，部分功能設計與互動思路的參考來源

以及上游 PCL-CE 的[全部貢獻者](https://github.com/PCL-Community/PCL-CE/graphs/contributors)。沒有他們的工作，就沒有這個專案。

## 🐛 回饋

本專案為第三方獨立維護，**請不要向上游 PCL 或 PCL-CE 儲存庫回饋本發行版的問題**。

遇到問題請提到[本儲存庫的 Issues](https://github.com/Sherlock-9522/PCL-HE/issues)，並附上「設定 → 檢視日誌 → 匯出日誌」產生的日誌壓縮包。
