#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 英雄角色
    /// </summary>
    [CharacterTypeId("hero")]
    [CharacterTag("Hero")]
    [CharacterTag("Player")]
    public sealed class HeroCharacter
    {
        public float Health { get; set; } = 500f;
        public float Attack { get; set; } = 80f;
        public float Defense { get; set; } = 20f;
        public float Speed { get; set; } = 5f;
    }

    /// <summary>
    /// 魔王角色
    /// </summary>
    [CharacterTypeId("demon_lord")]
    [CharacterTag("Boss")]
    [CharacterTag("Demon")]
    [CharacterTag("Enemy")]
    public sealed class DemonLordCharacter
    {
        public float Health { get; set; } = 800f;
        public float Attack { get; set; } = 60f;
        public float Defense { get; set; } = 30f;
        public float Speed { get; set; } = 3f;
    }

    /// <summary>
    /// 鐏劙濉?
    /// </summary>
    [CharacterTypeId("fire_tower")]
    [CharacterTag("Tower")]
    [CharacterTag("Damage")]
    public sealed class FireTowerCharacter
    {
        public float Health { get; set; } = 100f;
        public float Attack { get; set; } = 50f;
        public float Defense { get; set; } = 10f;
        public float Range { get; set; } = 10f;
        public float FireRate { get; set; } = 1f;
        public float SplashRadius { get; set; } = 2f;
    }

    /// <summary>
    /// 瀵掑啺濉?
    /// </summary>
    [CharacterTypeId("ice_tower")]
    [CharacterTag("Tower")]
    [CharacterTag("Control")]
    public sealed class IceTowerCharacter
    {
        public float Health { get; set; } = 80f;
        public float Attack { get; set; } = 30f;
        public float Defense { get; set; } = 8f;
        public float Range { get; set; } = 8f;
        public float FireRate { get; set; } = 0.8f;
        public float SlowEffect { get; set; } = 0.5f;
    }

    /// <summary>
    /// 鍝ュ竷鏋楀皬鍏?
    /// </summary>
    [CharacterTypeId("goblin")]
    [CharacterTag("Minion")]
    [CharacterTag("Fast")]
    [CharacterTag("Enemy")]
    public sealed class GoblinCharacter
    {
        public float Health { get; set; } = 100f;
        public float Attack { get; set; } = 10f;
        public float Defense { get; set; } = 2f;
        public float Speed { get; set; } = 2f;
        public float GoldReward { get; set; } = 10f;
    }

    /// <summary>
    /// 椋熶汉榄旂簿鑻?
    /// </summary>
    [CharacterTypeId("ogre")]
    [CharacterTag("Elite")]
    [CharacterTag("Tank")]
    [CharacterTag("Enemy")]
    public sealed class OgreCharacter
    {
        public float Health { get; set; } = 300f;
        public float Attack { get; set; } = 25f;
        public float Defense { get; set; } = 10f;
        public float Speed { get; set; } = 1f;
        public float GoldReward { get; set; } = 50f;
    }
}
