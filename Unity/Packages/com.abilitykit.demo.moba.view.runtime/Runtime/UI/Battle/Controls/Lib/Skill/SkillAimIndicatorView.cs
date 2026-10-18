using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Battle.View.Lib.Skill
{
    public sealed class SkillAimIndicatorView : MonoBehaviour
    {
        [SerializeField] private RectTransform _ring;
        [SerializeField] private RectTransform _dot;
        [SerializeField] private RectTransform _range;
        [SerializeField] private Image _ringImage;
        [SerializeField] private Image _dotImage;
        [SerializeField] private Image _rangeImage;

        public void Initialize(RectTransform ring, RectTransform dot, RectTransform range = null)
        {
            _ring = ring;
            _dot = dot;
            _range = range;
            _ringImage = ring != null ? ring.GetComponent<Image>() : null;
            _dotImage = dot != null ? dot.GetComponent<Image>() : null;
            _rangeImage = range != null ? range.GetComponent<Image>() : null;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
            {
                transform.SetAsLastSibling();
                EnsureTopCanvas();
            }
        }

        public void SetFromTo(Vector2 fromAnchored, Vector2 toAnchored, float maxRadius)
        {
            SetFromTo(fromAnchored, toAnchored, maxRadius, SkillButtonConfig.Default);
        }

        public void SetFromTo(Vector2 fromAnchored, Vector2 toAnchored, float maxRadius, SkillButtonConfig config)
        {
            EnsureSprites();

            var delta = ClampDelta(toAnchored - fromAnchored, maxRadius);
            var target = fromAnchored + delta;
            var dist = delta.magnitude;
            var dir = dist > 0.001f ? delta / dist : Vector2.up;
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var shape = config.IndicatorShape;

            SetElement(_ring, _ringImage, IsAnchorShape(shape), fromAnchored);
            SetElement(_dot, _dotImage, IsDotShape(shape), target);
            SetElement(_range, _rangeImage, IsRangeShape(shape), ResolveRangePosition(shape, fromAnchored, target));
            ApplySprites(shape);
            ApplyShapeColors(shape);

            if (_ring != null)
            {
                var diameter = shape == SkillAimIndicatorShape.TargetCircle
                    ? Mathf.Max(74f, maxRadius * 2f)
                    : Mathf.Max(74f, Mathf.Min(maxRadius * 0.5f, 144f));
                _ring.sizeDelta = new Vector2(diameter, diameter);
                _ring.SetAsFirstSibling();
            }

            if (_dot != null)
            {
                var isDirectionLike = shape == SkillAimIndicatorShape.DirectionLine ||
                                       shape == SkillAimIndicatorShape.DashLine;
                var size = isDirectionLike
                    ? new Vector2(Mathf.Max(80f, dist), Mathf.Max(24f, config.IndicatorWidthPixels))
                    : Vector2.one * Mathf.Max(48f, config.IndicatorWidthPixels * 0.8f);
                _dot.sizeDelta = size;
                _dot.pivot = isDirectionLike
                    ? new Vector2(0f, 0.5f)
                    : new Vector2(0.5f, 0.5f);
                _dot.anchoredPosition = isDirectionLike ? fromAnchored : target;
                _dot.localEulerAngles = isDirectionLike ? new Vector3(0f, 0f, angle) : Vector3.zero;
                _dot.SetAsLastSibling();
            }

            if (_range != null)
            {
                var width = shape == SkillAimIndicatorShape.DirectionArea
                    ? Mathf.Max(28f, config.IndicatorWidthPixels)
                    : Mathf.Max(76f, config.IndicatorWidthPixels);
                var length = Mathf.Max(width, config.IndicatorLengthPixels > 0f ? config.IndicatorLengthPixels : maxRadius);
                if (shape == SkillAimIndicatorShape.Sector)
                {
                    _range.sizeDelta = new Vector2(length, length);
                    _range.pivot = new Vector2(0.5f, 0.5f);
                    _range.localEulerAngles = new Vector3(0f, 0f, angle - 45f);
                }
                else if (shape == SkillAimIndicatorShape.DirectionArea)
                {
                    _range.sizeDelta = new Vector2(length, width);
                    _range.pivot = new Vector2(0f, 0.5f);
                    _range.anchoredPosition = fromAnchored;
                    _range.localEulerAngles = new Vector3(0f, 0f, angle);
                }
                else
                {
                    var diameter = shape == SkillAimIndicatorShape.SelfCircle
                        ? Mathf.Max(width, config.IndicatorLengthPixels)
                        : Mathf.Max(width, config.IndicatorWidthPixels);
                    _range.sizeDelta = Vector2.one * diameter;
                    _range.pivot = new Vector2(0.5f, 0.5f);
                    _range.localEulerAngles = Vector3.zero;
                }

                _range.SetAsLastSibling();
            }
        }

        private void EnsureTopCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 80;
        }

        private static Vector2 ClampDelta(Vector2 delta, float maxRadius)
        {
            var dist = delta.magnitude;
            if (dist > maxRadius && dist > 0.001f)
            {
                delta *= maxRadius / dist;
            }

            return delta;
        }

        private static bool IsAnchorShape(SkillAimIndicatorShape shape)
        {
            return shape == SkillAimIndicatorShape.DirectionLine ||
                   shape == SkillAimIndicatorShape.TargetCircle ||
                   shape == SkillAimIndicatorShape.Sector ||
                   shape == SkillAimIndicatorShape.DirectionArea ||
                   shape == SkillAimIndicatorShape.DashLine ||
                   shape == SkillAimIndicatorShape.LockProjectile ||
                   shape == SkillAimIndicatorShape.FanArea;
        }

        private static bool IsDotShape(SkillAimIndicatorShape shape)
        {
            return shape == SkillAimIndicatorShape.DirectionLine ||
                   shape == SkillAimIndicatorShape.TargetCircle ||
                   shape == SkillAimIndicatorShape.DashLine ||
                   shape == SkillAimIndicatorShape.LockProjectile;
        }

        private static bool IsRangeShape(SkillAimIndicatorShape shape)
        {
            return shape == SkillAimIndicatorShape.TargetCircle ||
                   shape == SkillAimIndicatorShape.SelfCircle ||
                   shape == SkillAimIndicatorShape.Sector ||
                   shape == SkillAimIndicatorShape.DirectionArea ||
                   shape == SkillAimIndicatorShape.DashLine ||
                   shape == SkillAimIndicatorShape.LockProjectile ||
                   shape == SkillAimIndicatorShape.FanArea;
        }

        private static Vector2 ResolveRangePosition(SkillAimIndicatorShape shape, Vector2 fromAnchored, Vector2 target)
        {
            return shape == SkillAimIndicatorShape.TargetCircle ||
                   shape == SkillAimIndicatorShape.LockProjectile
                ? target
                : fromAnchored;
        }

        private void ApplySprites(SkillAimIndicatorShape shape)
        {
            if (_ringImage != null) _ringImage.sprite = SkillAimIndicatorSprites.Ring;
            if (_dotImage != null)
            {
                _dotImage.sprite = (shape == SkillAimIndicatorShape.DirectionLine ||
                                    shape == SkillAimIndicatorShape.DashLine)
                    ? SkillAimIndicatorSprites.Direction
                    : SkillAimIndicatorSprites.Dot;
            }
            if (_rangeImage != null)
            {
                if (shape == SkillAimIndicatorShape.Sector)
                {
                    _rangeImage.sprite = SkillAimIndicatorSprites.Sector;
                }
                else if (shape == SkillAimIndicatorShape.DirectionArea)
                {
                    _rangeImage.sprite = SkillAimIndicatorSprites.DirectionArea;
                }
                else if (shape == SkillAimIndicatorShape.FanArea)
                {
                    _rangeImage.sprite = SkillAimIndicatorSprites.Sector;
                }
                else
                {
                    _rangeImage.sprite = SkillAimIndicatorSprites.Area;
                }
            }
        }

        private void ApplyShapeColors(SkillAimIndicatorShape shape)
        {
            var main = ResolveMainColor(shape);
            var fill = new Color(main.r, main.g, main.b, Mathf.Clamp01(main.a * 0.54f));
            var anchor = new Color(1f, 1f, 1f, 0.34f);
            if (_ringImage != null) _ringImage.color = anchor;
            if (_dotImage != null) _dotImage.color = main;
            if (_rangeImage != null) _rangeImage.color = fill;
        }

        private static Color ResolveMainColor(SkillAimIndicatorShape shape)
        {
            switch (shape)
            {
                case SkillAimIndicatorShape.SelfCircle:
                    return new Color(1f, 0.42f, 0.22f, 0.72f);
                case SkillAimIndicatorShape.TargetCircle:
                    return new Color(1f, 0.76f, 0.22f, 0.72f);
                case SkillAimIndicatorShape.Sector:
                    return new Color(0.35f, 0.92f, 1f, 0.7f);
                case SkillAimIndicatorShape.DashLine:
                    return new Color(0.95f, 0.78f, 0.25f, 0.72f);
                case SkillAimIndicatorShape.LockProjectile:
                    return new Color(0.95f, 0.45f, 0.55f, 0.72f);
                case SkillAimIndicatorShape.FanArea:
                    return new Color(0.7f, 0.95f, 0.45f, 0.72f);
                default:
                    return new Color(0.3f, 0.82f, 1f, 0.72f);
            }
        }

        private static void EnsureSprites()
        {
            SkillAimIndicatorSprites.Ensure();
        }

        private static void SetElement(RectTransform rect, Image image, bool visible, Vector2 pos)
        {
            if (rect == null) return;

            rect.gameObject.SetActive(visible);
            rect.anchoredPosition = pos;
            rect.localEulerAngles = Vector3.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            if (image != null)
            {
                var color = image.color;
                color.a = visible ? Mathf.Max(color.a, 0.18f) : 0f;
                image.color = color;
            }
        }
    }

    internal static class SkillAimIndicatorSprites
    {
        public static Sprite Ring { get; private set; }
        public static Sprite Dot { get; private set; }
        public static Sprite Area { get; private set; }
        public static Sprite Direction { get; private set; }
        public static Sprite DirectionArea { get; private set; }
        public static Sprite Sector { get; private set; }

        public static void Ensure()
        {
            if (Ring != null) return;

            Ring = CreateDiscSprite(128, 0.36f, 0.5f, new Color(1f, 1f, 1f, 0.38f));
            Dot = CreateDiscSprite(128, 0f, 0.5f, new Color(1f, 1f, 1f, 0.78f));
            Area = CreateDiscSprite(192, 0.28f, 0.5f, new Color(1f, 1f, 1f, 0.34f));
            Direction = CreateDirectionSprite(256, 80, new Color(1f, 1f, 1f, 0.78f));
            DirectionArea = CreateDirectionAreaSprite(256, 80, new Color(1f, 1f, 1f, 0.48f));
            Sector = CreateSectorSprite(256, 92f, new Color(1f, 1f, 1f, 0.38f));
        }

        private static Sprite CreateDiscSprite(int size, float innerRadius01, float outerRadius01, Color color)
        {
            var texture = CreateTexture(size, size);
            var center = (size - 1) * 0.5f;
            var inner = innerRadius01 * center;
            var outer = outerRadius01 * center;
            var border = Mathf.Max(1f, outer * 0.08f);

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                var alpha = d <= outer && d >= inner ? color.a : 0f;
                if (alpha > 0f)
                {
                    var edge = Mathf.Min(Mathf.Abs(d - inner), Mathf.Abs(outer - d));
                    alpha *= Mathf.Clamp01(edge / border);
                }

                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }

            texture.Apply();
            return CreateSprite(texture);
        }

        private static Sprite CreateDirectionSprite(int width, int height, Color color)
        {
            var texture = CreateTexture(width, height);
            var halfH = height * 0.5f;
            var bodyEnd = width - height * 0.72f;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var inBody = x <= bodyEnd && Mathf.Abs(y - halfH) <= height * 0.18f;
                var arrowT = Mathf.InverseLerp(bodyEnd, width - 1, x);
                var arrowHalf = Mathf.Lerp(height * 0.34f, 1f, arrowT);
                var inArrow = x > bodyEnd && Mathf.Abs(y - halfH) <= arrowHalf;
                texture.SetPixel(x, y, inBody || inArrow ? color : Color.clear);
            }

            texture.Apply();
            return CreateSprite(texture);
        }

        private static Sprite CreateDirectionAreaSprite(int width, int height, Color color)
        {
            var texture = CreateTexture(width, height);
            var border = Mathf.Max(2, height / 12);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var edge = x < border || x >= width - border || y < border || y >= height - border;
                var alpha = edge ? color.a : color.a * 0.34f;
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }

            texture.Apply();
            return CreateSprite(texture);
        }

        private static Sprite CreateSectorSprite(int size, float degrees, Color color)
        {
            var texture = CreateTexture(size, size);
            var center = new Vector2(size * 0.5f, size * 0.5f);
            var radius = size * 0.48f;
            var half = degrees * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var v = new Vector2(x, y) - center;
                var d = v.magnitude;
                var a = Vector2.Angle(Vector2.right, v);
                var inside = d <= radius && a <= half;
                var edge = Mathf.Min(radius - d, half - a);
                var alpha = inside ? color.a * Mathf.Clamp01(edge / 4f) : 0f;
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }

            texture.Apply();
            return CreateSprite(texture);
        }

        private static Texture2D CreateTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.DontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
