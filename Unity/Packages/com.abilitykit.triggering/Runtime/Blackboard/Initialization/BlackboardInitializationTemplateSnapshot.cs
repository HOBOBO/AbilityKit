#nullable enable
using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    internal sealed class BlackboardInitializationTemplateSnapshot
    {
        private readonly PlanSnapshot[] _plans;

        internal BlackboardInitializationTemplateSnapshot(IReadOnlyList<BlackboardInitializationPlan> plans)
        {
            _plans = new PlanSnapshot[plans.Count];
            for (var index = 0; index < _plans.Length; index++)
                _plans[index] = new PlanSnapshot(plans[index]);
        }

        internal bool Matches(IReadOnlyList<BlackboardInitializationPlan> plans)
        {
            if (_plans.Length != plans.Count) return false;
            for (var index = 0; index < _plans.Length; index++)
                if (!_plans[index].Matches(plans[index])) return false;
            return true;
        }

        private readonly struct PlanSnapshot
        {
            private readonly (int BoardId, string Name, string Scope, string OwnerId) _fields;
            private readonly KeySnapshot[]? _keys;

            internal PlanSnapshot(BlackboardInitializationPlan plan)
            {
                _fields = (plan.BoardId, plan.Name, plan.Scope, plan.OwnerId);
                _keys = plan.Keys == null ? null : new KeySnapshot[plan.Keys.Count];
                if (_keys == null) return;
                for (var index = 0; index < _keys.Length; index++)
                    _keys[index] = new KeySnapshot(plan.Keys[index]);
            }

            internal bool Matches(BlackboardInitializationPlan plan)
            {
                if (!_fields.Equals((plan.BoardId, plan.Name, plan.Scope, plan.OwnerId))) return false;
                if (_keys == null || plan.Keys == null) return _keys == null && plan.Keys == null;
                if (_keys.Length != plan.Keys.Count) return false;
                for (var index = 0; index < _keys.Length; index++)
                    if (!_keys[index].Matches(plan.Keys[index])) return false;
                return true;
            }
        }

        private readonly struct KeySnapshot
        {
            private readonly bool _present;
            private readonly (int Id, string Name, BlackboardKeyType Type, bool Read, bool Write) _schema;
            private readonly (int Int, bool Bool, int FloatBits, long DoubleBits, string String) _values;

            internal KeySnapshot(BlackboardInitializationKey? key)
            {
                _present = key != null;
                _schema = key != null ? (key.KeyId, key.Name, key.Type, key.CanRead, key.CanWrite) : default;
                _values = key != null ? ReadValues(key) : default;
            }

            internal bool Matches(BlackboardInitializationKey? key)
            {
                if (key == null) return !_present;
                return _present && _schema.Equals((key.KeyId, key.Name, key.Type, key.CanRead, key.CanWrite)) &&
                    _values.Equals(ReadValues(key));
            }

            private static (int, bool, int, long, string) ReadValues(BlackboardInitializationKey key)
            {
                /*
                 * 保留有符号零的区别；NaN 沿用原先只比较类别的行为。
                 */
                return (key.IntValue, key.BoolValue,
                    BitConverter.SingleToInt32Bits(float.IsNaN(key.FloatValue) ? float.NaN : key.FloatValue),
                    BitConverter.DoubleToInt64Bits(double.IsNaN(key.DoubleValue) ? double.NaN : key.DoubleValue),
                    key.StringValue);
            }
        }
    }
}
