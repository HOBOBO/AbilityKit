# Core 包稳定化与发布推进记录

> 文档类型：稳定化实施记录。2026-11-17 用户确认分阶段推进，以 Core 为首个试点；框架包统一使用中文多行注释，并审查明显临时代码。
>
> 工程规则见 [框架包工程规范](10-FrameworkPackageEngineeringStandards.md)，Core 行为边界见 [CoreBoundary.md](../../../Unity/Packages/com.abilitykit.core/Documentation~/CoreBoundary.md)。本记录不能替代正式发布批准。

## 1. 本次范围

先对 `com.abilitykit.core` 完成工程规范试点，包含 Runtime、Unity 兼容适配和 Editor 兼容代码。公开 API、数值、事件顺序、失败行为和兼容入口按现有契约保持；涉及新机制或公开契约调整时另列方案。Deterministic 是实际直接依赖，纳入消费验证闭包，后续单独完成同等工程整理。

统一规范适用于所有自有框架包，但本批不把其它包的注释迁移状态标为完成，不全仓批量格式化。

## 2. 初始事实

| 项目 | 2026-11-17 检查结果 |
|---|---|
| Core Runtime | 78 个 C# 文件，含保留的兼容能力 |
| 直接测试 | `AbilityKit.Core.Tests` 141 项通过 |
| 构建 | XML 注释缺失警告仍允许通过；Stable 属性策略定义在 `src/Directory.Build.targets` |
| 文档 | README 对 Numerics、DebugDraw 的迁移描述与边界文档不一致 |
| 目录 | Event/Eventing、Math/Mathematics、Generic/Identifiers 不一致 |
| 边界审计 | Collision 旧 Core 命名空间 6/5，Record 57/56，当前失败；不得扩大白名单掩盖 |
| 发布 | `tools/publish/release-manifest.json` 批次仍为 candidate |

## 3. 阶段及完成标准

| 阶段 | 工作 | 完成标准 |
|---|---|---|
| 一：规范与盘点 | 工程规范、索引、API/注释盘点、临时代码与迁移清单 | 事实与目标分开，检查工具可运行 |
| 二：Core 实施 | 中文多行注释、文档修正、保行为清理、目录整理、警告收紧 | Core 检查与直接测试通过，API 基线不漂移 |
| 三：消费与发布准备 | 干净的 Core+Deterministic 消费项目、平台矩阵、回归与发布报告 | 明确实际通过范围及剩余阻断，不以 .NET 成功代替 Unity/IL2CPP 证明 |

## 4. 临时代码与迁移审查

| 对象 | 证据与判定 | 处置 |
|---|---|---|
| `Continuous` | 已有独立包，但 Core 仍有弃用兼容契约与测试 | 保留、补中文契约，阻止新消费者 |
| `Config/Reflection/Unity` | 应用设置已归所属项目；旧入口承诺下个主版本前兼容 | 保留、补真实失败兜底语义 |
| `DebugDraw` 与三个 Marker 全局启动入口 | 已迁移/冻结；仍有兼容类型和行为测试 | 保留迁移说明，不当临时代码直接删除 |
| `Log` 空 catch | 日志失败不能中断业务是现有契约，Trace 当前映射 Info | 明确异常隔离与映射；展开压缩实现，避免误改行为 |
| `PoolConfigCenter` 返回辅助方法 | 单次赋值的私有包装仅用于三元表达式，增加阅读跳转 | 在原方法直接分支赋值，保持成功和失败输出 |
| `LayeredJsonSettingsStore` 三层读取 | 当前每次标量读取构建完整有效视图；底层只读一个键 | 按既有优先级直接取值后转换，保持转换与失败行为，并回归验证 |
| 两处旧命名空间审计超额 | 需定位具体类型与使用方；迁移可能涉及公开 API | 先追查来源与兼容性，不直接放宽上限 |

## 5. 结果与待办

实施后在此补录实际修改、命令、通过/失败及未覆盖项。只有完成相应验证才勾选：

- [x] 工程规范和中文多行注释检查落地。
- [x] Core 全部自有源码注释统一，XML 文档警告按包收紧（第十五、十六批；Unity 编译策略单列）。
- [ ] 临时代码清理和物理目录统一完成，Unity GUID 保持。
- [ ] Core 直接测试、公开 API 兼容及相关回归通过。
- [ ] 边界审计失败已修复，或形成明确发布阻断与待确认方案。
- [ ] 干净消费验证完成，正式平台证据和发布状态如实登记。

截至第十七批，本地 .NET 制品与真实 Unity Editor 的独立消费、候选替换及回退已通过；注册表安装、正式历史版本升级和 Player 平台矩阵尚未完成，消费阶段总项继续保留未完成状态。

### 首批实施证据

- 新增 `Unity/Packages/AGENTS.md`，让自有框架包后续修改统一遵循中文多行注释和临时代码审查规则。
- `tools/AbilityKit.PackageQuality` 已通过 6 个注释样式场景和 3 个可见性场景的自检；初始盘点为 81 个源码文件、1188 个声明、720 项注释问题。这是初始清单，不代表当前剩余数。
- Logging 的三个源码文件已完成中文多行契约说明，检查为 0 项问题。明确日志异常隔离、Trace 映射 Info、空接收器默认行为；压缩的异常隔离实现已展开。
- `PoolConfigCenter.TryGetConfig` 已改为直接分支赋值，删除仅用于三元表达式的两个私有返回包装方法，保持成功及失败输出。
- 增加日志路由、接收器故障隔离与默认接收器恢复契约测试；本批 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal` 为 144 项通过、0 失败、0 跳过，日志位于 `local/Logs/core-stabilization/core-tests.log`。其余文件仍存在 XML 注释警告，尚未收紧全包警告门槛。
- 生命周期注册的中文注释继续按单次释放、并发执行权和异常传播的现有实现补齐；其检查结果须以本次工具输出为准。
- 全包注释迁移、目录移动、命名空间超额审查、独立消费和真实平台验证仍待推进；尚未修改发布版本或候选批次状态。三层设置读取优化见下批实施证据。

### 第二批：单次释放与兼容设置读取

- `FlatJsonSettings`、`LayeredJsonSettingsStore` 补齐类型及公开成员的中文多行 XML 注释，明确三层优先级、空值遮蔽、失败输出、区域设置、实时视图与浅快照、通知异常及非线程安全边界；修正 `DisposableRegistration` 类型注释的缩进。
- 分层标量读取改为按覆盖层、持久层、基础层查询一个键，再调用扁平设置内部的转换方法。移除每次读取时合并全量字典并再次复制的冗余工作；没有新增公共 API，也没有改变既有转换算法。`BuildEffectiveView` 保留显式创建独立字典浅快照的行为。
- 新增 14 项分层设置测试，覆盖优先级、移除后的回退、空值与不支持值的遮蔽、空键和缺失键、浮点布尔容差、长整数溢出、浮点截断、区域设置、快照隔离、通知及其异常传播。四类标量读取的预热后测试断言当前线程托管分配为零，不将该局部证据扩展为全包零分配承诺。
- 新增 3 项释放注册测试，覆盖两种重载在回调中再次释放自身，以及带状态回调抛出异常后不再重试。原有并发单次释放测试继续保留。
- `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：161 项通过、0 失败、0 跳过；日志为 `local/Logs/core-stabilization/core-next-batch-tests.log`。构建同时经过既有 RS0016/RS0017 公共 API 门禁，未修改 API 基线。
- 注释检查范围为上述三个源码文件：将原文件复制到 `local/Logs/core-stabilization/next-batch-comment-scope` 后，运行 `dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- local/Logs/core-stabilization/next-batch-comment-scope`，结果为 3 个文件、0 项问题；日志为 `local/Logs/core-stabilization/core-next-batch-comments.log`。这是限定范围的检查，不能视为全包已统一。
- `Config` 和 `Reflection` 不符合 Core 后续纳入定位，但已有明确兼容窗口，不在本批直接删除或建立新的框架配置抽象。正式使用方应继续依赖所属应用包；移除旧入口须单列主版本迁移方案。
- README 删除实际已不存在的 `Numerics` 目录描述，并明确 DebugDraw 的正式能力已经迁至 Diagnostics；旧接口保留至既定兼容窗口。

### 第三批：事件契约、重入修复与目录统一

- `Runtime/Event` 的四个源码连同原 `.meta` 合并到既有 `Runtime/Eventing`，五个事件源码集中维护。四个源码资产 GUID、源码与元数据移动前后的 SHA-256 均核对一致，既有 Eventing 目录元数据保持；无外部资产引用的旧空目录及目录元数据已移除。迁移记录为 `local/Logs/core-stabilization/core-event-directory-move.json`。
- 五个事件源码的类型及公开成员采用中文多行契约注释，覆盖同步线程、优先级、快照变更、空值、运行时键值哈希、参数所有权、异常隔离及全局实例的生命周期边界。随包契约见 [同步事件契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/Eventing.md)。
- 修复一次性订阅在内层发布已经执行后，仍被外层旧快照再次调用的问题。执行状态在回调前记录，异常后也不重试；这恢复最多执行一次的既定契约，不为普通回调增加全局递归阻断，也不改变普通快照的退订行为。
- 新增 12 项事件回归，覆盖字符串与整数入口、回调异常、快照中退订与新增、回调内清空及替代通道、空引用事件名的参数所有权、释放失败及参数类型隔离。原有单监听器重入测试继续保留。
- 迁移后执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：173 项通过、0 失败、0 跳过；日志为 `local/Logs/core-stabilization/core-event-batch-final-tests.log`。构建经过既有公共 API 门禁，Shipped/Unshipped 文件未修改。
- `dotnet test src/AbilityKit.Triggering.Tests/AbilityKit.Triggering.Tests.csproj --no-restore --verbosity minimal`：26 项通过、0 失败、0 跳过；日志为 `local/Logs/core-stabilization/core-event-batch-triggering-tests.log`。
- 直接检查正式源码目录：`dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Eventing`，5 个文件、0 项问题；日志为 `local/Logs/core-stabilization/core-event-batch-final-comments.log`。未将局部检查记作全包完成。
- 文档及本机 Unity 生成工程的源码路径同步到 Eventing；本机工程文件未纳入版本管理。跨包事件设计中旧的 once 调用后移除、RemoveAt 误删及句柄清空 dispatcher 的说明已按实际实现修正。

### 旧 Core 命名空间超额的定位与迁移建议

本轮重新执行 `tools/audit_core_boundaries.ps1`，仍因 Collision 6/5、Record 57/56 失败；日志为 `local/Logs/core-stabilization/core-event-batch-boundaries.log`。保留原门禁和上限。

| 新增声明所在文件 | 当前归属问题与使用方 | 建议目标 |
|---|---|---|
| Collision 的 `Runtime/Math/IColliderBoundsSource.cs` | 在 `AbilityKit.Core.Mathematics` 下声明公开接口 `IColliderBoundsSource` 和 `ColliderBounds`；NaiveCollisionWorld、GridCollisionWorld 及 Projectile/AreaWorld 使用。文件新增于 `34595f4fd`。 | `AbilityKit.Combat.Collision` |
| Record 的 `Runtime/Record/FrameRecord/Analysis/CanonicalStateDiffAnalyzer.cs` | 在 `AbilityKit.Core.Recording.FrameRecord` 下声明公开分析器及规范状态 DTO；Record.Tools、Record.Tests、MOBA 状态导出器及其测试使用。文件新增于 `3b1cc05d5`。 | `AbilityKit.Record.FrameRecord` |

建议只将这两处新增类型及其消费者迁回所属领域，不向 Core 增加碰撞边界、规范状态 DTO 或分析器。公开完全限定名的变化需要按兼容性变更评审，明确升级及移除窗口；验收应覆盖碰撞与区域行为、Record 分析工具、规范状态序列化字段及版本，以及 MOBA 状态导出结果。

本批完成定位，未修改这些领域公开类型及消费者。若发布前采用统一更名，应列清所有公开符号和使用方后同步升级；若旧名称必须继续兼容，则先明确迁移窗口，保留发布阻断。不得通过搬入旧文件或扩大白名单掩盖新增的命名空间归属问题。

上述第三批完成时建议推进的 `Collections` 与 `Buffers` 已在下述第四批落实；两个领域命名空间迁移仍单列兼容性评审。

### 第四批：集合、缓冲容量与引用所有权

- `Runtime/Collections` 两个源码文件和 `Runtime/Buffers` 四个源码文件的类型、公开成员及实现说明统一为中文多行注释。明确稳定决胜、首次匹配、回调异常、完整整数帧键、严格裁剪边界、无效容量、逻辑容量与物理空间、引用保留及非线程安全范围。
- 核对实际实现后，更正 `RingFrameIndexedBuffer` 原先声称单调追加整体为常数时间的注释：存储仍先执行二分定位，只有末尾写入和头部淘汰无需搬移。不修改帧存储算法或公开 API。
- 临时代码检索未发现 Collections/Buffers 中的 TODO、FIXME、HACK 或占位实现。列表后端存在追加后立即裁剪的冗余扩容，改为满容量先移除最旧项再追加；扩容时先准备底层空间再提交逻辑容量，避免分配失败后提前改变逻辑上限。保留有职责的区间清理与查找辅助方法，以及自定义数组池返回非法数组时的防御性检查。
- 新增 14 项直接测试：两种方向在极端优先级及更新后的稳定顺序、匹配回调异常、列表首次满容量追加及扩容后首次满容量追加的分配断言、共享引用及空引用值、24 条帧/顺序缓冲路径的弱引用回收与不自动 Dispose、四种数组清理组合、归还异常及重入、非法池结果与异常身份。沿用已有 5000 步参考模型比较、整数极值、回绕、反复容量调整和非法索引测试。
- `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：187 项通过、0 失败、0 跳过，日志为 `local/Logs/core-stabilization/core-buffers-batch-tests.log`；构建经过既有 RS0016/RS0017 公共 API 门禁，未修改 Shipped/Unshipped 基线。本批源码未产生 XML 文档警告，其余目录仍有既有注释债务。
- `dotnet test src/AbilityKit.World.StateSync.Tests/AbilityKit.World.StateSync.Tests.csproj --no-restore --verbosity minimal`：42 项通过、0 失败、0 跳过，日志为 `local/Logs/core-stabilization/core-buffers-batch-statesync.log`。
- `dotnet test src/AbilityKit.Network.Runtime.Tests/AbilityKit.Network.Runtime.Tests.csproj --no-restore --filter 'FullyQualifiedName~AdaptiveNetworkBufferingTests' --verbosity minimal`：12 项通过、0 失败、0 跳过，日志为 `local/Logs/core-stabilization/core-buffers-batch-network.log`。此为定向回归，不代表完整网络测试套件。
- 对两个正式源码目录分别执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- <目录>`：Collections 2 个文件、Buffers 4 个文件均为 0 项问题，日志分别为 `local/Logs/core-stabilization/core-collections-batch-comments.log` 和 `core-buffers-batch-comments.log`。
- 新增随包文档 [集合、缓冲区与数组所有权契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/CollectionsAndBuffers.md)，同步 README、文档索引、准入文档和 CHANGELOG。说明 `IsDisposed` 表示访问权已撤销，不能证明自定义数组池已成功接收归还；池的归还异常传播且不重试。
- 本批未变更资源路径、程序集或公开符号。Core 这两组原语符合基础存储与所有权定位；网络窗口策略、逐元素回收、深复制和调度继续由领域调用方负责。

