# 安全政策

## 报告漏洞

如果你在 PCL-HE 里发现了安全问题，请**不要开公开的 issue** —— 那会在问题被修复之前先把它暴露给所有人。

私下报告请优先使用 GitHub 的私密漏洞报告通道：

- <https://github.com/Sherlock-9522/PCL-HE/security/advisories/new>

提交时请尽量写清：问题类型（例如任意代码执行、路径穿越、凭证泄露）、复现步骤、受影响的版本或提交号、以及能附上的日志或最小复现。

## 处理方式

这是一个由个人与 AI 协作维护的社区项目，没有专职的安全团队，所以下面是承诺的**尽力而为**范围，而不是 SLA：

- 收到报告后会尽快确认，通常几天内给出初步判断。
- 确认的漏洞会先在私下修复，修复发布后再公开细节，并在发行说明里致谢——如果你希望匿名，提前说一声即可。
- 如果评估后认为不是漏洞，会说明理由。

## 支持范围

只有 `main` 分支上的最新提交、以及最近一次发行版接受安全修复，更早的版本不再回补。

## 这些不算漏洞

- 启动器需要读写你本机的 Minecraft 目录、启动器数据目录与 Java 运行时，这是它的本职工作。
  只有当你证明了它**越出了**这些范围（例如被构造的实例名或压缩包路径写到别处），才算问题。
- 第三方登录（LittleSkin 等）走的是上游协议，凭证由上游签发与校验，本项目不额外留存。
- 微软正版登录目前不可用（应用登记尚未完成），这是**功能未就绪**，不是安全问题。
- 从 Releases 下载的程序会触发 SmartScreen「发布者未知」——因为发布产物没有代码签名证书，这在 <README.md> 里已说明。
- 需要你自己在 `secrets.json` 里填的 CurseForge API Key 等密钥：仓库与发行包里都不包含它们，泄露只可能来自你自己的机器。

---

# Security Policy

## Reporting a vulnerability

If you find a security issue in PCL-HE, please **do not open a public issue** — that exposes it to everyone before it can be fixed.

Use GitHub's private vulnerability reporting channel:

- <https://github.com/Sherlock-9522/PCL-HE/security/advisories/new>

Please include the issue type (e.g. arbitrary code execution, path traversal, credential leak), reproduction steps, the affected version or commit, and any logs or minimal repro you have.

## How reports are handled

This is a community project maintained by an individual working with AI assistance — there is no dedicated security team, so the following is a best-effort commitment, not an SLA:

- Reports are acknowledged as soon as possible, usually within a few days.
- Confirmed issues are fixed privately first; details go public after the fix ships, with credit in the release notes unless you ask to stay anonymous.
- If something turns out not to be a vulnerability, you will get the reasoning.

## Supported versions

Only the latest commit on `main` and the most recent release receive security fixes. Older versions are not back-ported.

## Not vulnerabilities

- The launcher reads and writes your local Minecraft directory, launcher data directory and Java runtimes — that is its job.
  It is only a problem if you can show it **escaping** those boundaries (e.g. a crafted instance name or archive path writing somewhere else).
- Third-party login (LittleSkin and similar) follows upstream protocols; credentials are issued and validated upstream and are not stored by this project.
- Microsoft login is currently unavailable (the app registration is pending) — that is an **unfinished feature**, not a security issue.
- SmartScreen reporting an unknown publisher on Releases downloads is expected: release artifacts carry no code signing certificate, as explained in <README.md>.
- Secrets such as the CurseForge API key you put in `secrets.json` are not shipped in the repo or the release archives; a leak can only come from your own machine.