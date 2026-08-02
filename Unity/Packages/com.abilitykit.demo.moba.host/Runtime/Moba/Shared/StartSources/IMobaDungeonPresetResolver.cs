namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public interface IMobaDungeonPresetResolver
    {
        bool TryResolve(int dungeonId, int presetId, out MobaDungeonPreset preset);
    }
}
