---
name: ability-kit
section: key-files
---

# Key files (reference paths)

## 技能/事件

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Services/Skill/SkillExecutor.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Services/Skill/SkillPipelineRunner.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Services/Skill/SkillPipelineContext.cs`（含 `SkillCastRequest`）
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Services/Skill/MobaSkillTriggering.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Services/Skill/MobaSkillTriggerArgs.cs`

## 触发器

- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Triggering/EventBus.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Triggering/TriggerRunner.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Triggering/Runtime/TriggeringWorldModule.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Triggering/Runtime/WorldTriggerContextFactory.cs`

## 被动技能

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Systems/MobaPassiveSkillTriggerRegisterSystem.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Base/TriggerDef.cs`（含 `AllowExternal`）

## BUFF

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Systems/Buffs/MobaBuffApplySystem.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Systems/Buffs/MobaBuffTickSystem.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Systems/Buffs/MobaBuffRemoveSystem.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Services/Buffs/MobaBuffService.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/Services/Buffs/MobaBuffTriggering.cs`

## Pipeline runtime / debugger

- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/AbilityPipeline.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Interface/IAbilityPipeline.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Interface/IAbilityPipelineRun.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Interface/IAbilityPipelinePhase.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Phase/AbilityPipelinePhaseBase.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Debug/AbilityPipelineLiveRegistry.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Graph/PipelineGraphAsset.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Editor/PipelineRuntimeDebugger/AbilityPipelineRunDebuggerWindow.cs`

## EffectSource（事件溯源树）

- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceRegistry.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceSnapshot.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceLiveRegistry.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Runtime/Ability/Share/Impl/Moba/EffectSource/EffectSourceKeys.cs`
- `Unity/Packages/com.abilitykit.ability.runtime/Editor/EffectSource/EffectSourceDebuggerWindow.cs`
