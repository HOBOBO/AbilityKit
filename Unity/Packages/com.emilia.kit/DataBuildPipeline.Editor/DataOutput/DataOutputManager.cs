using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEditor;

namespace Emilia.DataBuildPipeline.Editor
{
    public class DataOutputManager : BuildSingleton<DataOutputManager>
    {
        private List<IDataOutput> _dataOutputs = new List<IDataOutput>();

        public DataOutputManager()
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<IDataOutput>().Where((type) => type.IsAbstract == false && type.IsInterface == false).ToArray();
            int amount = types.Length;
            for (var i = 0; i < amount; i++)
            {
                Type type = types[i];
                object output = Activator.CreateInstance(type);
                IDataOutput dataOutput = output as IDataOutput;
                if (dataOutput != null) this._dataOutputs.Add(dataOutput);
            }
        }

        public List<IDataOutput> GetFinalizeBuildDisposeList(IBuildArgs buildArgs)
        {
            Type argsType = buildArgs.GetType();

            Dictionary<int, IDataOutput> dataOutputMap = new Dictionary<int, IDataOutput>();
            List<IDataOutput> dataOutputList = new List<IDataOutput>();

            while (argsType != typeof(object))
            {
                int amount = this._dataOutputs.Count;
                for (var i = 0; i < amount; i++)
                {
                    IDataOutput dataOutput = this._dataOutputs[i];
                    Type type = dataOutput.GetType();
                    
                    BuildPipelineAttribute attribute = type.GetCustomAttribute<BuildPipelineAttribute>();
                    if (attribute == null) continue;
                    
                    if (attribute.argsType != argsType) continue;
                    
                    int priority = 0;
                    BuildSequenceAttribute sequenceAttribute = type.GetCustomAttribute<BuildSequenceAttribute>();
                    if (sequenceAttribute != null) priority = sequenceAttribute.priority;
                    
                    if (dataOutputMap.TryAdd(priority, dataOutput) == false) continue;
                    
                    dataOutputList.Add(dataOutput);
                }
                
                argsType = argsType.BaseType;
            }

            dataOutputList.Sort((a, b) => {
                BuildSequenceAttribute attributeA = a.GetType().GetCustomAttribute<BuildSequenceAttribute>();
                BuildSequenceAttribute attributeB = b.GetType().GetCustomAttribute<BuildSequenceAttribute>();
                return attributeA.priority.CompareTo(attributeB.priority);
            });

            return dataOutputList;
        }
    }
}