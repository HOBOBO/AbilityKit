# MOBA Continuous 目录规范

本目录存放 MOBA 项目对通用 `IContinuous` 协议的应用实现：领域配置、运行时类型、
Modifier 投影、Tag 规则和 Triggering 集成。通用生命周期协议本身在
`com.abilitykit.continuous`，不在这里。

代码放在 `Runtime/Application/Services/Continuous` 下，命名空间统一为
`AbilityKit.Demo.Moba.Services`——目录分层只表达职责归属，不改变命名空间，
因此移动文件不会影响任何调用方。

## 分层

| 目录 | 职责 | 判断依据 |
|---|---|---|
| `Config/` | 领域配置与配置装饰接口 | 只描述「持续体是什么」，不执行副作用 |
| `Runtime/` | Manager、运行时基类与具体运行时、Tick 推进 | 持续体的生命周期宿主 |
| `Runtime/Views/` | 只读查询服务、运行时视图与视图构建 | 只读出口，被诊断面板、Validation 和 Trace 消费 |
| `Modifiers/` | Modifier 投影器、投影注册表、查询与公式求值 | 把持续体声明的 Modifier 投影到属性或技能参数 |
| `Tags/` | Tag 规则服务、规则求值、有效 Tag 查询 | 标签如何驱动激活/暂停/终止 |
| `Triggering/` | Trigger 绑定、周期触发运行时与 Modifier 触发服务 | 与 Triggering 子系统的集成边界 |

## 放置规则

新增文件前先回答一个问题：**它是否只服务于 Continuous？**

- 是 → 按上表选目录。
- 否 → 放回真正的领域目录，不要留在 Continuous。

历史上被误放在本目录根部、现已归位的两个文件，可以作为判据参考：

- `MobaResourceMutation` 资源增减，被 PlanActions 与 Skill/Economy 消费 → `Services/Unit/`
- `MobaResourceAttributeContextProjector` 属性上下文刷新，被 Combat/DamageService 消费 → `Services/Combat/`

## 稳定性约定

移动文件时 `.meta` 必须一起移动，guid 保持不变，否则 Unity 会丢失资源引用。
`AbilityKit.Demo.Moba.Core.csproj` 用 `Runtime/**/*.cs` glob 收录，不需要为新增子目录
修改工程文件。

确定性检查脚本 `tools/check_modifier_determinism.js` 递归扫描本目录，子目录自动覆盖。
