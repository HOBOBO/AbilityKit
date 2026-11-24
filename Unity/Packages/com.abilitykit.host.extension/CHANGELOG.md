# Changelog — com.abilitykit.host.extension

本包遵循 [Keep a Changelog](https://keepachangelog.com/) 风格；版本号遵循语义化版本。
AbilityKit 整体仍处于开发期，0.x 版本不承诺向后兼容。

## [Unreleased]

### 修复
- `FixedStepTickRunner.Step` 在回调异常后只保留尚未完成的时间预算，避免再次调用时把已经成功的帧重复计入。
- 帧时间和调度器由各自世界容器创建；只查询并驱动仍登记、且绑定到当前安装的实例。修复创建失败后的残留、重复创建重置时钟，以及销毁抛错或直接批量关闭后继续推进的问题。
- 自动启动完成记录和服务端回滚历史按当前登记的世界实例隔离；异常释放、批量关闭、同 ID 重建及回滚模块卸载后，不再读取旧记录或重演旧世界。Host 通知和同步协议不变。
- 客户端预测上下文和帧同步会话按当前世界实例隔离；已摘除世界不再参与预测、捕获或会话输入，重建不继承旧历史与队列。显式无世界会话及首次绑定的排队行为保持不变。

### 整理
- 将 `FramePacketNetAdapter` 迁入 MOBA 客户端会话层，并复用会话内部上下文；双写、缺帧和快照行为不变。原 `AbilityKit.Ability.Host.Extensions.Session` 公开类型不再由框架导出；其他项目应使用通用帧包、缓冲和快照路由装配自己的会话。
- 将 `WorldCatchUpDriver` 放入 `FrameSync/CatchUp`，将 `FrameSyncInputHubFactory` 与 `FrameJitterBufferHub<TFrame>` 放入 `FrameSync/Input`。三者的命名空间与公开签名保持不变。
- 补齐时间与自动启动模块的中文契约和包入口；保留历史文档与示例路径。
- Unity 运行时程序集关闭隐式引擎引用；.NET 工程显式声明已经直接使用的 `core`、`world.di`、`world.framesync`，不扩展源码编译范围。

### 验收
- 帧包适配器迁移与帧同步目录整理：Host Extension 44 项通过；`Unity-Instance2` EditMode 的远端帧流与会话诊断 30 项通过，包结构检查无链接或编码问题。
- 双世界帧提交诊断：新增 4 项，Host Extension 40 项通过；确认守卫可隔离模拟失败，并复现无守卫与回滚捕获失败时的输出缺口。本批未改变提交契约。
- 客户端预测与帧同步会话生命周期批次：新增 7 项定向用例，修复前复现 6 项缺陷，无世界会话首次绑定用例已通过；修复后 Host Extension 36 项、Shooter 定向 10 项、`Unity-Instance2` EditMode 定向 4 项通过。
- 自动启动与服务端回滚生命周期批次：新增 6 项回归在修复前均能复现；修复后 Host Extension 29 项、Shooter 帧同步与快照定向 10 项、`Unity-Instance2` EditMode 定向 2 项均通过。
- 世界时间服务生命周期批次：Host Extension 23 项、Shooter 帧同步与快照定向 14 项通过；`Time` 注释与包结构检查通过。通过 `Unity-Instance2` 补跑 Unity 2022.3.62f1 EditMode，帧时间恢复和正式世界装配定向测试 2 项均通过。
- Host Extension 18 项、Shooter 帧同步与快照定向 14 项通过；Unity 2022.3 EditMode 的帧时间恢复和正式世界装配定向 2 项通过。
- `Time` 与 `WorldStart` 通过中文多行注释检查；未对其余目录启用全包注释门禁，也未完成独立安装验证。

## [0.1.0] — 2026-07-31 — Beta

首个 Beta 里程碑。本版本的核心变更是**范围收口**：将 38 个 Moba 专属 host adapter
（Runtime/Moba/ 子树，含 3 个 asmdef：`AbilityKit.Host.Extensions.Moba` / `.Moba.Client` / `.Moba.Server`）
物理提取到新包 `com.abilitykit.demo.moba.host`。提取后本包仅包含通用框架代码
（FrameSync/Rollback/Session/Time/WorldStart/Server/Client），不再依赖任何
Moba protocol 或 demo-share 包。.NET 项目侧也同步拆分出 `src/AbilityKit.Demo.Moba.Host.csproj`。

### 迁移说明
- 如果你的项目依赖 Moba host 类型（`IMobaBattleRuntimePort`、`MobaGameStartSpec`、`MobaRoomOrchestrator` 等）：
  请新增对 `com.abilitykit.demo.moba.host`（Unity）或 `src/AbilityKit.Demo.Moba.Host.csproj`（.NET）的引用。
  Unity asmdef 的 `references` 数组无需改——Moba asmdef 名称未变，只要 package.json 依赖了新包即可解析。
- 如果你的项目仅使用通用 host.extension 类型（FrameSync/Rollback 等）：无需任何更改。
- Shooter / Samples 项目原本通过传递依赖意外继承到了 Moba 类型——本版本后它们不会再看到 Moba 类型。

### 其他变更
- `src/AbilityKit.Host.Extension.csproj`：开 `AbilityKitStable=true`，RootNamespace 修正。
- 本包已具备框架中立性；后续 0.1.0 的验收见通用基础层/同步层 roadmap。
