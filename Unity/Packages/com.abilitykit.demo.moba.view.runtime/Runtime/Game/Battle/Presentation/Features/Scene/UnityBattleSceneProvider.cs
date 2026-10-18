using System;
using AbilityKit.Game.Flow.Dungeons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Battle.Presentation.Features.Scene
{
    /// <summary>
    /// 用 Unity 场景资源装配副本。副本表只提供装配器扩展点 id，不提供场景资源名——
    /// 场景与地图都是玩法资源，由本装配器按玩法 id 解析（玩法常量不进副本表）。
    /// 加载使用 <see cref="LoadSceneMode.Additive"/>，这样副本切换不影响大厅场景，
    /// 回放与 headless 局可以完全跳过。
    /// </summary>
    public sealed class UnityBattleSceneProvider : IBattleSceneProvider
    {
        public string Id => "unity_map";

        public IBattleSceneHandle Assemble(in DungeonSceneContext context)
        {
            var dungeon = context.Dungeon;
            if (dungeon == null)
                throw new ArgumentException("Dungeon definition is required.", nameof(context));
            return new UnityBattleSceneHandle(dungeon, context.BattleMapId,
                _sceneResolver?.Invoke(dungeon.GameplayId));
        }

        /// <summary>
        /// 玩法 id → Unity 场景资源名的解析端口。场景名是玩法资源，由玩法侧提供，
        /// 副本表不持有；解析不到时只装配逻辑地图，不加载场景。
        /// </summary>
        private readonly Func<int, string> _sceneResolver;

        public UnityBattleSceneProvider(Func<int, string> sceneResolver = null) =>
            _sceneResolver = sceneResolver;

        private sealed class UnityBattleSceneHandle : IBattleSceneHandle
        {
            private readonly int _battleMapId;
            private UnityEngine.SceneManagement.Scene? _loaded;

            public UnityBattleSceneHandle(MobaDungeonDef dungeon, int battleMapId, string sceneAssetName)
            {
                _battleMapId = battleMapId;
                if (string.IsNullOrWhiteSpace(sceneAssetName)) return;
                SceneManager.LoadScene(sceneAssetName, LoadSceneMode.Additive);
                _loaded = SceneManager.GetSceneByName(sceneAssetName);
                IsLoaded = _loaded.HasValue && _loaded.Value.IsValid() && _loaded.Value.isLoaded;
                if (!IsLoaded)
                {
                    _loaded = null;
                    // 场景缺失属于配置错误，必须让副本进入失败，而不是继续跑一个空场景。
                    throw new InvalidOperationException(
                        $"副本 {dungeon.Id}（玩法 {dungeon.GameplayId}）的场景 '{sceneAssetName}' 加载失败。");
                }
            }

            public bool IsLoaded { get; private set; }
            public int BattleMapId => _battleMapId;

            public void Dispose()
            {
                if (_loaded.HasValue && _loaded.Value.IsValid() && _loaded.Value.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(_loaded.Value);
                    IsLoaded = false;
                }
                _loaded = null;
            }
        }
    }
}
