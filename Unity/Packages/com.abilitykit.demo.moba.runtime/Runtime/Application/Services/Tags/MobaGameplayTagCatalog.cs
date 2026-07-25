using System;
using System.Collections.Generic;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 运行时规则与配置校验共用的集中语义标签目录。
    /// </summary>
    public static class MobaGameplayTagCatalog
    {
        public static class State
        {
            public const string Untargetable = "State.Untargetable";
            public const string Invulnerable = "State.Invulnerable";
            public const string Silenced = "State.Silenced";
            public const string Disabled = "State.Disabled";
            public const string Stunned = "State.Stunned";
            public const string Rooted = "State.Rooted";
            public const string Suppressed = "State.Suppressed";
            public const string ControlImmune = "State.ControlImmune";
            public const string SuperArmor = "State.SuperArmor";
            public const string Feared = "State.Feared";
            public const string Asleep = "State.Asleep";
            public const string Charmed = "State.Charmed";
        }

        public static class Debuff
        {
            public const string Slow = "Debuff.Slow";
        }

        public static readonly string[] UntargetableAliases = { State.Untargetable, "Untargetable" };
        public static readonly string[] InvulnerableAliases = { State.Invulnerable, "Invulnerable" };
        public static readonly string[] SilencedAliases = { State.Silenced, "Silenced", "silenced" };
        public static readonly string[] DisabledAliases = { State.Disabled, "Disabled", "disabled" };
        public static readonly string[] StunnedAliases = { State.Stunned, "Stunned", "stunned" };
        public static readonly string[] RootedAliases = { State.Rooted, "Rooted", "rooted" };
        public static readonly string[] SuppressedAliases = { State.Suppressed, "Suppressed", "suppressed" };
        public static readonly string[] SuperArmorAliases = { State.SuperArmor, "SuperArmor", "super_armor" };
        public static readonly string[] ControlImmuneAliases = Combine(new[] { State.ControlImmune, "ControlImmune", "control_immune" }, SuperArmorAliases);
        public static readonly string[] FearedAliases = { State.Feared, "Feared", "feared" };
        public static readonly string[] AsleepAliases = { State.Asleep, "Asleep", "Sleeping", "asleep", "sleeping" };
        public static readonly string[] CharmedAliases = { State.Charmed, "Charmed", "charmed" };

        public static readonly string[] MoveBlockedAliases = Combine(StunnedAliases, DisabledAliases, SuppressedAliases, RootedAliases, FearedAliases, AsleepAliases);
        public static readonly string[] CastBlockedAliases = Combine(StunnedAliases, DisabledAliases, SuppressedAliases, SilencedAliases, FearedAliases, AsleepAliases);
        public static readonly string[] ControlBlockedAliases = Combine(StunnedAliases, FearedAliases, CharmedAliases, AsleepAliases);
        public static readonly string[] AllNames = Combine(
            UntargetableAliases,
            InvulnerableAliases,
            SilencedAliases,
            DisabledAliases,
            StunnedAliases,
            RootedAliases,
            SuppressedAliases,
            ControlImmuneAliases,
            FearedAliases,
            AsleepAliases,
            CharmedAliases,
            new[] { Debuff.Slow });

        public static readonly GameplayTagContainer UntargetableTags = ToContainer(UntargetableAliases);
        public static readonly GameplayTagContainer InvulnerableTags = ToContainer(InvulnerableAliases);
        public static readonly GameplayTagContainer CastBlockedTags = ToContainer(CastBlockedAliases);
        public static readonly GameplayTagContainer StunnedTags = ToContainer(StunnedAliases);
        public static readonly GameplayTagContainer DisabledTags = ToContainer(DisabledAliases);
        public static readonly GameplayTagContainer SuppressedTags = ToContainer(SuppressedAliases);
        public static readonly GameplayTagContainer MoveBlockedTags = ToContainer(MoveBlockedAliases);
        public static readonly GameplayTagContainer ControlImmuneTags = ToContainer(ControlImmuneAliases);
        public static readonly GameplayTagContainer ControlBlockedTags = ToContainer(ControlBlockedAliases);

        static MobaGameplayTagCatalog()
        {
            RegisterAll();
        }

        public static void RegisterAll()
        {
            GameplayTagManager.Instance.RegisterTags(AllNames);
        }

        public static bool TryGet(string tagName, out GameplayTag tag)
        {
            tag = default;
            return !string.IsNullOrWhiteSpace(tagName)
                && GameplayTagManager.Instance.TryGetTag(tagName, out tag)
                && tag.IsValid;
        }

        public static bool TryResolve(string tagName, out GameplayTag tag)
        {
            tag = default;
            if (string.IsNullOrWhiteSpace(tagName)) return false;

            tag = global::AbilityKit.GameplayTags.GameplayTags.Tag(tagName);
            return tag.IsValid;
        }

        public static GameplayTagContainer ToContainer(IReadOnlyList<string> names)
        {
            var c = new GameplayTagContainer();
            Append(c, names);
            return c.Count > 0 ? c : null;
        }

        public static void Append(GameplayTagContainer container, IReadOnlyList<string> names)
        {
            if (container == null || names == null) return;

            for (int i = 0; i < names.Count; i++)
            {
                if (TryResolve(names[i], out var tag)) container.Add(tag);
            }
        }

        public static bool HasAny(GameplayTagContainer container, GameplayTagContainer query)
        {
            return container != null && query != null && query.Count > 0 && container.HasAny(query, exact: false);
        }

        private static string[] Combine(params string[][] groups)
        {
            if (groups == null || groups.Length == 0) return Array.Empty<string>();

            var total = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null) total += groups[i].Length;
            }

            if (total == 0) return Array.Empty<string>();

            var result = new string[total];
            var offset = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                if (group == null || group.Length == 0) continue;

                Array.Copy(group, 0, result, offset, group.Length);
                offset += group.Length;
            }

            return result;
        }
    }
}
