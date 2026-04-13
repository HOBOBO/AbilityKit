using System;

namespace AbilityKit.Demo.Moba.Services
{
    // ========================================================================
    // 鎶€鑳介噴鏀炬祦绋嬩腑鐨勫彲閫夊鐞嗛」
    //
    // 璁捐鎬濊矾锛?
    //  1. 姣忎釜澶勭悊椤归兘鏄竴涓嫭绔嬬殑妯″潡锛屽彲閫夊湴娣诲姞鍒版妧鑳芥祦绋嬩腑
    //  2. 閫氳繃閰嶇疆鍐冲畾鏈夊摢浜涘鐞嗛」锛屼互鍙婂畠浠殑鎵ц椤哄簭
    //  3. 澶勭悊椤瑰彲浠ユ槸"妫€鏌?锛堟潯浠堕獙璇侊級鎴?鎿嶄綔"锛堟秷鑰椼€丅uff绛夛級
    //  4. 涓嶇粦瀹氬叿浣撲笟鍔￠€昏緫锛岄€氳繃 ActionId 璺敱鍒板叿浣撳疄鐜?
    //
    // Luban 瀵煎嚭绀轰緥锛?
    // {
    //   "SkillId": 1001,
    //   "PreCastHandlers": [
    //     { "Type": "check_cooldown" },
    //     { "Type": "check_resource" },
    //     { "Type": "consume_mana", "Amount": 100 },
    //     { "Type": "apply_buff", "BuffId": 2001 }
    //   ]
    // }
    // ========================================================================

    /// <summary>
    /// 澶勭悊椤圭被鍨?
    /// </summary>
    public enum EHandlerType
    {
        // ========== 妫€鏌ョ被 ==========
        /// <summary>鍐峰嵈妫€鏌?/summary>
        CheckCooldown = 1,
        /// <summary>璧勬簮妫€鏌ワ紙璧勬簮鏄惁瓒冲锛屼絾涓嶆墸闄わ級</summary>
        CheckResource = 2,
        /// <summary>鐘舵€佹鏌ワ紙鐪╂檿銆佹矇榛樼瓑锛?/summary>
        CheckState = 3,
        /// <summary>鐩爣妫€鏌ワ紙璺濈銆佹湁鏁堟€х瓑锛?/summary>
        CheckTarget = 4,

        // ========== 鎿嶄綔绫?==========
        /// <summary>娑堣€楄祫婧愶紙妫€鏌ュ悗瀹為檯鎵ｉ櫎锛?/summary>
        ConsumeResource = 101,
        /// <summary>寮€濮嬪喎鍗?/summary>
        StartCooldown = 102,
        /// <summary>娣诲姞Buff</summary>
        ApplyBuff = 103,
        /// <summary>娣诲姞鏍囩</summary>
        AddTag = 104,
        /// <summary>绉婚櫎鏍囩</summary>
        RemoveTag = 105,

        // ========== 閫氱敤 ==========
        /// <summary>鑷畾涔堿ction锛堥€氳繃 ActionId 璺敱锛?/summary>
        CustomAction = 1000,
    }

    /// <summary>
    /// 鎶€鑳藉鐞嗛」 DTO 鍩虹被
    /// 鎵€鏈夊鐞嗛」 DTO 閮藉簲缁ф壙姝ょ被
    /// </summary>
    [Serializable]
    public abstract class SkillHandlerDTO
    {
        /// <summary>
        /// 澶勭悊椤圭被鍨?
        /// </summary>
        public int Type;
    }

    // ========================================================================
    // 妫€鏌ョ被澶勭悊椤?
    // ========================================================================

    /// <summary>
    /// 鍐峰嵈妫€鏌ュ鐞嗛」
    /// </summary>
    [Serializable]
    public class CheckCooldownDTO : SkillHandlerDTO
    {
        public CheckCooldownDTO()
        {
            Type = (int)EHandlerType.CheckCooldown;
        }
    }

