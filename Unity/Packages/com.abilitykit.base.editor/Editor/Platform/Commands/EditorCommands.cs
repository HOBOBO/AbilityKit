#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Editor.Platform.Commands
{
    public sealed class EditorCommandContext
    {
        public EditorCommandContext(object host = null, object selection = null)
        {
            Host = host;
            Selection = selection;
        }

        public object Host { get; }
        public object Selection { get; }
    }

    public sealed class EditorCommand
    {
        private readonly Action<EditorCommandContext> _execute;
        private readonly Func<EditorCommandContext, bool> _canExecute;
        private readonly Func<EditorCommandContext, bool> _isChecked;

        public EditorCommand(
            string id,
            string labelKey,
            Action<EditorCommandContext> execute,
            string tooltipKey = null,
            int order = 0,
            Func<EditorCommandContext, bool> canExecute = null,
            Func<EditorCommandContext, bool> isChecked = null)
        {
            Id = RequireValue(id, nameof(id));
            LabelKey = RequireValue(labelKey, nameof(labelKey));
            TooltipKey = tooltipKey ?? string.Empty;
            Order = order;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _isChecked = isChecked;
        }

        public string Id { get; }
        public string LabelKey { get; }
        public string TooltipKey { get; }
        public int Order { get; }

        public bool CanExecute(EditorCommandContext context = null) => _canExecute?.Invoke(context) ?? true;
        public bool IsChecked(EditorCommandContext context = null) => _isChecked?.Invoke(context) ?? false;

        public bool TryExecute(EditorCommandContext context = null)
        {
            if (!CanExecute(context)) return false;
            _execute(context);
            return true;
        }

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }
    }

    public sealed class EditorCommandRegistry
    {
        private readonly Dictionary<string, EditorCommand> _commands =
            new Dictionary<string, EditorCommand>(StringComparer.Ordinal);

        public event Action CommandsChanged;

        public IReadOnlyList<EditorCommand> Commands => _commands.Values
            .OrderBy(command => command.Order)
            .ThenBy(command => command.Id, StringComparer.Ordinal)
            .ToArray();

        public IDisposable Register(EditorCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (_commands.ContainsKey(command.Id))
            {
                throw new InvalidOperationException($"Editor command '{command.Id}' is already registered.");
            }

            _commands.Add(command.Id, command);
            CommandsChanged?.Invoke();
            return new Registration(this, command);
        }

        public bool TryGet(string id, out EditorCommand command)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                command = null;
                return false;
            }

            return _commands.TryGetValue(id, out command);
        }

        public bool Execute(string id, EditorCommandContext context = null)
        {
            return TryGet(id, out var command) && command.TryExecute(context);
        }

        public void Clear()
        {
            if (_commands.Count == 0) return;
            _commands.Clear();
            CommandsChanged?.Invoke();
        }

        private void Unregister(EditorCommand command)
        {
            if (!_commands.TryGetValue(command.Id, out var current) || !ReferenceEquals(current, command)) return;
            _commands.Remove(command.Id);
            CommandsChanged?.Invoke();
        }

        private sealed class Registration : IDisposable
        {
            private EditorCommandRegistry _owner;
            private readonly EditorCommand _command;

            public Registration(EditorCommandRegistry owner, EditorCommand command)
            {
                _owner = owner;
                _command = command;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null) return;
                _owner = null;
                owner.Unregister(_command);
            }
        }
    }
}
#endif
