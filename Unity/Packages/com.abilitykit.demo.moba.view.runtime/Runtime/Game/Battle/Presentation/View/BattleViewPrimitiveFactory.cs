using UnityEngine;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewPrimitiveFactory
    {
        public GameObject CreateActorFallback()
        {
            return CreateActorFallback(actorId: 0, modelId: 0);
        }

        public GameObject CreateActorFallback(int actorId, int modelId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("actor.model:" + modelId)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(1f, 2f, 1f);
            ApplyColor(go, PickColor(actorId + modelId, 1f));
            return go;
        }

        /// <summary>Creates a fallback shell for Summon / Clone entities.</summary>
        public GameObject CreateSummonFallback(int actorId, int modelId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("summon.model:" + modelId)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(0.8f, 1.5f, 0.8f);
            ApplyColor(go, PickColor(actorId + modelId + 100, 0.9f));
            return go;
        }

        /// <summary>Creates a fallback shell for Turret entities.</summary>
        public GameObject CreateTurretFallback(int actorId, int modelId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("turret.model:" + modelId)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.transform.localScale = new Vector3(1.5f, 3f, 1.5f);
            ApplyColor(go, PickColor(actorId + modelId + 200, 1f));
            return go;
        }

        /// <summary>Creates a fallback shell for Monster entities.</summary>
        public GameObject CreateMonsterFallback(int actorId, int modelId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("monster.model:" + modelId)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = new Vector3(1f, 1f, 1f);
            ApplyColor(go, PickColor(actorId + modelId + 300, 0.85f));
            return go;
        }

        /// <summary>Creates a fallback shell for Building entities.</summary>
        public GameObject CreateBuildingFallback(int actorId, int modelId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("building.model:" + modelId)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(3f, 4f, 3f);
            ApplyColor(go, PickColor(actorId + modelId + 400, 0.7f));
            return go;
        }

        public GameObject CreateAoeModelFallback()
        {
            return CreateAoeModelFallback(modelId: 0);
        }

        public GameObject CreateAoeModelFallback(int modelId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("aoe.model:" + modelId)) return null;
            if (modelId == BattleViewPlaceholderIds.AoeSectorModel)
            {
                return CreateSectorFallback(modelId, degrees: 90f);
            }

            return CreateAoeRangeFallback(templateId: modelId, radius: 1f, delayMs: 0);
        }

        public GameObject CreateAoeRangeFallback(int templateId, float radius, int delayMs)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ConfigureAoeRange(go, templateId, radius, delayMs);
            return go;
        }

        public void ConfigureAoeRange(GameObject go, int templateId, float radius, int delayMs)
        {
            if (go == null) return;

            var r = Mathf.Max(0.5f, radius);
            go.transform.localScale = new Vector3(r * 2f, 0.04f, r * 2f);
            ApplyColor(go, ResolveAoeColor(templateId, delayMs));
        }

        public GameObject CreateVfxFallback()
        {
            return CreateVfxFallback(vfxId: 0);
        }

        public GameObject CreateVfxFallback(int vfxId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("vfx:" + vfxId)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.5f;
            ApplyColor(go, ResolveVfxColor(vfxId));
            return go;
        }

        public GameObject CreateProjectileFallback(int vfxId)
        {
            if (!BattleViewFallbackPolicy.AllowFallback("projectile.vfx:" + vfxId)) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.localScale = new Vector3(0.35f, 0.35f, 0.8f);
            ApplyColor(go, ResolveProjectileColor(vfxId));
            return go;
        }

        private static GameObject CreateSectorFallback(int modelId, float degrees)
        {
            var go = new GameObject("AoeSectorFallback", typeof(MeshFilter), typeof(MeshRenderer));
            var mesh = BuildSectorMesh(36, Mathf.Clamp(degrees, 1f, 180f));
            mesh.hideFlags = HideFlags.DontSave;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            ApplyColor(go, ResolveAoeColor(modelId, delayMs: 0));
            return go;
        }


        private static Mesh BuildSectorMesh(int segments, float degrees)
        {
            var safeSegments = Mathf.Max(3, segments);
            var vertices = new Vector3[safeSegments + 2];
            var triangles = new int[safeSegments * 3];
            vertices[0] = Vector3.zero;

            var start = -degrees * 0.5f;
            for (var i = 0; i <= safeSegments; i++)
            {
                var angle = (start + degrees * i / safeSegments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }

            for (var i = 0; i < safeSegments; i++)
            {
                var ti = i * 3;
                triangles[ti] = 0;
                triangles[ti + 1] = i + 1;
                triangles[ti + 2] = i + 2;
            }

            var mesh = new Mesh { name = "AoeSectorFallbackMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            if (go == null) return;
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var material = shader != null ? new Material(shader) : new Material(renderer.sharedMaterial);
            material.color = color;
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            ConfigureTransparency(material, color.a);
            renderer.material = material;
        }

        private static void ConfigureTransparency(Material material, float alpha)
        {
            if (material == null || alpha >= 0.99f) return;

            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static Color ResolveAoeColor(int templateId, int delayMs)
        {
            var color = PickColor(templateId, 0.35f);
            if (delayMs > 0) color = Color.Lerp(color, new Color(1f, 0.95f, 0.25f, 0.35f), 0.45f);
            color.a = 0.35f;
            return color;
        }

        private static Color ResolveVfxColor(int vfxId)
        {
            return PickColor(vfxId, 0.8f);
        }

        private static Color ResolveProjectileColor(int vfxId)
        {
            return Color.Lerp(PickColor(vfxId, 1f), new Color(0.15f, 0.95f, 1f, 1f), 0.5f);
        }

        private static Color PickColor(int seed, float alpha)
        {
            var value = Mathf.Abs(seed);
            switch (value % 6)
            {
                case 0: return new Color(0.95f, 0.25f, 0.2f, alpha);
                case 1: return new Color(0.2f, 0.7f, 1f, alpha);
                case 2: return new Color(0.25f, 0.9f, 0.35f, alpha);
                case 3: return new Color(1f, 0.8f, 0.15f, alpha);
                case 4: return new Color(0.8f, 0.35f, 1f, alpha);
                default: return new Color(1f, 0.45f, 0.15f, alpha);
            }
        }
    }

    internal static class BattleViewPlaceholderIds
    {
        // VFX placeholders
        public const int ProjectileVfx = 90000001;
        public const int ProjectileSpawnVfx = 90000002;
        public const int ProjectileHitVfx = 90000003;
        public const int ProjectileExpireVfx = 90000004;
        public const int PresentationCueVfx = 90000005;
        public const int ActorDeathVfx = 90000006;
        public const int ActorRespawnVfx = 90000007;
        public const int SkillCastVfx = 90000008;
        public const int BuffApplyVfx = 90000009;
        public const int BuffTickVfx = 90000010;
        public const int SummonSpawnVfx = 90000011;
        public const int SummonDespawnVfx = 90000012;

        // AOE model placeholders
        public const int AoeCircleModel = 90000101;
        public const int AoeSectorModel = 90000102;

        // Shell model placeholders
        public const int CharacterModel = 90001001;
        public const int TurretModel = 90001002;
        public const int MonsterModel = 90001003;
        public const int BuildingModel = 90001004;

        // Selection ring placeholder
        public const int SelectionRingModel = 90000201;

        public static bool IsPlaceholderVfx(int vfxId)
        {
            return vfxId >= ProjectileVfx && vfxId <= SummonDespawnVfx;
        }
    }

    internal static class BattleViewFallbackPolicy
    {
        public static bool AllowFallback(string missingDependency)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            Log.Error("[BattleView] Missing required presentation config: " + missingDependency);
            return false;
#endif
        }

        public static int DevelopmentOnly(int placeholderId, string missingDependency)
        {
            return AllowFallback(missingDependency) ? placeholderId : 0;
        }
    }
}
