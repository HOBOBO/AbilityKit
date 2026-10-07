#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.Hero
{
    /// <summary>
    /// 对 characters.json（数组形式的字符表 JSON）做插入 / 更新操作，保持其他 Hero 条目字段不变。
    /// 设计目标：
    ///   * 读取已有 JSON，反序列化为 CharacterDTO[]；
    ///   * 找不到该 HeroId 或没传 Id，按 Id 升序插入；
    ///   * 写盘时使用 Formatting.Indented + Newtonsoft Json。
    /// </summary>
    public sealed class HeroJsonPatcher
    {
        public string JsonPath { get; }

        public HeroJsonPatcher(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath)) throw new ArgumentNullException(nameof(jsonPath));
            JsonPath = jsonPath;
        }

        /// <summary>读取现有 characters.json，文件不存在则返回空数组。</summary>
        public CharacterDTO[] Load()
        {
            if (!File.Exists(JsonPath)) return Array.Empty<CharacterDTO>();

            var json = File.ReadAllText(JsonPath);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<CharacterDTO>();

            var arr = JsonConvert.DeserializeObject<CharacterDTO[]>(json);
            return arr ?? Array.Empty<CharacterDTO>();
        }

        /// <summary>
        /// 把 heroDto 插入 / 更新到 characters.json 中，写盘。
        /// 既存的相同 Id 条目会被原样替换；如果原条目带额外字段，会尽量合并保留。
        /// </summary>
        public CharacterDTO[] Upsert(CharacterDTO heroDto, IEnumerable<int> skillIds)
        {
            if (heroDto == null) throw new ArgumentNullException(nameof(heroDto));
            var all = new List<CharacterDTO>(Load());

            // 规范化 HeroDto：name 写入 Name，传入的 SkillIds 写入 SkillIds 数组
            heroDto.SkillIds = skillIds != null ? new List<int>(skillIds).ToArray() : Array.Empty<int>();
            heroDto.PassiveSkillIds ??= Array.Empty<int>();

            // 删除并合并
            int existingIndex = -1;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].Id == heroDto.Id)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                var existing = all[existingIndex];
                // 保留被动技能/外部字段
                if (existing.PassiveSkillIds != null && existing.PassiveSkillIds.Length > 0
                    && (heroDto.PassiveSkillIds == null || heroDto.PassiveSkillIds.Length == 0))
                {
                    heroDto.PassiveSkillIds = existing.PassiveSkillIds;
                }
                existing.Name = heroDto.Name;
                existing.ModelId = heroDto.ModelId;
                existing.AttributeTemplateId = heroDto.AttributeTemplateId;
                existing.SkillIds = heroDto.SkillIds;
                existing.PassiveSkillIds = heroDto.PassiveSkillIds;
                all[existingIndex] = existing;
            }
            else
            {
                all.Add(heroDto);
            }

            // 按 Id 升序排序，便于差异比对
            all.Sort((a, b) => a.Id.CompareTo(b.Id));

            Save(all);
            return all.ToArray();
        }

        /// <summary>写盘。外部数组，调用方负责正确性。</summary>
        public void Save(IReadOnlyList<CharacterDTO> entries)
        {
            var arr = entries ?? (IReadOnlyList<CharacterDTO>)Array.Empty<CharacterDTO>();
            var json = JsonConvert.SerializeObject(arr, Formatting.Indented);
            var dir = Path.GetDirectoryName(JsonPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(JsonPath, json);
        }
    }
}
#endif
