# 设计说明

## 目标

在 Windows 上提供「不终止任何进程、不挂起任何程序」的内存回收能力，作为 PCL-HE 的随身子组件，独立维护与发布。

## 三档设计

### 轻量护航（Guarded）

- 只对**当前进程**调用 `EmptyWorkingSet`（psapi）。
- 目的：启动器自身（WPF 常驻 300 MB ~ 1.5 GB）把暂时用不上的页交还系统。
- 零权限需求、零副作用、可随时执行；代价是切回启动器时要重新渲染一次。

### 游戏内护航（GameBoost）

- 先对游戏进程调用 `SetProcessInformation(ProcessMemoryPriorityInformation, MEMORY_PRIORITY_NORMAL)`，再把游戏 PID 放进 `ProtectedProcessIds` 逐个清理其它进程。
- 目的：游戏跑着、内存又快满时，把后台程序（浏览器、聊天软件、编辑器）的闲置页换出，同时让系统在内存压力下**最后**才换出游戏。
- 不需提权：只动当前用户有权限打开的进程。实测这台机器上可打开进程占全部工作集的 73%，够用。
- 受保护集合按「游戏 PID + 同名进程」构造：多开整合包时同一个 `javaw` 名下可能跑着另一个游戏，不能误伤。

### 深度清理（DeepSystemWide）

- 提权 → 启用 `SeProfileSingleProcessPrivilege` + `SeIncreaseQuotaPrivilege` → `NtSetSystemInformation(80, 2)`。
- 全系统一次性移出工作集页；**没有排除参数**，所以游戏在跑时必须先向用户说明代价并二次确认。

### 逐进程清理（DeepPerProcess）

- 遍历进程逐个 `EmptyWorkingSet`，可跳过指定 PID。游戏内护航建立在它之上。
- `MaxProcesses` 只用于自检与限流，正常执行不设上限。

## 时序约束（调用方必须遵守）

1. 启动前优化在后台线程与启动准备阶段并行执行，但必须在 `gameProcess.Start()` **之前**收尾。
2. 启动前若已授权过提权通道，走深度清理；未授权则只跑轻量护航并在左下角说明。
3. 游戏运行中的手动清理一律走游戏内护航，绝不自动触发。
4. 结果以普通提示报告（清扫量、耗时），不阻塞界面。

## 统计口径

- 主指标：执行前后**可用物理内存**差值（`GlobalMemoryStatusEx`）与全部进程工作集总和。
- 副作用指标：已修改页列表峰值（需要写回的量）、耗时、进程数（必须不变）。
- 深度清理的主指标取「可用内存变化」，逐进程与游戏内护航取「工作集缩减量」，二者不可混用。
- 游戏内护航额外报告「成功提升优先级的进程数」，它是效果指标而不是收益指标。

## 权限与失败处理

- `AdjustTokenPrivileges` 返回 `TRUE` **不代表成功**（可能部分成功），必须查 `Marshal.GetLastWin32Error()`。
- `TOKEN_PRIVILEGES` 必须声明为 `[StructLayout(LayoutKind.Sequential, Pack = 4)]`，否则 LUID 错位 → 特权启用静默失败、内核返回 `0xC0000061`。
- `SetProcessInformation` 对权限不足或已退出的进程返回 `FALSE`，只记为该进程失败，不影响整批。
- 调用失败一律把 `NTSTATUS` / 错误码原样带出，不吞错、不假装成功。

## 风险与缓解

| 风险 | 说明 | 缓解 |
| --- | --- | --- |
| 游戏 / 前台程序被清到 | 内核命令无排除参数 | 游戏运行中改用游戏内护航；深度清理需二次确认 |
| 写回抖动 | 已修改页需要写回页面文件 | 只在用户显式触发时执行，不做定时任务 |
| 未公开接口 | 信息类 80 属逆向结果 | 隔离在本项目内；失败即报错 |
| 误用于其他用户会话 | 管理员下会波及全会话 | 仅手动触发，不做后台常驻 |
| 多开游戏被误清 | 同名 `javaw` 进程不止一个 | 受保护集合按进程名扩展，全部排除 |

## 来源

- RAMMap（Sysinternals）Empty 菜单；`NtSetSystemInformation` 信息类 80 的社区记录。
- PCL-CE `PCL.Core/App/Tools/MemSwapService.cs`（Apache-2.0）曾用同款机制，后移除。
- 微软内存口径与硬缺页代价实测（Alois Kraus：《Windows Memory Management》相关实测）。
