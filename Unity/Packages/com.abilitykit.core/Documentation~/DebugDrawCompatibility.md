# 旧 DebugDraw 兼容与迁移契约

调试绘制的正式所有权属于 `com.abilitykit.diagnostics`。Core 的 `AbilityKit.Core.Debugging` 六个公开运行时类型和 `AbilityKit.Core.Editor.Debugging` 两个公开编辑器入口保留至下一主版本移除窗口，均有 Obsolete 提示；内部 HandlesDebugDraw 只服务旧驱动。新代码使用 `AbilityKit.Diagnostics.DebugDraw` 和 `AbilityKit.Diagnostics.Editor.DebugDraw`，不向 Core 添加绘制机制。准入规则见 [Core 边界](./CoreBoundary.md)。

## 值与接口

| 类型 | 既有语义 |
|---|---|
| `DebugDrawMask` | 原样保存任意 int 位模式，允许负数、未知位和组合。相等及哈希只基于整数；与空引用、装箱 int 或其他类型不相等。None 为零，All 为负一，包含所有未知位。ToString 使用当前区域设置的十进制整数格式，不是稳定的持久化或分类名称格式。 |
| `DebugDrawColor` | 四个字节通道原样保存，不转换色彩空间。三参数构造默认 Alpha 为 255；默认结构体和无参构造均为透明黑色。命名颜色 Green、Red、Yellow、Cyan、White 均不透明。 |
| `DebugDrawStyle` | 复制一个颜色值。Default 返回不透明绿色，与默认结构体的透明黑色不同；没有线宽、持续时长或深度策略。 |
| `DebugDrawContext` | 仅复制 EnabledMask，不自行执行过滤、预算或条目限制。默认上下文掩码为零。 |
| `IDebugDraw` | 定义球体、胶囊、AABB 和线段请求；没有默认实现、空绘制器、全局运行时入口或统一参数校验。坐标、单位和退化行为由实现决定。 |
| `IDebugDrawContributor` | 内容提供方自行提交绘制请求，接口不自动登记或释放对象。旧编辑器驱动在异常隔离外读取 Mask。 |

这些类型与 Diagnostics 的同名类型具有不同的类型身份，不能直接交换接口实例或共用静态登记。迁移时需要修改相关类型引用和登记所有者，而非让旧接口自动转发。Core 不反向依赖 Diagnostics，也没有为兼容整理引入这种依赖。

## 编辑器设置与生命周期

旧 DebugDrawEditorSettings 仅保存域内静态值，不写入 EditorPrefs、项目资产或磁盘；没有变更通知、参数校验和线程同步。域重载后重新初始化，禁用域重载时不能假定切换播放状态就会恢复默认值。旧设置与 Diagnostics 设置是独立状态。

| 设置 | 初始值及执行责任 |
|---|---|
| Masks | None=0，Collision=1，Targeting=2，Nav=4，All=-1；不限制额外分类位。 |
| Enabled | true；驱动还要求播放模式和非零 EnabledMask。 |
| EnabledMask | Collision；不拒绝未知位或负值。 |
| MaxItemsPerContributor | 2048；只是公开设置槽，旧驱动不读取或强制限制，负值也不拒绝。 |
| CollisionColor | 不透明绿色；旧驱动不读取或覆盖每条请求的样式。 |
| CollisionLayerMask | 0；旧驱动不执行碰撞层过滤，零值解释由内容提供方决定。 |

上述未被驱动读取的公开设置仍属于兼容 API，不作为私有残留删除，也不借本次整理新增其执行机制。

旧 DebugDrawSceneViewDriver 带 InitializeOnLoad，在静态构造中先退订再订阅当前回调，挂接 SceneView.duringSceneGui、EditorApplication.update 和 playModeStateChanged。没有公开 Stop/Dispose 或退出时清空登记的路径。播放状态通知只请求重绘，不清空列表或重置设置；所有者必须在不再需要绘制时注销，不能依赖进入/退出播放自动清理对象引用。实际初始化和域重载由 Unity 控制，.NET 测试不验证这些行为。

Register 拒绝空引用，使用 List 默认相等比较去重并按首次登记顺序追加，不是固定的引用身份比较；自定义 Equals 可以影响去重和删除，比较异常不隔离。Unregister 接受空引用，否则移除首个相等项，不释放对象。列表无并发保护，仅应在编辑器主线程使用。登记和注销不立即请求重绘。

## 更新、过滤与回调边界

