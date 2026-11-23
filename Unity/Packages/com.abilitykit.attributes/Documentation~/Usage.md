# Attributes 使用契约与限制

从[包入口](../README.md)查看安装条件与最小用法。本文只说明使用本包时必须掌握的行为。

## 注册与对象上下文

- 在应用启动期用 `AttrRegistry.Register` 注册属性。使用默认注册表时，在定义注册完毕后由应用统一调用 `AttrRegistry.Freeze()`，构建依赖图并检查循环。库或插件不应擅自冻结进程共享的默认注册表。
- `AttributeContext` 默认使用 `AttributeRegistry.DefaultRegistry`。使用自定义 `IAttributeRegistry` 时，在创建组或读写属性前设置 `AttributeContext.Registry`，并由实现负责属性定义和依赖查询；不要在已有属性实例的上下文中切换注册表。
- `AttributeId` 的数值部分不作为跨进程或跨注册表的稳定标识。对外保存项目约定的稳定名称或 ID，并在目标注册表重新解析。

## 修改器与读取

- `AddModifier` 返回修改器句柄；单项移除用相同属性 ID 和句柄调用 `RemoveModifier`。按来源批量移除可使用 `ClearModifiers(sourceId)`，调用方负责维护来源 ID，不用它代替 Buff 生命周期。
- 常规业务用 `GetValue` 读取计算结果。自定义公式、约束和依赖声明从 `Runtime/Attributes/` 下的接口核对。

## 静默实时求值

`AttributeContext.GetValueSilently(id)` 与 `GetValue(id)` 使用相同的公式、Modifier、约束及缓存。它仍会重新计算 dirty 属性并将依赖属性标脏，但不触发此次同步调用范围内、同一 Context 的 Instance `Changed`、Group `AttributeChanged` 和 Context `AttributeChanged`。通过公式、Modifier 或直接访问 Group/Instance 发生的同 Context 嵌套读取也处于该静默范围。

此入口用于状态恢复后的派生投影。读取完成后不排队或补发历史通知；后续正常求值继续按正常规则通知。发生异常时传播原异常并退出静默范围，已完成的缓存更新不回退。它不负责序列化、对象生命周期或事务，也不抑制任意自定义公式中的外部副作用。

静默范围只属于本 Context 的同步调用，不向其他 Context 隐式传播，不支持并发访问。正常业务应继续使用 `GetValue`；不要以静默读取替代正常玩法变化通知。调用前仍须保证恢复所需的属性基值、Modifier 与其他输入已按自身所有权恢复。
