#nullable enable
using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Definition
{
    public sealed class ValidationIssue
    {
        public ValidationIssue(string code, string path, string message)
        {
            Code = code ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} at {Path}: {Message}";
    }
}
