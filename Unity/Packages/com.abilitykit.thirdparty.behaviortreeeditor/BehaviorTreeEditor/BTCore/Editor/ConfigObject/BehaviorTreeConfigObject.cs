using BTCore.Runtime;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BTCore.Editor
{
    /// <summary>
    /// 行为树配置文件
    /// </summary>
    [CreateAssetMenu(fileName = "行为树配置", menuName = "行为树/行为树配置")]
    public class BehaviorTreeConfigObject : SerializedScriptableObject, IBehavior
    {
        [LabelText("文件名")]
        public string Name;
        public BehaviorSource source;
        public int instanceID => GetInstanceID();
        public Object GetObject(bool local = false)
        {
            return this;
        }

        public void SaveSource(BTree bTree)
        {
            if (source == null)
            {
                source = new BehaviorSource();
            }
            var json = JsonConvert.SerializeObject(bTree, BTDef.SerializerSettingsAll);
            source.TreeJson = json;
            //设置文件脏标记
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
            AssetDatabase.Refresh();
            Debug.LogError("保存成功");
        }

        public BTree GetSource(bool local = false)
        {
            if (source != null)
            {
                source.Load();
                return source.Tree == null ? new BTree() : source.Tree;
            }
            return new BTree();
        }
    }
}