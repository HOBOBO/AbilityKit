using System;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    /// <summary>
    /// 鎶€鑳介噴鏀炬潯浠舵鏌ョ粨鏋?
    /// </summary>
    public readonly struct SkillConditionResult
    {
        /// <summary>
        /// 鏄惁閫氳繃
        /// </summary>
        public bool Passed { get; }

        /// <summary>
        /// 澶辫触鍘熷洜锛堢敤浜庢樉绀虹粰鐜╁锛?
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// 澶辫触鍘熷洜鐨勫叧閿瓧锛堢敤浜嶶I鏄剧ず锛?
        /// </summary>
        public string FailureKey { get; }

        /// <summary>
        /// 澶辫触鍘熷洜鐨勫弬鏁帮紙鐢ㄤ簬鏍煎紡鍖栵級
        /// </summary>
        public object[] FailureParams { get; }

        public static SkillConditionResult Pass => new(true, null, null, null);

        public static SkillConditionResult Fail(string reason, string failureKey = null, params object[] @params)
            => new(false, reason, failureKey, @params);

        private SkillConditionResult(bool passed, string reason, string failureKey, object[] @params)
        {
            Passed = passed;
            FailureReason = reason;
            FailureKey = failureKey;
            FailureParams = @params;
        }

        public SkillConditionResult And(SkillConditionResult other)
        {
            if (!Passed) return this;
            return other;
        }

        public SkillConditionResult Or(SkillConditionResult other)
        {
            if (Passed) return this;
            return other;
        }
    }

    /// <summary>
    /// 鎶€鑳芥潯浠舵帴鍙?
    /// 瀹氫箟鎶€鑳介噴鏀惧墠缃潯浠剁殑妫€鏌ラ€昏緫
    /// </summary>
    public interface ISkillCondition
    {
        /// <summary>
        /// 鏉′欢鍞竴鏍囪瘑
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 鏉′欢鏄剧ず鍚嶇О
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 鏉′欢鎻忚堪锛堢敤浜庤皟璇?鏃ュ織锛?
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 妫€鏌ユ潯浠舵槸鍚︽弧瓒?
        /// </summary>
        /// <param name="context">鎶€鑳界绾夸笂涓嬫枃</param>
        /// <returns>妫€鏌ョ粨鏋?/returns>
        SkillConditionResult Check(SkillPipelineContext context);

        /// <summary>
        /// 妫€鏌ユ槸鍚﹀彲浠ュ湪鎸佺画妫€娴嬫ā寮忎笅宸ヤ綔
        /// 鏌愪簺鏉′欢锛堝鍐峰嵈锛夊彲浠ユ寔缁鏌ワ紝鏈変簺锛堝璧勬簮锛夊彧妫€鏌ヤ竴娆?
        /// </summary>
        bool SupportsContinuousCheck { get; }
    }

    /// <summary>
    /// 鎶€鑳芥潯浠跺熀绫?
    /// 鎻愪緵閫氱敤鐨勬潯浠舵鏌ヨ兘鍔?
    /// 鑷姩浠?SkillConditionAttribute 璇诲彇 Id 鍜?DisplayName
    /// </summary>
    public abstract class SkillConditionBase : ISkillCondition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public virtual string Description => DisplayName;
        public virtual bool SupportsContinuousCheck => false;

        protected SkillConditionBase()
        {
            var attr = GetType().GetCustomAttributes(typeof(SkillConditionAttribute), false)
                .FirstOrDefault() as SkillConditionAttribute;
            Id = attr?.Id ?? GetType().Name;
            DisplayName = attr?.DisplayName ?? Id;
        }

        public abstract SkillConditionResult Check(SkillPipelineContext context);

        protected static SkillConditionResult Fail(string reason, string failureKey = null, params object[] @params)
            => SkillConditionResult.Fail(reason, failureKey, @params);

        protected static SkillConditionResult Pass => SkillConditionResult.Pass;
    }

    /// <summary>
    /// 鎶€鑳芥潯浠剁壒鎬?
    /// 鐢ㄤ簬鑷姩鍙戠幇鍜屾敞鍐屾妧鑳芥潯浠?
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SkillConditionAttribute : Attribute
    {
        /// <summary>
        /// 鏉′欢鍞竴鏍囪瘑
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 鏉′欢鏄剧ず鍚嶇О
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 浼樺厛绾э紝鏁板€艰秺澶т紭鍏堢骇瓒婇珮
        /// </summary>
        public int Priority { get; set; } = 0;

        public SkillConditionAttribute(string id, string displayName = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? id;
        }
    }
}