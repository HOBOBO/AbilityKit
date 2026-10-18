using System;
using System.Collections.Generic;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Demo.Moba.Services.Triggering.Cue
{
    /// <summary>
    /// 表现 cue 的可变草稿。所有域（触发器/行为/Buff/Tag/AOE/Projectile）共用同一条装配路径：
    /// 业务代码只填它关心的字段，模板负责 key、默认值、legacy 字符串回填等共性。
    ///
    /// 这样新增一种生命周期表现需求 = 在对应 reporter 里加一个调用点，
    /// 而不需要再手写一遍 30 字段的装箱逻辑。
    /// </summary>
    public struct CueDraft
    {
        public MobaPresentationCueStage Stage;
        public PresentationCueDomain Domain;
        public PresentationCueClass Class;
        public PresentationCueLifetime Lifetime;
        public int ContextKind;
        public int OriginKind;
        public long SourceContextId;
        public long RootContextId;
        public long OwnerContextId;
        public int SourceConfigId;
        public string ContextEventId;

        public int TemplateId;
        public int VfxId;
        public int SfxId;

        public int SourceActorId;
        public int TargetActorId;
        public int[] Targets;

        public int DurationMsOverride;
        public float OffsetX;
        public float OffsetY;
        public float OffsetZ;
        public float Scale;
        public float ColorR;
        public float ColorG;
        public float ColorB;
        public float ColorA;

        /// <summary>显式的稳定实例 key。留空时由 <see cref="MobaCueReporterBase"/> 按域推导。</summary>
        public string InstanceKey;
        public long InstanceId;

        public int StackCount;
        public int MaxStackCount;
        public float ElapsedSeconds;
        public float RemainingSeconds;
        public int LifecycleReason;

        public int CueLevel;
        public int ReplicationMode;

        public int[] NumericParamKeys;
        public float[] NumericParamValues;
        public string[] StringParamKeys;
        public string[] StringParamValues;
        public int ParameterSchemaVersion;
        public MobaPresentationCueParameter[] Parameters;

        public static CueDraft Create(PresentationCueDomain domain, MobaPresentationCueStage stage)
        {
            return new CueDraft
            {
                Domain = domain,
                Stage = stage,
                Class = PresentationCueClass.Placement,
                Lifetime = PresentationCueLifetime.Unknown,
                ParameterSchemaVersion = 1,
                // 表现语义默认值：白色、单位缩放。不透明黑（0,0,0,0）几乎总是模板缺省值写错的产物。
                Scale = 1f,
                ColorR = 1f,
                ColorG = 1f,
                ColorB = 1f,
                ColorA = 1f,
            };
        }

        /// <summary>按模板填充表现资源的公共部分（定位 + 视觉）。模板缺省值在这里统一兜底。</summary>
        public void ApplyTemplate(Config.BattleDemo.MO.PresentationTemplateMO template)
        {
            if (template == null) return;

            TemplateId = template.Id;
            if (VfxId <= 0) VfxId = template.AssetId;
            OffsetX = template.OffsetX;
            OffsetY = template.OffsetY;
            OffsetZ = template.OffsetZ;
            if (DurationMsOverride <= 0) DurationMsOverride = template.DefaultDurationMs;
            if (Scale <= 0f) Scale = template.Scale > 0f ? template.Scale : 1f;
            if (ColorR <= 0f) ColorR = 1f;
            if (ColorG <= 0f) ColorG = 1f;
            if (ColorB <= 0f) ColorB = 1f;
            if (ColorA <= 0f) ColorA = 1f;
            if (template.Radius > 0f)
            {
                CueNumericParams.SetRadius(ref this, template.Radius);
            }
        }
    }

    /// <summary>
    /// param bag 的受控构造。取而代之的是在 cue 上平铺新字段——
    /// 至少这里的 key 是有语义的常量，且新增 key 必须走同一个入口。
    /// </summary>
    public static class CueNumericParams
    {
        public const int Radius = 2;

        /// <summary>把 radius 合并进草稿已有的 bag，返回合并后的 keys/values。</summary>
        public static void SetRadius(ref CueDraft draft, float radius)
        {
            if (radius <= 0f) return;

            var keys = draft.NumericParamKeys;
            var values = draft.NumericParamValues;

            // 已有同名 key 就地覆盖，避免同一个语义在 bag 里出现两次。
            if (keys != null)
            {
                for (var i = 0; i < keys.Length; i++)
                {
                    if (keys[i] != Radius) continue;
                    if (values == null || i >= values.Length)
                    {
                        var grown = new float[keys.Length];
                        if (values != null) Array.Copy(values, grown, values.Length);
                        grown[i] = radius;
                        draft.NumericParamValues = grown;
                    }
                    else
                    {
                        values[i] = radius;
                    }

                    return;
                }
            }

            var mergedKeys = new int[(keys?.Length ?? 0) + 1];
            var mergedValues = new float[(keys?.Length ?? 0) + 1];
            if (keys != null)
            {
                Array.Copy(keys, mergedKeys, keys.Length);
                if (values != null) Array.Copy(values, mergedValues, Math.Min(values.Length, keys.Length));
            }

            mergedKeys[mergedKeys.Length - 1] = Radius;
            mergedValues[mergedValues.Length - 1] = radius;
            draft.NumericParamKeys = mergedKeys;
            draft.NumericParamValues = mergedValues;
        }
    }

    public static class CueParameterBag
    {
        public static MobaPresentationCueParameter Integer(int key, long value) => new MobaPresentationCueParameter
        {
            Key = key,
            ValueKind = (int)PresentationCueParameterKind.Integer,
            IntegerValue = value,
        };

        public static MobaPresentationCueParameter Float(int key, float value) => new MobaPresentationCueParameter
        {
            Key = key,
            ValueKind = (int)PresentationCueParameterKind.Float,
            FloatValue = value,
        };

        public static MobaPresentationCueParameter String(int key, string value) => new MobaPresentationCueParameter
        {
            Key = key,
            ValueKind = (int)PresentationCueParameterKind.String,
            StringValue = value,
        };

        public static MobaPresentationCueParameter Boolean(int key, bool value) => new MobaPresentationCueParameter
        {
            Key = key,
            ValueKind = (int)PresentationCueParameterKind.Boolean,
            BoolValue = value,
        };

        public static MobaPresentationCueParameter Vector3(int key, float x, float y, float z) => new MobaPresentationCueParameter
        {
            Key = key,
            ValueKind = (int)PresentationCueParameterKind.Vector3,
            VectorX = x,
            VectorY = y,
            VectorZ = z,
        };

        public static MobaPresentationCueParameter Vector4(int key, float x, float y, float z, float w) => new MobaPresentationCueParameter
        {
            Key = key,
            ValueKind = (int)PresentationCueParameterKind.Vector4,
            VectorX = x,
            VectorY = y,
            VectorZ = z,
            VectorW = w,
        };

        public static MobaPresentationCueParameter[] FromLegacy(
            int[] numericKeys,
            float[] numericValues,
            string[] stringKeys,
            string[] stringValues)
        {
            var numericCount = Math.Min(numericKeys?.Length ?? 0, numericValues?.Length ?? 0);
            var stringCount = Math.Min(stringKeys?.Length ?? 0, stringValues?.Length ?? 0);
            var result = new MobaPresentationCueParameter[numericCount + stringCount];
            var index = 0;
            for (var i = 0; i < numericCount; i++)
            {
                result[index++] = new MobaPresentationCueParameter
                {
                    Key = numericKeys[i],
                    ValueKind = (int)PresentationCueParameterKind.Float,
                    FloatValue = numericValues[i],
                };
            }

            for (var i = 0; i < stringCount; i++)
            {
                result[index++] = new MobaPresentationCueParameter
                {
                    Key = MobaPresentationCueKeys.StableHash(stringKeys[i]),
                    ValueKind = (int)PresentationCueParameterKind.String,
                    StringValue = stringValues[i],
                };
            }

            return result;
        }
    }

    /// <summary>
    /// 各域 cue reporter 的模板基类。收敛掉所有域共有的部分：
    /// 空值守卫、legacy 字符串回填、稳定 key 生成、active store 与复制策略的调用。
    ///
    /// 子类只需实现 <see cref="Populate"/>（以及可选的 <see cref="ResolveInstanceKey"/>）。
    /// </summary>
    public abstract class MobaCueReporterBase
    {
        private readonly MobaPresentationCueSnapshotService _snapshots;

        protected MobaCueReporterBase(MobaPresentationCueSnapshotService snapshots)
        {
            _snapshots = snapshots;
        }

        /// <summary>域身份。用于 legacy 字符串回填与按域批量过滤。</summary>
        protected abstract PresentationCueDomain Domain { get; }

        /// <summary>
        /// 唯一出口。负责：守卫 → 填充 → 生成稳定 key → 回填 legacy 字段 → 上报。
        /// 子类不要绕过它直接调 Report。
        /// </summary>
        protected void Emit(ref CueDraft draft)
        {
            if (_snapshots == null) return;
            if (PresentationCueSemantics.Classify((int)draft.Stage) == PresentationCueTransition.None) return;
            if (draft.Domain == PresentationCueDomain.None) draft.Domain = Domain;
            if (draft.Lifetime == PresentationCueLifetime.Unknown)
            {
                draft.Lifetime = PresentationCueSemantics.ResolveLifetime(
                    draft.Domain,
                    (AbilityKit.Demo.Moba.Share.PresentationCueStage)draft.Stage);
            }
            if (draft.Parameters == null)
            {
                draft.Parameters = CueParameterBag.FromLegacy(
                    draft.NumericParamKeys,
                    draft.NumericParamValues,
                    draft.StringParamKeys,
                    draft.StringParamValues);
            }

            Populate(ref draft);

            if (string.IsNullOrEmpty(draft.InstanceKey))
            {
                draft.InstanceKey = ResolveInstanceKey(in draft);
            }

            if (draft.VfxId <= 0 && draft.TemplateId > 0) draft.VfxId = draft.TemplateId;

            // 保留一个版本的 legacy 字段，让仍在读 CueKind/OwnerKind 的旧消费方（含 console/HUD）不掉线。
            var legacy = PresentationCueSemantics.ToLegacyDomainName(draft.Domain);
            var entry = new MobaPresentationCueSnapshotEntry
            {
                Stage = (int)draft.Stage,
                CueKind = legacy,
                OwnerKind = legacy,
                TemplateId = draft.TemplateId,
                VfxId = draft.VfxId,
                SfxId = draft.SfxId,
                RequestKey = draft.InstanceKey,
                InstanceKey = draft.InstanceKey,
                InstanceId = draft.InstanceId,
                ContextKind = draft.ContextKind,
                OriginKind = draft.OriginKind,
                SourceContextId = draft.SourceContextId,
                RootContextId = draft.RootContextId,
                OwnerContextId = draft.OwnerContextId,
                SourceConfigId = draft.SourceConfigId,
                ContextEventId = draft.ContextEventId,
                SourceActorId = draft.SourceActorId,
                TargetActorId = draft.TargetActorId,
                Targets = draft.Targets,
                OffsetX = draft.OffsetX,
                OffsetY = draft.OffsetY,
                OffsetZ = draft.OffsetZ,
                DurationMsOverride = draft.DurationMsOverride,
                Scale = draft.Scale > 0f ? draft.Scale : 1f,
                ColorR = draft.ColorR,
                ColorG = draft.ColorG,
                ColorB = draft.ColorB,
                ColorA = draft.ColorA,
                StackCount = draft.StackCount,
                MaxStackCount = draft.MaxStackCount,
                ElapsedSeconds = draft.ElapsedSeconds,
                RemainingSeconds = draft.RemainingSeconds,
                LifecycleReason = draft.LifecycleReason,
                CueLevel = draft.CueLevel,
                ReplicationMode = draft.ReplicationMode,
                NumericParamKeys = draft.NumericParamKeys,
                NumericParamValues = draft.NumericParamValues,
                StringParamKeys = draft.StringParamKeys,
                StringParamValues = draft.StringParamValues,
                Lifetime = (int)draft.Lifetime,
                Domain = (int)draft.Domain,
                ParameterSchemaVersion = draft.ParameterSchemaVersion > 0 ? draft.ParameterSchemaVersion : 1,
                Parameters = draft.Parameters,
            };

            _snapshots.Report(in entry);
        }

        /// <summary>把域特有的字段写进草稿。这是子类唯一的必填实现。</summary>
        protected abstract void Populate(ref CueDraft draft);

        /// <summary>
        /// 生成稳定表现 key。默认实现对"一次性" cue 返回 null（由 snapshot service 的 fallback 兜底），
        /// 对有生命周期的实例型 cue 必须覆写，否则刷新/结束会命中不到同一个表现对象。
        /// </summary>
        protected virtual string ResolveInstanceKey(in CueDraft draft) => null;
    }
}
