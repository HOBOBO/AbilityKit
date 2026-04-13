using System;

namespace AbilityKit.Demo.Moba.Services
{
    // ========================================================================
    // 鏉′欢 DTO 鍩虹被 鈥?鐢ㄤ簬 Luban 瀵煎嚭
    //
    // 璁捐鍘熷垯:
    //  1. 鎵€鏈夋潯浠?DTO 缁ф壙姝ゆ娊璞＄被锛岀敤浜?Luban 璇嗗埆鍜屽鍑?
    //  2. 鍩虹被鏄┖鐨勶紝鍏蜂綋鍙傛暟鍦ㄥ悇鑷殑 DTO 绫讳腑瀹氫箟
    //  3. 閫氳繃杞崲鍣ㄨ浆涓鸿Е鍙戝櫒鐨?ICondition锛屽疄鐜颁唬鐮佸鐢?
    // ========================================================================

    /// <summary>
    /// 鏉′欢 DTO 绌哄熀绫?
    /// 鎵€鏈夐厤缃寲鏉′欢閮藉簲缁ф壙姝ょ被锛岀敤浜?Luban 瀵煎嚭璇嗗埆
    /// </summary>
    [Serializable]
    public abstract class SkillConditionDTO
    {
        /// <summary>
        /// 鏉′欢绫诲瀷鏍囪瘑锛堝搴旇Е鍙戝櫒涓殑 Condition 绫诲瀷鍚嶏級
        /// </summary>
        public string Type;
    }

    // ========================================================================
    // 绠€鍗曟潯浠?DTO锛堟棤棰濆鍙傛暟锛?
    // ========================================================================

    /// <summary>
    /// 甯搁噺鏉′欢 DTO - 濮嬬粓杩斿洖鍥哄畾缁撴灉
    /// </summary>
    [Serializable]
    public class ConstConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 甯搁噺鍊硷紙true=閫氳繃锛宖alse=澶辫触锛?
        /// </summary>
        public bool Value = true;

