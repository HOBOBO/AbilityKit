//------------------------------------------------------------
//        File:  BTree.cs
//       Brief:  BTree
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-01
//============================================================

using System.Collections.Generic;
using System.Runtime.Serialization;
using BTCore.Runtime.Blackboards;
using BTCore.Runtime.Composites;
using BTCore.Runtime.Conditions;
using BTCore.Runtime.Decorators;

namespace BTCore.Runtime
{
    public class BTree
    {
        public BTData BTData { get; set; } = new();
        public Blackboard Blackboard { get; set; } = new();
        public BTSettings Settings { get; set; } = new();
        
        public NodeState TreeState = NodeState.Inactive;
        
        private readonly List<BTNode> _nodeList = new(); // 前序遍历方式存放所有节点
        private readonly List<int> _parentIndex = new(); // 记录每个节点的父节点的索引值
        private readonly List<List<int>> _childrenIndex = new(); // 记录每个节点的所有子节点的索引值
        private readonly List<int> _relativeChildIndex = new(); // 记录每个节点位于其父节点下的索引值
        private readonly List<int> _parentCompositeIndex = new(); // 记录每个节点其最近的组合父节点索引值
        private readonly List<List<int>> _childConditionalIndex = new(); // 记录每个节点的所有子节点中包含条件节点的索引值

        private readonly List<ConditionalReevaluate> _conditionalReevaluates = new(); // 保持所有生成的条件评估对象
        private readonly Dictionary<int, ConditionalReevaluate> _index2ConditionalReevaluate = new(); // 索引值 -> 条件评估对象

        private readonly List<Stack<int>> _runStack = new(); // 节点运行栈
        
        private int _preIndex;
        private NodeState _preState; 
        
        // TODO 其他序列化可能不会触发回调
        [OnDeserialized]
        private void OnAfterDeserialize(StreamingContext context) {
            BTData.Nodes.ForEach(node => {
                node.SetBlackboard(Blackboard);
            });
        }
        
        /// <summary>
        /// 反序列化后重建树的节点之间的连接关系
        /// </summary>
        public void RebuildTree() {
            var treeNodes = BTData.Nodes;
            treeNodes.ForEach(RebindChild);
        }
        
        private void RebindChild(BTNode node) {
            var childrenGuids = node.GetChildrenGuids();
            foreach (var guid in childrenGuids) {
                if (node is EntryNode entryNode) {
                    entryNode.SetChild(BTData.GetNodeByGuid(guid));
                    break;
                }
                if (node is ParentNode parentNode) {
                    parentNode.AddChild(BTData.GetNodeByGuid(guid));
                }
            }

            // 重新绑定节点之间关系后，才能进行初始化操作
            node.Init();
        }

        public void Enable() {
            if (BTData.EntryNode?.GetChild() == null) {
                BTLogger.Error("Entry node is null!");
                return;
            }
            
            _parentIndex.Add(-1);
            _relativeChildIndex.Add(-1);
            _parentCompositeIndex.Add(-1);
            _runStack.Add(new Stack<int>());
            // 前序遍历BT，填充结构数值
            Preorder(BTData.EntryNode.GetChild(), -1);
            // 推入根节点索引到运行栈中
            PushNode(0, 0);
        }

        public void Restart() {
            // 未删除的条件评估CompositeIndex为-1
            RemoveChildConditionalReevaluate(-1);
            // 推入根节点索引到运行栈中
            PushNode(0, 0);
        }
        
        private void Preorder(BTNode root, int parentCompositeIndex) {
            if (root == null) {
                return;
            }
            
            _nodeList.Add(root);
            var index = _nodeList.Count - 1;
            
            if (root is ParentNode parentNode) {
                _childrenIndex.Add(new List<int>());
                _childConditionalIndex.Add(new List<int>());
                var children = parentNode.GetChildren();
                for (var i = 0; i < children.Count; i++) {
                    _parentIndex.Add(index);
                    _relativeChildIndex.Add(i);
                    _childrenIndex[index].Add(_nodeList.Count);
                    if (root is Composite) {
                        parentCompositeIndex = index;
                    }
                    _parentCompositeIndex.Add(parentCompositeIndex);
                    Preorder(children[i], parentCompositeIndex);
                }
            }
            else {
                // 非ParentNode节点没有子节点
                _childrenIndex.Add(null);
                _childConditionalIndex.Add(null);
                
                // 当前节点为条件节点
                if (root is Condition) {
                    var parentIndex = _parentCompositeIndex[index];
                    if (parentIndex != -1) {
                        _childConditionalIndex[parentIndex].Add(index);
                    }
                }
            }
        }
        
