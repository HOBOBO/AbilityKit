using UnityEngine;

namespace AbilityKit.Game.Flow
{
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
}
