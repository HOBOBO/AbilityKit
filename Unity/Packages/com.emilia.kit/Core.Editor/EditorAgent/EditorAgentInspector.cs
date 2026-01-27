using System;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    [CustomEditor(typeof(EditorAgent))]
    public class EditorAgentInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("添加代理"))
            {
                OdinMenu tree = new OdinMenu("选择代理");

                Type[] array = TypeCache.GetTypesDerivedFrom<IAgent>().ToArray();
                foreach (Type type in array)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    AgentNameAttribute attribute = type.GetCustomAttribute<AgentNameAttribute>();
                    if (attribute == null) continue;
                    string displayName = attribute.Name;
                    tree.AddItem(displayName, () => OnAddAgent(type));
                }

                tree.ShowInPopup(300);
            }
            base.OnInspectorGUI();
        }

        void OnAddAgent(Type type)
        {
            EditorAgent agent = (EditorAgent) target;
            IAgent instance = (IAgent) Activator.CreateInstance(type);
            agent.AddAgent(instance);
        }
    }
}