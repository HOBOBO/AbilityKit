using AbilityKit.Ability.Explain;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbilityKit.Ability.Explain.Editor
{
    internal sealed class ExplainDebugRenderer
    {
        private readonly ScrollView _debugView;

        public ExplainDebugRenderer(ScrollView debugView)
        {
            _debugView = debugView;
        }

        public void Clear()
        {
            _debugView?.Clear();
        }

        public void Render(ExplainResolveResult result)
        {
            if (_debugView == null) return;

            _debugView.Clear();

            var header = new Label("Debug")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = AbilityExplainStyles.Padding,
                    paddingTop = 8,
                    paddingBottom = 4
                }
            };

            _debugView.Add(header);

            if (result == null)
            {
                AddLine("(null result)");
                return;
            }

            AddLine($"Elapsed: {result.ElapsedMs} ms");
            AddLine($"CacheHit: {result.CacheHit}");
            AddLine($"Partial: {result.Partial}");

            if (!string.IsNullOrEmpty(result.Exception))
            {
                var ex = new Label(result.Exception)
                {
                    style =
                    {
                        paddingLeft = AbilityExplainStyles.Padding,
                        whiteSpace = WhiteSpace.Normal,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        color = new Color(0.85f, 0.25f, 0.25f, 1f)
                    }
                };
                _debugView.Add(ex);
            }

            if (!string.IsNullOrEmpty(result.Debug))
            {
                var dbg = new Label(result.Debug)
                {
                    style =
                    {
                        paddingLeft = AbilityExplainStyles.Padding,
                        whiteSpace = WhiteSpace.Normal
                    }
                };
                _debugView.Add(dbg);
            }
        }

        private void AddLine(string text)
        {
            _debugView.Add(new Label(text) { style = { paddingLeft = AbilityExplainStyles.Padding } });
        }
    }
}
