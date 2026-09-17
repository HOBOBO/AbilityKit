using System.Text;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// 战斗调试编辑器的中文显示投影。这里只转换界面文本，不改变诊断契约中的枚举值。
    /// </summary>
    internal static class BattleDebugDisplayText
    {
        public static string ActorKind(BattleDiagnosticActorKind value)
        {
            switch (value)
            {
                case BattleDiagnosticActorKind.Hero: return "英雄";
                case BattleDiagnosticActorKind.Minion: return "小兵";
                case BattleDiagnosticActorKind.Monster: return "野怪";
                case BattleDiagnosticActorKind.Building: return "建筑";
                case BattleDiagnosticActorKind.Summon: return "召唤物";
                case BattleDiagnosticActorKind.Projectile: return "投射物";
                case BattleDiagnosticActorKind.Area: return "区域";
                default: return "未知";
            }
        }

        public static string EventKind(BattleDiagnosticEventKind value)
        {
            switch (value)
            {
                case BattleDiagnosticEventKind.SkillRuntimeStarted: return "技能运行时开始";
                case BattleDiagnosticEventKind.SkillRuntimeEnded: return "技能运行时结束";
                case BattleDiagnosticEventKind.TraceNodeStarted: return "Trace 节点开始";
                case BattleDiagnosticEventKind.TraceNodeEnded: return "Trace 节点结束";
                case BattleDiagnosticEventKind.TracePredictionRetracted: return "Trace 预测撤销";
                case BattleDiagnosticEventKind.Damage: return "伤害";
                case BattleDiagnosticEventKind.Heal: return "治疗";
                case BattleDiagnosticEventKind.BuffAdded: return "Buff 添加";
                case BattleDiagnosticEventKind.BuffRemoved: return "Buff 移除";
                case BattleDiagnosticEventKind.ProjectileSpawned: return "投射物生成";
                case BattleDiagnosticEventKind.ProjectileEnded: return "投射物结束";
                case BattleDiagnosticEventKind.AreaSpawned: return "区域生成";
                case BattleDiagnosticEventKind.AreaEnded: return "区域结束";
                case BattleDiagnosticEventKind.Warning: return "警告";
                case BattleDiagnosticEventKind.Exception: return "异常";
                case BattleDiagnosticEventKind.Sync: return "同步";
                case BattleDiagnosticEventKind.SummonSpawned: return "召唤物生成";
                case BattleDiagnosticEventKind.SummonEnded: return "召唤物结束";
                case BattleDiagnosticEventKind.EffectStarted: return "效果开始";
                case BattleDiagnosticEventKind.EffectEnded: return "效果结束";
                case BattleDiagnosticEventKind.ProjectileHit: return "投射物命中";
                case BattleDiagnosticEventKind.TriggerAnalysis: return "触发分析";
                case BattleDiagnosticEventKind.SkillFailure: return "技能失败";
                case BattleDiagnosticEventKind.TriggerAnalysisAggregate: return "触发分析汇总";
                case BattleDiagnosticEventKind.TargetSearch: return "目标搜索";
                case BattleDiagnosticEventKind.InputCommand: return "输入命令";
                case BattleDiagnosticEventKind.SkillEconomy: return "技能经济";
                default: return "未知事件";
            }
        }

        public static string DefinitionKind(BattleDiagnosticDefinitionKind value)
        {
            switch (value)
            {
                case BattleDiagnosticDefinitionKind.Skill: return "技能";
                case BattleDiagnosticDefinitionKind.Trigger: return "触发器";
                case BattleDiagnosticDefinitionKind.Effect: return "效果";
                case BattleDiagnosticDefinitionKind.Buff: return "Buff";
                case BattleDiagnosticDefinitionKind.Projectile: return "投射物";
                case BattleDiagnosticDefinitionKind.Area: return "区域";
                case BattleDiagnosticDefinitionKind.Summon: return "召唤物";
                case BattleDiagnosticDefinitionKind.Actor: return "Actor";
                default: return "未知";
            }
        }

        public static string EventOutcome(BattleDiagnosticEventOutcome value)
        {
            switch (value)
            {
                case BattleDiagnosticEventOutcome.Succeeded: return "成功";
                case BattleDiagnosticEventOutcome.Failed: return "失败";
                case BattleDiagnosticEventOutcome.Cancelled: return "已取消";
                case BattleDiagnosticEventOutcome.Interrupted: return "已中断";
                default: return "无";
            }
        }

        public static string SkillExecutionStage(BattleDiagnosticSkillExecutionStage value)
        {
            switch (value)
            {
                case BattleDiagnosticSkillExecutionStage.PreCastStarted: return "前摇开始";
                case BattleDiagnosticSkillExecutionStage.PreCastCompleted: return "前摇完成";
                case BattleDiagnosticSkillExecutionStage.CastStarted: return "释放开始";
                case BattleDiagnosticSkillExecutionStage.CastCompleted: return "释放完成";
                case BattleDiagnosticSkillExecutionStage.CastFailed: return "释放失败";
                case BattleDiagnosticSkillExecutionStage.CastInterrupted: return "释放中断";
                case BattleDiagnosticSkillExecutionStage.EconomyReserved: return "资源预留";
                case BattleDiagnosticSkillExecutionStage.ResourceConsumed: return "资源扣除";
                case BattleDiagnosticSkillExecutionStage.EconomyCommitted: return "经济提交";
                case BattleDiagnosticSkillExecutionStage.EconomyRefunded: return "资源返还";
                case BattleDiagnosticSkillExecutionStage.EconomyRejected: return "经济拒绝";
                case BattleDiagnosticSkillExecutionStage.RuntimeWaitingChildren: return "等待子对象";
                case BattleDiagnosticSkillExecutionStage.RuntimeFinalized: return "运行时结束";
                case BattleDiagnosticSkillExecutionStage.RuntimeForceTerminated: return "强制终止";
                case BattleDiagnosticSkillExecutionStage.RuntimeCleared: return "运行时清理";
                default: return value.ToString();
            }
        }

        public static string SkillCastPhase(BattleDebugSkillCastPhase value)
        {
            switch (value)
            {
                case BattleDebugSkillCastPhase.InputToTarget: return "输入→选目标";
                case BattleDebugSkillCastPhase.TargetToCast: return "选目标→释放";
                case BattleDebugSkillCastPhase.PreCast: return "前摇";
                case BattleDebugSkillCastPhase.Cast: return "释放";
                case BattleDebugSkillCastPhase.Economy: return "经济提交";
                case BattleDebugSkillCastPhase.WaitingChildren: return "等待子对象";
                case BattleDebugSkillCastPhase.RuntimeTail: return "运行时收尾";
                case BattleDebugSkillCastPhase.Total: return "执行总跨度";
                default: return value.ToString();
            }
        }

        public static string TraceState(BattleDiagnosticTraceNodeState value)
        {
            switch (value)
            {
                case BattleDiagnosticTraceNodeState.Active: return "进行中";
                case BattleDiagnosticTraceNodeState.Ended: return "已结束";
                case BattleDiagnosticTraceNodeState.Failed: return "失败";
                case BattleDiagnosticTraceNodeState.ForceEnded: return "强制结束";
                case BattleDiagnosticTraceNodeState.Truncated: return "已截断";
                default: return value.ToString();
            }
        }

        public static string RuntimeObjectKind(BattleDiagnosticRuntimeObjectKind value)
        {
            switch (value)
            {
                case BattleDiagnosticRuntimeObjectKind.Actor: return "Actor";
                case BattleDiagnosticRuntimeObjectKind.Projectile: return "投射物";
                case BattleDiagnosticRuntimeObjectKind.Area: return "区域";
                case BattleDiagnosticRuntimeObjectKind.Summon: return "召唤物";
                default: return "全部类型";
            }
        }

        public static string RuntimeObjectState(BattleDiagnosticRuntimeObjectState value)
        {
            switch (value)
            {
                case BattleDiagnosticRuntimeObjectState.Active: return "活跃";
                case BattleDiagnosticRuntimeObjectState.Ended: return "已结束";
                default: return "全部状态";
            }
        }

        public static string DiscoveryKind(BattleDiagnosticRuntimeObjectDiscoveryKind value)
        {
            switch (value)
            {
                case BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleCreated: return "生命周期创建";
                case BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill: return "活跃对象回填";
                case BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleEndedOnly: return "仅观察到结束";
                default: return "未知";
            }
        }

        public static string Completeness(BattleDiagnosticDataCompleteness value)
        {
            switch (value)
            {
                case BattleDiagnosticDataCompleteness.Complete: return "完整";
                case BattleDiagnosticDataCompleteness.Partial: return "部分完整";
                case BattleDiagnosticDataCompleteness.Unreliable: return "不可靠";
                default: return "全部完整度";
            }
        }

        public static string TriggerStage(BattleDiagnosticTriggerAnalysisStage value)
        {
            switch (value)
            {
                case BattleDiagnosticTriggerAnalysisStage.Budget: return "预算";
                case BattleDiagnosticTriggerAnalysisStage.Conditions: return "条件";
                case BattleDiagnosticTriggerAnalysisStage.Plan: return "计划";
                case BattleDiagnosticTriggerAnalysisStage.Execution: return "执行";
                default: return "全部阶段";
            }
        }

        public static string TriggerResult(BattleDiagnosticTriggerAnalysisResult value)
        {
            switch (value)
            {
                case BattleDiagnosticTriggerAnalysisResult.Passed: return "通过";
                case BattleDiagnosticTriggerAnalysisResult.Failed: return "失败";
                case BattleDiagnosticTriggerAnalysisResult.Blocked: return "阻止";
                case BattleDiagnosticTriggerAnalysisResult.Skipped: return "跳过";
                default: return "全部结果";
            }
        }

        public static string SelectionKind(BattleDiagnosticSelectionKind value)
        {
            switch (value)
            {
                case BattleDiagnosticSelectionKind.Frame: return "帧";
                case BattleDiagnosticSelectionKind.Actor: return "Actor";
                case BattleDiagnosticSelectionKind.Event: return "事件";
                case BattleDiagnosticSelectionKind.TraceRoot: return "Trace 根节点";
                case BattleDiagnosticSelectionKind.TraceNode: return "Trace 节点";
                case BattleDiagnosticSelectionKind.SkillRuntime: return "技能运行时";
                case BattleDiagnosticSelectionKind.Attack: return "攻击";
                case BattleDiagnosticSelectionKind.ConfigAsset: return "配置资源";
                case BattleDiagnosticSelectionKind.Warning: return "警告";
                case BattleDiagnosticSelectionKind.Exception: return "异常";
                default: return "无";
            }
        }

        public static string EventChannel(BattleDiagnosticEventChannel value)
        {
            switch (value)
            {
                case BattleDiagnosticEventChannel.Skill: return "技能";
                case BattleDiagnosticEventChannel.Effect: return "效果";
                case BattleDiagnosticEventChannel.Buff: return "Buff";
                case BattleDiagnosticEventChannel.TemporaryEntity: return "临时实体";
                case BattleDiagnosticEventChannel.DamageAndHeal: return "伤害与治疗";
                case BattleDiagnosticEventChannel.Sync: return "同步";
                case BattleDiagnosticEventChannel.WarningAndException: return "警告与异常";
                case BattleDiagnosticEventChannel.Trigger: return "触发";
                case BattleDiagnosticEventChannel.Targeting: return "目标搜索";
                case BattleDiagnosticEventChannel.Input: return "输入";
                case BattleDiagnosticEventChannel.All: return "全部";
                case BattleDiagnosticEventChannel.None: return "无";
            }

            var builder = new StringBuilder();
            AppendChannel(builder, value, BattleDiagnosticEventChannel.Skill, "技能");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.Effect, "效果");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.Buff, "Buff");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.TemporaryEntity, "临时实体");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.DamageAndHeal, "伤害与治疗");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.Sync, "同步");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.WarningAndException, "警告与异常");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.Trigger, "触发");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.Targeting, "目标搜索");
            AppendChannel(builder, value, BattleDiagnosticEventChannel.Input, "输入");
            return builder.Length == 0 ? "无" : builder.ToString();
        }

        private static void AppendChannel(
            StringBuilder builder,
            BattleDiagnosticEventChannel value,
            BattleDiagnosticEventChannel flag,
            string label)
        {
            if ((value & flag) == 0) return;
            if (builder.Length > 0) builder.Append("、");
            builder.Append(label);
        }

        public static string PayloadKind(BattleDiagnosticPayloadKind value)
        {
            switch (value)
            {
                case BattleDiagnosticPayloadKind.SyncSnapshotReceived: return "同步快照接收";
                case BattleDiagnosticPayloadKind.TriggerAnalysis: return "触发分析";
                case BattleDiagnosticPayloadKind.SkillFailure: return "技能失败";
                case BattleDiagnosticPayloadKind.BuffLifecycle: return "Buff 生命周期";
                case BattleDiagnosticPayloadKind.TriggerAnalysisAggregate: return "触发分析汇总";
                case BattleDiagnosticPayloadKind.TargetSearch: return "目标搜索";
                case BattleDiagnosticPayloadKind.InputCommand: return "输入命令";
                case BattleDiagnosticPayloadKind.SkillExecution: return "技能执行";
                default: return "无";
            }
        }

        public static string SkillFailureSource(string value)
        {
            switch (value)
            {
                case "Input": return "输入";
                case "Cast": return "施法";
                case "Preparation": return "准备";
                case "CombatRules": return "战斗规则";
                case "StartReject": return "启动拒绝";
                case "Pipeline": return "流水线";
                case "Unknown": return "未知";
                default: return string.IsNullOrEmpty(value) ? "无" : value;
            }
        }

        public static string SkillFailureStage(string value)
        {
            switch (value)
            {
                case "Resolve": return "解析";
                case "CastGate": return "施法门禁";
                case "Preparation": return "准备";
                case "Execution": return "执行";
                default: return string.IsNullOrEmpty(value) ? "无" : value;
            }
        }

        public static string SkillStage(SkillCastStage value)
        {
            switch (value)
            {
                case SkillCastStage.PreCast: return "施法前摇";
                case SkillCastStage.Cast: return "施法";
                case SkillCastStage.Channeling: return "引导中";
                case SkillCastStage.Completed: return "已完成";
                case SkillCastStage.Cancelled: return "已取消";
                case SkillCastStage.Failed: return "失败";
                default: return "未知";
            }
        }

        public static string SkillRuntimeEndReason(MobaSkillRuntimeEndReason value)
        {
            switch (value)
            {
                case MobaSkillRuntimeEndReason.PipelineCompleted: return "流水线完成";
                case MobaSkillRuntimeEndReason.Cancelled: return "已取消";
                case MobaSkillRuntimeEndReason.Failed: return "失败";
                case MobaSkillRuntimeEndReason.OwnerRemoved: return "所有者已移除";
                case MobaSkillRuntimeEndReason.RollbackCleanup: return "回滚清理";
                default: return "无";
            }
        }

        public static string SkillRuntimeChildKind(MobaSkillRuntimeChildKind value)
        {
            switch (value)
            {
                case MobaSkillRuntimeChildKind.Effect: return "效果";
                case MobaSkillRuntimeChildKind.Projectile: return "投射物";
                case MobaSkillRuntimeChildKind.Area: return "区域";
                case MobaSkillRuntimeChildKind.Buff: return "Buff";
                case MobaSkillRuntimeChildKind.Summon: return "召唤物";
                case MobaSkillRuntimeChildKind.Periodic: return "周期任务";
                case MobaSkillRuntimeChildKind.Presentation: return "表现";
                case MobaSkillRuntimeChildKind.ProjectileLauncher: return "投射物发射器";
                default: return "未知";
            }
        }

        public static string SkillRuntimeValueKind(MobaSkillRuntimeValueKind value)
        {
            switch (value)
            {
                case MobaSkillRuntimeValueKind.Int: return "整数";
                case MobaSkillRuntimeValueKind.Long: return "长整数";
                case MobaSkillRuntimeValueKind.Float: return "浮点数";
                case MobaSkillRuntimeValueKind.Bool: return "布尔值";
                case MobaSkillRuntimeValueKind.String: return "字符串";
                case MobaSkillRuntimeValueKind.ActorId: return "Actor ID";
                case MobaSkillRuntimeValueKind.ContextId: return "上下文 ID";
                case MobaSkillRuntimeValueKind.Vec3: return "三维向量";
                case MobaSkillRuntimeValueKind.ActorIdSet: return "Actor ID 集合";
                case MobaSkillRuntimeValueKind.ContextIdSet: return "上下文 ID 集合";
                default: return "未设置";
            }
        }

        public static string SkillRuntimeBlackboardScope(MobaSkillRuntimeBlackboardScope value)
        {
            switch (value)
            {
                case MobaSkillRuntimeBlackboardScope.Cast: return "本次施法";
                case MobaSkillRuntimeBlackboardScope.Effect: return "效果";
                case MobaSkillRuntimeBlackboardScope.Target: return "目标";
                case MobaSkillRuntimeBlackboardScope.Child: return "子对象";
                default: return "未知";
            }
        }

        public static string SkillRuntimeBlackboardFlags(MobaSkillRuntimeBlackboardFlags value)
        {
            if (value == MobaSkillRuntimeBlackboardFlags.None) return "无";
            var builder = new StringBuilder();
            AppendFlag(builder, value, MobaSkillRuntimeBlackboardFlags.Rollback, "回滚");
            AppendFlag(builder, value, MobaSkillRuntimeBlackboardFlags.Snapshot, "快照");
            AppendFlag(builder, value, MobaSkillRuntimeBlackboardFlags.NetworkSync, "网络同步");
            AppendFlag(builder, value, MobaSkillRuntimeBlackboardFlags.Debug, "调试");
            return builder.Length == 0 ? value.ToString() : builder.ToString();
        }

        private static void AppendFlag(
            StringBuilder builder,
            MobaSkillRuntimeBlackboardFlags value,
            MobaSkillRuntimeBlackboardFlags flag,
            string label)
        {
            if ((value & flag) == 0) return;
            if (builder.Length > 0) builder.Append("、");
            builder.Append(label);
        }

        public static string ContinuousState(ContinuousState value)
        {
            switch (value)
            {
                case AbilityKit.Continuous.ContinuousState.Inactive: return "未激活";
                case AbilityKit.Continuous.ContinuousState.Activating: return "激活中";
                case AbilityKit.Continuous.ContinuousState.Active: return "运行中";
                case AbilityKit.Continuous.ContinuousState.Paused: return "已暂停";
                case AbilityKit.Continuous.ContinuousState.Expired: return "已过期";
                case AbilityKit.Continuous.ContinuousState.Aborted: return "已中止";
                default: return "未知";
            }
        }

        public static string ContinuousKind(string value)
        {
            switch (value)
            {
                case "TriggerIntervalContinuous": return "触发器周期过程";
                case "PipelineContinuous": return "流水线持续过程";
                default: return string.IsNullOrEmpty(value) ? "持续过程" : value;
            }
        }

        public static string ActorRelation(BattleDiagnosticActorRelation value)
        {
            switch (value)
            {
                case BattleDiagnosticActorRelation.Source: return "来源";
                case BattleDiagnosticActorRelation.Target: return "目标";
                case BattleDiagnosticActorRelation.Either: return "来源或目标";
                default: return "任意关系";
            }
        }

        public static string Availability(BattleDiagnosticDataAvailability value)
        {
            switch (value)
            {
                case BattleDiagnosticDataAvailability.Available: return "可用";
                case BattleDiagnosticDataAvailability.NotProduced: return "尚未产生";
                case BattleDiagnosticDataAvailability.NotCaptured: return "未捕获";
                case BattleDiagnosticDataAvailability.Evicted: return "已淘汰";
                case BattleDiagnosticDataAvailability.Truncated: return "已截断";
                case BattleDiagnosticDataAvailability.Unsupported: return "不支持";
                case BattleDiagnosticDataAvailability.Disconnected: return "已断开";
                case BattleDiagnosticDataAvailability.Error: return "错误";
                default: return value.ToString();
            }
        }

        public static string ConnectionState(BattleDiagnosticConnectionState value)
        {
            switch (value)
            {
                case BattleDiagnosticConnectionState.Connecting: return "连接中";
                case BattleDiagnosticConnectionState.Connected: return "已连接";
                case BattleDiagnosticConnectionState.Faulted: return "故障";
                default: return "未连接";
            }
        }

        public static string CaptureState(BattleDiagnosticCaptureState value)
        {
            switch (value)
            {
                case BattleDiagnosticCaptureState.Capturing: return "采集中";
                case BattleDiagnosticCaptureState.Frozen: return "已冻结";
                default: return "关闭";
            }
        }

        public static string Bool(bool value) => value ? "是" : "否";

        public static string EventScope(BattleDebugDiagnosticEventScope value)
        {
            switch (value)
            {
                case BattleDebugDiagnosticEventScope.DamageAndEffects: return "伤害与效果";
                case BattleDebugDiagnosticEventScope.DamageAndHeal: return "伤害与治疗";
                case BattleDebugDiagnosticEventScope.Effects: return "效果";
                case BattleDebugDiagnosticEventScope.Skills: return "技能";
                case BattleDebugDiagnosticEventScope.Buffs: return "Buff";
                case BattleDebugDiagnosticEventScope.TemporaryEntities: return "临时实体";
                case BattleDebugDiagnosticEventScope.Warnings: return "警告";
                case BattleDebugDiagnosticEventScope.Triggers: return "触发";
                case BattleDebugDiagnosticEventScope.Targeting: return "目标搜索";
                case BattleDebugDiagnosticEventScope.Input: return "输入命令";
                default: return "全部事件";
            }
        }

        public static string BuffLifecycleStage(BattleDiagnosticBuffLifecycleStage value)
        {
            switch (value)
            {
                case BattleDiagnosticBuffLifecycleStage.Applied: return "已应用";
                case BattleDiagnosticBuffLifecycleStage.Refreshed: return "已刷新";
                case BattleDiagnosticBuffLifecycleStage.StackChanged: return "层数变化";
                case BattleDiagnosticBuffLifecycleStage.Interval: return "周期触发";
                case BattleDiagnosticBuffLifecycleStage.Removed: return "已移除";
                default: return value.ToString();
            }
        }

        public static string EffectDurationPolicy(BattleDiagnosticEffectDurationPolicy value)
        {
            switch (value)
            {
                case BattleDiagnosticEffectDurationPolicy.Instant: return "瞬时";
                case BattleDiagnosticEffectDurationPolicy.Duration: return "限时";
                case BattleDiagnosticEffectDurationPolicy.Infinite: return "无限";
                default: return value.ToString();
            }
        }

        public static string InvestigationConfidence(BattleDebugInvestigationConfidence value)
        {
            switch (value)
            {
                case BattleDebugInvestigationConfidence.Confirmed: return "已确认";
                case BattleDebugInvestigationConfidence.Inferred: return "推断";
                default: return "证据不足";
            }
        }

        public static string InvestigationCause(BattleDebugInvestigationCause value)
        {
            switch (value)
            {
                case BattleDebugInvestigationCause.TriggerConditionFailed: return "触发条件失败";
                case BattleDebugInvestigationCause.TriggerBudgetBlocked: return "触发预算阻止";
                case BattleDebugInvestigationCause.TriggerPlanRejected: return "触发计划被拒绝";
                case BattleDebugInvestigationCause.TriggerExecutionFailed: return "触发执行失败";
                case BattleDebugInvestigationCause.EffectExecutionFailed: return "效果执行失败";
                case BattleDebugInvestigationCause.RuntimeFailed: return "运行时失败";
                case BattleDebugInvestigationCause.SkillFailure: return "技能失败";
                default: return "未知原因";
            }
        }

        public static string ConfigKind(BattleDebugConfigKind value)
        {
            switch (value)
            {
                case BattleDebugConfigKind.Skill: return "技能";
                case BattleDebugConfigKind.SkillFlow: return "技能流程";
                case BattleDebugConfigKind.TriggerPlan: return "触发计划";
                case BattleDebugConfigKind.Effect: return "效果";
                case BattleDebugConfigKind.Buff: return "Buff";
                case BattleDebugConfigKind.Projectile: return "投射物";
                case BattleDebugConfigKind.Area: return "区域";
                case BattleDebugConfigKind.Summon: return "召唤物";
                case BattleDebugConfigKind.ContinuousProcess: return "持续过程";
                case BattleDebugConfigKind.PresentationTemplate: return "表现模板";
                default: return "未知";
            }
        }

        public static string TraceKind(string value)
        {
            switch (value)
            {
                case "SkillCast": return "技能施放";
                case "SkillPhase": return "技能阶段";
                case "SkillEffect": return "技能效果";
                case "EffectExecution": return "效果执行";
                case "EffectAction": return "效果动作";
                case "TriggerPlan": return "触发计划";
                default: return string.IsNullOrEmpty(value) ? "未知" : value;
            }
        }

        public static string MetricName(string metric, string fallback)
        {
            switch (metric)
            {
                case BattleDiagnosticFrameMetricKeys.PredictionConfirmedFrame: return "确认帧";
                case BattleDiagnosticFrameMetricKeys.PredictionPredictedFrame: return "预测帧";
                case BattleDiagnosticFrameMetricKeys.PredictionAheadFrames: return "预测超前帧数";
                case BattleDiagnosticFrameMetricKeys.PredictionBacklog: return "预测积压";
                case BattleDiagnosticFrameMetricKeys.PredictionWindow: return "预测窗口";
                case BattleDiagnosticFrameMetricKeys.PredictionStalled: return "预测停滞";
                case BattleDiagnosticFrameMetricKeys.NetworkDelayFrames: return "缓冲延迟";
                case BattleDiagnosticFrameMetricKeys.NetworkBufferedCount: return "已缓冲帧数";
                case BattleDiagnosticFrameMetricKeys.NetworkTargetGap: return "目标帧差";
                case BattleDiagnosticFrameMetricKeys.NetworkDuplicateTotal: return "重复包";
                case BattleDiagnosticFrameMetricKeys.NetworkLateTotal: return "迟到包";
                case BattleDiagnosticFrameMetricKeys.RollbackActive: return "回滚进行中";
                case BattleDiagnosticFrameMetricKeys.RollbackReplayToFrame: return "回放目标帧";
                case BattleDiagnosticFrameMetricKeys.RollbackLastFrame: return "最近回滚帧";
                case BattleDiagnosticFrameMetricKeys.RollbackTotal: return "回滚次数";
                case BattleDiagnosticFrameMetricKeys.RollbackRestoreFailedTotal: return "恢复失败次数";
                default: return string.IsNullOrEmpty(fallback) ? metric : fallback;
            }
        }

        public static string CompoundMetricName(string stableId, string fallback)
        {
            switch (stableId)
            {
                case "prediction.backlog_stall": return "预测无法消化积压";
                case "network.late_target_gap": return "迟到交付正在扩大目标帧差";
                case "rollback.restore_failure": return "回滚恢复失败";
                default: return fallback;
            }
        }

        public static string MetricUnit(string unit)
        {
            switch (unit)
            {
                case "frame":
                case "frames": return "帧";
                case "count": return "次";
                default: return unit;
            }
        }
    }
}
