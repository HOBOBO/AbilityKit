using AbilityKit.Configs;
using AbilityKit.Triggering;
using UnityEngine;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class GlobalVarInstaller : MonoBehaviour
    {
        public GlobalVarsSO Asset;
        public bool ApplyOnAwake = true;

        private void Awake()
        {
            if (!ApplyOnAwake) return;
            if (Asset == null) return;
            Asset.ApplyToGlobalStore();
        }
    }
}