    /// <summary>
    /// 璧勬簮妫€鏌ュ鐞嗛」锛堟鏌ヨ祫婧愭槸鍚﹁冻澶燂紝涓嶆墸闄わ級
    /// </summary>
    [Serializable]
    public class CheckResourceDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 璧勬簮绫诲瀷锛?=Mana, 1=Hp, 2=Energy, ...锛?
        /// </summary>
        public int ResourceType;

        /// <summary>
        /// 闇€瑕佹鏌ョ殑鏈€灏忓€硷紙閫氳繃 NumericRefDTO 鏀寔甯搁噺/鍙橀噺绛夛級
        /// </summary>
        public NumericRefDTO MinAmount;

        public CheckResourceDTO()
        {
            Type = (int)EHandlerType.CheckResource;
        }
    }

    /// <summary>
    /// 鐘舵€佹鏌ュ鐞嗛」锛堢湬鏅曘€佹矇榛樸€佺瑷€绛夛級
    /// </summary>
    [Serializable]
    public class CheckStateDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 瑕佹鏌ョ殑鐘舵€佹爣绛惧垪琛?
        /// </summary>
        public string[] RequiredTags;

        /// <summary>
        /// 涓嶈兘鏈夌殑鐘舵€佹爣绛惧垪琛?
        /// </summary>
        public string[] BlockedTags;

        /// <summary>
        /// 妫€鏌ュ璞★細0=Caster, 1=Target
        /// </summary>
        public int Target;

        public CheckStateDTO()
        {
            Type = (int)EHandlerType.CheckState;
        }
    }

    /// <summary>
    /// 鐩爣妫€鏌ュ鐞嗛」
    /// </summary>
    [Serializable]
    public class CheckTargetDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 鏄惁蹇呴』鏈夌洰鏍?
        /// </summary>
        public bool RequireTarget;

        /// <summary>
        /// 鏈€灏忚窛绂伙紙0琛ㄧず涓嶆鏌ワ級
        /// </summary>
        public NumericRefDTO MinDistance;

        /// <summary>
        /// 鏈€澶ц窛绂伙紙0琛ㄧず涓嶆鏌ワ級
        /// </summary>
        public NumericRefDTO MaxDistance;

        /// <summary>
        /// 鐩爣蹇呴』婊¤冻鐨勬爣绛?
        /// </summary>
        public string[] TargetTags;

        public CheckTargetDTO()
        {
            Type = (int)EHandlerType.CheckTarget;
        }
    }

    // ========================================================================
    // 鎿嶄綔绫诲鐞嗛」
    // ========================================================================

    /// <summary>
    /// 璧勬簮娑堣€楀鐞嗛」
    /// </summary>
    [Serializable]
    public class ConsumeResourceDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 璧勬簮绫诲瀷
        /// </summary>
        public int ResourceType;

        /// <summary>
        /// 娑堣€楅噺锛堟敮鎸佸父閲?鍙橀噺/琛ㄨ揪寮忥級
        /// </summary>
        public NumericRefDTO Amount;

        /// <summary>
        /// 澶辫触鎻愮ず Key
        /// </summary>
        public string FailMessageKey;

        public ConsumeResourceDTO()
        {
            Type = (int)EHandlerType.ConsumeResource;
        }
    }

    /// <summary>
    /// 寮€濮嬪喎鍗村鐞嗛」
    /// </summary>
    [Serializable]
    public class StartCooldownDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 鍐峰嵈鏃堕棿锛堟绉掞紝鏀寔鍙橀噺锛?
        /// </summary>
        public NumericRefDTO CooldownMs;

        public StartCooldownDTO()
        {
            Type = (int)EHandlerType.StartCooldown;
        }
    }

    /// <summary>
    /// 娣诲姞Buff澶勭悊椤?
    /// </summary>
    [Serializable]
    public class ApplyBuffDTO : SkillHandlerDTO
    {
        /// <summary>
        /// Buff閰嶇疆ID
        /// </summary>
        public int BuffId;

        /// <summary>
        /// 娣诲姞鐩爣锛?=Caster, 1=Target
        /// </summary>
        public int Target;

        /// <summary>
        /// 鍙犲姞绛栫暐
        /// </summary>
        public int StackPolicy;

        public ApplyBuffDTO()
        {
            Type = (int)EHandlerType.ApplyBuff;
        }
    }

    /// <summary>
    /// 娣诲姞鏍囩澶勭悊椤?
    /// </summary>
    [Serializable]
    public class AddTagDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 瑕佹坊鍔犵殑鏍囩鍒楄〃
        /// </summary>
        public string[] Tags;

        /// <summary>
        /// 娣诲姞鐩爣锛?=Caster, 1=Target
        /// </summary>
        public int Target;

        /// <summary>
        /// 鎸佺画鏃堕棿锛堟绉掞級锛?1琛ㄧず姘镐箙
        /// </summary>
        public NumericRefDTO DurationMs;

        public AddTagDTO()
        {
            Type = (int)EHandlerType.AddTag;
        }
    }

    /// <summary>
    /// 绉婚櫎鏍囩澶勭悊椤?
    /// </summary>
    [Serializable]
    public class RemoveTagDTO : SkillHandlerDTO
    {
        /// <summary>
        /// 瑕佺Щ闄ょ殑鏍囩鍒楄〃
        /// </summary>
        public string[] Tags;

        /// <summary>
        /// 绉婚櫎鐩爣锛?=Caster, 1=Target
        /// </summary>
        public int Target;

        public RemoveTagDTO()
        {
            Type = (int)EHandlerType.RemoveTag;
        }
    }

    // ========================================================================
    // 閫氱敤澶勭悊椤?
    // ========================================================================

    /// <summary>
    /// 鑷畾涔堿ction澶勭悊椤?
    /// 閫氳繃 ActionId 璺敱鍒板叿浣撶殑 PlanAction 瀹炵幇
    /// </summary>
    [Serializable]
    public class CustomActionDTO : SkillHandlerDTO
    {
        /// <summary>
        /// Action鍚嶇О锛堝 "consume_resource", "give_damage" 绛夛級
        /// </summary>
        public string ActionName;

        /// <summary>
        /// 鍏峰悕鍙傛暟瀛楀吀
        /// </summary>
        public NamedArgDTO[] Args;

        public CustomActionDTO()
        {
            Type = (int)EHandlerType.CustomAction;
        }
    }

    /// <summary>
    /// 鍏峰悕鍙傛暟 DTO
    /// </summary>
    [Serializable]
    public class NamedArgDTO
    {
        /// <summary>
        /// 鍙傛暟鍚嶇О
        /// </summary>
        public string Name;

        /// <summary>
        /// 鍙傛暟鍊?
        /// </summary>
        public NumericRefDTO Value;
    }

    // ========================================================================
    // 鎶€鑳芥祦绋嬮厤缃?
    // ========================================================================

    /// <summary>
    /// 鎶€鑳芥祦绋嬪鐞嗛厤缃?
    /// 鍖呭惈鎵€鏈夊彲閫夌殑澶勭悊椤?
    /// </summary>
    [Serializable]
    public class SkillFlowHandlerConfigDTO
    {
        /// <summary>
        /// 閲婃斁鍓嶅鐞嗛」鍒楄〃锛堟寜椤哄簭鎵ц锛?
        /// </summary>
        public SkillHandlerDTO[] PreCastHandlers;

        /// <summary>
        /// 閲婃斁鍚庡鐞嗛」鍒楄〃
        /// </summary>
        public SkillHandlerDTO[] PostCastHandlers;

        /// <summary>
        /// 閲婃斁澶辫触鏃跺鐞嗛」鍒楄〃锛堢敤浜庡洖婊氾級
        /// </summary>
        public SkillHandlerDTO[] OnFailHandlers;
    }
}
