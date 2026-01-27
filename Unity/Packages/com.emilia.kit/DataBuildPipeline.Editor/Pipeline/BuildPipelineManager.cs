using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Emilia.DataBuildPipeline.Editor
{
    public class BuildPipelineManager : BuildSingleton<BuildPipelineManager>
    {
        private Dictionary<Type, Type> pipelines = new Dictionary<Type, Type>();

        public BuildPipelineManager()
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<IBuildPipeline>().ToArray();
            int amount = types.Length;
            for (var i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsInterface) continue;

                BuildPipelineAttribute attribute = type.GetCustomAttribute<BuildPipelineAttribute>();
                if (attribute == null) continue;
                this.pipelines.Add(attribute.argsType, type);
            }
        }

        public IBuildPipeline GetPipeline(Type argsType)
        {
            if (this.pipelines.TryGetValue(argsType, out Type type) == false) return new UniversalBuildPipeline();
            IBuildPipeline pipeline = Activator.CreateInstance(type) as IBuildPipeline;
            return pipeline;
        }
    }
}