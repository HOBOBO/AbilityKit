using NUnit.Framework;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudSkillFailureTextTests
    {
        [TestCase("skill.pipeline.failed", "not_enough_mana", "蓝量不足")]
        [TestCase("skill.start.rejected", "Skill cooldown is not ready.", "技能冷却中")]
        [TestCase("skill.start.alreadyRunning", "Skill is already running.", "技能正在释放")]
        [TestCase("skill.cast.targetMissing", "No valid target is within cast range.", "没有有效目标")]
        [TestCase("skill.cast.failed", "Target is outside cast range.", "超出施法范围")]
        [TestCase("skill.cast.failed", "Unknown failure.", "技能释放失败")]
        public void Format_MapsStableFailureDetails(string code, string message, string expected)
        {
            Assert.AreEqual(expected, BattleHudSkillFailureText.Format(code, message));
        }
    }
}
