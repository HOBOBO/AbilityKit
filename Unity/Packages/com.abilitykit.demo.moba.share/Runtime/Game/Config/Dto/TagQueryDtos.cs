using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class TagTemplateDTO
    {
        public int Id;
        public string Name;

        public string[] RequiredTagNames;
        public string[] BlockedTagNames;
        public string[] GrantTagNames;
        public string[] RemoveTagNames;
    }

    [Serializable]
    public sealed class ContinuousTagTemplateDTO
    {
        public int Id;
        public string Name;

        public string[] ActivationRequiredTagNames;
        public string[] ActivationBlockedTagNames;
        public string[] ApplicationTagNames;
        public string[] RemovalRequiredTagNames;
        public string[] RemovalBlockedTagNames;
        public string[] OngoingRequiredTagNames;
        public string[] OngoingBlockedTagNames;
        public string[] RemovalTagNames;
    }

    public enum SearchTargetProviderKind
    {
        AllActors = 1,
        ExplicitTarget = 2,
        ContextTarget = 3,
        Caster = 4,
        SameTeam = 5,
        EnemyTeam = 6,
        MainType = 7,
        UnitSubType = 8,
    }

    public enum SearchTargetRuleKind
    {
        RequireValidId = 0x0204,
        RequireHasPosition = 0x0205,
        CircleShape = 0x0101,
        SectorShape = 0x0102,
        RectangleShape = 0x0303,
        CapsuleShape = 0x0304,
        ExcludeCaster = 0x0301,
        ExcludeExplicitTarget = 0x0302,
        Whitelist = 0x0201,
        Blacklist = 0x0202,
    }

    public enum SearchTargetScorerKind
    {
        Zero = 0x2001,
        SeededHashRandom = 0x2002,
        DistanceToCaster = 0x2004,
        DistanceToExplicitTarget = 0x2005,
    }

    public enum SearchTargetSelectorKind
    {
        TopKByScore = 0x1001,
        StreamingTopKByScore = 0x1002,
    }

    public enum SearchTargetPointKind
    {
        Caster = 0,
        AimPosition = 1,
        ExplicitTarget = 2,
    }

    public enum SearchQueryExplicitTargetPolicy
    {
        PreferExplicitTarget = 0,
        SearchWhenMissing = 1,
        IgnoreExplicitTarget = 2,
    }

    [Serializable]
    public abstract class SearchTargetComponentDTO
    {
        public int Id;
        public int Kind;
    }

    [Serializable]
    public sealed class SearchTargetProviderDTO : SearchTargetComponentDTO
    {
        public int Param;
    }

    [Serializable]
    public sealed class SearchTargetRuleDTO : SearchTargetComponentDTO
    {
        public int Center;
        public int Forward;
        public float Radius;
        public float HalfAngleDeg;
        public float LocalOffsetForward;
        public float LocalOffsetRight;
        public float Width;
        public float Length;
        public int[] ActorIds;
    }

    [Serializable]
    public sealed class SearchTargetScorerDTO : SearchTargetComponentDTO
    {
        public int Source;
        public int RandomSeed;
        public int Direction;
    }

    [Serializable]
    public sealed class SearchTargetSelectorDTO : SearchTargetComponentDTO
    {
    }

    [Serializable]
    public sealed class SearchQueryTemplateDTO
    {
        public int Id;
        public string Name;

        public int MaxCount;
        public int ExplicitTargetPolicy;

        public SearchTargetProviderDTO Provider;
        public SearchTargetRuleDTO[] Rules;
        public SearchTargetScorerDTO[] Scorers;
        public SearchTargetSelectorDTO Selector;

    }
}
