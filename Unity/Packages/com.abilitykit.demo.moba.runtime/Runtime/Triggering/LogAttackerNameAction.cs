using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Triggering
{
    public sealed class LogAttackerNameAction : ITriggerAction
    {
        private readonly string _format;

        public LogAttackerNameAction(string format)
        {
            _format = format;
        }

        public static LogAttackerNameAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new LogAttackerNameAction("{0}发动了攻击");

            if (args.TryGetValue("format", out var fmtObj) && fmtObj is string fmt && !string.IsNullOrEmpty(fmt))
            {
                return new LogAttackerNameAction(fmt);
            }

            return new LogAttackerNameAction("{0}发动了攻击");
        }

        public void Execute(TriggerContext context)
        {
            var attackerName = "Unknown";
            if (context.Source is GameObject go) attackerName = go.name;
            else if (context.Source != null) attackerName = context.Source.ToString();

            Debug.Log(string.Format(_format, attackerName));
        }
    }
}