上述第四批建议推进的 `Pooling` 已在下述第五批落实。领域命名空间迁移、全包 XML 注释收紧、干净消费及 Unity/IL2CPP 平台证据仍为独立待办；第四批未重新执行领域边界审计，不把第三批已记录的失败标为解决，也未提升发布候选状态。

### 第五批：对象池生命周期、作用域及配置诊断

- 覆盖 `Runtime/Pooling` 的 27 个 C# 文件，统一类型及公开成员中文多行注释，补齐实现说明。核对引用身份校验、回调顺序、锁内状态与锁外回调、异常传播、预热容量预留、统计提交、值类型句柄、作用域、配置胜出规则和诊断采样的真实边界。
- 修复三处既定契约问题：构造预热失败时清理此前成功预热的对象；溢出销毁期间保留同一对象的转换标记，防止回调重新归还已承诺销毁对象；无参 `PoolManager.TrimAll` 改为调用每个池自己的无参 `Trim`，恢复各池配置策略。仅增加内部控制接口成员，公共签名、默认参数与 API 基线保持。
- 新增 13 项直接测试，覆盖构造工厂/回调失败及清理聚合、溢出销毁重入、各池裁剪策略、归还失败后的所有权与重试、运行时预热部分成功和预留恢复、获取及两个销毁回调异常身份与执行顺序、NeverTrim/强制裁剪、KeepAll 历史语义、值类型句柄副本和作用域释放失败后不重试。三处修复对应的首轮 5 项回归在修复前全部失败，日志为 `local/Logs/core-stabilization/core-pooling-before-fixes.log`。
- `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：200 项通过、0 失败、0 跳过；日志为 `local/Logs/core-stabilization/core-pooling-final-tests.log`。构建经过既有 RS0016/RS0017 门禁，Shipped/Unshipped 未修改。Pooling 未出现 XML 文档警告；Math 等其余区域仍有既有债务，未收紧全包文档警告门槛。
- `dotnet test src/AbilityKit.Triggering.Tests/AbilityKit.Triggering.Tests.csproj --no-restore --verbosity minimal`：26 项通过、0 失败、0 跳过；日志为 `local/Logs/core-stabilization/core-pooling-triggering-tests.log`。该包实际通过 `TriggeringRuntimePools` 使用 Core 池与作用域，本批未修改其源码。
- `dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Pooling`：27 个文件、0 项问题；日志为 `local/Logs/core-stabilization/core-pooling-final-comments.log`。本次为 Pooling 范围证据，不表示全包完成。
- 剥离注释后的源码差异检查覆盖 27 个文件；相对 HEAD 只有本批 `ObjectPool`、`PoolManager`、内部控制接口，以及首批已记录的 `PoolConfigCenter` 私有返回包装清理存在代码差异。审查记录为 `local/Logs/core-stabilization/core-pooling-code-audit.json`。TODO、FIXME、HACK、临时、占位及调试输出检索未发现待删除实现；正式回调清理、异常聚合和弱引用归还登记均有明确职责，保留。
- 新增随包文档 [对象池生命周期与配置契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/Pooling.md)，同步 README、文档索引、边界文档与 CHANGELOG。纠正不可变模块、按优先级查询、句柄安全注销等过强措辞：配置链实际在锁内按注册顺序查询全部提供者，以优先级与注册顺序选择胜出者；模块暴露内部字典的只读接口；注册按引用身份去重，不具有引用计数或代次隔离。
- Pooling 仍属于现有兼容基础设施，不在本批扩大职责。池配置只提供创建参数，不接管应用持久化、序列化、资源加载或热更新。`PooledObject<T>` 不具备重复释放幂等性；作用域释放不强制回收借出对象、不使原始池引用失效，也不提供并发关闭屏障，均已按真实行为说明。

#### 保留的契约评审项与下一批

`PoolTrimPolicy.KeepAll` 当前与 `KeepDefaultCapacity` 使用相同值，仍会裁剪到默认容量。名称与行为有冲突，但直接改为保留所有空闲对象会改变已发布行为；本批补文档和兼容断言，后续单列更名/弃用或语义调整方案。值类型句柄和配置注册代次隔离同样不在工程整理中增设新机制。

下一批建议整理 `Runtime/Math` 的六个数学原语文件：优先核对单位、零向量与零四元数、退化输入、插值边界、浮点容差、矩阵/旋转顺序及跨端确定性适用范围，补中文多行注释和针对性契约测试。不要把浮点数学工具宣传成定点锁步能力，也不在注释整理中重写数值算法。Generic/Identifiers 的目录命名统一、剩余兼容源码、领域命名空间迁移、干净消费及 Unity/IL2CPP 平台证据继续独立推进。本批未重跑领域边界审计、未将既有失败标为解决，未变更版本或发布候选状态。

### 第六批：浮点数学契约与退化输入

- 整理 Runtime/Math 六个文件，初始盘点为 114 个声明、119 项注释问题，最终中文多行注释检查为六个文件、零项问题。公开成员逐项说明实际语义，包含运算符、字段、构造、转换及相等比较。
- 核对本批开始时备份，去掉注释后的计算代码完全一致；未调整算法、阈值、表达式求值顺序、数据布局、命名空间或资源路径。记录为 local/Logs/core-stabilization/core-math-code-audit.json。
- 新增 33 项数值契约测试，覆盖绝对容差边界、NaN 和有符号零、反序限制区间、插值端点、定点开方分辨率和回退、向量归一化、相等与 Numerics 转换、弧度与乘法顺序、四种矩阵转换分支、退化朝向、非单位及零四元数、负缩放、逆缩放阈值、一般逆变换反例及射线参数。
- 执行 dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal：233 项通过、零失败、零跳过。日志为 local/Logs/core-stabilization/core-math-final-tests.log。构建经过既有 RS0016/RS0017 门禁；公开基线保持不变。
- 执行 dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Math：六个文件、零项问题；日志为 local/Logs/core-stabilization/core-math-final-comments.log。该范围不包含 Runtime/Mathematics 桥接类型，也不表示全包已统一。
- 新增随包 [数学原语契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/Mathematics.md)，同步 README、文档索引、边界及 CHANGELOG。保留局部整数开方事实，修正原注释对全部模拟数学跨端不漂移的过强推断。
- 实现审查保留四元数转换分支、局部逆缩放及退化回退；这些代码承担既有数值职责，不因重复外观直接合并，避免改变浮点求值或兼容结果。检索未发现 TODO、FIXME、HACK、临时占位或调试输出残留；审查结果为 local/Logs/core-stabilization/core-math-temporary-code-review.json。

- 执行 dotnet test src/AbilityKit.Combat.Collision.Abstractions.Tests/AbilityKit.Combat.Collision.Abstractions.Tests.csproj --no-restore --verbosity minimal：13 项通过、零失败、零跳过。该包真实使用 Transform3、Ray3 及向量原语；日志为 local/Logs/core-stabilization/core-math-collision-tests.log。

#### 保留项与下一批

TransformDirection 包含缩放，默认四元数不同于恒等旋转，极小长度会受定点量化影响；这些现有边界已明确。Transform3.Inverse 在非均匀缩放与旋转不可交换时不能表示一般精确逆变换，应使用直接 InverseTransformPoint/Direction 逐次逆变换；一般逆矩阵、可逆性返回值及相应公开契约单列评审。

下一批建议整理稳定标识与单调时间：核对哈希黄金向量、注册表冲突及默认值、时间单位和回退边界，再评估 Generic 到 Identifiers 的物理目录统一并保持 Unity GUID。桥接类型和 Math/Mathematics 目录统一、剩余兼容源码、全包文档警告、领域命名空间超额、干净消费及 Unity/IL2CPP 证据继续单列。本批未重跑领域边界审计，未提升发布候选状态。

### 第七批：稳定标识与单调时间

- 覆盖 Runtime/Generic 的 StableHashV1、StableStringIdRegistry 与 Runtime/Timing 的 MonotonicTime 三个源码文件，补充中文多行 XML 契约和实现块注释。明确原始代码单元与 UTF-8 字节编码、非法代理项替换、局部冲突、零及负标识、查询失败输出、线程安全、时间源、单位、取整及溢出。
- 修改前定向运行 StableHashTests 与 MonotonicTimeTests：22 项通过、零失败、零跳过；日志为 local/Logs/core-stabilization/core-identifiers-timing-baseline-tests.log。初始注释检查只覆盖 Generic 两个文件，不将这一结果误记为三个文件；最终分别对 Generic 与 Timing 执行检查。
- 新增 IdentifiersAndTimingContractTests 的 28 项边界测试，覆盖 UTF-8 编码宽度黄金向量、未配对代理项与后续合法代理对、非负入口的空引用、重复注册、未知查询默认输出、真实零标识、负标识、真实哈希冲突的两个注册顺序、冲突拒绝后的双向状态、按序号区分大小写、不做 Unicode 规范化及注册顺序独立性。
- 时间换算以 BigInteger 的独立整数公式为参照：ToMilliseconds 覆盖 63 组输入，包含正负极值、向零截断、十进制余数路径及抛出溢出；DurationToTimestampTicks 覆盖 49 组输入，包含极端频率、向上取整以及整秒与小数贡献的饱和路径。另覆盖非法频率、零时长仍校验频率、负时长和校验顺序，以及静态读取与共享时钟的 Stopwatch 来源。
- 执行 dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal：261 项通过、零失败、零跳过。日志为 local/Logs/core-stabilization/core-identifiers-timing-final-tests.log。构建经过既有 RS0016/RS0017 门禁，Shipped 与 Unshipped 未修改；未放宽可空性或全包文档警告策略。
- 分别执行 dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Generic 和 Runtime/Timing：两个与一个源码文件均为零项注释问题。日志分别为 local/Logs/core-stabilization/core-identifiers-comments.log 与 core-timing-comments.log；不表示全包注释统一已完成。
- 执行 dotnet test src/AbilityKit.Triggering.Tests/AbilityKit.Triggering.Tests.csproj --no-restore --verbosity minimal：26 项通过、零失败、零跳过。Triggering 真实使用非负 UTF-16 哈希和单调时间进行标识与执行跟踪，日志为 local/Logs/core-stabilization/core-identifiers-timing-triggering-tests.log；本批未修改使用方源码。
- 对照本批开始时备份，三个源码文件剥离 XML 文档、实现块注释及空行后完全一致，未改变算法、求值顺序、异常规则、冲突规则、公开签名或路径。严格核验结果含非空比较长度及 SHA256，记录为 local/Logs/core-stabilization/core-identifiers-timing-code-audit.json；公开 API 与 Unity 元数据差异检查通过。
- 临时代码检索未发现 TODO、FIXME、HACK、临时占位或调试输出；代理项替换、冲突拒绝、十进制余数换算及饱和分支各自承担既有正式契约，保留实现。记录为 local/Logs/core-stabilization/core-identifiers-timing-temporary-code-review.json。
- 新增随包 [稳定标识与单调时间契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/IdentifiersAndTiming.md)，同步包 README、文档索引、CoreBoundary 和 CHANGELOG。详细行为集中在随包契约文档，边界文档保留版本治理、准入与领域所有权要求。
- 本批三个原语均符合 Core 的独立基础设施定位，没有新增名称管理策略、冲突自动消解、宿主调度或模拟时间机制。Generic 是既有物理目录，本批保持原路径和 GUID；目录命名统一仍按单独兼容项处理，不新增职责不明的兜底目录。

#### 保留项与下一批

下一批建议聚焦 Runtime/Mathematics/DeterministicMathBridge.cs。只读盘点为一个源码文件、17 个声明、14 项注释问题，清单为 local/Logs/core-stabilization/core-mathematics-next-batch-inventory.json。需核对标量与向量的非有限输入处理差异、定点表示范围、量化、零向量和零四元数、三角及指数函数定义域，并修正把局部定点桥接推断为所有浮点运算跨运行时位一致的过强说明。优先补契约、中文多行注释和针对性测试，保持既有算法与公开 API；Math/Mathematics 目录合并单独评估。

剩余兼容源码、全包文档警告、领域命名空间超额、独立干净消费、Unity/Mono/IL2CPP 平台证据和正式发布评审继续单列。本批未重跑领域边界审计、未把历史失败标为解决，未变更包版本或提升发布候选状态。

### 第八批：定点数学桥接的边界与确定性适用域

- 覆盖 Runtime/Mathematics/DeterministicMathBridge.cs 一个源码文件，原盘点为 17 个声明、14 项注释问题；本批补齐全部公开入口的中文多行 XML 注释，说明转换、量化、范围、单位、退化回退及异常传播。移除“有限值精确换算”“其余浮点运算本身跨平台位一致”以及未经平台验证的完整确定性承诺。
- 修改前运行 Core 全量测试：261 项通过、零失败、零跳过，日志为 local/Logs/core-stabilization/core-bridge-baseline-tests.log。备份位于 local/Logs/core-stabilization/bridge-before/DeterministicMathBridge.cs。
- 新增 DeterministicMathBridgeContractTests 的 44 项测试，覆盖标量转换黄金原始计数、向零量化、非有限值在标量与严格几何入口中的差异、定点转换范围、浮点视图丢位及转回溢出、坐标轴顺序、Epsilon 实际原始计数、平方和量化退化、向量零与四元数恒等回退、分量乘积与求和溢出、严格开方、弧度与 Atan2 参数及负零规则、指数下溢与溢出、对数量化定义域、整数及分数幂和中间溢出。
- 本批特别固定两项易误写的兼容边界：Pow(2, -63) 会因中间整数幂溢出失败；最小负定点底数 -2147483648 的整数幂在取绝对值时溢出，零指数也不例外。不在注释整理中以数学恒等式重写内核分支或改变异常。
- 执行 dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal：305 项通过、零失败、零跳过；日志为 local/Logs/core-stabilization/core-bridge-tests.log。构建经过既有 RS0016/RS0017 门禁，公开基线未修改；本文件不再出现 XML 文档警告，其余兼容区域仍有既有债务。
- 执行 dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Mathematics：一个源码文件、零项注释问题，日志为 local/Logs/core-stabilization/core-bridge-final-comments.log。该证据不代表全包注释检查完成。
- 分别执行 dotnet test src/AbilityKit.Combat.Collision.Abstractions.Tests/AbilityKit.Combat.Collision.Abstractions.Tests.csproj 与 src/AbilityKit.Combat.Motion.Tests/AbilityKit.Combat.Motion.Tests.csproj，均加 --no-restore --verbosity minimal：碰撞 13 项、运动 8 项通过，零失败、零跳过。实际使用方覆盖转换、长度、方向归一化和角度函数，日志分别为 local/Logs/core-stabilization/core-bridge-AbilityKit.Combat.Collision.Abstractions.Tests.log 与 core-bridge-AbilityKit.Combat.Motion.Tests.log。
- 相对批次开始备份，剥离注释与空行后的源码完全一致，比较 2258 个非注释字符；记录为 local/Logs/core-stabilization/core-bridge-code-audit.json。公开 API、包版本、源码路径、Unity 元数据及定点内核均未修改，兼容检查记录为 core-bridge-api-meta-kernel-diff.log。
- 临时代码检索未发现 TODO、FIXME、HACK、临时占位或调试输出。不同入口的归零守卫与严格转换是既有行为差异；零长度回退、逐入口内核委托和显式分量顺序均保留，不因外观重复而合并。审查为 local/Logs/core-stabilization/core-bridge-temporary-code-review.json。
- 新增随包 [定点数学桥接契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/DeterministicMathBridge.md)，同步 README、文档索引、数学原语文档、CoreBoundary 及 CHANGELOG。桥接为多个框架包复用的已有窄范围适配，符合当前 Core 定位；领域范围校验、完整模拟数值链与未来拆包依赖闭包不在本批扩展。
- 最终检查汇总为 local/Logs/core-stabilization/core-bridge-final-review.json。本批保持 Math 与 Mathematics 现有路径；目录统一仍按独立兼容事项评估，未宣称所有能力或所有目标平台稳定。

#### 依赖评审发现与下一批

只读核对 Fixed64 发现 NET7_0_OR_GREATER 使用 Int128 乘积右移，其他目标使用 decimal 除以 2^32 后转 long。精确负小数乘积的截断方向不同：原始计数 -1 与 1 相乘，前者得到 -1，后者得到零。另需核对 decimal 中间范围与其他内核分支的目标差异。这是定点依赖的跨目标算术兼容问题，不能由当前 .NET 测试推断 Mono 或 IL2CPP 一致；统一舍入、溢出、黄金向量与持久化影响需先形成单独方案，再决定数值行为。当前文档已经收回过强承诺，本批没有修改内核。

下一批 Core 整理建议推进 Runtime/Continuous 的弃用兼容面：只读盘点为 7 个源码文件、139 个声明、39 项注释问题，清单位于 local/Logs/core-stabilization/core-continuous-next-batch-inventory.json。重点核对管理器更新顺序、变更与重入、生命周期回调、时长和层级/互斥/叠加策略的现有契约，统一中文多行注释并补有意义的兼容测试。该能力已迁往 com.abilitykit.continuous，不新增 Core 使用方，不直接删除兼容入口，也不把工程整理变成领域机制扩展。

全包文档警告收紧、其他兼容源码、领域命名空间超额、独立干净消费、Unity/Mono/IL2CPP 证据及正式发布评审仍未完成。本批未重跑领域边界审计，未将历史失败标为解决，未更新版本或提升发布候选状态。

### 第九批：Continuous 弃用兼容面与迁移差异

- 覆盖 Runtime/Continuous 七个源码文件，原盘点为 139 个声明、39 项注释问题；本批统一中文多行 XML 契约和实现块注释，补齐标签容器成员及空绑定器说明，展开全部枚举项注释。修正“Core 不提供默认实现”及实现可选配置便自动支持策略的误导描述。
- 修改前运行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：305 项通过、零失败、零跳过；日志为 `local/Logs/core-stabilization/core-continuous-baseline-tests.log`，源码备份为 `local/Logs/core-stabilization/continuous-before/`。
- 新增 ContinuousCompatibilityContractTests 的 37 项直接契约测试，覆盖枚举值、注册顺序及默认相等比较、配置 Id 不去重、准入短路、拒绝原因清理差异、注册绑定器快照、倒序补偿包含失败者、原异常保留、公共注册事件失败不回滚、构造重复项与动态增删、激活失败保留旧状态、重复激活、状态未提交、暂停/恢复部分成功、结束事件缺失、finally 注销及异常覆盖、事件原因透传、只注销不结束、查询实时包装与副本、直接变更造成的索引差异、批处理快照及中断、回调重入、标签策略与可选配置不自动执行。
- 执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：342 项通过、零失败、零跳过；日志为 `local/Logs/core-stabilization/core-continuous-final-tests.log`。构建沿用 RS0016/RS0017 门禁，未修改 Shipped/Unshipped、警告策略或兼容白名单。其他兼容区域的既有 XML 注释债务仍保留。
- 执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Continuous`：七个源码文件、零项注释问题；日志为 `local/Logs/core-stabilization/core-continuous-final-comments.log`。该范围结论不表示全包统一已经完成。
- 执行 `dotnet test src/AbilityKit.Continuous.Tests/AbilityKit.Continuous.Tests.csproj --no-restore --verbosity minimal`：独立包现有三项测试通过、零失败、零跳过，日志为 `local/Logs/core-stabilization/core-continuous-target-package-tests.log`。其中激活绑定器失败后结束并注销的测试与旧 Core 保留激活及登记的断言共同说明迁移差异；独立包不是旧 Core 的生产消费者，此结果不是行为等价或实际项目升级验收的证明。
- 对照批次开始备份，七个源码文件剥离注释、行首尾空白及空行后的代码完全一致，共比较 17865 个非注释字符，逐文件记录 SHA256 与非空长度；审查为 `local/Logs/core-stabilization/core-continuous-code-audit.json`。公开 API、版本、路径及 Unity 元数据保持；检查为 `core-continuous-api-meta-diff.log`。
- 临时代码检索未发现 TODO、FIXME、HACK、临时占位或调试输出；移除无意义单行扩展接口分隔线。正式空绑定器、注册补偿异常隔离、仅限制激活的标签策略及 protected ResolveEndReason 均有兼容职责，保留并说明默认执行链；记录为 `local/Logs/core-stabilization/core-continuous-temporary-code-review.json`。独立包和旧实现存在真实语义差异，不因外观重复合并。
- 新增随包 [Core Continuous 兼容与迁移契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/ContinuousCompatibility.md)，同步包 README、文档索引、CoreBoundary 和 CHANGELOG。文档明确管理器没有 Tick，不自动执行时长、互斥、层级、级联或叠加；新能力继续归属 `com.abilitykit.continuous`，不新增旧 Core 生产使用方。

