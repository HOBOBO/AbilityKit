using UnityEngine;

namespace AbilityKit.Game.Battle.Presentation.Features.Loading
{
    internal sealed class BattleLoadingScreenRenderer
    {
        private static GUIStyle _boldHeaderCache;

        public void Draw(
            in BattleLoadingSnapshot snapshot,
            IBattleLoadingCommandSink commands)
        {
            if (!snapshot.IsVisible) return;

            const float cardWidth = 480f;
            const float cardHeight = 200f;
            var cx = Screen.width * 0.5f;
            var cy = Screen.height * 0.5f;
            var rect = new Rect(
                cx - cardWidth * 0.5f,
                cy - cardHeight * 0.5f,
                cardWidth,
                cardHeight);

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(dim, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("Loading Battle Assets", BoldHeaderStyle());
            GUILayout.Space(8f);
            GUILayout.Label(snapshot.StatusLine);

            var progress = Mathf.Clamp01(snapshot.Progress01);
            DrawProgressBar(progress);

            GUILayout.Space(8f);
            GUILayout.Label(
                $"{snapshot.LoadedCount} / {snapshot.TotalCount}  " +
                $"({Mathf.RoundToInt(progress * 100f)}%)");
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (snapshot.IsLoading)
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(32f)))
                {
                    commands?.RequestCancel();
                }
            }
            else if (snapshot.Completed && !snapshot.Success)
            {
                if (GUILayout.Button("Retry", GUILayout.Height(32f)))
                {
                    commands?.RequestRetry();
                }

                if (GUILayout.Button("Back to Lobby", GUILayout.Height(32f)))
                {
                    commands?.RequestReturnLobby();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static GUIStyle BoldHeaderStyle()
        {
            if (_boldHeaderCache != null) return _boldHeaderCache;
            _boldHeaderCache = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 18
            };
            return _boldHeaderCache;
        }

        private static void DrawProgressBar(float progress01)
        {
            const float barHeight = 22f;
            var rect = GUILayoutUtility.GetRect(
                0f,
                barHeight,
                GUILayout.ExpandWidth(true));
            var previousColor = GUI.color;

            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            var fillRect = new Rect(
                rect.x,
                rect.y,
                rect.width * Mathf.Clamp01(progress01),
                rect.height);
            GUI.color = new Color(0.25f, 0.75f, 0.95f, 1f);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }
    }
}
