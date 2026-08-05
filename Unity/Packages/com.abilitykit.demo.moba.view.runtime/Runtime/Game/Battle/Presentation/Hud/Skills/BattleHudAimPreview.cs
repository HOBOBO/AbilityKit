using System.Collections.Generic;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudAimPreview
    {
        private readonly BattleHudAimPreviewPositionResolver _positions;
        private readonly BattleHudAimPreviewObjectFactory _objects;
        private const float SubmittedPreviewDurationSeconds = 0.45f;

        private IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> _skillSpecs;
        private BattleHudAimPreviewObject _preview;
        private int _lastSubmissionVersion;
        private float _submittedPreviewRemainingSeconds;

        public BattleHudAimPreview(
            BattleHudAimPreviewPositionResolver positions = null,
            BattleHudAimPreviewObjectFactory objects = null)
        {
            _positions = positions ?? new BattleHudAimPreviewPositionResolver();
            _objects = objects ?? new BattleHudAimPreviewObjectFactory();
        }

        internal GameObject PreviewRoot => _preview?.Root;

        public void SetSkillSpecs(IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> skillSpecs)
        {
            _skillSpecs = skillSpecs;
        }

        public void Tick(BattleContext ctx, float deltaTime = 0f)
        {
            if (!_positions.TryResolve(ctx, out var state) || !TryGetSpec(state.Slot, out var spec))
            {
                Hide();
                return;
            }

            if (state.SubmissionVersion > 0)
            {
                if (state.SubmissionVersion != _lastSubmissionVersion)
                {
                    _lastSubmissionVersion = state.SubmissionVersion;
                    // LockProjectile 使用 spec.LockOnDurationSeconds 控制瞄准停留时间；其它形状用统一的短停留
                    _submittedPreviewRemainingSeconds = spec.PreviewShape == BattleHudSkillPreviewShape.LockProjectile
                        ? Mathf.Max(SubmittedPreviewDurationSeconds, spec.LockOnDurationSeconds)
                        : SubmittedPreviewDurationSeconds;
                }
                else
                {
                    _submittedPreviewRemainingSeconds -= Mathf.Max(0f, deltaTime);
                }

                if (_submittedPreviewRemainingSeconds <= 0f)
                {
                    Hide();
                    return;
                }
            }
            else
            {
                _submittedPreviewRemainingSeconds = 0f;
            }

            EnsurePreview();
            _preview.Apply(state, spec);
        }

        public void Clear()
        {
            if (_preview != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_preview.Root);
                }
                else
                {
                    Object.DestroyImmediate(_preview.Root);
                }
            }

            _preview = null;
            _lastSubmissionVersion = 0;
            _submittedPreviewRemainingSeconds = 0f;
        }

        private bool TryGetSpec(int slot, out BattleHudSkillPresentationSpec spec)
        {
            if (_skillSpecs != null &&
                _skillSpecs.TryGetValue(slot, out spec) &&
                spec.PreviewShape != BattleHudSkillPreviewShape.None)
            {
                return true;
            }

            spec = default;
            return false;
        }

        private void Hide()
        {
            if (_preview != null)
            {
                _preview.SetVisible(false);
            }
        }

        private void EnsurePreview()
        {
            if (_preview != null) return;

            _preview = _objects.Create();
        }
    }

    internal readonly struct BattleHudAimPreviewState
    {
        public readonly int Slot;
        public readonly Vector3 CasterPosition;
        public readonly Vector3 AimDirection;
        public readonly float AimDistance;
        public readonly int SubmissionVersion;

        public BattleHudAimPreviewState(
            int slot,
            Vector3 casterPosition,
            Vector3 aimDirection,
            float aimDistance,
            int submissionVersion = 0)
        {
            Slot = slot;
            CasterPosition = casterPosition;
            AimDirection = aimDirection;
            AimDistance = aimDistance;
            SubmissionVersion = submissionVersion;
        }
    }

    internal sealed class BattleHudAimPreviewPositionResolver
    {
        private bool _hasLastCasterPosition;
        private Vector3 _lastCasterPosition;

        public bool TryResolve(BattleContext ctx, out BattleHudAimPreviewState state)
        {
            state = default;
            if (ctx == null) return false;

            if (!ctx.TryReadHudSkillAimPreview(out var slot, out var aimDx, out var aimDz, out var submissionVersion))
            {
                return false;
            }

            if (!TryResolveCasterPosition(ctx, out var casterPosition))
            {
                return false;
            }

            var aim = new Vector3(aimDx, 0f, aimDz);
            var distance = aim.magnitude;
            var direction = distance > 0.001f ? aim / distance : Vector3.forward;
            state = new BattleHudAimPreviewState(slot, casterPosition, direction, distance, submissionVersion);
            return true;
        }

        private bool TryResolveCasterPosition(BattleContext ctx, out Vector3 position)
        {
            position = default;
            if (ctx == null || !ctx.TryResolveLocalActorWorldPos(out position))
            {
                return TryUseLastCasterPosition(out position);
            }

            _lastCasterPosition = position;
            _hasLastCasterPosition = true;
            return true;
        }

        private bool TryUseLastCasterPosition(out Vector3 position)
        {
            position = _lastCasterPosition;
            return _hasLastCasterPosition;
        }
    }

    internal sealed class BattleHudAimPreviewObjectFactory
    {
        public BattleHudAimPreviewObject Create()
        {
            var root = new GameObject("SkillAimPreview");
            root.hideFlags = HideFlags.DontSave;

            var line = CreatePrimitive(root.transform, "Line", PrimitiveType.Cube);
            var circle = CreatePrimitive(root.transform, "Circle", PrimitiveType.Cylinder);
            var dot = CreatePrimitive(root.transform, "Dot", PrimitiveType.Sphere);
            var sector = CreateSector(root.transform, "Sector", segments: 36, degrees: 90f);
            var casterRing = CreateRing(root.transform, "CasterRing", segments: 72, thickness01: 0.18f);
            var edgeRing = CreateRing(root.transform, "EdgeRing", segments: 72, thickness01: 0.12f);

            var preview = new BattleHudAimPreviewObject(root, line, circle, dot, sector, casterRing, edgeRing);
            preview.SetVisible(false);
            return preview;
        }

        private static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent, false);

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = CreateMaterial(new Color(0.2f, 0.75f, 1f, 0.28f));
            }

            return go;
        }

        private static GameObject CreateSector(Transform parent, string name, int segments, float degrees)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent, false);

            var mesh = BuildSectorMesh(Mathf.Max(3, segments), Mathf.Clamp(degrees, 1f, 180f));
            mesh.hideFlags = HideFlags.DontSave;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().material = CreateMaterial(new Color(0.2f, 0.75f, 1f, 0.28f));
            return go;
        }

        private static GameObject CreateRing(Transform parent, string name, int segments, float thickness01)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(parent, false);

            var mesh = BuildRingMesh(Mathf.Max(8, segments), Mathf.Clamp01(thickness01));
            mesh.hideFlags = HideFlags.DontSave;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().material = CreateMaterial(new Color(0.2f, 0.75f, 1f, 0.42f));
            return go;
        }

        internal static Mesh BuildSectorMesh(int segments, float degrees)
        {
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;

            var start = -degrees * 0.5f;
            for (var i = 0; i <= segments; i++)
            {
                var angle = (start + degrees * i / segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            for (var i = 0; i < segments; i++)
            {
                var ti = i * 3;
                triangles[ti] = 0;
                triangles[ti + 1] = i + 1;
                triangles[ti + 2] = i + 2;
            }

            var mesh = new Mesh { name = "SkillAimPreviewSector" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh BuildRingMesh(int segments, float thickness01)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            var inner = Mathf.Clamp01(1f - thickness01);
            for (var i = 0; i < segments; i++)
            {
                var angle = Mathf.PI * 2f * i / segments;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction;
            }

            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                var ti = i * 6;
                var inner0 = i * 2;
                var outer0 = inner0 + 1;
                var inner1 = next * 2;
                var outer1 = inner1 + 1;
                triangles[ti] = inner0;
                triangles[ti + 1] = outer0;
                triangles[ti + 2] = outer1;
                triangles[ti + 3] = inner0;
                triangles[ti + 4] = outer1;
                triangles[ti + 5] = inner1;
            }

            var mesh = new Mesh { name = "SkillAimPreviewRing" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var material = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            material.color = color;
            material.hideFlags = HideFlags.DontSave;
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            ConfigureTransparency(material, color.a);
            return material;
        }

        private static void ConfigureTransparency(Material material, float alpha)
        {
            if (material == null) return;

            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
        }
    }

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
