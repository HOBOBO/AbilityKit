using System;
using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Kit.Editor;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;

namespace Emilia.Variables.Editor
{
    [Serializable]
    public class EditorParameter
    {
        public const string DragAndDropKey = "EditorParameter";
        
        [HideInInspector]
        public string key;

        [HideLabel, HorizontalGroup(0.3f)]
        public string description = "描述";

        [HideLabel, HorizontalGroup(0.7f)]
        public Variable value;
    }

    [Serializable, HideMonoScript]
    public partial class EditorParametersManager : TitleAsset
    {
        public override string title => "参数列表";

        /// <summary>
        /// 菜单过滤的类型
        /// </summary>
        public virtual IList<Type> filterTypes => TypeCache.GetTypesDerivedFrom<Variable>();

        [LabelText("参数定义"), HideReferenceObjectPicker, NonSerialized, OdinSerialize,
         ListDrawerSettings(ShowFoldout = false, CustomAddFunction = nameof(Add))]
        public List<EditorParameter> parameters = new List<EditorParameter>();

        void Add()
        {
            IList<Type> types = filterTypes;

            OdinMenu menu = new OdinMenu("选择参数类型");

            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract) continue;

                string label = type.Name;

                LabelTextAttribute labelTextAttribute = type.GetCustomAttribute<LabelTextAttribute>();
                if (labelTextAttribute != null) label = labelTextAttribute.Text;

                menu.AddItem(label, () => AddParameter(type));
            }

            menu.ShowInPopup();

            void AddParameter(Type type)
            {
                Variable variable = ReflectUtility.CreateInstance(type) as Variable;
                if (variable == null) return;

                EditorParameter parameter = new EditorParameter();
                parameter.key = Guid.NewGuid().ToString();
                parameter.value = variable;

                Undo.RegisterCompleteObjectUndo(this, "EditorParametersManage Add");
                parameters.Add(parameter);
            }
        }

        public EditorParameter GetParameter(string key)
        {
            return parameters.Find(p => p.key == key);
        }

        /// <summary>
        /// 转换为VariablesManage
        /// </summary>
        public VariablesManager ToParametersManage()
        {
            VariablesManager variablesManage = new VariablesManager();

            int amount = parameters.Count;
            for (int i = 0; i < amount; i++)
            {
                EditorParameter editorParameter = parameters[i];
                Variable variable = VariableUtility.Create(editorParameter.value.type);
                variablesManage.SetThisValue(editorParameter.key, variable);
            }

            return variablesManage;
        }
    }
}