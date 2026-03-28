using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using Newtonsoft.Json;
using UnityEngine;

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

        public static VfxDatabase LoadFromResources(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException(nameof(path));

            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException($"Vfx json not found in Resources: {path}");

            var json = asset.text;
            if (string.IsNullOrEmpty(json)) throw new InvalidOperationException($"Vfx json is empty: {path}");

            var arr = JsonConvert.DeserializeObject<VfxDTO[]>(json);
            var dict = new Dictionary<int, VfxDTO>();
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var dto = arr[i];
                    if (dto == null || dto.Id <= 0) continue;
                    dict[dto.Id] = dto;
                }
            }

            return new VfxDatabase(dict);
        }
    }
}
