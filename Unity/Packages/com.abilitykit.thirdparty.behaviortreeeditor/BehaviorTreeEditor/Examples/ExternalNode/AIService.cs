//------------------------------------------------------------
//        File:  AIService.cs
//       Brief:  AIService
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BTCore.Runtime;
using BTCore.Runtime.Externals;
using Newtonsoft.Json;

namespace Examples.ExternalNode
{
    public interface IAIService
    {
        /// <summary>
        /// Crate AIAgent
        /// </summary>
        /// <param name="strategy">对应BT资源json配置数据</param>
        /// <returns></returns>
        IAIAgent CreateAIAgent(string strategy);
    }
    
    public class AIService : IAIService
    {

        private readonly Dictionary<string, Type> _name2ExternalNodeTypes = new();

        public AIService() {
            var types = GetTypesImpInterface(typeof(IExternalNode));
            foreach (var type in types) {
                _name2ExternalNodeTypes.Add(type.Name, type);
            }
        }

        private IEnumerable<Type> GetTypesImpInterface(Type interfaceType) {
            return Assembly.GetExecutingAssembly().GetTypes().Where(x => x.GetInterfaces().Contains(interfaceType));
        }

        private IExternalNode CreateExternalNode(string typeName) {
            if (!_name2ExternalNodeTypes.ContainsKey(typeName)) {
                return null;
            }
            
            return Activator.CreateInstance(_name2ExternalNodeTypes[typeName]) as IExternalNode;
        }
        
        public IAIAgent CreateAIAgent(string strategy) {
            var btTree = (BTree) null;
            try {
                // 对于含有外部节点配置的BT数据，先替换外部节点，再重建树的连接关系
                btTree = JsonConvert.DeserializeObject<BTree>(strategy, BTDef.SerializerSettingsAuto);
                ReplaceWithExternalNodes(btTree);
            }
            catch (Exception ex) {
                BTLogger.Error($"BT data deserialize failed! \nex: {ex}");
                return null;
            }

            var aiAgent = new AIAgent(btTree);
            return aiAgent;
        }

        /// <summary>
        /// 将编辑器中ExternalNode节点需要替换为实际外部工程中的节点类型
        /// </summary>
        /// <param name="btTree"></param>
        /// <returns></returns>
        private void ReplaceWithExternalNodes(BTree btTree) {
            if (btTree == null) {
                return;
            }
            
            var nodes = btTree.BTData.Nodes;
            for (var i = 0; i < nodes.Count; i++) {
                var node = nodes[i];
                if (node is not IExternalNode externalNode) {
                    continue;
                }
                
                // 创建对应的外部节点
                var newNode = CreateExternalNode(externalNode.TypeName);
                if (newNode == null) {
                    continue;
                }
                    
                newNode.TypeName = externalNode.TypeName;
                newNode.Properties = externalNode.Properties;
                
                if (newNode is not BTNode btNode) {
                    continue;
                }
                
                btNode.Guid = node.Guid;
                btTree.BTData.ReplaceNode(i, btNode);
            }
            
            // 节点替换完毕后，重建BT节点关系
            btTree.RebuildTree();
            // 启用BT
            btTree.Enable();
        }
    }
}
