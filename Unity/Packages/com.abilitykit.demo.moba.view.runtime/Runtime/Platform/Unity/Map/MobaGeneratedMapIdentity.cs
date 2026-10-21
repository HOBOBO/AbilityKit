using System;
using UnityEngine;

namespace AbilityKit.Demo.Moba.View
{
    [DisallowMultipleComponent]
    public sealed class MobaGeneratedMapIdentity : MonoBehaviour
    {
        [SerializeField] private int _mapId;

        public int MapId => _mapId;

        public void SetMapId(int mapId)
        {
            if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId));
            _mapId = mapId;
        }
    }
}