#### 保留限制与下一批

旧管理器的活动集合与实时状态可不同；TryResume 不自动登记且成功不清空旧拒绝原因；注册以外的通知遍历实时绑定器列表；批处理快照仍可操作已注销项；回调没有通用重入事务屏障。注册回调中注销可使外层 Register 返回成功而登记已撤销。这些实际兼容行为已通过测试与文档固定，不在弃用面整理中增设新机制。若未来需要修复，应在正式能力包中单独明确失败后责任、重入规则和升级影响。

下一批建议整理 `Runtime/Reflection/ReflectionInvokeUtils.cs` 与 `Runtime/DisposeUtils.cs` 两个小范围兼容工具。Reflection 的只读盘点为一个文件、四个声明、四项注释问题，清单为 `local/Logs/core-stabilization/core-reflection-next-batch-inventory.json`；此数量不包含 DisposeUtils。优先核对类型查找和静态调用异常边界、反射包装异常、失败输出、释放异常传播与隔离、finally 清空引用及重入限制，再补中文多行注释、直接测试和迁移说明。释放策略仍由资源所有者负责，广泛反射仍由所属领域的窄契约替代，不新增 Core 使用方或未经验证的 AOT 承诺。

Config/Markers/DebugDraw 等其他兼容源码、全包文档警告收紧、领域命名空间超额、干净消费和 Unity/Mono/IL2CPP 验证继续单列。本批未重跑领域边界审计，未将历史失败标为解决，未变更包版本或提升发布候选状态。

### 第十批：反射与释放工具的异常及所有权边界

