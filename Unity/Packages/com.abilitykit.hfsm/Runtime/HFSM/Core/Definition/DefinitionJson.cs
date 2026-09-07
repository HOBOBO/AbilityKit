#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace AbilityKit.HFSM.Definition
{
    /// <summary>
    /// Canonical JSON codec for runtime definitions. It never persists CLR type information and
    /// rejects schema drift instead of silently accepting misspelled or duplicate properties.
    /// </summary>
    public static class DefinitionJson
    {
        public static string Save(StateMachineDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            DefinitionValidator.ValidateOrThrow(definition);

            var output = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
            using (var writer = new JsonTextWriter(output)
            {
                Formatting = Formatting.Indented,
                Indentation = 2,
                IndentChar = ' ',
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
            })
            {
                writer.WriteStartObject();
                WriteValue(writer, "formatVersion", definition.FormatVersion);
                WriteValue(writer, "definitionId", definition.DefinitionId);
                WriteValue(writer, "rootMachineId", definition.RootMachineId);
                writer.WritePropertyName("machines");
                writer.WriteStartArray();
                foreach (var machine in definition.Machines.OrderBy(item => item.Id, StringComparer.Ordinal))
                {
                    WriteMachine(writer, machine);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            return output.ToString();
        }

        public static StateMachineDefinition Load(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (string.IsNullOrWhiteSpace(json))
                throw new DefinitionJsonException("$", "HFSM definition JSON is empty.");

            JToken token;
            try
            {
                token = JToken.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Ignore,
                    LineInfoHandling = LineInfoHandling.Load,
                });
            }
            catch (JsonException exception)
            {
                throw new DefinitionJsonException("$", exception.Message, exception);
            }

            var root = RequireObject(token, "$", "HFSM definition must be an object.");
            EnsureProperties(root, "$", "formatVersion", "definitionId", "rootMachineId", "machines");

            var definition = new StateMachineDefinition
            {
                FormatVersion = ReadInt32(root, "formatVersion", "$"),
                DefinitionId = ReadString(root, "definitionId", "$"),
                RootMachineId = ReadString(root, "rootMachineId", "$"),
                Machines = new List<MachineDefinition>(),
            };

            var machines = ReadArray(root, "machines", "$");
            for (var index = 0; index < machines.Count; index++)
            {
                definition.Machines.Add(ReadMachine(machines[index], $"$.machines[{index}]"));
            }

            DefinitionValidator.ValidateOrThrow(definition);
            return definition;
        }

        private static void WriteMachine(JsonWriter writer, MachineDefinition machine)
        {
            writer.WriteStartObject();
            WriteValue(writer, "id", machine.Id);
            WriteValue(writer, "initialStateId", machine.InitialStateId);
            WriteValue(writer, "rememberLastState", machine.RememberLastState);
            writer.WritePropertyName("states");
            writer.WriteStartArray();
            foreach (var state in machine.States.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                WriteValue(writer, "id", state.Id);
                WriteValue(writer, "behaviorKey", state.BehaviorKey);
                WriteValue(writer, "childMachineId", state.ChildMachineId);
                WriteValue(writer, "requiresExitApproval", state.RequiresExitApproval);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("transitions");
            writer.WriteStartArray();
            foreach (var transition in machine.Transitions.OrderBy(item => item, Comparer<TransitionDefinition>.Create(StateMachineDefinition.CompareTransitions)))
            {
                writer.WriteStartObject();
                WriteValue(writer, "id", transition.Id);
                WriteValue(writer, "fromAnyState", transition.FromAnyState);
                WriteValue(writer, "fromStateId", transition.FromStateId);
                WriteValue(writer, "toStateId", transition.ToStateId);
                WriteValue(writer, "triggerId", transition.TriggerId);
                WriteValue(writer, "conditionKey", transition.ConditionKey);
                WriteValue(writer, "actionKey", transition.ActionKey);
                WriteValue(writer, "priority", transition.Priority);
                WriteValue(writer, "forceImmediate", transition.ForceImmediate);
                WriteValue(writer, "minimumActiveDurationRaw", transition.MinimumActiveDurationRaw);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static MachineDefinition ReadMachine(JToken token, string path)
        {
            var item = RequireObject(token, path, "Machine must be an object.");
            EnsureProperties(item, path, "id", "initialStateId", "rememberLastState", "states", "transitions");
            var machine = new MachineDefinition
            {
                Id = ReadString(item, "id", path),
                InitialStateId = ReadString(item, "initialStateId", path),
                RememberLastState = ReadBoolean(item, "rememberLastState", path),
                States = new List<StateDefinition>(),
                Transitions = new List<TransitionDefinition>(),
            };

            var states = ReadArray(item, "states", path);
            for (var index = 0; index < states.Count; index++)
            {
                machine.States.Add(ReadState(states[index], $"{path}.states[{index}]"));
            }

            var transitions = ReadArray(item, "transitions", path);
            for (var index = 0; index < transitions.Count; index++)
            {
                machine.Transitions.Add(ReadTransition(transitions[index], $"{path}.transitions[{index}]"));
            }

            return machine;
        }

        private static StateDefinition ReadState(JToken token, string path)
        {
            var item = RequireObject(token, path, "State must be an object.");
            EnsureProperties(item, path, "id", "behaviorKey", "childMachineId", "requiresExitApproval");
            return new StateDefinition
            {
                Id = ReadString(item, "id", path),
                BehaviorKey = ReadString(item, "behaviorKey", path),
                ChildMachineId = ReadString(item, "childMachineId", path),
                RequiresExitApproval = ReadBoolean(item, "requiresExitApproval", path),
            };
        }

        private static TransitionDefinition ReadTransition(JToken token, string path)
        {
            var item = RequireObject(token, path, "Transition must be an object.");
            EnsureProperties(
                item,
                path,
                "id",
                "fromAnyState",
                "fromStateId",
                "toStateId",
                "triggerId",
                "conditionKey",
                "actionKey",
                "priority",
                "forceImmediate",
                "minimumActiveDurationRaw");
            return new TransitionDefinition
            {
                Id = ReadString(item, "id", path),
                FromAnyState = ReadBoolean(item, "fromAnyState", path),
                FromStateId = ReadString(item, "fromStateId", path),
                ToStateId = ReadString(item, "toStateId", path),
                TriggerId = ReadString(item, "triggerId", path),
                ConditionKey = ReadString(item, "conditionKey", path),
                ActionKey = ReadString(item, "actionKey", path),
                Priority = ReadInt32(item, "priority", path),
                ForceImmediate = ReadBoolean(item, "forceImmediate", path),
                MinimumActiveDurationRaw = ReadInt64(item, "minimumActiveDurationRaw", path),
            };
        }

        private static void EnsureProperties(JObject item, string path, params string[] allowedNames)
        {
            var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (var property in item.Properties())
            {
                if (!allowed.Contains(property.Name))
                    throw new DefinitionJsonException($"{path}.{property.Name}", "Unknown property.");
            }

            for (var index = 0; index < allowedNames.Length; index++)
            {
                if (item.Property(allowedNames[index], StringComparison.Ordinal) == null)
                    throw new DefinitionJsonException($"{path}.{allowedNames[index]}", "Required property is missing.");
            }
        }

        private static JObject RequireObject(JToken token, string path, string message)
        {
            if (token is JObject item) return item;
            throw new DefinitionJsonException(path, message);
        }

        private static JArray ReadArray(JObject item, string name, string path)
        {
            var token = item[name];
            if (token is JArray array) return array;
            throw new DefinitionJsonException($"{path}.{name}", "Expected an array.");
        }

        private static string ReadString(JObject item, string name, string path)
        {
            var token = item[name];
            if (token?.Type == JTokenType.String) return token.Value<string>() ?? string.Empty;
            throw new DefinitionJsonException($"{path}.{name}", "Expected a string.");
        }

        private static bool ReadBoolean(JObject item, string name, string path)
        {
            var token = item[name];
            if (token?.Type == JTokenType.Boolean) return token.Value<bool>();
            throw new DefinitionJsonException($"{path}.{name}", "Expected a boolean.");
        }

        private static int ReadInt32(JObject item, string name, string path)
        {
            var value = ReadInt64(item, name, path);
            if (value < int.MinValue || value > int.MaxValue)
                throw new DefinitionJsonException($"{path}.{name}", "Integer is outside Int32 range.");
            return (int)value;
        }

        private static long ReadInt64(JObject item, string name, string path)
        {
            var token = item[name];
            if (token?.Type == JTokenType.Integer)
            {
                try
                {
                    return token.Value<long>();
                }
                catch (Exception exception) when (exception is OverflowException || exception is FormatException)
                {
                    throw new DefinitionJsonException($"{path}.{name}", "Integer is outside Int64 range.", exception);
                }
            }

            throw new DefinitionJsonException($"{path}.{name}", "Expected an integer.");
        }

        private static void WriteValue(JsonWriter writer, string name, string value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value ?? string.Empty);
        }

        private static void WriteValue(JsonWriter writer, string name, bool value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private static void WriteValue(JsonWriter writer, string name, int value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private static void WriteValue(JsonWriter writer, string name, long value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }
    }
}
