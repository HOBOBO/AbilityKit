using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaSkillCastInstanceSyncSettings))]
    public sealed class MobaSkillCastInstanceSyncSettings : IService
    {
        public int RetainCompletedFrames { get; set; } = 30;
        public int DestroyConfirmGateFrames { get; set; } = 10;

        public void Dispose()
        {
        }
    }
}