- 覆盖 Runtime/Reflection/ReflectionInvokeUtils.cs 与 Runtime/DisposeUtils.cs 两个弃用兼容源码。批次开始备份位于 `local/Logs/core-stabilization/reflection-disposal-before/`，对备份执行注释盘点为两个文件、九个声明、九项问题，清单为 `core-reflection-disposal-before-inventory.json`。本批统一全部类型和公开成员的中文多行 XML 注释，为正式异常隔离补充中文实现块说明。
- 修改前执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：342 项通过、零失败、零跳过；日志为 `local/Logs/core-stabilization/core-reflection-disposal-baseline-tests.log`。
- 新增 ReflectionAndDisposalCompatibilityTests 的 50 项直接测试。反射覆盖空及未知名称、已加载嵌套类型、大小写、闭合泛型、首次程序集解析回调失败的传播与调用入口捕获、返回值丢弃、方法缺失或不符合标记、重载歧义、可选参数与 params 不填充、开放泛型、原始反射包装异常及返回失败 Task 时仍报告调用成功。
- 释放测试覆盖全部四个重载的空引用、成功与重复调用、原异常传播、回调收到原异常且引用尚可见、空回调隔离、回调失败替代原异常、重入重复释放、回调替换引用被 finally 清空而新对象不被释放，以及接口装箱对象与原值副本、外部别名的差异。程序集解析故障回调使用唯一名称过滤并在 finally 中退订，避免污染其他测试。
- 执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：392 项通过、零失败、零跳过，日志为 `local/Logs/core-stabilization/core-reflection-disposal-final-tests.log`。构建沿用 RS0016/RS0017 门禁；未修改 API 基线、可空标注或警告策略。其他兼容区域仍有既有 XML 注释债务。
- 因注释工具只接受目录，本批将两个权威源码复制到 `local/Logs/core-stabilization/reflection-disposal-quality-input/`，逐文件验证字节一致及 SHA256，再执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- local/Logs/core-stabilization/reflection-disposal-quality-input`：两个文件、零项问题，日志为 `core-reflection-disposal-comments.log`。该临时检查目录不参与包交付或 .NET 源码编译，不表示全包检查完成。
- 对照批次开始备份，剥离注释、行首尾空白及空行后的源码完全一致，共比较 2553 个非注释字符；记录为 `local/Logs/core-stabilization/core-reflection-disposal-code-audit.json`。公开 API、包版本、源码路径及目标 Unity 元数据保持；检查为 `core-reflection-disposal-api-meta-diff.log`。
- 在 Unity/Packages、src、tools 的 C# 源码中按两个类型名检索，结果仅包含定义及 Core 兼容测试；日志为 `core-reflection-disposal-callers.log`。本批没有新增生产使用方，也没有将无当前生产引用视作可提前删除兼容 API 的依据。未另跑无相关消费者的跨包测试，不将直接测试表述为真实项目迁移验收。
- 临时代码检索未发现 TODO、FIXME、HACK、临时占位或调试输出。FindType 整体遍历失败返回空值、无诊断调用重载丢弃异常、释放异常回调隔离与 finally 清空均为正式兼容职责，保留；两个重载族不因重复外观合并。记录为 `local/Logs/core-stabilization/core-reflection-disposal-temporary-code-review.json`。
- 新增随包 [反射与释放工具兼容契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/ReflectionAndDisposalCompatibility.md)，同步 README、文档索引、CoreBoundary 与 CHANGELOG。广泛反射发现和资源释放策略不符合 Core 新能力准入方向，继续由领域或资源所有者的窄契约承接。本批不新增通用反射、装配或释放机制。
- 最终检查汇总为 `local/Logs/core-stabilization/core-reflection-disposal-final-review.json`，包含测试、检查副本一致性、源码对比、文档链接、UTF-8 与空白检查。

#### 保留限制与下一批

首次 Type.GetType 与后续程序集遍历的隔离范围不同；前者可能传播解析异常，后者整体遇错返回空值。程序集遍历故障未通过注入穷举，文档仅按源码明确其边界。静态调用不按零参数签名选择重载、不解包异常、不等待任务。释放工具的引用清空发生在回调之后，没有重入或并发防护，可能清空回调写入的新引用；回调异常还会替代原释放异常。这些实际行为已按适用分支测试并明确，不在弃用窗口整理中改变。

下一批建议完成 Config 的四个剩余文件：JsonSettingsFiles、PersistentJsonConfigLoader、ModuleInstallerConfig、ModuleInstallerConfigSet。Config 全目录只读盘点为六个源码文件、61 个声明、18 项注释问题，清单为 `local/Logs/core-stabilization/core-config-remaining-next-batch-inventory.json`；其中包含前批已整理的 FlatJsonSettings 和 LayeredJsonSettingsStore，剩余四个文件共 28 个声明。应核对文件读取/写入失败兜底、写入是否原子、静态缓存键与更新时间、反序列化回调及并发失效边界、配置有效性、重复模块键和默认安装方法，保持领域策略归属与旧 API 兼容窗口。

全包注释警告收紧、其余兼容源码、领域命名空间超额、干净消费、Unity/Mono/IL2CPP 和正式发布评审继续单列。本批未重跑领域边界审计，未将既有失败标为解决，未更新包版本或提升发布候选状态。

### 第十一批：配置文件、对象缓存及模块描述

- 覆盖 Runtime/Config 的 JsonSettingsFiles、PersistentJsonConfigLoader、ModuleInstallerConfig 和 ModuleInstallerConfigSet 四个剩余源码文件。批次开始备份位于 `local/Logs/core-stabilization/config-files-before/`，对备份执行注释盘点为四个文件、28 个声明、18 项问题，清单为 `core-config-files-before-inventory.json`。本批补齐全部类型及公开成员的中文多行 XML 注释，说明私有缓存键和值的职责，为文件与加载失败兜底补中文块注释。
- 修改前执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：392 项通过、零失败、零跳过；日志为 `local/Logs/core-stabilization/core-config-files-baseline-tests.log`。
- 新增 ConfigFileCompatibilityContractTests 的 55 项直接测试。文件工具覆盖空/非法路径、缺失文件与目录、空文本与空回调、原文传递、仅复制字典结构、不缓存、回调返回空及异常、保存参数拒绝、父目录先创建、原字典传入、空输出清空文件、序列化失败保留目录和既有文件、父目录创建失败与目标写入失败的回调边界。
- 缓存测试覆盖空委托绕过已有缓存、可变对象及跨回调复用、完整路径规范化、请求泛型类型参与键、写入时间变化、内容和长度改变但时间不变、空结果缓存、默认对象不缓存、空文件与空白文本差异、解析失败重试、文件删除/重建及旧缓存复现、空文本失败不清除旧项、清空不释放对象、回调清空后外层仍提交、信号控制的并发未命中及后提交覆盖、加载中修改文件以及默认构造失败传播。测试使用唯一临时目录，显式设置并断言文件时间戳；并发测试用信号且有超时和 finally 收尾，不通过 sleep 推测提交顺序。
- 模块描述测试覆盖空键/空类型、空白字符串有效、类型不解析、方法回退和空白保留、忽略空元素及无效配置、首个有效重复键、序号匹配、空查询、空数组以及后续数组和对象变更。对预期不调用的回调，在外部断言调用次数，避免生产兜底 catch 隔离测试异常导致假通过。
- 执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：447 项通过、零失败、零跳过；日志为 `local/Logs/core-stabilization/core-config-files-final-tests.log`。构建沿用 RS0016/RS0017 门禁，没有修改 Shipped/Unshipped、可空标注或警告策略。其他区域仍存在既有 XML 注释债务。
- 执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Config`：全目录六个源码文件、零项注释问题；日志为 `local/Logs/core-stabilization/core-config-files-comments.log`。该范围包含前批已整理的 FlatJsonSettings 和 LayeredJsonSettingsStore；本批保留其既有工作区内容，未重新实现二者，也不表示全包完成。
- 对照批次开始备份，四个源码文件剥离注释、行首尾空白及空行后完全一致，共比较 4643 个非注释字符，记录长度与 SHA256；审查为 `local/Logs/core-stabilization/core-config-files-code-audit.json`。公开 API、包版本、路径、目标 Unity 元数据及旧 UnityJsonSettingsBootstrap 源码保持；检查为 `core-config-files-api-meta-diff.log`。
- 类型名检索保存为 `core-config-files-symbol-references.log`。Core 内旧 UnityJsonSettingsBootstrap 真实调用 JsonSettingsFiles；MOBA 现有测试和应用源码中的同名类型使用 AbilityKit.Demo.Moba.View.Settings 或 AbilityKit.Demo.Moba.Bootstrap，并非旧 Core 使用方。不新增生产使用方，未用同名 MOBA 测试代替旧 Core 回归，未修改 MOBA 或 Unity 适配器。本批 .NET 直接测试不能证明旧 Unity 适配器已在 Unity 编译或运行。
- 临时代码审查保留文件失败返回空设置/假、持久化加载返回空值、私有缓存记录、历史默认方法名和首个匹配规则；这些是兼容职责，不按占位删除。检索唯一命中的“临时文件”仅为注释说明未采用原子保存，并非临时实现。记录为 `local/Logs/core-stabilization/core-config-files-temporary-code-review.json`。
- 新增随包 [配置文件、缓存与模块描述兼容契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/ConfigurationFileCompatibility.md)，同步包 README、文档索引、CoreBoundary 及 CHANGELOG。应用设置持久化和安装策略继续由应用/引导所有者承接，本批未引入原子保存、内容版本、并发失效屏障或新的安装机制。
- 最终检查汇总为 `local/Logs/core-stabilization/core-config-files-final-review.json`，包含测试数、注释范围、源码对比、API 与元数据、文档链接及 UTF-8/空白检查。

#### 保留限制与下一批

保存直接覆盖且不回滚，失败前可以已创建目录或执行回调；缓存仅以完整路径、请求类型及写入时间判断命中，既共享可变对象也缓存空值。回调不参与键，加载在锁外，并发可重复解析、以后提交者覆盖，清空也没有在途提交屏障；默认构造异常不在加载隔离范围内。模块有效性只检查非空字符串，不解析类型或方法。以上保持既有兼容行为，不据此宣称具备应用配置管理或可靠持久化能力。

下一批建议整理 Runtime/Markers 八个源码文件；只读盘点为 82 个声明、18 项注释问题，清单为 `local/Logs/core-stabilization/core-markers-next-batch-inventory.json`。需核对普通/键控注册表的去重、键替换与类型目录差异、查询视图及延迟遍历、实例创建异常、程序集与特性扫描顺序、部分扫描失败、回调变更与重入，以及 MarkerSystem/Bootstrapper 已冻结的全局副作用。同步校正旧 Marker README 的能力承诺，补中文多行注释和必要契约测试，不新增程序集发现或全局启动机制。

DebugDraw 与 Unity 专属兼容源码、全包注释警告收紧、领域命名空间超额、干净消费、Unity/Mono/IL2CPP 和正式发布验收继续单列。本批未重跑领域边界审计，未将历史失败标为解决，未变更版本或提升发布候选状态。

### 第十二批：Marker 登记、扫描与全局引导兼容契约

- 覆盖 Runtime/Markers 八个源码文件；原只读盘点为 82 个声明、18 项注释问题，批次开始备份为 `local/Logs/core-stabilization/markers-before/`。统一全部类型及公开成员的中文多行 XML 注释与实现块说明，纠正键控 Count、实例创建、命名空间扫描特性数量以及实例构造自动登记等旧注释。
- 修改前执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：447 项通过、零失败、零跳过，日志为 `local/Logs/core-stabilization/core-markers-baseline-tests.log`。
- 新增 MarkerCompatibilityContractTests 的 46 项直接契约测试。覆盖正式空回调、基本类型过滤及无特性/开放泛型登记、首次顺序、键覆盖和历史目录、类型登记不增加键、实时视图、延迟 Where、ForEach/Find 回调追加、异常与空委托、默认键比较、缺失查询、每次新建、类型不匹配、值类型装箱、无参构造缺失、构造包装异常与开放泛型创建失败。
- 扫描测试使用可控 Assembly 注入程序集/类型顺序、重复项、空项和 ReflectionTypeLoadException 部分加载，覆盖非继承特性、多个直接特性、特性构造和回调异常、部分成功、已加载程序集名称/排除辅助入口、命名空间文本前缀与首项短路、首个特性语义、引用深度/顺序/简单名称去重及引用枚举和加载隔离。未穷举全部反射失败或跨运行时行为。
- 全局测试覆盖类型/引用/委托去重、实时计数重复求和、浅快照、Reset 不清外部对象、空扫描与零登记、过滤先于扫描、异常包装、回调新增登记或 Reset 的版本影响、同线程嵌套扫描成功后外层失败仍可保留真值。引导测试覆盖实例基类构造中的虚属性、禁用自动登记、空注册表、静态一次、Reset 后不重登记、重写跳过登记及静态初始化失败缓存和部分副作用。新旧全局状态测试采用同一个 xUnit Collection 隔离，未禁用整个程序集并发。
- 最终执行 Core 全量同一命令：493 项通过、零失败、零跳过，日志为 `local/Logs/core-stabilization/core-markers-final-tests.log`。首次测试出现的一项 xUnit2031 已改用 Assert.Single 的谓词重载，最终日志无此警告；没有放宽警告或测试断言。沿用 RS0016/RS0017 公共 API 门禁，不修改 Shipped/Unshipped。
- 执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Markers`：八个源码文件、零项问题，日志为 `local/Logs/core-stabilization/core-markers-comments.log`。全包另作只读盘点：81 个源码文件、1191 个声明、101 项注释问题，记录为 `core-after-markers-inventory.json` 和同名日志；盘点命令成功不代表全包注释门禁通过。
- 临时代码审查后移除三类明确残留：ScanByNamespace 从不读取的 namespaceSet 分配；StaticMarkerBootstrapper 仅赋值从不读取的私有 _registered 字段、赋值及 CS0414 抑制；两个未使用的 using。原静态构造和 Register 调用保留，其副作用测试通过。正式空回调、引用加载 catch 隔离、部分类型加载恢复及已冻结的公开兼容 API 均保留；类型目录和各扫描入口存在语义差异，不因外观重复合并。记录为 `core-markers-temporary-code-review.json`。
- 对照批次开始备份，八个文件剥离注释及行首尾空白后，仅有上述七行非注释内容删除，其他控制流程一致；清理后共 14659 个非注释字符，逐文件记录非空长度和 SHA256，审查为 `core-markers-code-audit.json`。公开 API、包版本、源码路径和目标 Unity 元数据保持，检查为 `core-markers-api-meta-diff.log`；本批没有修改 MOBA、Diagnostics 或消费方运行时。
- 类型名检索在 Unity/Packages、src、tools 的 C# 源码中进行，排除 bin/obj，完整结果为 `core-markers-symbol-references.log`。Targeting、Triggering、World、Samples 和 MOBA 所有者仍有真实引用；没有把缺少全局启动使用方视作提前删除兼容入口的依据，也没有新增生产消费者。
- 执行 `dotnet test src/AbilityKit.Combat.Targeting.Tests/AbilityKit.Combat.Targeting.Tests.csproj --no-restore --verbosity minimal`：69 项通过、零失败、零跳过，日志为 `core-markers-targeting-tests.log`。其中包含特性登记与实例创建，现有测试未直接调用 Scan，不将该结果表述为消费者扫描启动验证。该消费者未修改的 CompositeTargetingTests.cs:147 仍有 CS8601 警告，本批未隐藏或宣称解决。
- 新增随包 [Marker 注册与扫描兼容契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/Markers.md)，旧 Runtime README 改为源码导航并纠正命名空间，移除“所有 Attribute 必须继承”、自动代码生成及“零运行时开销”等过强描述；同步包 README、索引、CoreBoundary 与 CHANGELOG。特性、注册表、扫描器当前并非全部有 Obsolete 标记；全局 MarkerSystem 与三个引导基类明确弃用冻结，新发现策略继续归所属所有者。
- 最终检查汇总为 `local/Logs/core-stabilization/core-markers-final-review.json`，包括测试、注释、允许的私有清理、API 与元数据、文档新链接及 UTF-8/空白检查。

#### 保留限制与下一批

键控表保留被替换的历史类型且 Count 只统计键；Types 暴露实时列表接口；普通遍历可能接纳回调新增项；实例创建无缓存。基础扫描处理全部直接特性，命名空间扫描仅首个，引用深度边界仍可能加载不扫描的程序集。全局锁不形成启动事务，嵌套扫描可使外层失败后初始化标记仍为真，Reset 不阻止旧快照继续产生副作用；静态引导 Reset 后不能重启。这些兼容限制已明确，不以整理名义增加新的发现或全局启动机制。

下一批建议整理 Runtime/DebugDraw 与 Editor/DebugDraw 四个源码文件，共 65 个声明、57 项注释问题（Runtime 一个文件、35 个声明、35 项问题；Editor 三个文件、30 个声明、22 项问题）。清单为 `local/Logs/core-stabilization/core-debugdraw-next-batch-inventory.json`，分目录检查日志为 `core-debugdraw-runtime-next-batch-inventory.log` 和 `core-debugdraw-editor-next-batch-inventory.log`。应核对旧值类型、空绘制器与默认入口、编辑器状态持久化、SceneView 事件订阅/退出和现有 Diagnostics 所有权，明确哪些仅兼容而不可作为正式能力。保留已有元数据与 API，不新建 Core 绘制机制；Editor 实际验证需单列，不能由 .NET 通过替代。

剩余 UnityJsonSettingsBootstrap 等源码、全包文档警告收紧、领域命名空间超额、独立干净消费、Unity/Mono/IL2CPP 与正式发布评审仍未完成。本批未重跑领域边界审计，未将历史失败标为解决，未更新版本或提升发布候选状态。

### 第十三批：旧 DebugDraw 值契约与编辑器兼容边界

- 覆盖 Runtime/DebugDraw/DebugDrawTypes.cs 与 Editor/DebugDraw 的 HandlesDebugDraw、DebugDrawEditorSettings 和 DebugDrawSceneViewDriver 四个源码。原盘点为 65 个声明、57 项注释问题；批次开始源码备份位于 `local/Logs/core-stabilization/debugdraw-before/`。全部类型和公开成员补中文多行 XML 注释，英文编辑器说明改为中文块注释，并说明正式异常隔离的边界。
- 修改前执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：493 项通过、零失败、零跳过，日志为 `local/Logs/core-stabilization/core-debugdraw-baseline-tests.log`。
- 新增 DebugDrawCompatibilityContractTests 的 12 项直接兼容测试，覆盖任意整数/符号位掩码、同类型装箱相等、其他类型与空值不相等、哈希与字典键、None/All、当前区域设置的负号文本、命名颜色与显式 Alpha、默认透明黑与命名 Default 的区别、颜色与上下文值复制，以及全部六个运行时类型的完整 Obsolete 消息和非错误级别。
- 执行同一 Core 全量测试命令：505 项通过、零失败、零跳过，最终日志为 `local/Logs/core-stabilization/core-debugdraw-final-tests.log`，未出现新增测试警告。沿用 RS0016/RS0017 公共 API 门禁，没有修改 Shipped/Unshipped、可空标注或警告策略。
- 分别对 Runtime/DebugDraw 和 Editor/DebugDraw 执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-build --no-restore -- <目录>`：一个运行时源码、三个编辑器源码均为零项注释问题，日志为 `core-debugdraw-runtime-comments.log` 和 `core-debugdraw-editor-comments.log`。该检查评估注释和语法格式，不编译 Unity API，不作为编辑器生命周期验收。
- 全包只读盘点为 81 个源码、1191 个声明、44 项注释问题，较上批减少 57 项；记录为 `core-after-debugdraw-inventory.json` 和同名日志。全包普通检查的失败项保存在 `core-after-debugdraw-comments.log`，集中为 Observability 的 38 项和 UnityJsonSettingsBootstrap 的六项；盘点返回成功不意味着全包门禁通过。
- 四个源码剥离注释、行首尾空白及空行后完全一致，共比较 8188 个非注释字符，逐文件记录非空长度和 SHA256；审查为 `core-debugdraw-code-audit.json`。公开 API、弃用消息、默认值、包版本、源码路径、两个目标 asmdef 以及 Unity 元数据保持；检查为 `core-debugdraw-api-meta-diff.log`。本批不修改 Diagnostics、MOBA 或其他使用方运行时。
- 临时代码审查未发现可确认删除的私有残留，不为清理数量而改动实现。旧 Draw catch 是正式静默隔离，公开但不由驱动读取的预算、碰撞颜色及层掩码仍是兼容 API；补明限制而不直接删除或增加其执行机制。目标源码没有空绘制器、默认全局运行时绘制入口或磁盘持久化，不因前批核对计划提到这些可能项便补造实现。审查为 `core-debugdraw-temporary-code-review.json`。
- 旧命名空间精确检索在 Unity/Packages、src、tools 的 C# 源码中进行并排除 bin/obj；记录为 `core-debugdraw-legacy-usings.log`，结果仅有定义、旧 Core Editor 自用及 Core 兼容测试。广泛符号线索另存 `core-debugdraw-symbol-references.log`，其中其他领域同名 CollisionLayerMask 不是旧设置消费者。没有当前生产引用不等于可提前删除兼容 API；不新增旧入口消费者。
- 对照 Diagnostics 正式源码，明确两者是不同类型和独立静态状态，旧 Draw catch 不记录异常而正式驱动调用 Debug.LogException。执行 `dotnet test src/AbilityKit.Diagnostics.Tests/AbilityKit.Diagnostics.Tests.csproj --no-restore --verbosity minimal --filter FullyQualifiedName~DebugDrawTypesTests`：目标包既有三项绘制值类型测试通过、零失败、零跳过，日志为 `core-debugdraw-diagnostics-tests.log`。这是目标包核对，不是旧 Editor 消费方回归或迁移行为等价证明；该测试工程编译仍有未修改范围的既有可空性和 xUnit1031 警告，本批未隐藏或宣称解决。
- 新增随包 [旧 DebugDraw 兼容与迁移契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/DebugDrawCompatibility.md)，同步包 README、随包索引、CoreBoundary 与 CHANGELOG。文档明确域内设置非持久化、初始化事件订阅与无公开停止入口、播放状态不清登记、重绘请求和确切呈现的区别、实时列表变更、Mask 在 try 外、作用域恢复范围、非有限输入与胶囊非完整轮廓的限制。
- 最终检查汇总为 `local/Logs/core-stabilization/core-debugdraw-final-review.json`，包含测试与警告范围、目标注释结果、去注释一致性、API/asmdef/元数据、UTF-8、链接及空白检查。