        public void Update() {
            ReevaluateConditionalNode();

            for (var i  = _runStack.Count - 1; i >= 0; i--) {
                var stack = _runStack[i];
                _preIndex = -1;
                _preState = NodeState.Inactive;
            
                // 1. 运行栈前一次的状态已经是Running，则不需要再运行栈，防止一个节点再一个tick运行多次
                // 2. 并行节点的子节点的运行栈元素全部弹出时，需要删除该运行栈。这里需要加个索引越界判断
                while (_preState != NodeState.Running && i < _runStack.Count && stack.Count > 0) {
                    var index = stack.Peek();
                    // 避免同一个节点在一帧内执行多次
                    if (_preIndex == index) {
                        break;
                    }
                
                    _preIndex = index;
                    _preState = RunNode(index, i, _preState);
                }
            }
        }

        private void ReevaluateConditionalNode() {
            for (var i = _conditionalReevaluates.Count - 1; i >= 0; i--) {
                var conditionalReevaluate = _conditionalReevaluates[i];
                // 父组合节点索引设置为-1，不进行评估
                if (conditionalReevaluate.CompositeIndex == -1) {
                    continue;
                }
        
                var curState = _nodeList[conditionalReevaluate.Index].Update();
                // 条件节点状态没有发生变化
                if (curState == conditionalReevaluate.State) {
                    continue;
                }
                
                // 倒序遍历所有运行栈
                for (var j = _runStack.Count - 1; j >= 0; j--) {
                    var stack = _runStack[j];
                    if (stack.Count <= 0) {
                        continue;
                    }
                    
                    // 寻找条件节点与当前运行栈顶节点的共同父节点索引
                    var curNodeIndex = stack.Peek();
                    var commonParentIndex = FindCommonParentIndex(conditionalReevaluate.Index, curNodeIndex);
                    // 若公共父节点是条件评估CompositeIndex的子节点
                    if (IsParentNode(conditionalReevaluate.CompositeIndex, commonParentIndex)) {
                        // 先提前记录下运行栈长度，后面popNode可能删除栈元素
                        var curStackLen = _runStack.Count;
                        // 1. 将当前运行节点到commonParentIndex中间的节点执行popNode弹出运行栈
                        while (curNodeIndex != -1 && curNodeIndex != commonParentIndex && curStackLen == _runStack.Count) {
                            PopNode(curNodeIndex, j, NodeState.Failure, false);
                            curNodeIndex = _parentIndex[curNodeIndex];
                        }
                    }
                }

                // 2. 重评估成功的条件节点右侧(包括自己)的条件重评估对象属于低优先级需要删除
                for (var j = _conditionalReevaluates.Count - 1; j >= i; j--) {
                    var rightConditionalReevaluate = _conditionalReevaluates[j];
                    // 右边条件重评估对象索引需在当前条件评估的CompositeIndex下
                    if (IsParentNode(conditionalReevaluate.CompositeIndex, rightConditionalReevaluate.Index)) {
                        _index2ConditionalReevaluate.Remove(rightConditionalReevaluate.Index);
                        _conditionalReevaluates.RemoveAt(j);
                    }
                }
                
                // 3. 同一组合节点下的条件重评估对象需停止运行, 从其左边节点倒序遍历 
                if (_nodeList[_parentCompositeIndex[conditionalReevaluate.Index]] is Composite compositeNode) {
                    for (var j = i - 1; j >= 0; j--) {
                        var leftConditionalReevaluate = _conditionalReevaluates[j];
                        // 两者有同一个父组合节点
                        if (_parentCompositeIndex[leftConditionalReevaluate.Index] ==
                            _parentCompositeIndex[conditionalReevaluate.Index]) {
                            switch (compositeNode.AbortType) {
                                // 父组合节点设置为LowerPriority，则将其CompositeIndex设置为-1，不运行
                                case AbortType.LowerPriority:
                                    leftConditionalReevaluate.CompositeIndex = -1;
                                    break;
                                case AbortType.Self or AbortType.Both:
                                    leftConditionalReevaluate.Index = _parentCompositeIndex[conditionalReevaluate.Index];
                                    break;
                            }
                        }
                    }
                }
                
        
                // 4. 当前的变化的条件节点到其组合节点之间需执行OnConditionAbort
                var conditionalParentIndex = new List<int>();
                for (var j = _parentIndex[conditionalReevaluate.Index];
                     j != conditionalReevaluate.CompositeIndex;
                     j = _parentIndex[j]) {
                    conditionalParentIndex.Add(j);
                }
                conditionalParentIndex.Add(conditionalReevaluate.CompositeIndex);
                // 从上至下开始执行OnConditionalAbort，主要是为了改变组合节点的子节点索引序号。
                // 当j == 0时，传入的索引即为条件评估对象的Index
                for (var j = conditionalParentIndex.Count - 1; j >= 0; j--) {
                    var parentNode = _nodeList[conditionalParentIndex[j]] as ParentNode;
                    parentNode?.OnConditionalAbort(j != 0
                        ? _relativeChildIndex[conditionalParentIndex[j - 1]]
                        : _relativeChildIndex[conditionalReevaluate.Index]);
                }
            }
        }

