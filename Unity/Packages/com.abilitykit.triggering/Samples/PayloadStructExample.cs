using System;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Example
{
    /// <summary>
    /// PayloadStruct 强类型访问器使用示例
    /// 展示如何使用强类型 Payload 避免装箱开销
    /// </summary>
    public static class PayloadStructExample
    {
        // ========== 1. 定义 Payload 结构体 ==========

        /// <summary>
        /// 伤害事件 Payload
        /// </summary>
        public readonly struct DamagePayload
        {
            public readonly int TargetId;
            public readonly int CasterId;
            public readonly double Damage;
            public readonly bool IsCritical;

            public DamagePayload(int targetId, int casterId, double damage, bool isCritical)
            {
                TargetId = targetId;
                CasterId = casterId;
                Damage = damage;
                IsCritical = isCritical;
            }
        }

        // ========== 2. 定义字段访问器（编译时生成） ==========

        /// <summary>
        /// Payload 字段 ID（与 JSON 配置中的 FieldId 对应）
        /// </summary>
        public static class DamagePayloadFields
        {
            public const int TargetId = 1;
            public const int CasterId = 2;
            public const int Damage = 3;
            public const int IsCritical = 4;

            /// <summary>
            /// 强类型字段访问器（使用结构体委托，避免装箱）
            /// </summary>
            public static readonly PayloadField<DamagePayload, int> TargetIdField =
                PayloadField<DamagePayload, int>.Create(TargetId, "TargetId", p => p.TargetId);

            public static readonly PayloadField<DamagePayload, int> CasterIdField =
                PayloadField<DamagePayload, int>.Create(CasterId, "CasterId", p => p.CasterId);

            public static readonly PayloadField<DamagePayload, double> DamageField =
                PayloadField<DamagePayload, double>.Create(Damage, "Damage", p => p.Damage);

            public static readonly PayloadField<DamagePayload, bool> IsCriticalField =
                PayloadField<DamagePayload, bool>.Create(IsCritical, "IsCritical", p => p.IsCritical);
        }

        // ========== 3. 创建强类型访问器注册表 ==========

        public static void Run()
        {
            // 创建强类型访问器注册表
            var accessorRegistry = new StronglyTypedPayloadAccessorRegistry();

            // 注册 DamagePayload 的访问器（使用委托，避免装箱）
            accessorRegistry.Register<DamagePayload>(new DelegatePayloadAccessor<DamagePayload>(
                intGetter: (payload, fieldId) =>
                {
                    switch (fieldId)
                    {
                        case DamagePayloadFields.TargetId: return payload.TargetId;
                        case DamagePayloadFields.CasterId: return payload.CasterId;
                        default: throw new InvalidOperationException($"Unknown int field: {fieldId}");
                    }
                },
                doubleGetter: (payload, fieldId) =>
                {
                    if (fieldId == DamagePayloadFields.Damage) return payload.Damage;
                    throw new InvalidOperationException($"Unknown double field: {fieldId}");
                },
                boolGetter: (payload, fieldId) =>
                {
                    if (fieldId == DamagePayloadFields.IsCritical) return payload.IsCritical;
                    throw new InvalidOperationException($"Unknown bool field: {fieldId}");
                }
            ));

            // ========== 4. 使用 PayloadStruct 访问字段 ==========

            var payload = new DamagePayload(1001, 1, 500.0, true);
            var payloadStruct = PayloadStruct<DamagePayload>.FromValue(payload);

            // 强类型访问（编译时检查，无装箱）
            var targetId = payloadStruct.Get(DamagePayloadFields.TargetIdField);
            var damage = payloadStruct.Get(DamagePayloadFields.DamageField);
            var isCrit = payloadStruct.Get(DamagePayloadFields.IsCriticalField);

            UnityEngine.Debug.Log($"Target: {targetId}, Damage: {damage}, Critical: {isCrit}");

            // ========== 5. 在触发器中使用 ==========

            var eventBus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            // 创建 ExecCtx 时注入强类型访问器
            var ctx = new ExecCtx<Unit>(
                context: default,
                eventBus: eventBus,
                functions: functions,
                actions: actions,
                blackboards: null,
                payloads: null,  // 旧版访问器
                stronglyTypedPayloads: accessorRegistry,  // 新版强类型访问器
                idNames: null,
                numericDomains: null,
                numericFunctions: null,
                policy: default,
                control: new ExecutionControl()
            );

            // 使用强类型访问 Payload
            if (ctx.TryGetPayloadDouble(in payload, DamagePayloadFields.Damage, out var dmg))
            {
                UnityEngine.Debug.Log($"Retrieved damage via ExecCtx: {dmg}");
            }

            if (ctx.TryGetPayloadInt(in payload, DamagePayloadFields.TargetId, out var tid))
            {
                UnityEngine.Debug.Log($"Retrieved target via ExecCtx: {tid}");
            }

            UnityEngine.Debug.Log("PayloadStruct Example completed!");
        }

        /// <summary>
        /// CodeGen 生成的访问器示例
        /// 展示如何在实际项目中使用代码生成器
        /// </summary>
        public static void CodeGenExample()
        {
            // 这是 CodeGen 会自动生成的代码模板

            // [GeneratePayloadAccessor]
            // public readonly struct DamagePayload { ... }

            // 生成的访问器类
            /*
            public readonly struct DamagePayloadAccessor : IPayloadAccessor<DamagePayload>
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool TryGetInt(in DamagePayload args, int fieldId, out int value)
                {
                    switch (fieldId)
                    {
                        case DamagePayloadFields.TargetId:
                            value = args.TargetId;
                            return true;
                        case DamagePayloadFields.CasterId:
                            value = args.CasterId;
                            return true;
                        default:
                            value = default;
                            return false;
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool TryGetDouble(in DamagePayload args, int fieldId, out double value)
                {
                    switch (fieldId)
                    {
                        case DamagePayloadFields.Damage:
                            value = args.Damage;
                            return true;
                        default:
                            value = default;
                            return false;
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool TryGetBool(in DamagePayload args, int fieldId, out bool value)
                {
                    switch (fieldId)
                    {
                        case DamagePayloadFields.IsCritical:
                            value = args.IsCritical;
                            return true;
                        default:
                            value = default;
                            return false;
                    }
                }
            }
            */

            UnityEngine.Debug.Log("CodeGen Example - see comments for generated code template");
        }

        /// <summary>
        /// 占位类型
        /// </summary>
        public readonly struct Unit { }
    }
}
