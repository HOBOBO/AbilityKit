using System;

namespace AbilityKit.Ability.Server.Modules
{
    public interface ILogicWorldServerModule
    {
        void Install(LogicWorldServerOptions options);
        void Uninstall(LogicWorldServerOptions options);
    }
}
