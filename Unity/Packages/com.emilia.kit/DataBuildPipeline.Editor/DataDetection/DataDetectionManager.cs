using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Emilia.DataBuildPipeline.Editor
{
    public class DataDetectionManager : BuildSingleton<DataDetectionManager>
    {
        private List<IDataDetection> _detections = new List<IDataDetection>();

        public DataDetectionManager()
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<IDataDetection>().Where((type) => type.IsAbstract == false && type.IsInterface == false).ToArray();
            int amount = types.Length;
            for (var i = 0; i < amount; i++)
            {
                Type type = types[i];
                object detection = Activator.CreateInstance(type);
                IDataDetection guideDataDetection = detection as IDataDetection;
                if (guideDataDetection != null) this._detections.Add(guideDataDetection);
            }
        }

        public List<IDataDetection> GetDataDetectionList(IBuildArgs buildArgs)
        {
            Type argsType = buildArgs.GetType();
            List<IDataDetection> list = new List<IDataDetection>();

            while (argsType != typeof(object))
            {
                int amount = this._detections.Count;
                for (int i = 0; i < amount; i++)
                {
                    IDataDetection detection = this._detections[i];
                    Type type = detection.GetType();
                    BuildPipelineAttribute attribute = type.GetCustomAttribute<BuildPipelineAttribute>();
                    if (attribute == null) continue;
                    if (attribute.argsType != argsType) continue;
                    list.Add(detection);
                }

                argsType = argsType.BaseType;
            }

            return list;
        }
    }
}