        private int FindCommonParentIndex(int conditionalIndex, int curNodeIndex) {
            var hashSet = new HashSet<int>();
            while (conditionalIndex != -1) {
                hashSet.Add(conditionalIndex);
                conditionalIndex = _parentIndex[conditionalIndex];
            }

            while (!hashSet.Contains(curNodeIndex)) {
                curNodeIndex = _parentIndex[curNodeIndex];
            }

            return curNodeIndex;
        }

        private void  PushNode(int index, int stackIndex) {
            var stack = _runStack[stackIndex];
            // 防止重复索引添加到runStack中
            if (stack.Count > 0 && stack.Peek() == index) {
                return;
            }
            
            stack.Push(index);
            var node = _nodeList[index];
            node.Start();
            // BTLogger.Debug($"Push Node: {node}");
        }

        private NodeState PopNode(int index, int stackIndex, NodeState state, bool popChildren = true) {
            var stack = _runStack[stackIndex];
            stack.Pop();
            var node = _nodeList[index];
            node.Stop();
            node.State = state;
            
            // BTLogger.Debug($"Pop Node: {node}");
            
            // 有父节点为ParentNode, 需要根据子节点的状态改变其变化
            var parentIndex = _parentIndex[index];
            if (parentIndex != -1) {
                // 当前节点为条件节点，找到其最近的组合父节点
                if (node is Condition && _parentCompositeIndex[index] != -1) {
                    if (_nodeList[_parentCompositeIndex[index]] is Composite compositeNode) {
                        // 条件节点的父组合节点的AbortType不为None，需要生成条件重评估对象
                        if (compositeNode.AbortType != AbortType.None) {
                            // 父composite为LowerPriority时，条件重评估不希望此时执行，而是父composite popNode出去再执行
                            var compositeIndex = compositeNode.AbortType == AbortType.LowerPriority ? -1 : _parentCompositeIndex[index];
                            if (_index2ConditionalReevaluate.ContainsKey(index)) {
                                var conditionalReevaluate = _index2ConditionalReevaluate[index];
                                conditionalReevaluate.CompositeIndex = compositeIndex;
                                conditionalReevaluate.State = state;
                            }
                            else {
                                var conditionalReevaluate = new ConditionalReevaluate(index, state, compositeIndex);
                                _conditionalReevaluates.Add(conditionalReevaluate);
                                _index2ConditionalReevaluate.Add(index, conditionalReevaluate);
                                // BTLogger.Debug($"生成条件评估对象：{conditionalReevaluate}");
                            }
                        }
                    }
                }

                // 子节点弹出栈才会影响到部分父组合节点的状态(Sequence、Selector)
                var parentNode = _nodeList[parentIndex] as ParentNode;
                parentNode?.OnChildExecute(_relativeChildIndex[index], state);
                
                // 父节点为装饰节点，可能改变返回状态
                if (parentNode is Decorator decorator) {
                    state = decorator.Decorate(state);
                }
            }
            
            // 组合节点被弹出栈时
            if (node is Composite composite) {
                // 当前组合节点的中断类型为Self or None or 运行栈中已经没有元素了，需删除该节点管理的条件评估
                if (composite.AbortType is AbortType.Self or AbortType.None || stack.Count <= 0) {
                    RemoveChildConditionalReevaluate(index);
                }
                // 将其ConditionalReevaluate的CompositeIndex上移到更高层的组合节点
                else if (composite.AbortType is AbortType.LowerPriority or AbortType.Both) {
                    for (var i = 0; i < _childConditionalIndex[index].Count; i++) {
                        // 得到其直接条件孩子节点的索引值
                        var childConditionalIndex = _childConditionalIndex[index][i];
                        // 上移生成的条件重评估对象
                        if (_index2ConditionalReevaluate.ContainsKey(childConditionalIndex)) {
                            var conditionalReevaluate = _index2ConditionalReevaluate[childConditionalIndex];
                            conditionalReevaluate.CompositeIndex = _parentCompositeIndex[index];
                        }
                    }

                    // 将更低的孙子子节点的条件评估对象的CompositeIndex也往上移动
                    for (var i = 0; i < _conditionalReevaluates.Count; i++) {
                        var conditionalReevaluate = _conditionalReevaluates[i];
                        if (conditionalReevaluate.CompositeIndex == index) {
                            conditionalReevaluate.CompositeIndex = _parentCompositeIndex[index];
                        }
                    }
                }
            }

            // 并行节点只要有一个子节点返回failed，其他子节点都需要退出
            if (popChildren) {
                for (var i = _runStack.Count - 1; i > stackIndex; i--) {
                    var backStack = _runStack[i];
                    // 判定后面运行栈的栈顶元素跟当前index代表的元素是否为父子关系
                    if (backStack.Count > 0 && IsParentNode(index, backStack.Peek())) {
                        // 弹出backStack里面的元素
                        for (var j = backStack.Count - 1; j >= 0; j--) {
                            PopNode(backStack.Peek(), i, NodeState.Failure, false);
                        }
                    }
                }
            }
            
            // 当前运行栈还有元素
            if (stack.Count > 0) {
                return state;
            }
            
            // 当前为主运行栈
            if (stackIndex == 0) {
                if (Settings.RestartWhenComplete) {
                    Restart();
                }
            } 
            // 删除当前运行栈
            else {
                _runStack.RemoveAt(stackIndex);
                // 主动返回Running，退出Update里面的While循环
                state = NodeState.Running;
            }

            return state;
        }

