using AbilityKit.Ability.Explain;
using AbilityKit.Ability.Explain.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Samples.MockIntegration
{
    [InitializeOnLoad]
    internal static class MockTimelineDetailsSectionProvider
    {
        static MockTimelineDetailsSectionProvider()
        {
            AbilityExplainRegistry.Register(new Provider());
        }

        private sealed class Provider : IExplainDetailsSectionProvider
        {
            public int Priority => 0;

            public bool CanProvide(ExplainNode node, ExplainDetailsContext context)
            {
                return node != null && node.Title != null && node.Title.Contains("发射子弹");
            }

            public void Build(VisualElement container, ExplainNode node, ExplainDetailsContext context)
            {
                var header = new Label("Timeline (Preview)")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        marginTop = 10,
                        paddingLeft = 6
                    }
                };
                container.Add(header);

                container.Add(BuildEventRow("t=0.00s", "Cast", NavigationTarget.OpenEditor("actioneditor", null)));
                container.Add(BuildEventRow("t=0.20s", "SpawnProjectile", NavigationTarget.OpenEditor("actioneditor", null)));
                container.Add(BuildEventRow("t=0.60s", "Hit", NavigationTarget.OpenEditor("actioneditor", null)));
            }

            private VisualElement BuildEventRow(string time, string title, NavigationTarget target)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        paddingLeft = 6,
                        paddingRight = 6,
                        height = 20
                    }
                };

                row.Add(new Label(time) { style = { width = 70 } });
                row.Add(new Label(title) { style = { flexGrow = 1 } });

                var btn = new Button(() => AbilityExplainRegistry.GetNavigator(target)?.Navigate(target)) { text = "Open" };
                btn.style.marginLeft = 6;
                row.Add(btn);

                return row;
            }
        }
    }
}
