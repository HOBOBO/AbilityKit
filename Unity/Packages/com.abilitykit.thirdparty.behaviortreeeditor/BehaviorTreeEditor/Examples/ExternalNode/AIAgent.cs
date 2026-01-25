//------------------------------------------------------------
//        File:  AIAgent.cs
//       Brief:  AIAgent
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using BTCore.Runtime;

namespace Examples.ExternalNode
{
    public interface IAIAgent
    {
        void Tick();
    }
    
    public class AIAgent : IAIAgent
    {
        private readonly BTree _btTree;
        
        public AIAgent(BTree btTree) {
            _btTree = btTree;
        }
        
        public void Tick() {
            _btTree?.Update();
        }
    }
}
