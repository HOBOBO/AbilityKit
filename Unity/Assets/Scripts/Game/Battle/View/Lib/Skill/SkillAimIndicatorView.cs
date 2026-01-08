using UnityEngine;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    public sealed class SkillAimIndicatorView : MonoBehaviour
    {
        [SerializeField] private RectTransform _ring;
        [SerializeField] private RectTransform _dot;

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetFromTo(Vector2 fromAnchored, Vector2 toAnchored, float maxRadius)
        {
            if (_ring != null) _ring.anchoredPosition = fromAnchored;

            if (_dot != null)
            {
                var delta = toAnchored - fromAnchored;
                var dist = delta.magnitude;
                if (dist > maxRadius && dist > 0.001f)
                {
                    delta = delta * (maxRadius / dist);
                }
                _dot.anchoredPosition = fromAnchored + delta;
            }
        }
    }
}