        public ConstConditionDTO()
        {
            Type = "Const";
        }
    }

    /// <summary>
    /// 鐩爣瀛樺湪鏉′欢 DTO
    /// </summary>
    [Serializable]
    public class HasTargetConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 鏄惁鍙栧弽锛坱rue=瑕佹眰娌℃湁鐩爣锛?
        /// </summary>
        public bool Negate;

        public HasTargetConditionDTO()
        {
            Type = "HasTarget";
        }
    }

    // ========================================================================
    // 澶嶅悎鏉′欢 DTO
    // ========================================================================

    /// <summary>
    /// And 缁勫悎鏉′欢 DTO
    /// </summary>
    [Serializable]
    public class AndConditionDTO : SkillConditionDTO
    {
        public SkillConditionDTO Left;
        public SkillConditionDTO Right;

        public AndConditionDTO()
        {
            Type = "And";
        }
    }

    /// <summary>
    /// Or 缁勫悎鏉′欢 DTO
    /// </summary>
    [Serializable]
    public class OrConditionDTO : SkillConditionDTO
    {
        public SkillConditionDTO Left;
        public SkillConditionDTO Right;

        public OrConditionDTO()
        {
            Type = "Or";
        }
    }

    /// <summary>
    /// Not 鏉′欢 DTO
    /// </summary>
    [Serializable]
    public class NotConditionDTO : SkillConditionDTO
    {
        public SkillConditionDTO Inner;

        public NotConditionDTO()
        {
            Type = "Not";
        }
    }

    /// <summary>
    /// 澶氭潯浠剁粍鍚?DTO锛堟敮鎸佸涓潯浠讹級
    /// </summary>
    [Serializable]
    public class MultiConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 缁勫悎鏂瑰紡锛? = And, 1 = Or
        /// </summary>
        public int Combinator;

        /// <summary>
        /// 瀛愭潯浠跺垪琛?
        /// </summary>
        public SkillConditionDTO[] Conditions;

        public MultiConditionDTO()
        {
            Type = "Multi";
        }
    }

    // ========================================================================
    // 鏁板€煎紩鐢?DTO
    // 涓庤Е鍙戝櫒鍖呬腑鐨?NumericValueRef 瀵归綈
    // ========================================================================

    /// <summary>
    /// 鏁板€煎紩鐢ㄧ被鍨嬶紙涓?ENumericValueRefKind 瀵归綈锛?
    /// </summary>
    [Serializable]
    public enum ENumericRefKind : byte
    {
        Const = 0,
        Blackboard = 1,
        PayloadField = 2,
        Var = 3,
        Expr = 4,
    }

    /// <summary>
    /// 姣旇緝鎿嶄綔绗?
    /// </summary>
    [Serializable]
    public enum ECompareOp : byte
    {
        Equal = 0,
        NotEqual = 1,
        GreaterThan = 2,
        GreaterThanOrEqual = 3,
        LessThan = 4,
        LessThanOrEqual = 5,
    }

    /// <summary>
    /// 鏁板€煎紩鐢?DTO
    /// 涓庤Е鍙戝櫒鍖呬腑鐨?NumericValueRef 缁撴瀯瀵归綈
    /// </summary>
    [Serializable]
    public class NumericRefDTO
    {
        /// <summary>
        /// 寮曠敤绫诲瀷
        /// </summary>
        public ENumericRefKind Kind;

        /// <summary>
        /// 甯搁噺鍊硷紙Kind = Const 鏃朵娇鐢級
        /// </summary>
        public double ConstValue;

        /// <summary>
        /// 榛戞澘ID锛圞ind = Blackboard 鏃朵娇鐢級
        /// </summary>
        public int BoardId;

        /// <summary>
        /// 榛戞澘閿甀D锛圞ind = Blackboard 鏃朵娇鐢級
        /// </summary>
        public int KeyId;

        /// <summary>
        /// 瀛楁ID锛圞ind = PayloadField 鏃朵娇鐢級
        /// </summary>
        public int FieldId;

        /// <summary>
        /// 鍩烮D锛圞ind = Var 鏃朵娇鐢級
        /// </summary>
        public string DomainId;

        /// <summary>
        /// 閿悕锛圞ind = Var 鏃朵娇鐢級
        /// </summary>
        public string Key;

        /// <summary>
        /// 琛ㄨ揪寮忔枃鏈紙Kind = Expr 鏃朵娇鐢級
        /// </summary>
        public string ExprText;

        /// <summary>
        /// 鍒涘缓甯搁噺寮曠敤
        /// </summary>
        public static NumericRefDTO Const(double value) => new NumericRefDTO { Kind = ENumericRefKind.Const, ConstValue = value };

        /// <summary>
        /// 鍒涘缓榛戞澘寮曠敤
        /// </summary>
        public static NumericRefDTO Blackboard(int boardId, int keyId) => new NumericRefDTO { Kind = ENumericRefKind.Blackboard, BoardId = boardId, KeyId = keyId };

        /// <summary>
        /// 鍒涘缓 Payload 瀛楁寮曠敤
        /// </summary>
        public static NumericRefDTO PayloadField(int fieldId) => new NumericRefDTO { Kind = ENumericRefKind.PayloadField, FieldId = fieldId };

        /// <summary>
        /// 鍒涘缓鍙橀噺寮曠敤
        /// </summary>
        public static NumericRefDTO Var(string domainId, string key) => new NumericRefDTO { Kind = ENumericRefKind.Var, DomainId = domainId, Key = key };

        /// <summary>
        /// 鍒涘缓琛ㄨ揪寮忓紩鐢?
        /// </summary>
        public static NumericRefDTO Expr(string exprText) => new NumericRefDTO { Kind = ENumericRefKind.Expr, ExprText = exprText };
    }

    /// <summary>
    /// 鏁板€兼瘮杈冩潯浠?DTO
    /// </summary>
    [Serializable]
    public class NumericCompareConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 姣旇緝鎿嶄綔绗?
        /// </summary>
        public ECompareOp Op;

        /// <summary>
        /// 宸︽搷浣滄暟
        /// </summary>
        public NumericRefDTO Left;

        /// <summary>
        /// 鍙虫搷浣滄暟
        /// </summary>
        public NumericRefDTO Right;

        public NumericCompareConditionDTO()
        {
            Type = "NumericCompare";
        }
    }

    /// <summary>
    /// Payload 瀛楁姣旇緝鏉′欢 DTO
    /// </summary>
    [Serializable]
    public class PayloadCompareConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// Payload 瀛楁ID
        /// </summary>
        public int FieldId;

        /// <summary>
        /// 姣旇緝鎿嶄綔绗?
        /// </summary>
        public ECompareOp Op;

        /// <summary>
        /// 姣旇緝鍊?
        /// </summary>
        public NumericRefDTO CompareValue;

        /// <summary>
        /// 鏄惁鍙栧弽
        /// </summary>
        public bool Negate;

        public PayloadCompareConditionDTO()
        {
            Type = "PayloadCompare";
        }
    }

    // ========================================================================
    // Moba 鐗规湁鏉′欢 DTO
    // 杩欎簺鏄?Moba 涓氬姟鐗规湁鐨勬潯浠讹紝涓嶉€氱敤
    // ========================================================================

    /// <summary>
    /// 鍐峰嵈鏉′欢 DTO锛圡oba 鐗规湁锛?
    /// </summary>
    [Serializable]
    public class CooldownConditionDTO : SkillConditionDTO
    {
        public CooldownConditionDTO()
        {
            Type = "Moba_Cooldown";
        }
    }

    /// <summary>
    /// 鏂芥硶鐘舵€佹潯浠?DTO锛圡oba 鐗规湁锛?
    /// </summary>
    [Serializable]
    public class CastingStateConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 鏄惁妫€鏌ユ鍦ㄦ柦娉曪紙false=妫€鏌ユ湭鍦ㄦ柦娉曪級
        /// </summary>
        public bool ExpectCasting;

        public CastingStateConditionDTO()
        {
            Type = "Moba_CastingState";
        }
    }

    /// <summary>
    /// 鑷韩閲婃斁鏉′欢 DTO锛圡oba 鐗规湁锛?
    /// </summary>
    [Serializable]
    public class SelfOnlyConditionDTO : SkillConditionDTO
    {
        public SelfOnlyConditionDTO()
        {
            Type = "Moba_SelfOnly";
        }
    }

    /// <summary>
    /// 鏍囩鏉′欢 DTO锛圡oba 鐗规湁锛屽熀浜庤Е鍙戝櫒鐨?TagQuery锛?
    /// </summary>
    [Serializable]
    public class TagConditionDTO : SkillConditionDTO
    {
        /// <summary>
        /// 闇€瑕佺殑鏍囩鍒楄〃
        /// </summary>
        public string[] RequiredTags;

        /// <summary>
        /// 蹇界暐鐨勬爣绛惧垪琛?
        /// </summary>
        public string[] IgnoreTags;

        /// <summary>
        /// 鏄惁鍙栧弽
        /// </summary>
        public bool Negate;

        public TagConditionDTO()
        {
            Type = "Moba_Tag";
        }
    }
}
