using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Game.Battle.Vfx
{
    public sealed class VfxDatabase
    {
        private readonly Dictionary<int, VfxDTO> _byId;

        public VfxDatabase(Dictionary<int, VfxDTO> byId)
        {
            _byId = byId ?? throw new ArgumentNullException(nameof(byId));
        }

        public bool TryGet(int id, out VfxDTO dto)
        {
            return _byId.TryGetValue(id, out dto);
        }

        public static VfxDatabase FromEntries(IEnumerable<VfxDTO> entries)
        {
            var dict = new Dictionary<int, VfxDTO>();
            if (entries != null)
            {
                foreach (var dto in entries)
                {
                    if (dto == null || dto.Id <= 0) continue;
                    dict[dto.Id] = dto;
                }
            }

            return new VfxDatabase(dict);
        }
    }
}
