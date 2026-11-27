# Ability-Kit World Entitas 世界适配模块开发设计文档

> 文档类型：历史设计材料
> 核校状态：部分核校；保留旧路径与正文，2026-11-25 补充当前契约导航，不代表历史方案全部成立。
>
> 当前接入、contexts factory 前置条件、初始化和释放失败边界见[使用指南](../Documentation~/Usage.md)。contexts 在 EntitasWorld 构造函数内创建，Composer 负责后续组合；下文旧流程不能替代此顺序。本轮没有 Unity 或业务 contexts 验收。

> **阅读对象**：需要把 Entitas ECS 与 AbilityKit World 创建、模块安装、系统执行连接起来的开发者。
>
> **文档目标**：说明 World Entitas 包的世界封装、系统基类、模块安装和扩展点。

---

## 一、设计理念

World Entitas 是 AbilityKit World 对 Entitas ECS 的适配包。它让 Entitas Contexts/Systems 可以被包装成 AbilityKit 的 World，并通过模块安装器、系统排序 Attribute 和创建选项扩展参与 World 生命周期。

---

## 二、模块边界

负责：

- 定义 `IEntitasWorld`、`IEntitasWorldContext`、`IEntitasContextsFactory`。
- 提供 `EntitasWorld`、`EntitasWorldContext`、`EntitasWorldComposer`。
- 提供 `WorldSystemBase`、`ReactiveWorldSystemBase`。
- 提供 `IEntitasSystemsInstaller` 和 `AutoSystemInstaller`。
- 提供 `WorldSystemAttribute`、`WorldSystemOrder`。
- 提供 TickCounter 示例模块。
- 提供 WorldCreateOptions Entitas 扩展。

不负责：

- 不包含 Entitas 第三方源码本身。
- 不定义具体游戏组件。
- 不替代 World DI，只做 Entitas 适配。

---

## 三、典型流程

```mermaid
sequenceDiagram
    participant App as 应用
    participant Options as WorldCreateOptions
    participant Composer as EntitasWorldComposer
    participant World as EntitasWorld
    participant Systems as Entitas Systems

    App->>Options: 配置 Entitas factory/installers
    Options->>Composer: 创建 Contexts/Systems
    Composer-->>World: EntitasWorld
    World->>Systems: Initialize/Execute/Cleanup
```

---

## 四、注意事项

- 系统排序 Attribute 应保持稳定，避免执行顺序隐式变化。
- TickCounter 模块是示例/辅助模块，真实项目可替换。
- Entitas 生成代码和 Contexts factory 由项目侧提供。

---

*文档版本：1.0*
*最后更新：2026-06-05*
