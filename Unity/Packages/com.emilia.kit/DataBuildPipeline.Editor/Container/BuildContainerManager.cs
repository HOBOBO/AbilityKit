using System;
using System.Collections.Generic;
using Sirenix.Utilities;
using UnityEditor;

namespace Emilia.DataBuildPipeline.Editor
{
    public class BuildContainerManager : BuildSingleton<BuildContainerManager>
    {
        private Dictionary<Type, Type> buildContainerTypes = new Dictionary<Type, Type>();

        public BuildContainerManager()
        {
            IList<Type> types = TypeCache.GetTypesDerivedFrom<IBuildContainer>();

            int count = types.Count;
            for (int i = 0; i < count; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface) continue;

                BuildPipelineAttribute buildPipelineAttribute = type.GetCustomAttribute<BuildPipelineAttribute>();
                if (buildPipelineAttribute == null) continue;

                buildContainerTypes[buildPipelineAttribute.argsType] = type;
            }
        }

        public IBuildContainer CreateBuildContainer(IBuildArgs buildArgs)
        {
            Type argsType = buildArgs.GetType();
            Type buildContainerType = this.buildContainerTypes.GetValueOrDefault(argsType);
            if (buildContainerType == null) return new BuildContainer();
            return Activator.CreateInstance(buildContainerType) as IBuildContainer;
        }
    }
}