#### 保留限制与下一批

旧驱动仅编辑器主线程使用，域重载行为取决于 Unity 配置，设置不写入 EditorPrefs 或资产；绘制遍历不是快照，回调新增可能进入同轮，移除当前项可能跳过后移项。Mask 属性异常中断遍历，Draw 异常静默隔离；不存在预算强制、全局运行时绘制器或通用重入屏障。球体由三个圆盘近似，胶囊只有平行边与端面圆盘；这些诊断图形不作为玩法几何依据。已补真实契约，未借兼容整理新增持久化、绘制或生命周期机制。

Core 的 .NET 工程不编译 Editor 三文件。本批没有在真实 Unity 中编译或运行，没有以自制 Unity 桩模拟其验收；真实域重载/禁用域重载、事件订阅/退出、播放状态重绘及 Handles 几何仍需单列验收。不将注释工具或目标包值类型测试表述为已完成这些验证。

下一批建议整理 Runtime/Observability/ObservationPrimitives.cs：只读盘点为一个源码文件、39 个声明、38 项注释问题，清单为 `local/Logs/core-stabilization/core-observability-next-batch-inventory.json`。核对三个值引用的默认值、负数/零及代次有效性、构造不校验与 IsValid 的区别、相等/哈希和标识所有权，以及同步 sink 的接受/启用契约与正式空 sink。同步审查实际使用方和 Core 定位，统一中文多行注释并补有意义的直接测试，不增加观测调度或领域事件策略。其后仍有旧 Unity 设置适配器一个文件、六个声明、六项注释问题，清单为 `core-unity-next-batch-inventory.json`，不把该平台适配器计入已完成范围。

全包文档警告收紧、领域命名空间超额、独立干净消费、Unity/Mono/IL2CPP 和正式发布评审仍未完成。本批未重跑领域边界审计，未将历史失败标为解决，未修改版本或提升发布候选状态。

### 第十四批：观测关联原语契约与职责准入复核

