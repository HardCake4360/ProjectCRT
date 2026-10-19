using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NoSleep
{
    public enum EventParameterType { Int, Bool, String }

    [Serializable]
    public sealed class EventParameter
    {
        public string name;
        public EventParameterType type;
        public int intValue;
        public bool boolValue;
        public string stringValue;
    }

    [Serializable]
    public sealed class EventArgument
    {
        public string name;
        public EventParameterType type;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public string fromParameter;
    }

    [Serializable]
    public sealed class EventLocalCondition
    {
        public string parameter;
        public EventParameterType type;
        public NumberComparison comparison = NumberComparison.Equal;
        public int intValue;
        public bool boolValue = true;
        public string stringValue;

        internal bool Evaluate(Dictionary<string, EventValue> locals)
        {
            if (!locals.TryGetValue(parameter ?? "", out var value) || value.Type != type)
                throw new ArgumentException("Local condition parameter is missing or has another type: " + parameter);
            switch (type)
            {
                case EventParameterType.Int: return StateCondition.Compare(value.Int, intValue, comparison);
                case EventParameterType.Bool: return value.Bool == boolValue;
                case EventParameterType.String: return string.Equals(value.String, stringValue ?? "", StringComparison.Ordinal);
                default: throw new ArgumentException("Unknown local condition type.");
            }
        }
    }

    public readonly struct EventValue
    {
        public EventParameterType Type { get; }
        public int Int { get; }
        public bool Bool { get; }
        public string String { get; }
        public EventValue(int value) { Type = EventParameterType.Int; Int = value; Bool = false; String = null; }
        public EventValue(bool value) { Type = EventParameterType.Bool; Int = 0; Bool = value; String = null; }
        public EventValue(string value) { Type = EventParameterType.String; Int = 0; Bool = false; String = value ?? ""; }
        public override string ToString() => Type == EventParameterType.Int ? Int.ToString(System.Globalization.CultureInfo.InvariantCulture) :
            Type == EventParameterType.Bool ? (Bool ? "true" : "false") : String;
    }

    [CreateAssetMenu(menuName = "No Sleep/Common Event", fileName = "CommonEvent")]
    public sealed class CommonEventDefinition : ScriptableObject
    {
        public string commonId;
        public string displayName = "Common Event";
        public List<EventParameter> parameters = new List<EventParameter>();
        public List<EventCommand> commands = new List<EventCommand>();
        public bool EnsureStableIds()
        {
            bool changed = false;
            if (string.IsNullOrWhiteSpace(commonId)) { commonId = Guid.NewGuid().ToString("N"); changed = true; }
            return EventProgram.EnsureCommandIds(commands) || changed;
        }
        public List<string> ValidateIdentity()
        {
            var errors = new List<string>();
            if (!EventProgram.ValidIdentity(commonId)) errors.Add("Common event ID is missing or invalid.");
            EventProgram.ValidateCommandIdentities(commands, errors, "Common event");
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (parameters == null) errors.Add("Common event parameters are missing.");
            else foreach (var parameter in parameters)
                if (parameter == null || !EventParameters.ValidName(parameter.name) || !names.Add(parameter.name) || !Enum.IsDefined(typeof(EventParameterType), parameter.type))
                    errors.Add("Common event has an invalid or duplicate parameter.");
            return errors;
        }
        public bool ValidateIdentity(out string error)
        { var errors = ValidateIdentity(); error = string.Join("\n", errors); return errors.Count == 0; }
    }

    internal static class EventParameters
    {
        static readonly Regex Tokens = new Regex(@"\{param:([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant);
        public static bool ValidName(string name) => !string.IsNullOrEmpty(name) && Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");
        public static string Resolve(string text, Dictionary<string, EventValue> locals)
        {
            return Tokens.Replace(text ?? "", match => locals.TryGetValue(match.Groups[1].Value, out var value) ? value.ToString() :
                throw new ArgumentException("Text parameter is missing: " + match.Groups[1].Value));
        }
        public static IEnumerable<string> Referenced(string text)
        { foreach (Match match in Tokens.Matches(text ?? "")) yield return match.Groups[1].Value; }
        public static Dictionary<string, EventValue> Bind(List<EventParameter> parameters, List<EventArgument> arguments, Dictionary<string, EventValue> caller)
        {
            var values = new Dictionary<string, EventValue>(StringComparer.Ordinal);
            foreach (var parameter in parameters) values.Add(parameter.name, Value(parameter.type, parameter.intValue, parameter.boolValue, parameter.stringValue));
            var provided = new HashSet<string>(StringComparer.Ordinal);
            if (arguments != null) foreach (var argument in arguments)
            {
                if (argument == null || !provided.Add(argument.name ?? "") || !values.TryGetValue(argument.name ?? "", out var expected))
                    throw new ArgumentException("Unknown or duplicate common argument.");
                EventValue value = string.IsNullOrEmpty(argument.fromParameter)
                    ? Value(argument.type, argument.intValue, argument.boolValue, argument.type == EventParameterType.String ? Resolve(argument.stringValue, caller) : argument.stringValue)
                    : caller.TryGetValue(argument.fromParameter, out var inherited) ? inherited : throw new ArgumentException("Caller parameter is missing: " + argument.fromParameter);
                if (value.Type != expected.Type) throw new ArgumentException("Common argument has another type: " + argument.name);
                values[argument.name] = value;
            }
            return values;
        }
        static EventValue Value(EventParameterType type, int number, bool boolean, string text)
        {
            switch (type)
            {
                case EventParameterType.Int: return new EventValue(number);
                case EventParameterType.Bool: return new EventValue(boolean);
                case EventParameterType.String: return new EventValue(text);
                default: throw new ArgumentException("Unknown parameter type.");
            }
        }
    }
}
