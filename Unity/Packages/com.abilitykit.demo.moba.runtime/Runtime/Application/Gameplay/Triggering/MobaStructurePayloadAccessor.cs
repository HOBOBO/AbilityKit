using AbilityKit.Triggering.Payload;
using AbilityKit.Demo.Moba.Gameplay.Structures;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Core.Eventing;

namespace AbilityKit.Demo.Moba.Gameplay.Triggering
{
    public sealed class MobaStructurePayloadAccessor :
        IPayloadIntAccessor<MobaStructureEventArgs>, IPayloadDoubleAccessor<MobaStructureEventArgs>
    {
        private static readonly int StructureActorIdId = StableStringId.Get("payload:" + MobaStructurePayloadFields.StructureActorId);
        private static readonly int StructureIdId = StableStringId.Get("payload:" + MobaStructurePayloadFields.StructureId);
        private static readonly int KindId = StableStringId.Get("payload:" + MobaStructurePayloadFields.Kind);
        private static readonly int TeamIdId = StableStringId.Get("payload:" + MobaStructurePayloadFields.TeamId);
        private static readonly int SourceActorIdId = StableStringId.Get("payload:" + MobaStructurePayloadFields.SourceActorId);
        private static readonly int DamageId = StableStringId.Get("payload:" + MobaStructurePayloadFields.Damage);
        private static readonly int RemainingHpId = StableStringId.Get("payload:" + MobaStructurePayloadFields.RemainingHp);

        public static bool SupportsStructureField(int fieldId) => fieldId == StructureActorIdId || fieldId == StructureIdId || fieldId == KindId || fieldId == TeamIdId || fieldId == SourceActorIdId;
        public static bool SupportsStructureDoubleField(int fieldId) => fieldId == DamageId || fieldId == RemainingHpId || SupportsStructureField(fieldId);

        public bool TryGet(in MobaStructureEventArgs args, int fieldId, out int value)
        {
            if (fieldId == StructureActorIdId) value = args.StructureActorId;
            else if (fieldId == StructureIdId) value = args.StructureId;
            else if (fieldId == KindId) value = (int)args.Kind;
            else if (fieldId == TeamIdId) value = args.TeamId;
            else if (fieldId == SourceActorIdId) value = args.SourceActorId;
            else { value = default; return false; }
            return true;
        }

        public bool TryGet(in MobaStructureEventArgs args, int fieldId, out double value)
        {
            if (fieldId == DamageId) value = args.Damage;
            else if (fieldId == RemainingHpId) value = args.RemainingHp;
            else if (TryGet(in args, fieldId, out int integer)) value = integer;
            else { value = default; return false; }
            return true;
        }
    }
}
