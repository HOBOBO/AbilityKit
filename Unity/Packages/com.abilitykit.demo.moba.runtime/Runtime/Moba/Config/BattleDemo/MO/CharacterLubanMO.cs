using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    /// <summary>
    /// 鍩轰簬 Luban 浜岃繘鍒堕厤缃殑 Character MO
    /// 娉ㄦ剰锛氭妧鑳藉拰琚姩鎶€鑳戒粠 AttributeTemplate 涓幏鍙?
    /// </summary>
    public sealed class CharacterLubanMO
    {
        /// <summary>
        /// 瑙掕壊缂栧彿 (瀵瑰簲 DRCharacters.Code)
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 鑱屼笟鍒楄〃
        /// </summary>
        public IReadOnlyList<int> Career { get; }

        /// <summary>
        /// 妯″瀷缂栧彿
        /// </summary>
        public int ModelId { get; }

        /// <summary>
        /// 灞炴€фā鏉跨紪鍙?(寮曠敤 AttributeTemplate 琛?
        /// </summary>
        public int AttributeTemplateId { get; }

        public CharacterLubanMO(global::cfg.DRCharacters dr)
        {
            if (dr == null) throw new ArgumentNullException(nameof(dr));
            Id = dr.Code;
            Career = dr.Career ?? new List<int>();
            ModelId = dr.ModelId;
            AttributeTemplateId = dr.AttributeTemplateId;
        }
    }
}