        private  bool IsParentNode(int parentIndex, int childIndex) {
            for (var i = childIndex; i != -1; i = _parentIndex[i]) {
                if (i == parentIndex) {
                    return true;
                }
            }

            return false;
        }

        private void RemoveChildConditionalReevaluate(int index) {
            for (var i = _conditionalReevaluates.Count - 1; i >= 0; i--) {
                if (_conditionalReevaluates[i].CompositeIndex == index) {
                    // BTLogger.Debug($"移除条件评估对象：{_conditionalReevaluates[i]}");
                    _index2ConditionalReevaluate.Remove(_conditionalReevaluates[i].Index);
                    _conditionalReevaluates.RemoveAt(i);
                }
            }
        }

        private NodeState RunNode(int index, int stackIndex, NodeState preState) {
            PushNode(index, stackIndex);
            var node = _nodeList[index];
            var state = preState;

            if (node is ParentNode parentNode) {
                state = RunParentNode(index, stackIndex, state);
                // 并行节点的状态是由多个子节点状态共同决定的
                state = parentNode.OverrideState(state);
            }
            else {
                state = node.Update();
            }

            if (state != NodeState.Running) {
                state = PopNode(index, stackIndex, state);
            }

            return state;
        }

        private NodeState RunParentNode(int index, int stackIndex, NodeState preState) {
            if (_nodeList[index] is not ParentNode node) {
                return NodeState.Failure;
            }

            // 防止running状态下的并行节点再次进入运行
            if (node.CanRunParallel() && node.OverrideState(NodeState.Running) == NodeState.Running) {
                return preState;
            }
            
            var childState = NodeState.Inactive;
            var preIndex = -1;
            while (node.CanExecute() && (childState != NodeState.Running || node.CanRunParallel())) {
                // 并行节点进入可能会改变索引值，这里先记录下
                var childIndex = node.Index;
                // 并行子节点进入，需增加运行栈
                if (node.CanRunParallel()) {
                    _runStack.Add(new Stack<int>());
                    stackIndex = _runStack.Count - 1;
                    node.OnChildStart();
                }

                // 防止可以重复运行的装饰节点(Repeater、untilSuccess等)一直进入While运行
                var curIndex = childIndex;
                if (curIndex == preIndex) {
                    preState = NodeState.Running;
                    break;
                }

                preIndex = curIndex;
                childState = preState = RunNode(_childrenIndex[index][childIndex], stackIndex, preState);
            }
            
            // 当持续性的子节点运行完成后，弹出栈，可能会导致parentNode的CanExecute方法返回false
            // 而childState默认为Inactive，因此需要返回记录上一次运行的子节点状态
            return preState;
        }
    }

    public class ConditionalReevaluate
    {
        public int Index;
        public NodeState State;
        public int CompositeIndex;

        public ConditionalReevaluate(int index, NodeState state, int compositeIndex) {
            Index = index;
            State = state;
            CompositeIndex = compositeIndex;
        }

        public override string ToString() {
            return $"Conditional reevaluate, index:{Index} state:{State} composite index:{CompositeIndex}";
        }
    }
}
