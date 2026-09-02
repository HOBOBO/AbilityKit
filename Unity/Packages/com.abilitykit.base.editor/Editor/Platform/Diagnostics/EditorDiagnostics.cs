#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AbilityKit.Editor.Platform.Diagnostics
{
    public enum EditorDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class EditorDiagnostic
    {
        public EditorDiagnostic(
            string code,
            EditorDiagnosticSeverity severity,
            string message,
            string path = null,
            UnityEngine.Object target = null,
            Action locate = null,
            Action fix = null)
        {
            Code = RequireValue(code, nameof(code));
            Severity = severity;
            Message = RequireValue(message, nameof(message));
            Path = path ?? string.Empty;
            Target = target;
            Locate = locate;
            Fix = fix;
        }

        public string Code { get; }
        public EditorDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public string Path { get; }
        public UnityEngine.Object Target { get; }
        public Action Locate { get; }
        public Action Fix { get; }
        public bool CanLocate => Target != null || Locate != null;
        public bool CanFix => Fix != null;

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }
    }

    public sealed class EditorDiagnosticCollection
    {
        private readonly List<EditorDiagnostic> _items = new List<EditorDiagnostic>();

        public event Action Changed;

        public IReadOnlyList<EditorDiagnostic> Items => _items;
        public int ErrorCount => _items.Count(item => item.Severity == EditorDiagnosticSeverity.Error);
        public int WarningCount => _items.Count(item => item.Severity == EditorDiagnosticSeverity.Warning);
        public bool HasErrors => ErrorCount > 0;

        public void Add(EditorDiagnostic diagnostic)
        {
            _items.Add(diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
            Changed?.Invoke();
        }

        public void AddRange(IEnumerable<EditorDiagnostic> diagnostics)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            var changed = false;
            foreach (var diagnostic in diagnostics)
            {
                _items.Add(diagnostic ?? throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(diagnostics)));
                changed = true;
            }

            if (changed) Changed?.Invoke();
        }

        public void Replace(IEnumerable<EditorDiagnostic> diagnostics)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            var next = diagnostics.ToArray();
            if (next.Any(item => item == null))
            {
                throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(diagnostics));
            }

            _items.Clear();
            _items.AddRange(next);
            Changed?.Invoke();
        }

        public IReadOnlyList<EditorDiagnostic> Filter(
            EditorDiagnosticSeverity minimumSeverity,
            string search = null)
        {
            var query = _items.Where(item => item.Severity >= minimumSeverity);
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item => Contains(item.Code, search)
                                                  || Contains(item.Path, search)
                                                  || Contains(item.Message, search));
            }

            return query.ToArray();
        }

        public void Clear()
        {
            if (_items.Count == 0) return;
            _items.Clear();
            Changed?.Invoke();
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
