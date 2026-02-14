

# Ability Kit

## 简介

Ability Kit 是一个基于 Unity 引擎的高性能游戏能力与逻辑框架。它采用模块化设计，将技能编辑、触发器逻辑、战斗系统、属性计算等核心功能解耦，并提供了一套可视化的编辑器工具，帮助开发者快速构建复杂的游戏逻辑。

## 主要特性

1.  **可视化技能编辑器 (Timeline Editor)**
    *   基于时间轴的技能编辑体验，支持 Animation（动画）、Audio（音频）、Effect（特效）、Transform（位移/旋转/缩放）等多种轨道。
    *   支持逻辑节点配置，可导出 JSON 供运行时解析 (`ActionSchema`)。

2.  **灵活的触发器系统 (Trigger System)**
    *   事件驱动（Event Driven）架构。
    *   支持强大的条件组合（AND/OR/NOT）和行为列表。
    *   提供编辑器界面 (`AbilityListWindow`) 用于管理 Trigger 配置，支持筛选和导出。

3.  **战斗目标搜索 (SearchTarget)**
    *   高性能的目标查找模块，支持圆形、扇形、有向矩形等多种形状检测。
    *   支持基于索引（Tag/Key）的快速筛选和评分排序（TopK）。
    *   提供可组合的 Provider/Selector 模式，解耦候选源与筛选逻辑。

4.  **通用游戏模块 (Common Modules)**
    *   **Attribute System**: 支持公式计算、依赖关系和约束（Clamp）的属性系统。
    *   **Motion System**: 分组（Group）、优先级（Priority）、堆叠（Stacking）的动作控制系统，支持混合与跨组抑制。
    *   **Projectile System**: 完整的抛射物管理（发射、碰撞、回滚、区域判定 Area）。
    *   **Object Pool**: 高性能零 GC 对象池实现。

5.  **服务端与帧同步 (Server & Frame Sync)**
    *   提供逻辑世界服务器 (`LogicWorldServer`) 基础架构。
    *   支持帧包（Frame Packet）分发与快照（Snapshot）回滚。

## 项目结构

```
Assets/Scripts/Ability/
├── Editor/                          # Unity 编辑器扩展代码
│   ├── Triggering/                  # 触发器列表窗口、工具栏、变量键管理
│   └── ...
├── Impl/                            # 核心运行时实现
│   ├── ActionEditorImpl/            # 技能时间轴的运行时解析与播放 (Tracks, Clips)
│   ├── Triggering/                  # 触发器行为的运行时执行逻辑 (DebugLog, ExecuteEffect)
│   ├── BattleDemo/                  # 战斗相关演示
│   └── Server/                      # 帧同步服务器实现
├── Share/
│   ├── Base/                        # 基础定义（ActionDef, ConditionDef, TriggerContext 等）
│   ├── ActionSchema/                # 技能时间轴的数据结构（DTO）与运行时加载
│   ├── Battle/                      # 战斗核心模块
│   │   ├── EntityManager/           # 实体管理（EntityRegistry, KeyedEntityIndex）
│   │   ├── SearchTarget/            # 目标搜索系统（Rules, Scorers, Selectors）
│   │   └── SkillLibrary/            # 技能库索引管理
│   └── Common/                      # 通用工具库
│       ├── AttributeSystem/         # 属性系统
│       ├── MotionSystem/            # 动作系统
│       ├── Projectile/              # 抛射物系统
│       └── Pool/                    # 对象池
```

## 依赖项

该项目使用了以下第三方插件，请确保 Unity 项目中已正确引用：

*   **DOTween** (Demigiant): 用于动画过渡与路径动画。
*   **Odin Inspector** (Sirenix): 用于增强编辑器 UI 和复杂的属性序列化检查。

## 许可证

本项目遵循 LICENSE 文件中所述的许可协议。