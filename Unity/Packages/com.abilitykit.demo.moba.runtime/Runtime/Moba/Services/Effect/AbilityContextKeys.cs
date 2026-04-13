using System;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 缁熶竴鐨勮兘鍔涗笂涓嬫枃閿灇涓?
    /// 鐢ㄤ簬鏇夸唬 Magic String锛岄伩鍏嶉敭鍚嶆嫾鍐欓敊璇?
    /// </summary>
    public enum AbilityContextKeys
    {
        // ========== 婧簮鐩稿叧 ==========
        /// <summary>
        /// 婧簮涓婁笅鏂嘔D锛堝凡鍦ㄦ帴鍙ｄ腑鐩存帴瀹氫箟涓?SourceContextId 灞炴€э級
        /// </summary>
        SourceContextId,

        /// <summary>
        /// 涓婁笅鏂囩被鍨?
        /// </summary>
        ContextKind,

        // ========== 鍙備笌鑰呯浉鍏?==========
        /// <summary>
        /// 婧愯鑹睮D锛堟妧鑳芥柦娉曡€?Buff鏉ユ簮绛夛級
        /// </summary>
        SourceActorId,

        /// <summary>
        /// 鐩爣瑙掕壊ID
        /// </summary>
        TargetActorId,

        // ========== 鎶€鑳界浉鍏?==========
        /// <summary>
        /// 鎶€鑳絀D
        /// </summary>
        SkillId,

        /// <summary>
        /// 鎶€鑳芥Ы浣?
        /// </summary>
        SkillSlot,

        /// <summary>
        /// 鎶€鑳界瓑绾?
        /// </summary>
        SkillLevel,

        /// <summary>
        /// 鏂芥硶搴忓垪鍙?
        /// </summary>
        CastSequence,

        /// <summary>
        /// 鐩爣浣嶇疆
        /// </summary>
        AimPos,

        /// <summary>
        /// 鐩爣鏂瑰悜
        /// </summary>
        AimDir,

        /// <summary>
        /// 澶辫触鍘熷洜
        /// </summary>
        FailReason,

        // ========== Buff 鐩稿叧 ==========
        /// <summary>
        /// Buff ID
        /// </summary>
        BuffId,

        /// <summary>
        /// Buff 鍙犲姞灞傛暟
        /// </summary>
        BuffStackCount,

        // ========== 瀛愬脊鐩稿叧 ==========
        /// <summary>
        /// 瀛愬脊 ID
        /// </summary>
        ProjectileId,

        /// <summary>
        /// 瀛愬脊鍙戝皠浣嶇疆
        /// </summary>
        LaunchPosition,

        /// <summary>
        /// 瀛愬脊鍙戝皠鏂瑰悜
        /// </summary>
        LaunchDirection,

        /// <summary>
        /// 瀛愬脊閫熷害
        /// </summary>
        ProjectileSpeed,

        /// <summary>
        /// 瀛愬脊鍛戒腑瑙﹀彂鍣↖D
        /// </summary>
        HitTriggerPlanId,

        // ========== AOE 鍖哄煙鐩稿叧 ==========
        /// <summary>
        /// AOE 鍖哄煙 ID
        /// </summary>
        AreaId,

        /// <summary>
        /// AOE 涓績浣嶇疆
        /// </summary>
        AreaCenter,

        /// <summary>
        /// AOE 鍗婂緞
        /// </summary>
        AreaRadius,

        /// <summary>
        /// AOE 杩涘叆瑙﹀彂鍣↖D
        /// </summary>
        AreaEnterTriggerPlanId,

        /// <summary>
        /// AOE 绂诲紑瑙﹀彂鍣↖D
        /// </summary>
        AreaLeaveTriggerPlanId,

        // ========== 鍛戒腑鐩稿叧 ==========
        /// <summary>
        /// 鍛戒腑浣嶇疆
        /// </summary>
        HitPosition,

        /// <summary>
        /// 鍛戒腑娉曠嚎
        /// </summary>
        HitNormal,

        // ========== 鏃堕棿杞寸浉鍏?==========
        /// <summary>
        /// 鏃堕棿杞翠笅涓€涓簨浠剁储寮?
        /// </summary>
        TimelineNextEventIndex,

        // ========== 琚姩鎶€鑳界浉鍏?==========
        /// <summary>
        /// 琚姩鎶€鑳?ID
        /// </summary>
        PassiveSkillId,

        /// <summary>
        /// 琚姩鎶€鑳藉喎鍗寸粨鏉熸椂闂?
        /// </summary>
        PassiveCooldownEndTimeMs,
    }

    /// <summary>
    /// 涓婁笅鏂囬敭瀛楃涓叉槧灏?
    /// 鎻愪緵鏋氫妇鍒板瓧绗︿覆鐨勬槧灏?
    /// </summary>
    public sealed class AbilityContextKeyStrings
    {
        private static readonly string[] _keys;

        static AbilityContextKeyStrings()
        {
            _keys = new string[Enum.GetValues(typeof(AbilityContextKeys)).Length];
            foreach (AbilityContextKeys key in Enum.GetValues(typeof(AbilityContextKeys)))
            {
                _keys[(int)key] = ConvertToKeyString(key);
            }
        }

        /// <summary>
        /// 鑾峰彇閿殑瀛楃涓茶〃绀?
        /// </summary>
        public static string GetKey(AbilityContextKeys key)
        {
            return _keys[(int)key];
        }

        private static string ConvertToKeyString(AbilityContextKeys key)
        {
            var name = key.ToString();
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0)
                {
                    result.Append('.');
                }
                result.Append(char.ToLower(c));
            }
            return result.ToString();
        }
    }

    /// <summary>
    /// AbilityContextKeys 鏋氫妇鐨勬墿灞曟柟娉?
    /// </summary>
    public static class AbilityContextKeysExtensions
    {
        /// <summary>
        /// 鑾峰彇閿殑瀛楃涓茶〃绀?
        /// </summary>
        public static string ToKeyString(this AbilityContextKeys key)
        {
            return AbilityContextKeyStrings.GetKey(key);
        }
    }
}
