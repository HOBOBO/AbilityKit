using System;

namespace AbilityKit.Ability.Host.Modules
{
    public interface ILogicWorldServerModule
    {
        void Install(LogicWorldServerOptions options);
        void Uninstall(LogicWorldServerOptions options);
    }
}
