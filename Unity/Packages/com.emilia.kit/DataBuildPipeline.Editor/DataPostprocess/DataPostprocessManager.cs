using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.Utilities;
using UnityEditor;

namespace Emilia.DataBuildPipeline.Editor
{
    public class DataPostprocessManager : BuildSingleton<DataPostprocessManager>
    {
        private List<IDataPostprocess> _postprocessList = new List<IDataPostprocess>();

        public DataPostprocessManager()
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<IDataPostprocess>().Where((type) => type.IsAbstract == false && type.IsInterface == false).ToArray();
            var amount = types.Length;
            for (var i = 0; i < amount; i++)
            {
                Type type = types[i];
                var postprocess = Activator.CreateInstance(type);
                IDataPostprocess iDataPostprocess = postprocess as IDataPostprocess;
                if (iDataPostprocess != null) this._postprocessList.Add(iDataPostprocess);
            }
        }

        public List<IDataPostprocess> GetDataPostprocess(IBuildArgs buildArgs)
        {
            Type argsType = buildArgs.GetType();

            Dictionary<int, IDataPostprocess> dataPostprocessMap = new Dictionary<int, IDataPostprocess>();
            List<IDataPostprocess> dataPostprocessList = new List<IDataPostprocess>();

            while (argsType != typeof(object))
            {
                int amount = this._postprocessList.Count;
                for (var i = 0; i < amount; i++)
                {
                    IDataPostprocess postprocess = this._postprocessList[i];
                    Type type = postprocess.GetType();
                    
                    BuildPipelineAttribute attribute = type.GetCustomAttribute<BuildPipelineAttribute>();
                    if (attribute == null) continue;
                    
                    if (attribute.argsType != argsType) continue;
                    
                    int priority = 0;
                    BuildSequenceAttribute sequenceAttribute = type.GetCustomAttribute<BuildSequenceAttribute>();
                    if (sequenceAttribute != null) priority = sequenceAttribute.priority;
                    
                    if (dataPostprocessMap.TryAdd(priority, postprocess) == false) continue;
                    
                    dataPostprocessList.Add(postprocess);
                }
                
                argsType = argsType.BaseType;
            }

            dataPostprocessList.Sort((a, b) => {
                BuildSequenceAttribute attributeA = a.GetType().GetCustomAttribute<BuildSequenceAttribute>();
                BuildSequenceAttribute attributeB = b.GetType().GetCustomAttribute<BuildSequenceAttribute>();
                return attributeA.priority.CompareTo(attributeB.priority);
            });

            return dataPostprocessList;
        }
    }
}