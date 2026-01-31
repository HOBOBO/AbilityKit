using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Vfx;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public static class BattleViewFactory
    {
        public static MobaConfigDatabase Configs;
        public static VfxDatabase VfxDb;

        public static MobaConfigDatabase GetOrLoadConfigs()
        {
            if (Configs == null) Configs = MobaConfigLoader.LoadDefault();
            return Configs;
        }

        public static GameObject CreateShellGameObject(int actorId, int modelId)
        {
            var configs = GetOrLoadConfigs();

            GameObject prefab = null;
            if (configs != null && modelId > 0)
            {
                try
                {
                    var model = configs.GetModel(modelId);
                    if (model != null && !string.IsNullOrEmpty(model.PrefabPath))
                    {
                        prefab = Resources.Load<GameObject>(model.PrefabPath);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab);
                if (configs != null && modelId > 0)
                {
                    try
                    {
                        var model = configs.GetModel(modelId);
                        if (model != null)
                        {
                            var s = model.Scale <= 0f ? 1f : model.Scale;
                            go.transform.localScale = new Vector3(s, s, s);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(1f, 2f, 1f);
            }

            go.name = $"Actor_{actorId}";

            var attachRoot = new GameObject("AttachRoot");
            attachRoot.transform.SetParent(go.transform, worldPositionStays: false);
            attachRoot.transform.localPosition = Vector3.zero;
            return go;
        }

        public static GameObject CreateModelGo(int modelId)
        {
            if (modelId <= 0) return null;

            var configs = GetOrLoadConfigs();
            GameObject prefab = null;
            if (configs != null)
            {
                try
                {
                    var model = configs.GetModel(modelId);
                    if (model != null && !string.IsNullOrEmpty(model.PrefabPath))
                    {
                        prefab = Resources.Load<GameObject>(model.PrefabPath);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"AoeModel_{modelId}";
            return go;
        }

        public static GameObject CreateVfxGo(int vfxId)
        {
            if (vfxId <= 0) return null;
            if (VfxDb == null) VfxDb = VfxDatabase.LoadFromResources("vfx/vfx");
            if (VfxDb == null) return null;

            if (!VfxDb.TryGet(vfxId, out var dto) || dto == null || string.IsNullOrEmpty(dto.Resource))
            {
                return null;
            }

            var prefab = Resources.Load<GameObject>(dto.Resource);
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.5f;
            }

            go.name = $"AoeVfx_{vfxId}";
            return go;
        }

        public static int ResolveModelId(BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;

            var configs = GetOrLoadConfigs();
            if (configs == null) return 0;

            try
            {
                if (meta.Kind == BattleEntityKind.Character)
                {
                    var ch = configs.GetCharacter(meta.EntityCode);
                    return ch != null ? ch.ModelId : 0;
                }

                return 0;
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex);
                return 0;
            }
        }

        public static int ResolveProjectileVfxId(BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            if (meta.Kind != BattleEntityKind.Projectile) return 0;

            var configs = GetOrLoadConfigs();
            if (configs == null) return 0;

            try
            {
                var proj = configs.GetProjectile(meta.EntityCode);
                return proj != null ? proj.VfxId : 0;
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex);
                return 0;
            }
        }

        public static ProjectileMO TryGetProjectile(int templateId)
        {
            if (templateId <= 0) return null;
            var configs = GetOrLoadConfigs();
            if (configs == null) return null;
            try { return configs.GetProjectile(templateId); }
            catch (System.Exception ex) { Log.Exception(ex); return null; }
        }

        public static AoeMO TryGetAoe(int templateId)
        {
            if (templateId <= 0) return null;
            var configs = GetOrLoadConfigs();
            if (configs == null) return null;
            try { return configs.GetAoe(templateId); }
            catch (System.Exception ex) { Log.Exception(ex); return null; }
        }
    }
}
