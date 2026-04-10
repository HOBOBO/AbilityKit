namespace AbilityKit.Ability.Impl.Moba
{
    using AbilityKit.Ability.Impl.Moba;
    // 瀹炰綋涓荤被鍨嬶紙鐢ㄤ簬閫昏緫灞傚尯鍒嗕笉鍚屽ぇ绫诲疄浣擄級
    public enum EntityMainType
    {
        // 鏈畾涔?
        None = 0,

        // 鍗曚綅锛堣嫳闆?灏忓叺/閲庢€?闃插尽濉旂瓑锛?
        Unit = 1,

        // 鎶曞皠鐗╋紙瀛愬脊/椋炶閬撳叿/鎶€鑳芥姏灏勪綋锛?
        Projectile = 2,

        // 鍦烘櫙浜や簰鐗╋紙鍦板舰瑙﹀彂鍣?鍙嬀鍙栫墿/鏈哄叧绛夛級
        SceneObject = 3,

        // 鍙敜鐗╋紙鐢辫嫳闆?鎶€鑳藉垱寤虹殑涓存椂鍗曚綅锛?
        Summon = 4,

        // 鎶€鑳芥晥鏋滃疄浣擄紙渚嬪鎸佺画AOE鍖哄煙銆侀櫡闃卞尯鍩熺瓑锛?
        Effect = 5
    }

    // 鍗曚綅瀛愮被鍨嬶紙褰撲富绫诲瀷涓?Unit 鏃讹紝鐢ㄤ簬鏇寸粏鐨勫垎绫伙級
    public enum UnitSubType
    {
        // 鏈畾涔?
        None = 0,

        // 鑻遍泟
        Hero = 1,

        // 灏忓叺
        Minion = 2,

        // 閲庢€?
        Neutral = 3,

        // 棣栭/绮捐嫳鎬?
        Boss = 4,

        // 闃插尽濉?
        Tower = 5,

        // 鍩哄湴/姘存櫠
        Base = 6,

        // 瀛愬脊/椋炶閬撳叿锛堝綋涓荤被鍨嬩负 Projectile 鏃讹紝鐢ㄤ簬鏇寸粏鐨勫垎绫伙級
        Bullet = 7
    }

    public enum ProjectileEmitterType
    {
        None = 0,
        Linear = 1,
    }

    public enum ProjectileTargetMode
    {
        SkillAim = 0,
        ActorId = 1,
        Search = 2,
    }

    public enum ProjectileFaceMode
    {
        SkillAimDir = 0,
        ToTarget = 1,
        CasterForward = 2,
    }

    public enum ProjectileSpawnMode
    {
        LegacyAimPos = 0,
        FromCaster = 1,
        FromTargetPoint = 2,
    }

    public enum SearchQueryCenterMode
    {
        Caster = 0,
        AimPos = 1,
        ExplicitTarget = 2,
    }

    // 闃熶紞/闃佃惀绫诲瀷
    public enum Team
    {
        None = 0,
        Team1 = 1,
        Team2 = 2,
        Neutral = 3,
    }

    // 鎶€鑳芥Ы浣嶇被鍨嬶紙鏅€氭敾鍑?澶氫釜鎶€鑳芥Ы锛?
    public enum SkillSlot
    {
        None = 0,
        BasicAttack = 1,
        Skill1 = 2,
        Skill2 = 3,
        Skill3 = 4,
        Skill4 = 5,
        Skill5 = 6,
    }

    // 鎶€鑳界被鍨嬶紙鐢ㄤ簬鎶€鑳界郴缁?鍐峰嵈/鏂芥硶娴佺▼鍖哄垎锛?
    public enum SkillType
    {
        // 鏈畾涔?
        None = 0,

        // 鏅€氭敾鍑?
        NormalAttack = 1,

        // 涓诲姩鎶€鑳斤紙闇€瑕佺帺瀹惰Е鍙戯級
        Active = 2,

        // 琚姩鎶€鑳斤紙甯搁┗/瑙﹀彂鍨嬶級
        Passive = 3,

        // 缁堟瀬鎶€鑳?
        Ultimate = 4
    }

    public enum BattleAttributeType
    {
        // 鏈畾涔?
        None = 0,

        // 褰撳墠鐢熷懡
        HP = 1,

        // 鏈€澶х敓鍛?
        MAX_HP = 2,

        // 棰濆鐢熷懡锛堝姞鎴愬€硷級
        EXTRA_HP = 3,

        // 鐗╃悊鏀诲嚮
        PHYSICS_ATTACK = 4,

        // 娉曟湳鏀诲嚮
        MAGIC_ATTACK = 5,

        // 棰濆鐗╃悊鏀诲嚮锛堝姞鎴愬€硷級
        EXTRA_PHYSICS_ATTACK = 6,

        // 棰濆娉曟湳鏀诲嚮锛堝姞鎴愬€硷級
        EXTRA_MAGIC_ATTACK = 7,

        // 鐗╃悊闃插尽
        PHYSICS_DEFENSE = 8,

        // 娉曟湳闃插尽
        MAGIC_DEFENSE = 9,

        // 褰撳墠娉曞姏
        MANA = 10,

        // 鏈€澶ф硶鍔?
        MAX_MANA = 11,

        // 鏆村嚮鐜?
        CRITICAL_R = 12,

        // 鏀婚€熷姞鎴?
        ATTACK_SPEED_R = 13,

        // 鍐峰嵈缂╁噺
        COOLDOWN_REDUCE_R = 14,

        // 鐗╃悊绌块€?
        PHYSICS_PENETRATION_R = 15,

        // 娉曟湳绌块€?
        MAGIC_PENETRATION_R = 16,

        // 绉诲姩閫熷害
        MOVE_SPEED = 17,

        // 鐗╃悊鍚歌
        PHYSICS_BLOODSUCKING_R = 18,

        // 娉曟湳鍚歌
        MAGIC_BLOODSUCKING_R = 19,

        // 鏀诲嚮鑼冨洿
        ATTACK_RANGE = 20,

        // 姣忕鐢熷懡鍥炲
        PER_SECOND_BLOOD_R = 21,

        // 姣忕娉曞姏鍥炲
        PER_SECOND_MANA_R = 22,
        /// <summary>
        /// 闊ф€?
        /// </summary>
        RESILIENCE_R = 23,
    }

    public enum BuffStackingPolicy
    {
        None = 0,
        IgnoreIfExists = 1,
        Replace = 2,
        AddStack = 3,
        RefreshDuration = 4,
    }

    public enum BuffRefreshPolicy
    {
        None = 0,
        KeepRemaining = 1,
        ResetRemaining = 2,
        AddRemaining = 3,
    }

    public enum EffectExecuteMode
    {
        InternalOnly = 0,
        PublishEventOnly = 1,
        InternalThenPublishEvent = 2,
    }

    public enum DamageFormulaKind
    {
        None = 0,
        Standard = 1,
    }

    public enum HealFormulaKind
    {
        None = 0,
        Standard = 1,
    }

    public enum DamageCalcStage
    {
        None = 0,
        AttackCreated = 1,
        BeforeCalc = 2,
        CalcBegin = 3,
        AfterBase = 4,
        AfterMitigate = 5,
        AfterShield = 6,
        Final = 7,
        BeforeApply = 8,
        AfterApply = 9,
    }
}