using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudAimPreviewObject
    {
        private const float HeightOffset = 0.12f;

        private readonly GameObject _line;
        private readonly GameObject _circle;
        private readonly GameObject _dot;
        private readonly GameObject _sector;
        private readonly GameObject _casterRing;
        private readonly GameObject _edgeRing;

        public GameObject Root { get; }

        public BattleHudAimPreviewObject(
            GameObject root,
            GameObject line,
            GameObject circle,
            GameObject dot,
            GameObject sector,
            GameObject casterRing,
            GameObject edgeRing)
        {
            Root = root;
            _line = line;
            _circle = circle;
            _dot = dot;
            _sector = sector;
            _casterRing = casterRing;
            _edgeRing = edgeRing;
        }

        public void Apply(in BattleHudAimPreviewState state, in BattleHudSkillPresentationSpec spec)
        {
            SetVisible(true);
            SetColor(spec.Color);

            var direction = state.AimDirection.sqrMagnitude > 0.001f ? state.AimDirection.normalized : Vector3.forward;
            var range = Mathf.Max(0.1f, spec.Range);
            var distance = Mathf.Clamp(state.AimDistance, 0f, range);
            var target = state.CasterPosition + direction * distance;
            // 数据驱动的几何参数：缺省值兜底
            var selfRadius = spec.SelfRadius > 0f ? spec.SelfRadius : Mathf.Max(0.25f, spec.Radius);
            var fanRadius = spec.FanRadius > 0f ? spec.FanRadius : Mathf.Max(0.25f, spec.Radius);
            var fanAngle = spec.AngleDegrees > 0f ? spec.AngleDegrees : 90f;
            var sectorAngle = spec.AngleDegrees > 0f ? spec.AngleDegrees : 90f;
            var dashDistance = spec.DashDistance > 0f ? spec.DashDistance : range;
            var lockRadius = spec.LockProjectileRadius > 0f ? spec.LockProjectileRadius : Mathf.Max(0.45f, spec.Radius);

            switch (spec.PreviewShape)
            {
                case BattleHudSkillPreviewShape.DirectionLine:
                    ShowCasterRing(state.CasterPosition, Mathf.Max(0.65f, spec.Width * 0.65f));
                    ShowLine(state.CasterPosition, direction, range, spec.Width);
                    ShowDot(state.CasterPosition + direction * range, Mathf.Max(0.32f, spec.Width * 0.42f));
                    ShowEdgeRing(state.CasterPosition + direction * range, Mathf.Max(0.45f, spec.Width * 0.52f));
                    HideCircle();
                    HideSector();
                    break;
                case BattleHudSkillPreviewShape.DirectionArea:
                    ShowCasterRing(state.CasterPosition, Mathf.Max(0.75f, spec.Width * 0.55f));
                    ShowLine(state.CasterPosition, direction, range, spec.Width);
                    HideDot();
                    ShowEdgeRing(state.CasterPosition + direction * range, Mathf.Max(0.5f, spec.Width * 0.5f));
                    HideCircle();
                    HideSector();
                    break;
                case BattleHudSkillPreviewShape.DashLine:
                    ShowCasterRing(state.CasterPosition, Mathf.Max(0.85f, spec.Width * 0.58f));
                    var dashLength = Mathf.Max(0.1f, dashDistance);
                    ShowLine(state.CasterPosition, direction, dashLength, Mathf.Max(0.45f, spec.Width));
                    ShowDot(state.CasterPosition + direction * dashLength, Mathf.Max(0.42f, spec.Width * 0.46f));
                    ShowEdgeRing(state.CasterPosition + direction * dashLength, Mathf.Max(0.75f, spec.Width * 0.62f));
                    HideCircle();
                    HideSector();
                    break;
                case BattleHudSkillPreviewShape.TargetCircle:
                    ShowCasterRing(state.CasterPosition, 0.85f);
                    HideLine();
                    ShowCircle(target, Mathf.Max(0.25f, spec.Radius));
                    ShowDot(target, Mathf.Max(0.28f, spec.Radius * 0.2f));
                    ShowEdgeRing(target, Mathf.Max(0.35f, spec.Radius));
                    HideSector();
                    break;
                case BattleHudSkillPreviewShape.LockProjectile:
                    ShowCasterRing(state.CasterPosition, 0.8f);
                    ShowLine(state.CasterPosition, direction, Mathf.Max(0.1f, distance), Mathf.Max(0.22f, spec.Width * 0.18f));
                    ShowCircle(target, Mathf.Max(0.45f, lockRadius));
                    ShowDot(target, Mathf.Max(0.35f, lockRadius * 0.28f));
                    ShowEdgeRing(target, Mathf.Max(0.55f, lockRadius * 0.72f));
                    HideSector();
                    break;
                case BattleHudSkillPreviewShape.SelfCircle:
                    ShowCasterRing(state.CasterPosition, 0.95f);
                    HideLine();
                    ShowCircle(state.CasterPosition, selfRadius);
                    HideDot();
                    ShowEdgeRing(state.CasterPosition, Mathf.Max(0.35f, selfRadius));
                    HideSector();
                    break;
                case BattleHudSkillPreviewShape.Sector:
                    ShowCasterRing(state.CasterPosition, 0.85f);
                    HideLine();
                    HideCircle();
                    ShowDot(state.CasterPosition + direction * range, Mathf.Max(0.3f, spec.Width * 0.32f));
                    ShowSector(state.CasterPosition, direction, range, sectorAngle);
                    ShowEdgeRing(state.CasterPosition + direction * range, Mathf.Max(0.4f, spec.Width * 0.5f));
                    break;
                case BattleHudSkillPreviewShape.FanArea:
                    ShowCasterRing(state.CasterPosition, 0.75f);
                    ShowLine(state.CasterPosition, direction, range, Mathf.Max(0.18f, spec.Width * 0.22f));
                    HideCircle();
                    ShowDot(state.CasterPosition + direction * range, Mathf.Max(0.32f, spec.Width * 0.28f));
                    ShowSector(state.CasterPosition, direction, Mathf.Max(0.1f, fanRadius), fanAngle);
                    ShowEdgeRing(state.CasterPosition + direction * Mathf.Max(0.1f, fanRadius), Mathf.Max(0.45f, spec.Width * 0.46f));
                    break;
                default:
                    HideAllParts();
                    SetVisible(false);
                    break;
            }
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                HideAllParts();
            }

            if (Root != null)
            {
                Root.SetActive(visible);
            }
        }

        private void ShowLine(Vector3 start, Vector3 direction, float length, float width)
        {
            if (_line == null) return;

            _line.SetActive(true);
            var safeLength = Mathf.Max(0.1f, length);
            var safeWidth = Mathf.Max(0.08f, width);
            _line.transform.position = start + direction * (safeLength * 0.5f) + Vector3.up * HeightOffset;
            _line.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            _line.transform.localScale = new Vector3(safeWidth, 0.035f, safeLength);
        }

        private void ShowCircle(Vector3 center, float radius)
        {
            if (_circle == null) return;

            var diameter = Mathf.Max(0.1f, radius * 2f);
            _circle.SetActive(true);
            _circle.transform.position = center + Vector3.up * HeightOffset;
            _circle.transform.rotation = Quaternion.identity;
            _circle.transform.localScale = new Vector3(diameter, 0.035f, diameter);
        }

        private void ShowDot(Vector3 center, float radius)
        {
            if (_dot == null) return;

            var diameter = Mathf.Max(0.1f, radius * 2f);
            _dot.SetActive(true);
            _dot.transform.position = center + Vector3.up * (HeightOffset + 0.035f);
            _dot.transform.rotation = Quaternion.identity;
            _dot.transform.localScale = Vector3.one * diameter;
        }

        private void ShowCasterRing(Vector3 center, float radius)
        {
            ShowRing(_casterRing, center, radius, HeightOffset + 0.075f);
        }

        private void ShowEdgeRing(Vector3 center, float radius)
        {
            ShowRing(_edgeRing, center, radius, HeightOffset + 0.095f);
        }

        private static void ShowRing(GameObject ring, Vector3 center, float radius, float height)
        {
            if (ring == null) return;

            var diameter = Mathf.Max(0.1f, radius * 2f);
            ring.SetActive(true);
            ring.transform.position = center + Vector3.up * height;
            ring.transform.rotation = Quaternion.identity;
            ring.transform.localScale = new Vector3(diameter, 1f, diameter);
        }

        private float _lastSectorAngle = -1f;
        private int _lastSectorSegments = -1;

        private void ShowSector(Vector3 start, Vector3 direction, float length, float degrees)
        {
            if (_sector == null) return;

            var safeLength = Mathf.Max(0.1f, length);
            var clampedDegrees = Mathf.Clamp(degrees, 1f, 360f);
            const int segments = 36;
            if (_lastSectorAngle < 0f || Mathf.Abs(_lastSectorAngle - clampedDegrees) > 0.1f || _lastSectorSegments != segments)
            {
                var meshFilter = _sector.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    var existing = meshFilter.sharedMesh;
                    if (existing != null)
                    {
                        if (Application.isPlaying) Object.Destroy(existing); else Object.DestroyImmediate(existing);
                    }
                    var newMesh = BattleHudAimPreviewObjectFactory.BuildSectorMesh(segments, clampedDegrees);
                    newMesh.hideFlags = HideFlags.DontSave;
                    meshFilter.sharedMesh = newMesh;
                }
                _lastSectorAngle = clampedDegrees;
                _lastSectorSegments = segments;
            }
            _sector.SetActive(true);
            _sector.transform.position = start + Vector3.up * (HeightOffset + 0.01f);
            _sector.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            _sector.transform.localScale = new Vector3(safeLength, 1f, safeLength);
        }

        private void HideLine()
        {
            if (_line != null) _line.SetActive(false);
        }

        private void HideCircle()
        {
            if (_circle != null) _circle.SetActive(false);
        }

        private void HideDot()
        {
            if (_dot != null) _dot.SetActive(false);
        }

        private void HideSector()
        {
            if (_sector != null) _sector.SetActive(false);
        }

        private void HideRings()
        {
            if (_casterRing != null) _casterRing.SetActive(false);
            if (_edgeRing != null) _edgeRing.SetActive(false);
        }

        private void HideAllParts()
        {
            HideLine();
            HideCircle();
            HideDot();
            HideSector();
            HideRings();
        }

        private void SetColor(Color color)
        {
            SetColor(_line, color);
            SetColor(_circle, color);
            SetColor(_dot, Brighter(color, 1.35f, 0.9f));
            SetColor(_sector, color);
            SetColor(_casterRing, Brighter(color, 1.25f, 0.86f));
            SetColor(_edgeRing, Brighter(color, 1.45f, 0.78f));
        }

        private static Color Brighter(Color color, float factor, float alpha)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                Mathf.Clamp01(alpha));
        }

        private static void SetColor(GameObject go, Color color)
        {
            if (go == null) return;
            var renderer = go.GetComponent<Renderer>();
            var material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null) return;

            material.color = color;
        }
    }
}
