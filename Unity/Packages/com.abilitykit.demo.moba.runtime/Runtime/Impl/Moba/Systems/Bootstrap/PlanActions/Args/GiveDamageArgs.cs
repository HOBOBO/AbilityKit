using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// give_damage Action 鐨勫己绫诲瀷鍙傛暟
    /// </summary>
    public readonly struct GiveDamageArgs
    {
        /// <summary>
        /// 浼ゅ鍊?
        /// </summary>
        public readonly float DamageValue;

        /// <summary>
        /// 浼ゅ鍘熷洜鍙傛暟锛堝叧鑱?DamageReasonKind锛?
        /// </summary>
        public readonly int ReasonParam;

        /// <summary>
        /// 浼ゅ绫诲瀷锛堢墿鐞?榄旀硶/鐪熷疄锛?
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
