using NUnit.Framework;
using UnityEngine;
using AbilityKit.Game.Battle.View.Lib.Skill;

namespace AbilityKit.Game.Tests
{
    public sealed class SkillButtonPresentationTests
    {
        [Test]
        public void ApplyPresentation_PreservesSkillNameAndIconId()
        {
            var go = new GameObject("skill-button-presentation", typeof(RectTransform));
            try
            {
                var view = go.AddComponent<SkillButtonView>();
                view.ApplyPresentation("烬 Q", 901);

                Assert.AreEqual("烬 Q", view.PresentationName);
                Assert.AreEqual(901, view.PresentationIconId);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IconResolver_UsesNullForEmptyId()
        {
            Assert.IsNull(SkillIconResolver.Resolve(0));
            Assert.IsNull(SkillIconResolver.Resolve(-1));
        }
    }
}