每次编辑器更新计算“播放中、Enabled 为真、EnabledMask 非零”的合取条件。条件改变时立即重绘并将后续清理计数置为五；启用期间每次更新均重绘，即使没有内容提供方。关闭时计数大于零便递减并重绘。播放状态通知也将计数置为五并立即重绘，因此不应把全部路径描述成恰好五次重绘。此逻辑只请求重绘，不保证屏幕呈现的确切时机或帧数。

Scene GUI 回调在当前 Event 非空且不是 Repaint 时返回，Event 为空时可继续；随后检查开关、播放状态和掩码。本轮捕获 EnabledMask 并建立上下文，再按实时列表的实时长度遍历，以 `(contributor.Mask.Value & enabled.Value) != 0` 选取内容提供方。没有排序、快照、每项预算或通用重入屏障。

回调中追加项可能进入同轮；移除当前项可能跳过移位后的下一项。回调改变设置不替换本轮已捕获的掩码，也不重新检查开关。Mask 在 try 外读取，其异常会中断后续遍历。每个 Draw 则位于白色、单位矩阵的 Handles.DrawingScope 内，退出或抛错时由该作用域恢复其管理的颜色和矩阵；不表示隔离全部 Unity 全局状态。

旧驱动静默隔离作用域建立、Draw 和作用域退出的异常，保留已绘制内容并尝试继续遍历，没有回滚或诊断输出。正式 Diagnostics 驱动在相同绘制 catch 中调用 Debug.LogException，迁移后日志可见性会改变；两条链均不能因此推断 Mask 属性异常被隔离。不能仅因实现相似而把旧驱动直接替换为正式类型。

## 旧 Handles 适配器的形状近似

该内部适配器直接使用 Unity Handles，坐标和尺寸按世界单位处理。颜色字节除以 255f 变为 Unity Color，不作色彩空间转换。方法自身修改 Handles.color 且不恢复，旧驱动以每个内容提供方外围的作用域管理状态，不为每条请求单独建作用域。

| 请求 | 实际行为 |
|---|---|
| 球体 | 半径 <= 0 时忽略且不设置颜色；否则画三个正交圆盘，并非完整网格球。 |
| 胶囊 | 半径 <= 0 时忽略；轴长 <= 0.0001 时在首端画三个圆盘。其余情况画四条平行边和两个端面圆盘，没有半球弧线，不可据此宣称完整胶囊轮廓。 |
| AABB | 先写颜色，尺寸三个分量全零时返回，否则将 size 作为完整边长传给 DrawWireCube；不是半尺寸，不在此拒绝负分量。 |
| 线段 | 先写颜色，直接传递端点，重合端点不提前过滤。 |

均不专门拒绝 NaN、无穷或非法坐标，最终表现及异常还依赖 Unity API。它们只服务可视诊断，不应作为碰撞、命中或玩法几何的权威计算。未在本轮为旧胶囊补半球或新增几何校验，以免借兼容整理改变机制。

## 迁移及验证范围

迁移应同时替换运行时类型、编辑器命名空间和登记入口，并检查原有 Register/Unregister 配对、设置同步和日志预期。继续由 Diagnostics 所有者维护正式绘制能力，Core 仅保留兼容窗口。没有当前生产引用不等于可以提前删除已保留的公开契约。

本批四个源码仅修改注释，去注释代码与批次开始备份一致。DebugDrawCompatibilityContractTests 在 .NET 10 覆盖掩码相等/哈希与任意位、区域设置文本、颜色默认值/显式 Alpha、值复制及六个运行时类型的完整弃用消息。Diagnostics 既有三项值类型测试用于核对目标包，不能作为旧 Editor 的消费方回归或迁移行为等价证明。

Core 的 .NET 工程只包含 Runtime 纯 C# 源码，不编译这三个 Editor 文件；Editor asmdef 仅包含 Editor 平台，且引用 UnityEngine/UnityEditor。中文注释工具能检查源码和 XML 格式，但不替代真实 Unity 编译和运行。本轮没有用伪造 Unity 类型模拟该验收。

正式发布前仍需在真实 Unity 中验证域重载/禁用域重载、编辑器订阅与退出、开关和掩码、播放状态重绘、登记变更与异常隔离、Handles 状态恢复及退化几何显示；同时完成独立消费、Mono/IL2CPP 与升级回退验收。当前仅完成旧 Core 兼容契约整理，不提升正式发布状态。
