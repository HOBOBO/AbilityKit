using System;
using System.IO;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Editor
{
    internal static class MobaMapSceneGenerator
    {
        private const string MapJsonPath = "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/battle_maps.json";
        private const string GeneratedRootPath = "Assets/Generated";
        private const string GeneratedMobaPath = "Assets/Generated/Moba";
        private const string MapRootName = "GeneratedBattleMap";

        public static GameObject Refresh(Scene scene, int mapId)
        {
            var map = LoadMap(mapId);
            EnsureAssetFolders();

            var prefabPath = $"{GeneratedMobaPath}/BattleMap_{map.Id}.prefab";
            var prefabSource = BuildMapRoot(map);
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, prefabPath);
            Object.DestroyImmediate(prefabSource);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Failed to generate battle map prefab. path={prefabPath}");
            }

            RemoveGeneratedMapRoot(scene);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Failed to instantiate battle map prefab. path={prefabPath}");
            }

            instance.name = MapRootName;
            return instance;
        }

        private static BattleMapDTO LoadMap(int mapId)
        {
            var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(MapJsonPath);
            if (jsonAsset == null)
            {
                throw new FileNotFoundException("Battle map JSON asset was not found.", MapJsonPath);
            }

            var maps = JsonConvert.DeserializeObject<BattleMapDTO[]>(jsonAsset.text) ?? Array.Empty<BattleMapDTO>();
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && maps[i].Id == mapId) return maps[i];
            }

            throw new InvalidOperationException($"Battle map JSON does not contain map id {mapId}. path={MapJsonPath}");
        }

        private static GameObject BuildMapRoot(BattleMapDTO map)
        {
            var root = new GameObject($"{MapRootName}_Map{map.Id}_{SanitizeName(map.Name)}");
            root.transform.position = Vector3.zero;

            CreateGround(root.transform, map.Bounds);
            CreateWalkableAreas(root.transform, map.WalkableAreas, map.Bounds);
            CreateSpawnPoints(root.transform, map.SpawnPoints);
            CreateCollisionObjects(root.transform, map.CollisionObjects);
            return root;
        }

        private static void CreateGround(Transform parent, MapBoundsDTO bounds)
        {
            if (bounds?.Center == null || bounds.Size == null) return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground_VisualOnly";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = ToVector3(bounds.Center) + Vector3.down * 0.1f;
            ground.transform.localScale = new Vector3(bounds.Size.X, 0.2f, bounds.Size.Z);
            RemoveCollider(ground);
        }

        private static void CreateWalkableAreas(
            Transform parent,
            MapWalkableAreaDTO[] walkableAreas,
            MapBoundsDTO bounds)
        {
            var group = new GameObject("WalkableAreas_VisualOnly");
            group.transform.SetParent(parent, false);

            if (walkableAreas == null || walkableAreas.Length == 0)
            {
                if (bounds?.Center == null || bounds.Size == null) return;
                CreateWalkableAreaMarker(group.transform, 0, "Map Bounds", bounds.Center, bounds.Size);
                return;
            }

            for (int i = 0; i < walkableAreas.Length; i++)
            {
                var area = walkableAreas[i];
                if (area?.Center == null || area.Size == null) continue;
                CreateWalkableAreaMarker(group.transform, area.Id, area.Name, area.Center, area.Size);
            }
        }

        private static void CreateWalkableAreaMarker(
            Transform parent,
            int id,
            string name,
            MapVector3DTO center,
            MapVector3DTO size)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"WalkableArea_{id}_{SanitizeName(name)}";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = ToVector3(center) + Vector3.up * 0.01f;
            marker.transform.localScale = new Vector3(size.X, 0.02f, size.Z);
            RemoveCollider(marker);
        }

        private static void CreateSpawnPoints(Transform parent, MapSpawnPointDTO[] spawnPoints)
        {
            var group = new GameObject("SpawnPoints");
            group.transform.SetParent(parent, false);
            if (spawnPoints == null) return;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var spawn = spawnPoints[i];
                if (spawn == null) continue;

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Spawn_{spawn.Id}_Team{spawn.TeamId}";
                marker.transform.SetParent(group.transform, false);
                marker.transform.localPosition = ToVector3(spawn.Position) + Vector3.up * 0.05f;
                marker.transform.localRotation = Quaternion.Euler(0f, spawn.YawDegrees, 0f);
                marker.transform.localScale = new Vector3(0.8f, 0.05f, 0.8f);
                RemoveCollider(marker);
            }
        }

        private static void CreateCollisionObjects(Transform parent, MapCollisionObjectDTO[] collisionObjects)
        {
            var group = new GameObject("CollisionObjects");
            group.transform.SetParent(parent, false);
            if (collisionObjects == null) return;

            for (int i = 0; i < collisionObjects.Length; i++)
            {
                var config = collisionObjects[i];
                if (config == null) continue;
                CreateCollisionObject(group.transform, config);
            }
        }

        private static void CreateCollisionObject(Transform parent, MapCollisionObjectDTO config)
        {
            var root = new GameObject($"MapObject_{config.Id}_{SanitizeName(config.Name)}_Projectile{config.ProjectileResponse}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = ToVector3(config.Position);
            root.transform.localRotation = Quaternion.Euler(ToVector3(config.RotationEuler));

            var shape = config.ShapeType ?? "Box";
            switch (shape.ToLowerInvariant())
            {
                case "box":
                    var box = root.AddComponent<BoxCollider>();
                    box.size = ToVector3(config.Size);
                    if (config.GenerateView) CreateVisual(root.transform, PrimitiveType.Cube, ToVector3(config.Size));
                    break;

                case "sphere":
                    var sphere = root.AddComponent<SphereCollider>();
                    sphere.radius = config.Radius;
                    if (config.GenerateView) CreateVisual(root.transform, PrimitiveType.Sphere, Vector3.one * config.Radius * 2f);
                    break;

                case "capsule":
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.radius = config.Radius;
                    capsule.height = config.Height;
                    if (config.GenerateView)
                    {
                        CreateVisual(root.transform, PrimitiveType.Capsule, new Vector3(config.Radius * 2f, config.Height * 0.5f, config.Radius * 2f));
                    }
                    break;

                default:
                    Object.DestroyImmediate(root);
                    throw new InvalidOperationException($"Unsupported battle map collision shape. objectId={config.Id}, shape={config.ShapeType}");
            }
        }

        private static void CreateVisual(Transform parent, PrimitiveType primitiveType, Vector3 scale)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = scale;
            RemoveCollider(visual);
        }

        private static void RemoveGeneratedMapRoot(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && string.Equals(roots[i].name, MapRootName, StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(roots[i]);
                }
            }
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        private static Vector3 ToVector3(MapVector3DTO value)
        {
            return value == null ? Vector3.zero : new Vector3(value.X, value.Y, value.Z);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unnamed";
            return value.Replace('/', '_').Replace('\\', '_').Replace(' ', '_');
        }

        private static void EnsureAssetFolders()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedRootPath))
            {
                AssetDatabase.CreateFolder("Assets", "Generated");
            }

            if (!AssetDatabase.IsValidFolder(GeneratedMobaPath))
            {
                AssetDatabase.CreateFolder(GeneratedRootPath, "Moba");
            }
        }
    }
}
