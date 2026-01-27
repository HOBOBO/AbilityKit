using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Emilia.DataBuildPipeline.Editor
{
    public class DataBuildManager : BuildSingleton<DataBuildManager>
    {
        private List<IDataBuild> _dataBuilds = new List<IDataBuild>();

        public DataBuildManager()
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<IDataBuild>().Where((type) => type.IsAbstract == false && type.IsInterface == false).ToArray();
            int amount = types.Length;
            for (var i = 0; i < amount; i++)
            {
                Type type = types[i];
                object dataBuild = Activator.CreateInstance(type);
                IDataBuild build = dataBuild as IDataBuild;
                if (build == null) continue;
                this._dataBuilds.Add(build);
            }
        }

        public List<IDataBuild> GetDataBuildList(IBuildArgs buildArgs)
        {
            Type argsType = buildArgs.GetType();
            Dictionary<int, IDataBuild> dataBuildMap = new Dictionary<int, IDataBuild>();
            List<IDataBuild> dataBuildList = new List<IDataBuild>();

            while (argsType != typeof(object))
            {
                int amount = this._dataBuilds.Count;
                for (int i = 0; i < amount; i++)
                {
                    IDataBuild build = this._dataBuilds[i];
                    Type type = build.GetType();
                    
                    BuildPipelineAttribute attribute = type.GetCustomAttribute<BuildPipelineAttribute>();
                    if (attribute == null) continue;
                    
                    if (attribute.argsType != argsType) continue;

                    int priority = 0;
                    BuildSequenceAttribute sequenceAttribute = type.GetCustomAttribute<BuildSequenceAttribute>();
                    if (sequenceAttribute != null) priority = sequenceAttribute.priority;

                    if (dataBuildMap.TryAdd(priority, build) == false) continue;
                    
                    dataBuildList.Add(build);
                }

                argsType = argsType.BaseType;
            }

            dataBuildList.Sort((a, b) => {
                BuildSequenceAttribute attributeA = a.GetType().GetCustomAttribute<BuildSequenceAttribute>();
                BuildSequenceAttribute attributeB = b.GetType().GetCustomAttribute<BuildSequenceAttribute>();
                return attributeA.priority.CompareTo(attributeB.priority);
            });

            return dataBuildList;
        }
    }
}