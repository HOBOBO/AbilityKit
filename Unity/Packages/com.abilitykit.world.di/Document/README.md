# World 依赖注入与组合系统

## 文档列表

| 文档 | 定位 |
|------|------|
| [World依赖注入与组合系统开发设计文档.md](./World依赖注入与组合系统开发设计文档.md) | **推荐入门文档**，详细讲解设计理念、核心概念、架构图、快速入门 |

## 快速导航

### 核心概念
- **WorldLifetime** - 服务生命周期：Singleton / Scoped / Transient
- **WorldContainer** - Root 容器，缓存单例
- **WorldScope** - 每个逻辑世界一份实例的 Scope 容器
- **IWorldResolver** - 服务解析窄接口

### 关键约束
1. Root 容器不能直接解析 Scoped
2. Singleton 禁止捕获 Scoped（生命周期穿透）
3. 循环依赖会通过 resolve chain 输出诊断信息

### 模块系统
- **IWorldModule** - 组合子块接口
- **AttributeWorldServicesModule** - 可选属性扫描注册模块

## 相关模块

- [com.abilitykit.world.ecs](../com.abilitykit.world.ecs) - ECS 世界管理
- [com.abilitykit.world.motion](../com.abilitykit.world.motion) - 移动系统
- [com.abilitykit.host.extension](../com.abilitykit.host.extension) - Host 运行时框架
