using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Share.Impl.Moba.Components;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    /// <summary>
    /// consume_resource Action 鐨勫己绫诲瀷鍙傛暟
    /// 鐢ㄤ簬鎶€鑳介噴鏀炬椂娑堣€楄祫婧愶紙钃濋噺/鐢熷懡鍊?鑳介噺绛夛級
    /// </summary>
    public readonly struct ConsumeResourceArgs
    {
        /// <summary>
        /// 璧勬簮绫诲瀷
        /// </summary>
        public readonly ResourceType ResourceType;

        /// <summary>
        /// 娑堣€楅噺锛堥€氳繃 NumericValueRef 鏀寔甯搁噺/榛戞澘/鍙橀噺绛夊绉嶆潵婧愶級
        /// </summary>
        public readonly float Amount;

        /// <summary>
        /// 娑堣€楀け璐ユ椂鐨勬彁绀轰俊鎭?Key
        /// </summary>
        public readonly string FailMessageKey;

        public ConsumeResourceArgs(ResourceType resourceType, float amount, string failMessageKey = null)
        {
            ResourceType = resourceType;
            Amount = amount;
            FailMessageKey = failMessageKey ?? "not_enough_resource";
        }

        public static ConsumeResourceArgs Default => new ConsumeResourceArgs(ResourceType.Mana, 0f, "not_enough_mana");
    }
}
