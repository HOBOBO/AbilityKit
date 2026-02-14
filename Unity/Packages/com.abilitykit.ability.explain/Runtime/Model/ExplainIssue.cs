using System;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainIssue
    {
        public string IssueId;

        public ExplainSeverity Severity;

        public string Title;

        public string Message;

        public string NodeId;

        public ExplainSourceRef Source;

        public NavigationTarget NavigateTo;

        public static ExplainIssue Create(ExplainSeverity severity, string title, string message = null)
        {
            return new ExplainIssue
            {
                IssueId = Guid.NewGuid().ToString("N"),
                Severity = severity,
                Title = title,
                Message = message
            };
        }
    }
}
