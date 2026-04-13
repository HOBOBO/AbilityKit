using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;

namespace AbilityKit.Demo.Moba.Systems
{
    /// <summary>
    /// give_damage Action 閻ㄥ嫬宸辩猾璇茬€烽崣鍌涙殶
    /// </summary>
    public readonly struct GiveDamageArgs
    {
        /// <summary>
        /// 娴笺倕濂栭崐?
        /// </summary>
        public readonly float DamageValue;

        /// <summary>
        /// 娴笺倕濂栭崢鐔锋礈閸欏倹鏆熼敍鍫濆彠閼?DamageReasonKind閿?
        /// </summary>
        public readonly int ReasonParam;

        /// <summary>
        /// 娴笺倕濂栫猾璇茬€烽敍鍫㈠⒖閻?姒勬梹纭?閻喎鐤勯敍?
        /// </summary>
        public readonly DamageType DamageType;

        public GiveDamageArgs(float damageValue, int reasonParam, DamageType damageType = DamageType.Physical)
        {
            DamageValue = damageValue;
            ReasonParam = reasonParam;
            DamageType = damageType;
        }

        public static GiveDamageArgs Default => new GiveDamageArgs(0f, 0, DamageType.Physical);
    }
}