using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using ConfigReloadBus = AbilityKit.Ability.HotReload.ConfigReloadBus;
using ConfigReloadResult = AbilityKit.Ability.HotReload.ConfigReloadResult;

namespace AbilityKit.Demo.Moba.Config.Core
{
    public interface IMobaConfigLoadPipeline
    {
        ConfigReloadResult ReloadFromSource(MobaConfigDatabase database, IConfigSource source, string basePath = null, bool strict = true);
        ConfigReloadResult ReloadFromDtoProvider(MobaConfigDatabase database, IMobaConfigDtoProvider provider, bool strict = true);
        ConfigReloadResult ReloadFromResources(MobaConfigDatabase database, string resourcesDir, bool strict = true);
    }

    public sealed class MobaConfigLoadPipeline : IMobaConfigLoadPipeline
    {
        private readonly IMobaConfigTableRegistry _registry;
        private readonly ITextAssetLoader _textAssetLoader;

        public MobaConfigLoadPipeline(IMobaConfigTableRegistry registry, ITextAssetLoader textAssetLoader)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _textAssetLoader = textAssetLoader ?? NullTextAssetLoader.Instance;
        }

        public ConfigReloadResult ReloadFromSource(MobaConfigDatabase database, IConfigSource source, string basePath = null, bool strict = true)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (source == null) throw new ArgumentNullException(nameof(source));

            return database.ReloadFromSource(source, basePath, strict);
        }

        public ConfigReloadResult ReloadFromDtoProvider(MobaConfigDatabase database, IMobaConfigDtoProvider provider, bool strict = true)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            var dtoArraysByType = new Dictionary<Type, Array>(TypeNameComparer.Instance);
            var tables = _registry.Tables;

            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                if (!provider.TryGetDtos(definition.DtoType, out var dtos) || dtos == null)
                {
                    if (strict)
                    {
                        var fail = ConfigReloadResult.Fail(MobaConfigDatabase.ReloadConfigKey, database.Version, $"DTO provider did not return config: {definition.DtoType.FullName}");
                        ConfigReloadBus.Publish(fail);
                        return fail;
                    }

                    dtos = Array.CreateInstance(definition.DtoType, 0);
                }

                dtoArraysByType[definition.DtoType] = dtos;
            }

            return database.ReloadFromDtoArrays(dtoArraysByType, strict);
        }

        public ConfigReloadResult ReloadFromResources(MobaConfigDatabase database, string resourcesDir, bool strict = true)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));

            return database.ReloadFromResources(resourcesDir, strict);
        }
    }
}
