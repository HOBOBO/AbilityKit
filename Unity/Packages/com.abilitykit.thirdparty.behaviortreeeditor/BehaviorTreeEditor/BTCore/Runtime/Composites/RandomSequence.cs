//------------------------------------------------------------
//        File:  RandomSequence.cs
//       Brief:  RandomSequence
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-07-03
//============================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace BTCore.Runtime.Composites
{
    public class RandomSequence : Composite
    {
        public int Seed { get; set; }
        public bool UseSeed { get; set; }

        public override int Index => _executionOrder.Count > 0 ? _executionOrder.Peek() : 0;

        private Random _random;
        private readonly List<int> _childrenIndexes = new();
        private readonly Stack<int> _executionOrder = new();
        
        protected override void OnInit() {
            _random = UseSeed ? new Random(Seed) : new Random();
            
            _childrenIndexes.Clear();
            for (var i = 0; i < Children.Count; i++) {
                _childrenIndexes.Add(i);
            }
        }

        protected override void OnStart() {
            base.OnStart();
            Shuffle();
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            if (_executionOrder.Count > 0) {
                _executionOrder.Pop();
                State = NodeState.Running;
                return;
            }

            State = nodeState;
        }

        public override bool CanExecute() {
            return _executionOrder.Count > 0 && State != NodeState.Failure;
        }

        public override void OnConditionalAbort(int index) {
            Shuffle();
            State = NodeState.Running;
        }

        private void Shuffle() {
            _executionOrder.Clear();
            for (var i = _childrenIndexes.Count; i > 0; i--) {
                var j = _random.Next(0, i);
                var index = _childrenIndexes[j];
                _executionOrder.Push(index);
                _childrenIndexes[j] = _childrenIndexes[i - 1];
                _childrenIndexes[i - 1] = index;
            }

            foreach (var i in _executionOrder) {
                BTLogger.Debug($"随机序号：{i}");
            }
        }
    }
}