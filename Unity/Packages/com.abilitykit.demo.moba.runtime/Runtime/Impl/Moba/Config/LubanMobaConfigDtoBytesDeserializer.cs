using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class LubanMobaConfigDtoBytesDeserializer : IMobaConfigDtoBytesDeserializer
    {
        public Array DeserializeDtoArray(byte[] bytes, Type dtoType)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (dtoType == null) throw new ArgumentNullException(nameof(dtoType));

            if (dtoType == typeof(global::cfg.DRBuff))
            {
                var buf = global::Luban.ByteBuf.Wrap(bytes);
                var table = new global::cfg.Buffs(buf);
                return table.DataList.ToArray();
            }

            throw new NotSupportedException($"Luban bytes deserialization not supported for dtoType: {dtoType.FullName}. Migrate the table and extend {nameof(LubanMobaConfigDtoBytesDeserializer)}.");
        }
    }
}
