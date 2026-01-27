using Emilia.Kit.Editor;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Emilia.Variables.Editor
{
    [CreateAssetMenu(menuName = "Emilia/Variable/VariableGenerateSetting", fileName = "VariableGenerateSetting")]
    public class VariableGenerateSetting : ScriptableObject
    {
        public const string CreateIdentifier = "#CREATE#";
        public const string CreateFromPoolIdentifier = "#CREATE_FROM_POOL#";
        public const string ConvertIdentifier = "#CONVERT#";
        public const string EqualIdentifier = "#EQUAL#";
        public const string NotEqualIdentifier = "#NOT_EQUAL#";
        public const string GreaterThanIdentifier = "#GREATER_THAN#";
        public const string LessThanIdentifier = "#LESS_THAN#";
        public const string GreaterThanOrEqualIdentifier = "#GREATER_THAN_OR_EQUAL#";
        public const string LessThanOrEqualIdentifier = "#LESS_THAN_OR_EQUAL#";
        public const string AddIdentifier = "#ADD#";
        public const string SubtractIdentifier = "#SUBTRACT#";
        public const string MultiplyIdentifier = "#MULTIPLY#";
        public const string DivideIdentifier = "#DIVIDE#";

        public const string FileName = "VariableUtility";

        public const string TemplateText = @"
//===================================================
// 此文件由工具自动生成，请勿直接修改
//===================================================

using System;
using System.Collections.Generic;

namespace Emilia.Variables
{
    public static partial class VariableUtility
    {
        private static Dictionary<Type, Func<Variable>> variableCreate = new Dictionary<Type, Func<Variable>>() {
#CREATE#
        };
        
        private static Dictionary<Type, Func<Variable>> variableCreateFromPool = new Dictionary<Type, Func<Variable>>() {
#CREATE_FROM_POOL#
        };

        private static Dictionary<ConvertMatching, Func<Variable, Variable>> variableConvert = new Dictionary<ConvertMatching, Func<Variable, Variable>>() {
#CONVERT#
        };

        private static Dictionary<Type, Func<Variable, Variable, bool>> variableEqual = new Dictionary<Type, Func<Variable, Variable, bool>>() {
#EQUAL#
        };
        
        private static Dictionary<Type, Func<Variable, Variable, bool>> variableNotEqual = new Dictionary<Type, Func<Variable, Variable, bool>>() {
#NOT_EQUAL#    
        };
        
        private static Dictionary<Type, Func<Variable, Variable, bool>> variableGreaterOrEqual = new Dictionary<Type, Func<Variable, Variable, bool>>() {
#GREATER_THAN_OR_EQUAL#
        };
        
        private static Dictionary<Type, Func<Variable, Variable, bool>> variableGreater = new Dictionary<Type, Func<Variable, Variable, bool>>() {
#GREATER_THAN#
        };
       
        private static Dictionary<Type, Func<Variable, Variable, bool>> variableSmallerOrEqual = new Dictionary<Type, Func<Variable, Variable, bool>>() {
#LESS_THAN_OR_EQUAL#
        };
        
        private static Dictionary<Type, Func<Variable, Variable, bool>> variableSmaller = new Dictionary<Type, Func<Variable, Variable, bool>>() {
#LESS_THAN#
        };

        private static Dictionary<Type, Func<Variable, Variable, Variable>> variableAdd = new Dictionary<Type, Func<Variable, Variable, Variable>>() {
#ADD#
        };

        private static Dictionary<Type, Func<Variable, Variable, Variable>> variableSubtract = new Dictionary<Type, Func<Variable, Variable, Variable>>() {
#SUBTRACT#
        };

        private static Dictionary<Type, Func<Variable, Variable, Variable>> variableMultiply = new Dictionary<Type, Func<Variable, Variable, Variable>>() {
#MULTIPLY#
        };

        private static Dictionary<Type, Func<Variable, Variable, Variable>> variableDivide = new Dictionary<Type, Func<Variable, Variable, Variable>>() {
#DIVIDE#
        };
    }
}";

        [LabelText("生成文件路径")]
        public FolderAsset filePath;

        [Button("生成")]
        public void Generate()
        {
            VariableUtilityGenerate.Generate(this);
        }
    }
}