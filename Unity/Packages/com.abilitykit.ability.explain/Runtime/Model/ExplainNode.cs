using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainNode
    {
        public string NodeId;
        public string Kind;
        public string Title;
        public ExplainSeverity Severity;

        public List<string> SummaryLines;

        public ExplainSourceRef Source;

        public List<ExplainAction> Actions;

        public List<ExplainNode> Children;

        public bool HasChildren => Children != null && Children.Count > 0;

        public static ExplainNode Create(string title)
        {
            return new ExplainNode
            {
                NodeId = Guid.NewGuid().ToString("N"),
                Kind = null,
                Title = title,
                Severity = ExplainSeverity.None,
                SummaryLines = new List<string>(),
                Actions = new List<ExplainAction>(),
                Children = new List<ExplainNode>()
            };
        }
    }
}