- 覆盖 Runtime/Observability/ObservationPrimitives.cs 一个源码文件，原盘点为 39 个声明、38 项注释问题；批次开始备份位于 `local/Logs/core-stabilization/observability-before/`。五个紧密关联类型仍保留同文件，统一全部类型及公开成员的中文多行 XML 注释，纠正构造方法会保证有效参数的旧描述，明确值有效性、身份和生命周期的差别。
- 修改前执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：505 项通过、零失败、零跳过；日志为 `local/Logs/core-stabilization/core-observability-baseline-tests.log`。
- 新增 ObservationPrimitivesContractTests 的 26 项直接测试：运行键七组、定义引用六组和关联引用七组边界向量，覆盖原样保留无效组合、零和负值、最小/最大整数及代次、ParentId 不参与有效性、循环/自关联不校验。另覆盖默认结构体和可选参数、三种值的全部身份字段、无效值不折叠、同类型装箱/空值/其他类型、运算符及字典键一致性，以及空 sink 的封闭泛型实例边界、引用载荷、事件相等/哈希/格式化不被执行及 128 次并发拒绝。原六项 ObservationPrimitivesTests 保留，不写测试 sink 来冒充接口提供原子性或失败隔离。
- 最终执行相同 Core 全量命令：531 项通过、零失败、零跳过，日志为 `local/Logs/core-stabilization/core-observability-final-tests.log`；该最终日志无警告。沿用公共 API RS0016/RS0017 门禁，没有修改 Shipped/Unshipped、可空标注或警告策略。
- 执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-build --no-restore -- Unity/Packages/com.abilitykit.core/Runtime/Observability`：一个源码文件、零项注释问题，日志为 `core-observability-comments.log`。全包只读盘点为 81 个源码、1191 个声明、六项问题，较上批减少 38 项，记录为 `core-after-observability-inventory.json` 和同名日志。全包普通检查仍返回失败，六项都属于旧 UnityJsonSettingsBootstrap；完整失败项为 `core-after-observability-comments.log`，不把盘点成功表述为全包检查通过。
- 对照批次开始备份，去除注释、行首尾空白及空行后的代码完全一致，共比较 3168 个非注释字符，非空长度及 SHA256 保存为 `core-observability-code-audit.json`。公开 API、默认值、包版本、源码路径、目标 asmdef 及 Unity 元数据保持，检查为 `core-observability-api-meta-diff.log`。本批没有修改 MOBA、Diagnostics、事件模型或采集机制。
- 临时代码检索无 TODO/FIXME/HACK、临时占位或调试输出；没有确认应删除的私有残留。NullObservationSink 的私有空构造和恒假返回是正式空对象职责，保留并说明不保留事件、不调用事件方法、不释放引用载荷。无校验构造及不同字段集合的相等/哈希也按契约保留，不合并为通用对象目录、反射值基类或稳定外部哈希。记录为 `core-observability-temporary-code-review.json`。
- 类型名检索涵盖 Unity/Packages、src、tools 和 Docs，C# 排除 bin/obj；原始线索为 `core-observability-symbol-references.log`。按完整符号边界过滤同名子串并排除定义与测试后，直接生产引用只有同一 MOBA runtime 中的两个诊断文件，清单为 `core-observability-production-reference-files.json`。BattleDiagnosticRuntimeObjectKey 和 IMobaRuntimeObjectKeyResolver 等名称子串不计作这些 Core 类型的新消费者；不同 MOBA 程序集也不计作独立框架需求。没有执行 MOBA 使用方回归，不把 Core 单元测试表述为真实集成验收。
- 职责复核发现：这些类型语义表面中立，但当前证据不满足 Core 三个独立框架使用方准入要求，且五个类型目前登记在 PublicAPI.Unshipped。本批不更改 API、不自动弃用或迁移；将独立需求证据列为正式发布缺口。若后续仍限于同一诊断链，建议按领域或 Diagnostics 真实所有权单独评审迁移、依赖闭包与兼容窗口，不为凑数量新增消费者或扩展统一观测机制。
- 新增随包 [观测关联原语契约与准入复核](../../../Unity/Packages/com.abilitykit.core/Documentation~/Observability.md)，同步包 README、随包索引、CoreBoundary 和 CHANGELOG。将具体有效性、身份、同步接收及空对象规则集中到包内正文；入口文档明确本批整理并不证明正式纳入 Core 基础面。
- 最终检查汇总为 `local/Logs/core-stabilization/core-observability-final-review.json`，包含测试、注释范围、去注释一致性、API 与元数据、准入缺口、UTF-8/空白和新增文档链接检查。

#### 保留限制与下一批

运行键不会分配标识或推进代次，定义引用不验证配置存在或不可变，关联引用不检查父子关系；所有权、复用、持久化和解析归领域所有。IsValid 与相等是独立语义，无效值也保留全部身份字段。同步 sink 的启用提示不保证写入接受，接口不自动提供原子性、异步队列、顺序、异常隔离、深度不可变或事件保留；具体实现需另行规定。正式空 sink 可并发复用但不释放事件资源。本批明确限制，未增加新机制或承诺跨目标哈希。

下一批建议完成 Runtime/Unity/UnityJsonSettingsBootstrap.cs 的最后一个源码注释范围：一个文件、六个声明、六项注释问题，既有只读清单为 `local/Logs/core-stabilization/core-unity-next-batch-inventory.json`。核对协程延迟执行、StreamingAssets 到持久路径复制、请求/文件失败边界、异步与同步空回调差异、路径拼接及替换持久层的副作用。该入口已弃用，应用设置引导仍归应用所有者；不新增 Core 使用方或扩展持久化机制。真实 Unity 请求与协程验收必须单列，不能用 .NET 单元测试替代。

Observability 正式准入、全包文档警告收紧、领域命名空间超额、独立干净消费、Unity/Mono/IL2CPP 及正式发布评审仍未完成。本批未重跑领域边界审计，未将历史失败标为解决，未修改版本或提升发布候选状态。

### 第十五批：旧 Unity 设置引导兼容契约与注释范围收尾

- 覆盖 Runtime/Unity/UnityJsonSettingsBootstrap.cs 一个源码文件、六个声明，补齐中文多行 XML 注释，并用中文块注释说明落盘异常隔离。批次开始源码备份为 `local/Logs/core-stabilization/unity-settings-before/UnityJsonSettingsBootstrap.cs`。保留已弃用入口与下一主版本移除窗口，不新增应用设置机制或使用方。
- 修改前、最终均执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：531 项通过、零失败、零跳过，本批未新增测试；日志分别为 `core-unity-settings-baseline-tests.log` 与 `core-unity-settings-final-tests.log`。最终日志未出现警告，未修改警告策略或 API 基线。该工程排除 Runtime/Unity，结果仅代表现有纯 C# Core 回归，不是目标适配器的编译或行为验收。
- 对 Runtime/Unity 与 Core 包分别执行 `dotnet run --project tools/AbilityKit.PackageQuality --no-build --no-restore -- <目录>`，一个目标源码与全包 81 个源码均零项问题，日志为 `core-unity-settings-comments.log` 与 `core-after-unity-settings-comments.log`。另行盘点为 1191 个声明、零问题，清单和日志为 `core-after-unity-settings-inventory.json` 及同名日志。本批首次完成当前 Core 源码范围的中文多行注释门禁，不外推到其他包或发布资格。
- 明确迭代器延迟执行及嵌套枚举器必须由协程宿主推进、目标存在即跳过、字面协议前缀不等于 URI 规范化、空文本与空白文本差别、请求失败和异常传播的区别。落盘 catch 仅包围最终目录创建与直接写入，路径/请求/释放异常可能传播；下载后没有二次存在检查，仍可能覆盖期间创建的目标文件。
- 核对协程空反序列化回调仍尝试复制后以空设置替换持久层，而同步入口直接保留旧层；两者读取失败均可能清空持久层并同步通知，通知失败不回滚。保存入口的路径拼接在文件工具 catch 外，只写文件不更新存储、缓存或通知。复制与加载分别读取根路径，不承诺跨等待点快照。
- 临时代码检索未发现 TODO/FIXME/HACK、占位或调试输出；现有 catch、提前结束与已弃用公开 API 均承担兼容失败策略，保留而不按临时代码删除。没有可确认应删除的私有残留，不为清理数量改动实现。审查为 `core-unity-settings-temporary-code-review.json`。
- 类型与方法检索覆盖 Unity/Packages、src、tools 的 C# 源码，排除 bin/obj；日志为 `core-unity-settings-symbol-references.log`。检索到的 MOBA 调用使用 AbilityKit.Demo.Moba.View.Settings 的同名应用类型，未视作旧 Core 使用方；本批不修改 MOBA 或执行其业务验收。
- 去除注释、空行与行首尾空白后，3015 个非注释字符完全一致；审查保留字符串和字符字面量，避免将 file:// 与 :// 误识别成注释。长度与 SHA256 见 `core-unity-settings-code-audit.json`。公开 API、默认值、弃用消息、包版本、全部目标包 asmdef、源码路径和目标 Unity GUID 保持；Shipped/Unshipped 未变。
- 新增随包 [旧 Unity 设置引导兼容契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/UnitySettingsBootstrapCompatibility.md)，同步包 README、文档索引、配置文件正文、CoreBoundary 和 CHANGELOG；具体契约以该正文为准。UTF-8、文档本地链接、尾随空白及 API/元数据检查汇总为 `core-unity-settings-final-review.json`，可复查脚本为 `audit_unity_settings.py`，均位于 `local/Logs/core-stabilization/`。

#### 保留限制与下一批

本批没有真实 Unity 编译或运行，未用自制 Unity 桩冒充验收。请求失败/中止、协程停止与释放、StreamingAssets 平台路径、写权限、域重载、Mono/IL2CPP、独立干净消费和升级回退仍待验收。无路径沙箱、原子写入、备份、诊断和并发屏障均为兼容限制，应用若需增强应在所属包另行设计。

下一批建议推进 **Core 文档警告门禁收紧与发布缺口复核**。已用 `dotnet msbuild src/AbilityKit.Core/AbilityKit.Core.csproj -getProperty:GenerateDocumentationFile,WarningsAsErrors,WarningsNotAsErrors,NoWarn,TreatWarningsAsErrors` 只读核对最终属性：文档生成已开启、TreatWarningsAsErrors 为真，但 src/Directory.Build.targets 仍将 CS1570、CS1573、CS1574、CS1587、CS1591 放在 WarningsNotAsErrors；结果为 `core-xml-gate-next-batch-properties.log`。应先取得强制重编译的真实 XML 诊断，再在 Core 范围收紧，避免误改所有包的警告策略；Unity/Editor 不在此 .NET 工程内，其验收仍须独立记录。

随后复核 Core 对 Deterministic 的实际依赖闭包、API 门禁与发布检查脚本。Observability 独立使用方准入、历史领域命名空间超额、独立干净消费和平台矩阵仍是发布缺口；本批未重跑领域边界审计，未将历史失败标记为解决，未改版本或执行正式发布。

### 第十六批：Core XML 文档错误门禁与发布缺口复核

- 修改前执行 `dotnet build src/AbilityKit.Core/AbilityKit.Core.csproj --no-restore --no-incremental --verbosity minimal`，强制重编译 Core 及其直接工程依赖：零警告、零错误，日志为 `local/Logs/core-stabilization/core-xml-gate-baseline-build.log`。目标工程与共享策略备份在 `xml-gate-before/`；没有用增量构建的静默输出判断文档债务已消除。
- 新增 `src/AbilityKit.Core/Directory.Build.targets`，先导入上一级共享稳定包策略，再仅在 Core 范围移除 CS1570、CS1573、CS1574、CS1587、CS1591 的 WarningsNotAsErrors 豁免，并显式加入 WarningsAsErrors。匹配按完整分号分隔诊断项，保留其他警告基线、NoWarn 与公共 API 门禁；没有改动 src/Directory.Build.targets 或其他包策略。
- 清理 Core.csproj 的失实历史注释：此前声称存在逐项可空 NoWarn 列表，并引用不存在的根 Directory.Build.props，实际只保留 RS0026。替换为真实源码复用与警告继承说明，剥离 XML 注释后工程配置完全一致；没有扩大可空警告抑制、改源码集合或 ProjectReference。
- 新增 `tools/test_core_documentation_gate.ps1`：独立夹具导入真实 Core/共享 targets，五类错误文档逐项产生预期编译错误、正确中文多行文档通过、共享策略仍将五类诊断报告为警告，共七项验证。夹具通过本地空源恢复 SDK 工程，不引用应用、不修改正式源码、不连入公共 API 基线，也不使用 Unity 桩。脚本采用带 BOM 的 UTF-8，兼容仓库 Windows PowerShell 5.1 的脚本读取；生成源码和日志使用无 BOM UTF-8。
- 将上述脚本作为独立步骤加入 tools/test-gates.json 的 core-stability，超时为 120 秒，证据路径绑定 GateOutputDirectory。使用 `tools/run_test_gate.ps1 -Gate core-stability -StepName 'Core XML documentation compiler gate tests' -ResultsDirectory local/Logs/core-stabilization/core-xml-gate-runner` 验证真实运行器接线，七项通过；日志为 `core-xml-gate-runner.log`，夹具、逐项编译日志、latest-results.json 与 gate-summary 位于该运行目录。该次明确只执行新步骤，未运行或宣称整个 core-stability 通过。
- 收紧后再次执行同一强制构建：零警告、零错误，日志为 `core-xml-gate-final-build.log`。执行 `dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore --verbosity minimal`：531 项通过、零失败、零跳过，本批未新增运行时单测，日志为 `core-xml-gate-final-tests.log`。目标 .NET 编译继续排除 Runtime/Unity 且不包含 Editor，不将此结果外推到平台编译或生命周期验证。
- 使用 msbuild 的 getProperty 检查 Core 最终 GenerateDocumentationFile、WarningsAsErrors、WarningsNotAsErrors、NoWarn、TreatWarningsAsErrors，五类诊断已只位于错误列表，不在豁免或抑制列表；结果为 `core-xml-gate-final-properties.log`。同时读取 Deterministic 属性，记录其仍有独立 CS1591 抑制和非 Stable 的配置，没有把 Core 的整理结果归给依赖包。全包中文多行注释普通检查仍为 81 个源码、零项问题，日志为 `core-xml-gate-comments.log`。
- 结构化复核 Core 与 Deterministic 的 package.json、纯运行时 asmdef、.NET 工程、Unity lock 及发布批次：Core 仅依赖 Deterministic@0.1.0，Deterministic 当前无内部依赖；两个基础程序集 noEngineReferences 均为真，当前 Unity 项目使用嵌入包，两个包位于同一 candidate 批次。声明闭包记录为 `core-xml-gate-dependency-closure.json`，不把本地嵌入解析写成干净注册表安装或正式消费验证。
- 执行 `node tools/publish/audit-versions.js`：版本引用不匹配、BOM、框架偏离 cohort 均为零，日志为 `core-xml-gate-version-audit.log`。执行 `node tools/publish/release.js --batch batch-1-leaves --include-candidate`：只读 dry-run 列出七个候选包，日志为 `core-xml-gate-release-dry-run.log`。未更改批次状态、版本、标签或发布物。
- 发布工具只读审查发现 release.js 的同批依赖判断使用 planning 集合，未在该判断中要求依赖也处于 ready，或验证其计划版本等于请求版本；工具也不执行源码注释、Core 测试、Unity、干净消费或签核。本批不扩大到修改发布链，记录后续需要拒绝场景验证和固定源码证据，不能凭候选 dry-run 得出正式发布结论。
- 重跑 `tools/audit_core_boundaries.ps1`，仍失败于 Collision 旧 Core 命名空间 6/5 与 Record 57/56，日志为 `core-xml-gate-boundary-audit.log`。白名单和数量上限保持；审计在命名空间失败处终止，后面的检查未执行，没有将部分先行检查当作完整通过。
- 新增随包 [Core 验证门禁与发布缺口](../../../Unity/Packages/com.abilitykit.core/Documentation~/ValidationAndReleaseReadiness.md)，同步包 README、文档索引、CoreBoundary 和 CHANGELOG，消除当前条目“其余源码仍有 XML 注释债务”的过时概括；历史批次仍保留其当时验证范围。本文正文统一说明能力、限制与后续验收，不复制完整治理规范。
- 本批没有改动运行时实现、公开签名、API 基线、包版本、asmdef 或 Unity 元数据。新增门禁夹具是正式工具验证，不进入随包目录；失实工程说明已清理，共享警告基线不是临时代码，保留而不全局删除。UTF-8、本地链接、尾随空白、配置差异、元数据与结果汇总为 `core-xml-gate-final-review.json`，复核脚本为 `audit_core_xml_gate.py`，均位于本地证据目录。

#### 保留限制与下一批

Core 的五类历史文档警告豁免已经收紧；真实 Unity 编译、Mono/IL2CPP、旧适配器与编辑器生命周期仍未完成，不能将 `.NET` targets 当作 Unity 配置。CS1573 仅识别已有部分参数标签的遗漏，不要求所有方法写 param；中文格式继续交由语法树工具检查。Observability 的三个独立框架需求证据、领域命名空间超额、定点内核编译目标差异、发布工具拒绝条件和发布签核仍是明确缺口。

下一批建议推进 **Core + Deterministic 的独立干净消费验证**：先明确 UPM 与 .NET 两种交付形态，从隔离目录验证最小调用和实际依赖闭包，记录输入版本、源码集合与升级/回退路径；再根据可用 Unity 环境安排真实导入和平台编译。不能用现有 ProjectReference 测试工程、文档门禁夹具或嵌入包 lock 冒充消费验收。领域命名空间迁移继续按所有者与兼容影响单独评审，不提高上限使审计通过。本批没有正式发布或提升候选状态。

### 第十七批：Core 与 Deterministic 独立制品消费、候选替换与回退

- 新增 `tools/validate_core_consumer.py` 与固定 `tools/core-consumer/ConsumerProbe.cs`。以系统临时目录中的新隔离工程验证实际制品，不引用已有 Demo 或测试工程；输入仅为 Core/Deterministic 包、两者镜像工程及 API 基线、构建策略。HEAD 基线为 `bfdff7c52807b082285077b59790a2bc1d64d1f5`，候选为本轮工作区快照；输入逐文件 SHA256、消费源码、验证器和制品哈希均归档，不将脏工作区当作已提交版本。
- 最终从新目录串行执行 `python tools/validate_core_consumer.py`：.NET 和 Unity 三阶段全部通过，运行日志为 `local/Logs/core-stabilization/core-consumer-final-run.log`，指针为 `core-consumer-latest.json`。工具支持按模式准备或续跑，使用同目录运行锁防止并发改写报告；本批中间调试记录不作为最终通过证据。
- .NET 使用真实镜像工程在快照中 dotnet pack，生成 `0.1.0-consumer.baseline` 与 `0.1.0-consumer.current` 本地验证制品；Core 的 nuspec 仅依赖相应版本的 Deterministic，公共 API 分析器不进入运行依赖。正式包版本和工程未改，没有新增正式 NuGet 发布渠道或支持承诺。pack 保留缺少 NuGet 自述文件的提示，不借临时包装扩展包元数据。
- 独立 net10.0 工程只有 Core 的一个 PackageReference；NuGet.Config 清空其他源，只使用本地制品目录。每阶段使用新的缓存，project.assets.json 只包含 Core 与 Deterministic 两个 package，输出两份 DLL 必须与对应 nupkg 中的内容逐字节一致。安装基线、替换候选、回退基线各八项固定调用全部通过；不通过源码链接或 ProjectReference 访问仓库运行时。
- 另建失败工程和空缓存，本地源只保留 Core 制品，恢复明确以 NU1101 拒绝缺少的 AbilityKit.Deterministic，未从使用者全局缓存补齐。失败工程独立于可用的回退工程，日志为 `dotnet-missing-dependency.log`。这验证依赖确实需要安装，不是由同仓库的工程引用遮蔽。
- 找到并使用真实 Unity `2022.3.62f1` Editor，从没有 Library 的新工程导入标准 package/ 根目录 tgz。manifest 仅显式接入 Core、Deterministic 两个本地 tarball 和 Unity 内置 unitywebrequest 模块，没有 Demo、业务测试框架或其他 AbilityKit 包。HEAD 基线、工作区候选、回退基线三阶段均由真实 batchmode 执行固定入口，进程和本轮成功标志同时验收；不是 Unity DLL 镜像或自制桩。
- Unity 每阶段八项调用与三项包归属检查全部通过；验证 Core/Deterministic 的 PackageInfo 名称和 0.1.0 版本、注册的 AbilityKit 包恰好两个、lock 的 local-tarball 来源和本阶段制品路径。AbilityKit.Core、AbilityKit.Core.Unity、AbilityKit.Core.Editor 与 AbilityKit.Deterministic 四个程序集均生成。逐文件比对安装路径下 Core 的 81 个和 Deterministic 的七个 C# 源码，SHA256 与对应快照一致，避免旧缓存或错误包来源冒充验收。
- 固定最小调用覆盖 V1 哈希黄金值、经定点内核的开方、向量长度、Fixed64 公开类型、有序整数集合、去重、单次释放及旧设置空实例入口。旧 API 警告仅在夹具的明确兼容调用处局部标识；没有修改生产警告策略，也不把验证夹具计为 Core 三个独立生产使用方。
- Unity 三阶段日志均无 C# error/warning 诊断；这只针对该独立 Editor 工程与本轮输入，不宣称整个仓库零警告。UPM 源版本均为 0.1.0，结果准确表述为同版本候选制品替换与回退，不冒充正式历史版本升级；两个 tarball 都显式在 manifest 中，因此不证明 OpenUPM 自动解析传递依赖。Unity 使用的数值编译路径也不自动等价于 .NET Int128 路径。
- 最终另执行 Core 全量既有回归：531 项通过、零失败、零跳过，日志为 `core-consumer-core-tests.log`；未新增运行时单测。Core 全包中文多行注释普通检查仍为 81 个源码、零项问题，日志为 `core-consumer-comments.log`。本批不修改 Runtime、Editor、公开签名、API 基线、包版本、asmdef 或 Unity 元数据。
- 新增随包 [Core 与 Deterministic 独立消费验证](../../../Unity/Packages/com.abilitykit.core/Documentation~/IndependentConsumption.md)，同步 README、文档索引、CoreBoundary、验证与发布缺口正文及 CHANGELOG。原始报告、阶段恢复/运行日志、Unity 日志和 lock、制品、逐文件索引与输入快照 zip 已复制到 `local/Logs/core-stabilization/core-consumer-evidence/`，避免只依赖系统临时目录；汇总为 `core-consumer-final-review.json`，归档/复核脚本为 `audit_core_consumer.py`。
- 工具和消费源码采用中文说明；没有临时样例进入正式包。源码/项目快照与当前正式生产输入一致性、API/发布元数据、本地文档链接、UTF-8 及空白检查已记录。正式 Unity 项目、注册表、标签、发布批次状态及其他包源码均未改动。

#### 保留限制与下一批

本批完成两种本地交付形态的独立消费与最小调用，范围包括真实 Unity Editor 编译，尚未完成注册表或约定 git 安装、真实已发布旧版本升级回退、Player 构建、Mono/IL2CPP、平台文件权限、请求中止/协程释放、SceneView 呈现或域重载生命周期。不得继续笼统写成“未做任何真实 Unity 编译”，也不能扩大成完整平台矩阵或正式发布已经完成。

命名空间超额和 Observability 正式准入、定点内核目标算术差异仍保留；本批未重跑领域边界审计，不改写第十六批的实际失败结果。下一批建议处理 **发布计划的依赖状态与请求版本校验**：在只读计划与隔离测试中覆盖 ready 依赖 candidate、同批声明版本不满足、缺包/重复归属/未知批次以及命令失败退出，修正 release.js 已发现的拒绝条件缺口；不改版本、不推进 candidate、不执行打标或发布。真实正式安装与平台构建继续作为后续独立验收阶段。

### 第十八批：发布计划的依赖状态、请求版本与失败退出

- 修正 `tools/publish/release.js` 的同计划依赖判断：先核对请求精确 SemVer 的本地标签；没有标签时，依赖必须在本次所选计划内且实际声明版本相同。ready 包只接受 ready 提供者；candidate 显式预览允许候选闭包，但不能为 ready 包提供未打标依赖。保留已打标旧请求版本与当前依赖源码版本不同的合法情况，不要求把旧标签改成当前 cohort。
- 落实原先未生效的 `--include-candidate`，默认只选 ready；候选不因 `--tag` 获得打标资格，shipped 不作为未来计划提供者。`--batch` 只限制选择，不扩展未选依赖批次；未知批次、缺值、未知/重复参数及 `--tag`/`--dry-run` 冲突均拒绝。保留文档已有的显式 `--dry-run`，避免把旧示例变成无效入口。
- 在创建标签前校验全清单批次 ID、合法状态、包归属唯一性，以及扫描包的名称唯一性/目录一致性、已选包存在性、cohort、BOM、禁止前缀、精确内部依赖。标签占位符与 Git 引用格式也先校验；未选择包的完整版本/依赖闭包不由本次计划验收。未新增自动状态迁移、版本修改或发布授权。
- 将 shell 字符串调用替换为 `execFileSync` 参数数组，清理 Git 异常吞没后返回空字符串的假成功。标签枚举、引用校验、状态读取失败均非零退出；实际逐项创建中失败则立即停止、非零退出并报告先行成功数量，保留已成功本地标签、不自动回滚或推送。沿用 package.json 脏改警告，明确标签指向 HEAD，不冒充源码内容绑定已完成。
- 新增 `tools/publish/release.test.js`，使用 Node 内置运行器和真实 CLI 副本，在系统临时目录初始化独立最小 Git 仓库。49 项测试通过，覆盖正常 dry-run、候选预览、ready/candidate 混合、版本不符、已有精确标签、跨批筛选、缺包、重复归属、非法参数/版本/引用、BOM、禁止依赖、Git 标签/状态损坏、创建中途锁冲突、重复执行与脏改警告。失败校验检查标签无副作用；部分创建失败单独断言仅先行标签保留、后续项未执行。测试标签不进入真实仓库，夹具清理核验独占临时目录。
- 新增中文说明的 `tools/test_release_plan.ps1`，采用带 BOM 的 UTF-8 兼容 PowerShell 5.1；保存 TAP、Node/Git 版本和三个工具输入 SHA256。接入 `core-stability` 的 `Release plan dependency and failure tests` 单步骤、180 秒超时。通过真实运行器执行该步骤，49 项再次通过；只执行这个步骤，不宣称完整 core-stability 通过。正式结果位于 `local/Logs/core-stabilization/release-plan/gate-runner/20261118-142648-core-stability/core-stability/`。
- 真实仓库仅执行默认、指定候选批次与显式 dry-run：默认空计划，候选预览仍为七个包。版本审计的引用不匹配、BOM、框架偏离 cohort 均为零；真实标签引用前后逐字一致，发布 manifest、Core/Deterministic 的 package.json 哈希保持。本批未修改 Runtime、Editor、API 基线、包版本、asmdef 或 Unity 元数据，不推进 candidate、不打标、推送或发布。
- 发布工具 README 改为中文权威说明，准确记录状态选择、精确依赖、失败与部分成功边界；同步 Core README、文档索引、CoreBoundary、发布缺口与 CHANGELOG。工具保持在仓库发布目录，不引入 Core Runtime。未因工具修复重复运行此前 531 项运行时回归或 Unity 消费；其证据仍按第十七批实际范围引用。
- 本批启动时的文件副本、基线/最终 dry-run、版本审计、单步骤门禁与前后校验汇总保存在 `local/Logs/core-stabilization/release-plan/`；`final-review.json` 绑定工具哈希、前后保护项及检查结果。早期 Windows Git 不接受 Node 空设备路径作为全局配置的夹具问题已改为独立空配置文件，最终采用正常退出的门禁证据。

#### 保留限制与下一批

本批完成发布计划的状态与版本拒绝条件，不代表正式发布。源码与 HEAD、已有标签的提交内容尚未绑定；本地标签不证明远端或 OpenUPM 收录。Observability 的独立需求证据、Collision/Record 命名空间超额、Fixed64 编译目标差异、注册表实际安装、正式历史版本升级及 Player/生命周期矩阵继续保留。本批不重跑边界审计，也不改写此前失败或提高白名单。

下一批建议推进 **发布输入与 Git 提交内容的一致性校验**：先在只读/隔离仓库中核对所选包及依赖的源码、清单与 HEAD，覆盖仅 Runtime 脏改、未跟踪输入、已有标签指向不符等拒绝场景，再形成可复现的发布输入证据；保持版本与 candidate，不在真实仓库创建标签或发布。注册表和平台矩阵继续分阶段验收，不能用工具单测替代。

### 第十九批：发布输入与固定 Git 提交、标签内容一致性

- 为 `release.js` 增加只读 `--verify-inputs`，普通 dry-run 保留开发期声明预览；`--tag` 自动严格检查所有已选输入，包括显式加入的候选，不允许省略参数绕过。空计划不生成源码验收证据。实现独立放在 `tools/publish/release-inputs.js`，采用中文多行说明，不把发布策略放入 Core Runtime。
- 固定 HEAD 提交，要求执行中的 release.js、release-inputs.js 和发布清单均已提交且与该提交一致；已选包的完整目录参与核对，包括 Runtime、Editor、文档及元数据。拒绝新增、删除、暂存变更、未跟踪和忽略文件，拒绝符号链接、目录联接、子模块与非普通文件；范围外项目或未选包的工作区改动不作为本次输入。
- Git 状态之外，以不带 -w 的 `hash-object --stdin-paths` 批量核对实际文件的 Git 内容身份，覆盖 assume-unchanged 隐藏修改；路径使用 Git 双引号转义并通过参数数组/标准输入传递。按 Git 当前规则规范化换行，等价 CRLF/LF 不判为源码变化，非 Windows 同时核对可执行权限。设置 GIT_OPTIONAL_LOCKS=0，避免只读状态查询刷新索引；隔离测试逐字核对严格 dry-run 前后的索引内容不变。
- 已有标签须能剥离到提交，并在该提交中找到正确包名、精确版本、完整包子树；同版本已选包的标签 tree 必须等于本次输入 tree。允许不同提交但包内容相同，拒绝同版本不同内容标签，即使全部标签已存在也检查。已发布旧依赖从请求版本标签读取而非使用当前未选源码，递归检查标签内精确依赖闭包并记录其实际输入。
- 成功输出 `input-evidence` JSON：schemaVersion、固定 head、三个工具/清单 blob、所选包名称/版本/tree，以及依赖 tag/object/commit/tree。校验末尾复核 HEAD 和已使用标签对象未变化；新标签显式传入已验证提交 ID。Git 对象记录绑定规范化仓库内容，不冒充制品 SHA256、远端状态、注册表安装或发布签核，工具不提供并发工作区锁。
- 原 package.json 脏改警告升级为严格拒绝，保留普通预览供未提交计划使用；逐项创建失败的非零退出、保留先行成功项且停止后续项的契约不变。清理的是发布链已确认的假成功缺口，不删除 Core 兼容入口或改变运行时语义。
- 隔离 CLI 回归从 49 项扩展至 77 项，覆盖 Runtime/依赖源码/暂存修改、删除、新包未提交、未跟踪/忽略文件、工具/清单变更、隐藏修改、范围外脏改、同版本不同内容、不同提交相同内容、注解标签、非提交对象、错误版本、缺包目录、历史依赖、传递闭包、非法标签依赖、中文/空格路径、换行及外部联接。测试在系统临时目录使用真实 Git，所有测试标签均与工作仓库隔离。TAP 中的 `fixture-input-evidence` 明确仅为成功夹具样例。
- `test_release_plan.ps1` 将新增模块纳入四项输入哈希，测试前后核对输入未变化，发生变化则拒收证据。core-stability 的既有目标步骤超时从 180 秒调整至 300 秒，本批未修改其他步骤。最终只运行 `Release plan dependency and failure tests` 单步骤，77 项通过、零失败/零跳过，输入哈希稳定；不宣称整个 core-stability 通过。开发中的一轮虽然用例通过，但测试期间工具变化，门禁按设计拒收该轮，最终以冻结输入后的重跑结果为准。结束复核发现另一门禁有并行扩展的 MOBA 测试过滤器，原样保留，不归为本批变更或执行证据。
- 真实仓库默认与七包候选声明 dry-run、版本审计通过；严格候选 dry-run 返回 1，指出发布工具尚未与 HEAD 一致，未输出成功输入证据。当前有多批未提交整理，未为通过检查自动提交、清理或覆盖修改。真实标签引用、发布清单、Core/Deterministic 包版本、运行时/Editor 源码及 API 基线保护项保持；未打标、推送、发布或提升 candidate。
- 同步发布工具权威 README、Core README、文档索引、CoreBoundary、发布缺口与 CHANGELOG。证据保存在 `local/Logs/core-stabilization/release-inputs/`，包括启动备份、隔离测试、最终单步骤门禁、真实普通/严格 dry-run、版本审计与 `final-review.json`；复核脚本为 `audit_release_inputs.py`。本批不重复运行既有 531 项 Core 测试、Unity 消费或命名空间审计，保留其先前实际范围和失败状态。

#### 保留限制与下一批

本批完成严格输入校验机制，真实工作区尚未满足该校验，不能把隔离成功样例写成正式发布输入已固定。实际流程仍须处理已授权提交的范围，固定源码后重跑检查并绑定制品、构建配置和签核；Git 规范化依赖当前过滤规则，外部构建/工具配置不由这份包树证据自动覆盖。Observability 准入、Collision/Record 命名空间超额、Fixed64 编译目标差异、注册表安装、正式旧版本升级和平台生命周期缺口继续保留。

下一批建议推进 **Core + Deterministic 的独立 Unity Player 编译与最小运行验证**：先核对本机已安装的平台模块和后端，从无 Demo 的独立消费工程验证可用目标，记录 Mono/IL2CPP 的实际覆盖与缺失项、输入快照和构建产物；不把 Editor 通过外推为 Player 支持，不新增跨目标数值一致承诺。正式发布输入提交和注册表验收继续按各自阶段处理，版本与 candidate 保持。


## 附录：D1 整理前的包内维护与验收记录

2026-11-24 从包内迁入本记录，保留以下原文中的历史测试数量、工具说明和结论范围。“本次”“本批”指原记录的批次，不是 D1 重跑；当前包内只维护消费条件和使用限制。后续维护操作与本轮结果见 [D1 文档核校记录](../../../plans/2026-11-24-DocumentationSystemInventory.md#7-d1-基础包核校记录2026-11-2425)。

### Core 验证门禁与发布缺口

本文说明当前 Core 工程门禁、依赖闭包及尚未完成的发布证据。注释整理完成、构建通过和候选版本号不代表正式发布；API 准入与迁移规则仍以 [CoreBoundary](../../../Unity/Packages/com.abilitykit.core/Documentation~/CoreBoundary.md) 为准。

#### 中文注释与 XML 文档

退役 `AbilityKit.Core.Unity` 前，Core 的 81 个 C# 源码已通过中文多行注释检查。工具按常规及一组 Unity Editor 符号解析语法树，不能判断注释事实是否正确，也不覆盖所有平台编译符号组合。

仓库内可执行：

```powershell
dotnet run --project tools/AbilityKit.PackageQuality -- Unity/Packages/com.abilitykit.core
dotnet build src/AbilityKit.Core/AbilityKit.Core.csproj --no-restore --no-incremental
dotnet test src/AbilityKit.Core.Tests/AbilityKit.Core.Tests.csproj --no-restore
& tools/test_core_documentation_gate.ps1
```

使用 `--no-restore` 前须已恢复对应工程。`.NET` Core 开启文档生成；工程目录的 `Directory.Build.targets` 先导入 `src/Directory.Build.targets`，再仅为 Core 从 WarningsNotAsErrors 移除以下五类历史豁免并加入 WarningsAsErrors：

| 诊断 | 阻止的问题 |
|---|---|
| CS1570 | XML 文档格式损坏。 |
| CS1573 | 已写部分参数标签时，其他参数缺少匹配说明。 |
| CS1574 | XML 文档中的 cref 无法解析。 |
| CS1587 | XML 文档位于不能附着的语言元素前。 |
| CS1591 | 对外可见的类型或成员缺少 XML 文档。 |

其他历史警告沿用共享基线，不由本批扩大收紧。CS1573 不要求所有方法都必须含 param 标签，中文多行格式仍由注释工具负责。RS0016/RS0017 公共 API 门禁继续有效，Shipped 与 Unshipped 未重生成。

`test_core_documentation_gate.ps1` 用独立最小工程导入真实构建策略，分别验证五类诊断变为错误、正确文档可编译，以及共享策略仍将这五类报告为警告，共七项。夹具和日志保存在本地证据目录，不修改正式源码、不连接公共 API 基线，也不模拟 Unity。该步骤已登记到 `tools/test-gates.json` 的 `core-stability`；完整门禁还包含其他工程与边界审计，不能把七项通过写成整个门禁已通过。

Core 的 .NET 工程只复用纯运行时源码，明确排除 Runtime/Unity，且未包含 Editor。局部 MSBuild 门禁不会自动改变 Unity 编译器配置。平台编译和生命周期验证仍须独立完成。

#### 依赖与候选发布工具

当前包版本均为 0.1.0，最低 Unity 声明为 2022.3。依赖关系如下：

| 层次 | Core | Deterministic |
|---|---|---|
| UPM | 声明依赖 com.abilitykit.deterministic@0.1.0。 | 当前没有包依赖。 |
| 纯运行时 asmdef | 仅引用 AbilityKit.Deterministic，noEngineReferences 为真。 | 无程序集引用，noEngineReferences 为真。 |
| .NET | 唯一 ProjectReference 为 AbilityKit.Deterministic；公共 API 分析器为构建期私有依赖。 | 无 ProjectReference；仍有自身的公共 API 分析器和警告配置。 |
| 当前 Unity lock | Core 的依赖指向 Deterministic@0.1.0，两者为本地嵌入包。 | 本地嵌入包。 |

当前发布 manifest 将两者放在同一 candidate 批次。Core 已有直接数值内核依赖，不能继续把它描述为零依赖叶子包；消费安装和发布计划均须包含 Deterministic。仓库中本地嵌入包解析不等于注册表或 git 安装已验证，Core 的文档门禁也不自动证明依赖包达到相同整理标准。

只读版本审计与普通候选 dry-run 通过：版本引用不匹配、BOM 和框架偏离 cohort 均为零，候选计划列出七个包。普通预览只检查声明；严格输入检查另核对源码与提交/标签内容，但不执行 Core 全套测试、注释、真实 Unity、干净消费或负责人签核。

`release.js` 已修正只按包名判断同计划依赖的问题：ready 包要求依赖具有请求精确版本的本地标签，或为本次所选计划中同版本的 ready 包；candidate 只能显式预览，不能为未打标的 ready 依赖放行。重复归属、未知批次、缺包、非法参数及 Git 失败均有独立拒绝测试，标签创建中途失败会非零退出并保留已成功项。状态选择、版本形式和失败行为以仓库内 [发布工具说明](../../../tools/publish/README.md) 为权威；这些操作规则不属于 Core Runtime 能力，工具也不随包交付。

输入一致性检查使用只读参数 `--verify-inputs`，`--tag` 自动执行同样的检查。严格模式要求已选包的完整目录、执行工具和清单与固定 HEAD 一致，拒绝 Runtime/暂存修改、未跟踪/忽略文件和外部链接，并核对已有标签的包名、版本、子树及传递依赖。已发布旧依赖按其标签取证，不把未选择包的当前源码当成历史版本；同版本已选包不能复用内容不同的标签。

成功后输出固定提交、工具/清单 blob、包 tree 及依赖 tag/object/commit/tree 的 JSON 证据；新标签显式指向已验证提交。规范化 Git 对象不等于实际制品哈希，外部构建输入、并发修改和注册表状态仍需单独处理，具体边界见发布工具说明。

隔离 CLI 回归继续使用 `core-stability` 的 `Release plan dependency and failure tests` 单步骤，TAP 与工具输入哈希由 `tools/test_release_plan.ps1` 保存，并核对测试期间输入未变化。真实仓库默认与七包候选预览通过，严格候选 dry-run 因工具尚未提交而非零拒绝，未输出通过证据；没有为通过检查自动提交文件。候选状态与真实标签保持，未打标或发布。

#### 当前发布缺口

| 项目 | 当前证据与后续要求 |
|---|---|
| 源码注释与 .NET Core | 对象池与事件整理后的 79 个 Runtime 文件注释检查无问题；本批重跑 Core 538 项测试与 Public API 构建门禁通过。 |
| 命名空间所有权 | Collision 和 Record 的两处新增类型已迁回所属命名空间，既有白名单不变。项目直链检查按 Git 源码范围扫描，排除未跟踪且被忽略的临时工程；直接在当前工作区执行完整边界审计通过。 |
| Observability 准入 | 尚缺三个独立框架使用方证据，当前整理不代表正式纳入；见 [观测关联原语准入复核](../../../Unity/Packages/com.abilitykit.core/Documentation~/Observability.md)。 |
| 定点依赖目标差异 | Fixed64 的 Int128 与 decimal 编译路径在负数乘法舍入和中间范围上有既有差异，见 [定点数学桥接契约](../../../Unity/Packages/com.abilitykit.core/Documentation~/DeterministicMathBridge.md)，不能据 .NET 构建承诺跨目标位一致。 |
| 独立消费 | 本地 .NET 验证包的三阶段恢复/执行/回退及缺依赖拒绝通过；真实 Unity 本地 tarball 同版本替换与回退通过，详见 [独立消费验证](../../../Unity/Packages/com.abilitykit.core/Documentation~/IndependentConsumption.md)。注册表安装与正式旧版本升级回退仍待完成。 |
| Unity 平台 | 已在独立 Unity 2022.3.62f1 Editor 中编译 Runtime、Unity 适配与 Editor 并运行最小调用；Player、Mono/IL2CPP、协程/请求退出和编辑器域生命周期仍待验收。 |
| 发布执行 | 状态/精确版本、失败退出和提交内容一致性检查已补齐；当前工作区严格校验拒绝，需按流程固定已提交输入并重跑，绑定实际依赖制品、支持矩阵、回退版本与负责人证据，验证注册表收录。当前仍为 candidate，未在真实仓库打标、推送或发布。 |

测试数量与审计状态应按每次候选重新生成。完整发布要求沿用仓库 Beta 发布检查清单，本文不提升包的成熟度或版本承诺。


### Core 与 Deterministic 独立消费验证

Core 的纯运行时依赖 Deterministic。本文说明本地隔离验证的交付形态、输入与结论边界；正式发布准入仍见 [验证门禁与发布缺口](../../../Unity/Packages/com.abilitykit.core/Documentation~/ValidationAndReleaseReadiness.md)。

#### 验证输入与运行方式

仓库提供 `tools/validate_core_consumer.py` 和固定调用源码 `tools/core-consumer/ConsumerProbe.cs`。运行需要 Python 3.12+、.NET 10 SDK，以及已恢复到本机缓存的 Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4；真实 Unity 验证使用本机 Unity 2022.3.62f1 Editor。工具默认针对 Windows 开发环境：

```powershell
python tools/validate_core_consumer.py
```

可用 `--unity <Unity.exe 路径>` 指定 Editor。`--mode prepare` 只准备输入；`--mode dotnet` 或 `--mode unity` 可配合 `--run-root <已准备目录>` 分阶段执行，同一目录有运行锁，按阶段串行更新证据。仅完成某一模式不能声明另一模式通过；要求运行 Unity 时，找不到 Editor 会失败，不按跳过成功处理。

工具在系统临时目录建立全新的隔离目录，不从已有 Demo 工程拷贝 Library、obj、bin、manifest 或包缓存。准备两份输入：HEAD 中的基线，以及本轮工作区中的候选。逐文件 SHA256、HEAD 标识、消费源码哈希、SDK、验证器哈希与制品哈希写入报告；工作区修改不冒充已提交发布版本。只有所需包、镜像工程配置、API 基线和构建策略进入输入快照，消费代码在三阶段保持不变。

本地指针为 `local/Logs/core-stabilization/core-consumer-latest.json`，指向隔离目录中的 `report.json`。隔离目录保留原始输入、制品、命令日志、恢复清单及各阶段包锁，便于复查；不写回正式包、版本、注册表或标签。正式发布前应将选定输入和证据归档到版本化制品记录，不能依赖临时目录长期保存。

#### .NET 本地制品消费

当前仓库的公开发布流程面向 UPM，并未承诺 NuGet 包 ID、目标框架矩阵或发布源。本工具直接使用真实 Core 与 Deterministic 镜像工程执行 `dotnet pack`，仅在隔离快照中生成本地验证用 NuGet 包，不建立新的正式发布渠道。

验证制品分别使用 `0.1.0-consumer.baseline` 和 `0.1.0-consumer.current`，版本由命令属性传入，正式 package.json、csproj 与 API 基线不改。分析器只用于构建，不进入制品运行依赖；工具核对 Core 的 nuspec 只含相应版本的 Deterministic 依赖。

独立 net10.0 可执行工程仅声明一个 Core PackageReference，不链接仓库源码或镜像工程。NuGet.Config 清空其他源，只保留本地制品目录；每阶段使用新的消费包缓存，再检查 project.assets.json 只解析到 Core 与 Deterministic 两个包。运行后，输出目录中的两份 DLL 还须与相应 nupkg 内容逐字节一致，避免只改恢复清单但继续运行旧 DLL。

同一消费工程依次安装基线、替换为候选、回退到基线，每阶段执行八项最小调用。另建失败工程和空缓存，仅提供 Core 制品，确认缺少 Deterministic 时产生 NU1101，不从使用者全局缓存或外部源补齐依赖。失败工程不会破坏回退后可用的消费工程。

本次 .NET 三阶段及缺依赖拒绝验证通过。这证明本轮 net10.0 本地验证制品可在独立工程中恢复、执行和回退；不证明旧正式 NuGet 版本兼容、其他目标框架或线上安装可用。pack 输出中的缺少 NuGet 自述文件提示仍保留，未为了验证包装新增正式 NuGet 元数据或放宽源码警告。

#### 真实 Unity 本地 tarball 消费

工具从两份输入生成标准 `package/` 根目录的 UPM tgz，包含真实源码、asmdef、文档与原有 .meta。独立 Unity 工程只显式安装 Core、Deterministic 两个本地 tarball，不安装 Demo、测试框架或其他 AbilityKit 包。仍包含旧 `AbilityKit.Core.Unity` 的历史快照才额外安装 `com.unity.modules.unitywebrequest`；其余阶段校验包锁不含此模块。

首次导入没有 Library，之后在同一工程依次替换候选与回退基线。每阶段都用真实 Editor 批处理编译并执行固定消费入口；须同时满足进程成功、生成本轮成功文件、PackageInfo 显示正确包名与版本、AbilityKit 闭包仅有两个包、包锁指向本阶段 tarball，以及以下程序集已生成：

- AbilityKit.Deterministic
- AbilityKit.Core
- AbilityKit.Core.Editor

`AbilityKit.Core.Unity` 仅在含旧适配的历史快照阶段生成。

此外，工具逐文件比对 PackageInfo.resolvedPath 下的 C# 源码与相应输入快照的 SHA256，确保编译来源是安装的目标包。每阶段包含八项固定最小调用和三项包归属检查；最小调用覆盖稳定哈希、经过定点依赖的开方与向量长度、定点公开类型、有序集合及去重、单次释放，以及旧设置空实例兼容入口。该夹具不是新的生产使用方，不能拿它满足 Core 三消费者准入规则。

既有 Unity 2022.3.62f1 的三阶段记录使用了退役前的包。UPM 包版本保持 0.1.0，因此这里验证的是**同版本制品替换与回退**，不冒充已发布历史版本到新版本的升级。两个本地 tarball 都显式声明在 manifest 中，不能由此推断注册表会自动安装传递依赖；包锁和 PackageInfo 的证据仅对应本次本地安装。

本次未构建 Player，未验证 Mono/IL2CPP、运行时平台文件权限、请求中止与协程释放、SceneView 呈现或域重载生命周期。Editor 最小调用通过也不建立 .NET Int128 与 Unity decimal 算术路径的位一致性结论。

#### 正式候选还需完成

将实际可发布版本固定到已评审源码与依赖制品后，还需验证注册表或约定 git 安装、真实旧版本的升级/回退、支持平台 Player 构建与生命周期场景。命名空间超额、Observability 准入、定点依赖目标差异和发布工具拒绝条件继续保留，不因本地消费验证通过而取消。其他包尚未获得相同整理或消费验证结论。
