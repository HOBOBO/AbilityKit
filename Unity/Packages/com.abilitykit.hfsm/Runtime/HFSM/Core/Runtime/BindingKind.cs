#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{
    public enum BindingKind
    {
        State = 0,
        Condition = 1,
        Action = 2,
    }
}
