using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Protocol.Moba;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class ConfiguredBattleBootstrapper : IBattleBootstrapper, IBattleStartConfigProvider,
        IBattleFlowProfileProvider, IBattleFlowModeProvider
    {
        private readonly BattleStartConfig _sourceConfig;
        private readonly BattleStartPresetSO _preset;
        private BattleStartConfig _config;

        public ConfiguredBattleBootstrapper(BattleStartConfig sourceConfig, BattleStartPresetSO preset = null)
        {
            _sourceConfig = sourceConfig;
            _preset = preset;
        }

        public BattleStartConfig Config => _config;
        public string FlowProfileId => _preset != null ? _preset.FlowProfileId :
            _sourceConfig?.Preset != null ? _sourceConfig.Preset.FlowProfileId : _sourceConfig?.FlowProfileId;
        public string FlowHostMode => (_preset != null ? _preset.HostMode :
            _sourceConfig?.Preset != null ? _sourceConfig.Preset.HostMode :
            _sourceConfig != null ? _sourceConfig.HostMode : BattleHostMode.Local).ToString();

        public BattleStartPlan Build()
        {
            var cfg = CreateRuntimeConfig(_sourceConfig, _preset);
            _config = cfg;

            MobaBattleLaunchSpec launchSpec;
            if (cfg.UseRoomGameStartSpec)
            {
                var roomSpec = cfg.BuildRoomGameStartSpec();
                launchSpec = cfg.BuildLaunchSpec(in roomSpec);
            }
            else
            {
                launchSpec = cfg.BuildLaunchSpec();
            }

            var req = launchSpec.ToEnterReq();
            var initData = launchSpec.ToWorldInitData(MobaWorldBootstrapModule.InitOpCode);
            return cfg.BuildPlan(req, initData.Payload, initData.OpCode, launchSpec);
        }

        private static BattleStartConfig CreateRuntimeConfig(BattleStartConfig sourceConfig, BattleStartPresetSO preset)
        {
            var cfg = sourceConfig != null
                ? Object.Instantiate(sourceConfig)
                : ScriptableObject.CreateInstance<BattleStartConfig>();

            cfg.name = preset != null ? $"BattleStartConfig_Runtime_{preset.name}" : "BattleStartConfig_Runtime";
            if (preset != null)
            {
                cfg.Preset = preset;
            }

            return cfg;
        }
    }
}
