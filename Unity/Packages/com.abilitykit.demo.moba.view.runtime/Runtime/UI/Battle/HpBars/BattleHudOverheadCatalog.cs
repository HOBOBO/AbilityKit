using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Unity 只加载布局与显示语义，不读取玩法来源。
    /// </summary>
    internal sealed class BattleHudOverheadCatalog
    {
        private readonly Dictionary<int, OverheadLayoutDTO> _layouts = new Dictionary<int, OverheadLayoutDTO>();
        private readonly Dictionary<int, IndicatorDefinitionDTO> _definitions = new Dictionary<int, IndicatorDefinitionDTO>();
        public BattleHudOverheadCatalog()
        {
            var layouts = Resources.Load<TextAsset>("luban/moba/overhead_layouts");
            var definitions = Resources.Load<TextAsset>("luban/moba/indicator_definitions");
            if (layouts == null || definitions == null) throw new InvalidOperationException("Missing published overhead tables.");
            foreach (var row in JsonUtility.FromJson<Layouts>("{\"Items\":" + layouts.text + "}").Items) _layouts.Add(row.Id, row);
            foreach (var row in JsonUtility.FromJson<Definitions>("{\"Items\":" + definitions.text + "}").Items) _definitions.Add(row.Id, row);
        }
        public OverheadLayoutDTO Layout(int id) => _layouts.TryGetValue(id, out var row) ? row : null;
        public IndicatorDefinitionDTO Indicator(int id) => _definitions.TryGetValue(id, out var row) ? row : null;
        [Serializable] private sealed class Layouts { public OverheadLayoutDTO[] Items; }
        [Serializable] private sealed class Definitions { public IndicatorDefinitionDTO[] Items; }
    }
}
