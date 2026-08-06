# 目标查找模块文档索引

> 本目录是 Unity Package 的正式文档入口。当前行为以 [运行时手册](./Manual.md)、仓库 Targeting 设计文档和可编译源码为准；`Design/` 保存演进背景，不作为可编译 API 清单。

---

## 📚 文档列表

### 1. [运行时手册](./Manual.md)

**阅读对象**：接入、扩展或维护目标查找模块的开发者

**内容概要**：当前五阶段管线、查询契约、对象池、确定性、性能边界和扩展方式。

### 2. [组合示例](./Examples.md)

**阅读对象**：需要快速组装技能、AI 或效果目标查询的业务开发者

**内容概要**：候选源、规则、排序和 Top-K 的常见组合方式。

### 3. [历史开发设计文档](./Design/目标查找模块开发设计文档.md)

**阅读对象**：希望了解早期设计目标与演进背景的开发者

**注意**：部分类型只存在于设计阶段，使用前应回到运行时手册和源码确认。

**推荐阅读顺序**：运行时手册 → 组合示例 → 按需阅读历史设计。

---

## 🎯 快速入门

### 想了解当前运行时能力？

阅读 [运行时手册](./Manual.md) 的目标、数据流与关键接口。

### 想学习如何组合查询？

阅读 [组合示例](./Examples.md)。

### 想了解早期设计取舍？

阅读 [历史开发设计文档](./Design/目标查找模块开发设计文档.md)。

---

## 📖 概念速查

### 核心类

| 类 | 职责 |
|------|------|
| `TargetSearchEngine` | 查找引擎，协调整个流程 |
| `SearchQuery` | 查询配置，定义查找条件 |
| `SearchContext` | 查找上下文；普通实例 Dispose 只清理自身并可重新配置，池化实例归还后公开访问抛 `ObjectDisposedException` |
| `SearchResult` | 池化查询结果；重复释放幂等，归还后公开访问抛 `ObjectDisposedException`，`Ids` 视图仅在当前租约内有效 |
| `TargetingPool` | 目标查找模块统一对象池入口 |
| `TargetQueryDatabase` | query id 到查询工厂的并发安全目录；单操作受锁保护，Factory 构建和查询执行在锁外完成 |
| `SearchHit` | 命中结果结构 |

### 候选源

| 类型 | 职责 | 当前状态 |
|------|------|------|
| `ICandidateProvider` | 候选源接口 | 已实现 |
| 项目 Provider | 从实体索引、空间索引或显式集合推送候选 | 由接入层实现 |
| `ConcatCandidateProvider` | 按来源顺序拼接候选并保留重复 | 已实现 |
| `UnionDistinctCandidateProvider` | 按稳定键求并集，保留全局首次代表项与顺序 | 已实现 |
| `IntersectCandidateProvider` | 按稳定键求交集，保留第一来源首次代表项与顺序 | 已实现 |
| `ExceptCandidateProvider` | 按稳定键从主来源排除其他来源 | 已实现 |

查询中的多个 Rule 按顺序组成 AND 并在首个失败处短路；`AndRule`、`OrRule`、`NotRule` 支持短路和嵌套表达式。空 AND 为真，空 OR 为假，NOT 拒绝空操作数。集合型 Provider 优先使用 Context 中的 `IEntityKeyProvider`，缺失时使用 `EntityId.Value`；`BlacklistRule`、`ExcludeEntityRule` 仍是逐候选排除条件，不是 Provider 集合差集。

### 过滤规则

| 类 | 职责 |
|------|------|
| `ITargetRule` | 规则接口 |
| `AndRule` | 所有子规则通过，首个失败短路 |
| `OrRule` | 任一子规则通过，首个成功短路 |
| `NotRule` | 反转单个子规则 |
| `CircleShapeRule` | 圆形区域过滤 |
| `SectorShapeRule` | 扇形区域过滤 |
| `OrientedRectShapeRule` | 定向矩形过滤 |
| `ExcludeEntityRule` | 排除指定实体 |

### 评分器

| 类 | 职责 |
|------|------|
| `ITargetScorer` | 评分器接口 |
| `ZeroScorer` | 零分（不评分） |
| `DistanceToEntityScorer2D` | 距离评分 |
| `SeededHashRandomScorer` | 确定性随机评分 |

### 选择器

选择器通过 `SearchHitView` 只读访问命中，通过 `SearchResultWriter` 追加结果，不能直接修改引擎拥有的列表。`MaxCount` 是所有输出路径的硬上限；自定义选择器可读取完整命中，但 Writer 不允许写出超过预算。

| 类 | 职责 |
|------|------|
| `ITargetSelector` | 完整命中选择器接口 |
| `IStreamingTopKByScoreSelector` | 声明接受引擎严格 Top-K 语义的公开能力接口 |
| `TopKByScoreSelector` | 排序后取 TopK |
| `StreamingTopKByScoreSelector` | 实现 Streaming 能力的流式 TopK 策略 |

查询级去重键只在规则通过且全部评分有效后提交。常规池容量会保留以减少稳态扩容；异常大列表归还时缩回初始容量，异常大稳定键集合收缩内部容量，异常大命中与评分数组归还时释放底层存储。Rule、Scorer 和 Selector 注册表的注册、创建及扫描状态受实例锁保护；同一实现类型只能绑定一个 Attribute ID，冲突遵循首次注册生效。扫描异常可重试，但已完成的部分注册不会事务性回滚。

---

## 🔗 相关文档

- [实体管理模块](../../com.abilitykit.combat.entitymanager/Document/) - 实体查询系统
- [能力管线模块](../../com.abilitykit.pipeline/Document/能力管线模块开发设计文档.md) - 技能执行
- [触发器模块](../../com.abilitykit.triggering/Document/触发器模块开发设计文档.md) - 事件触发

---

## 💡 典型使用场景

| 场景 | 说明 |
|------|------|
| MOBA 技能目标 | 圆形、扇形、定向矩形范围 |
| RPG 范围攻击 | AOE 技能的目标选择 |
| AI 视野检测 | 扇形视野内的敌人 |
| 锁定系统 | 优先选择最近/血量最低的目标 |
| 录像回放 | 确定性随机用于结果一致 |

---

## 📁 包结构

```text
com.abilitykit.combat.targeting/
├── Runtime/                               # 仅包含运行时代码与程序集定义
│   └── SearchTarget/
│       ├── Abstractions/                  # 中性值对象与跨阶段能力接口
│       ├── Attributes/                    # 组件注册标记
│       ├── Execution/                     # Engine、Context、Hit、Result 与 Pool
│       ├── Pipeline/                      # 查询构建器
│       ├── Providers/                     # 候选协议与组合实现
│       ├── Queries/                       # 查询、排序描述与查询目录
│       ├── Registry/                      # Rule、Scorer、Selector 注册表
│       ├── Rules/                         # 过滤协议与内置规则
│       ├── Scorers/                       # 评分协议与内置评分器
│       └── Selectors/                     # 选择协议、命中视图与 Top-K
└── Documentation~/                        # 手册、示例和历史设计，不进入 Runtime 程序集
    ├── README.md
    ├── Manual.md
    ├── Examples.md
    └── Design/
```

`Runtime/` 不放 Markdown、示例资料或预留空目录。新增运行时职责目录时必须提交 Unity folder `.meta`；移动现有 C# 资产时应连同 `.meta` 一起移动以保留 GUID。

---

*最后更新：2026-08-06*
