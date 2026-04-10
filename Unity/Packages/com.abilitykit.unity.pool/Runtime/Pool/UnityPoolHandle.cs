#if UNITY_5_3_OR_NEWER
using UnityEngine;

namespace AbilityKit.Core.Common.Pool
{
    internal sealed class UnityPoolHandle : MonoBehaviour
    {
        [SerializeField] internal int PrefabInstanceId;
        [SerializeField] internal string Key;
    }
}
#endif
