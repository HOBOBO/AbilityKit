using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public sealed class ParameterProgram
    {
        public ParameterProgram(string name, ParameterValueType parameterType, object defaultValue)
        {
            Name = name ?? string.Empty;
            ParameterType = parameterType;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public ParameterValueType ParameterType { get; }
        public object DefaultValue { get; }
    }
}
