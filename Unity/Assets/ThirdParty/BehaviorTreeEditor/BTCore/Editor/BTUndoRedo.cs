//------------------------------------------------------------
//        File:  BTUndoRedo.cs
//       Brief:  BTUndoRedo
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-15
//============================================================

using System;
using System.Collections.Generic;
using BTCore.Runtime;
using Newtonsoft.Json;
using UnityEngine;

namespace BTCore.Editor
{
    public interface ICommand
    {
        void Execute();
        
        void Undo();
    }

    public class NodeDataCommand : ICommand
    {
        private readonly string _oldData;
        private readonly string _newData;
        private readonly BTView _btView;
        
        public NodeDataCommand(BTView btView, string oldData, string newData) {
            _btView = btView;
            _oldData = oldData;
            _newData = newData;
        }
        
        public void Execute() {
            try {
                var treeNodeData = JsonConvert.DeserializeObject<BTData>(_newData, BTDef.SerializerSettingsAuto);
                _btView.ImportData(treeNodeData);
            }
            catch (Exception e) {
                Debug.LogError($"Node data execute failed, ex: {e}");
            }
        }

        public void Undo() {
            try {
                var treeNodeData = JsonConvert.DeserializeObject<BTData>(_oldData, BTDef.SerializerSettingsAuto);
                _btView.ImportData(treeNodeData);
            }
            catch (Exception e) {
                Debug.LogError($"Node data undo failed, ex: {e}");
                return;
            }
        }
    }
    
    public class BTUndoRedo
    {
        private readonly Stack<ICommand> _undoStack = new Stack<ICommand>();
        private readonly Stack<ICommand> _redoStack = new Stack<ICommand>();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void AddCommand(ICommand command) {
            _undoStack.Push(command);
            _redoStack.Clear();
        }
        
        public void Undo() {
            if (_undoStack.Count <= 0) {
                return;
            }

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
        }

        public void Redo() {
            if (_redoStack.Count <= 0) {
                return;
            }

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);
        }

        public void Clear() {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
