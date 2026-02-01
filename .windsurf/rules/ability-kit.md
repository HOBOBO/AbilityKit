---
trigger: manual
---


# AbilityKit rules (index)

本文件作为索引页，详细规则按主题拆分在同目录下的多个文件中。

## Index

- [architecture.md](architecture.md)
  - UPM / asmdef 分层、依赖方向、DI/module 安装规则
- [pipeline_runtime_debugger.md](pipeline_runtime_debugger.md)
  - Pipeline runtime（Start/Run/Tick、Phase/Composite）与调试器（LiveRegistry/Trace/Graph）规则
- [effect_source.md](effect_source.md)
  - EffectSource（事件溯源树）：root/context 生命周期、purge、origin 引用与调试规则
- [triggering.md](triggering.md)
  - 触发器（IEventBus/TriggerRunner/TriggerRegistry）与扩展方式
- [skill.md](skill.md)
  - 技能与被动技能（AllowExternal、事件来源约定）
- [buff.md](buff.md)
  - BUFF 事件与多阶段协议（OnAdd/OnRemove/OnInterval）
- [config_and_editor.md](config_and_editor.md)
  - 配置/DTO/导出流程规范（TriggerDTO、MobaConfig 导出）
- [projectile.md](projectile.md)
  - 飞行物（Projectile）模块、配置表约定、网络事件与池化注意
- [damage.md](damage.md)
  - 伤害（Damage）结构要求（Spec/Parser/Resolver）与目标选择规范
- [summon.md](summon.md)
  - 召唤（Spawn Summon）模板化规则与落地步骤
- [presentation.md](presentation.md)
  - 表现（Presentation）模板表与 play/stop 事件契约
- [battle_view_runtime.md](battle_view_runtime.md)
  - 表现层（Game/Battle）实体与 View/Snapshot/VFX 流程
- [origin_and_event_contract.md](origin_and_event_contract.md)
  - 事件溯源（origin.*）与 PublishInherited 规则
- [performance.md](performance.md)
  - 高频路径性能/池化/生命周期强约束
- [framesync_prediction_rollback.md](framesync_prediction_rollback.md)
  - 客户端预测/回滚/重演/对账（reconcile）与 idealFrame gate 的设计约束与调试指标

## Quick summary

- **运行载体**：`EntitasWorld.Tick()` 驱动
- **时序**：`_triggerActions.Tick(deltaTime) -> Systems.Execute() -> Systems.Cleanup()`
- **资源释放**：`Systems.TearDown()`（不要在每帧 Cleanup 做一次性反注册）