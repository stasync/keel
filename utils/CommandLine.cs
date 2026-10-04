using Keel.Utils.Debug;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Keel.Utils
{
    public static class CommandLine
    {
        public static readonly string Raw;
        private static readonly Dictionary<string, List<string>> s_commandLineArguments;

        static CommandLine()
        {
            Raw = Environment.CommandLine;
            s_commandLineArguments = ParseCommandLineArguments(Raw);

            // Looking for config files.
            // File name - argument name.
            // File content - argument value (parameter).
            // ReSharper disable once NullableWarningSuppressionIsUsed
            foreach (var configFilePath in System.IO.Directory.GetFiles(path: DomainUtils.DomainDirectory!, searchPattern: "*.cmdparam"))
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(configFilePath).Trim();
                var value = System.IO.File.ReadAllText(configFilePath).Trim();

                if (!s_commandLineArguments.TryGetValue(fileName, out var arguments))
                    s_commandLineArguments.Add(fileName, arguments = new List<string>());

                arguments.Add(value);
            }
        }

        public static T CreateInstance<T>() =>
            CreateFromCommandLineData<T>(s_commandLineArguments);

        public static T CreateFromCommandLineData<T>(Dictionary<string, List<string>> commandLineArguments)
        {
            var instance = Activator.CreateInstance<T>();
            InjectCommandLineData(ref instance, commandLineArguments);
            return instance;
        }

        public static void InjectTo<T>(ref T target) where T : struct =>
            InjectCommandLineData(ref target, s_commandLineArguments);

        public static void InjectTo<T>(T target) where T : class =>
            InjectCommandLineData(ref target, s_commandLineArguments);

        public static void InjectCommandLineData<T>(ref T target, Dictionary<string, List<string>> commandLineArguments)
        {
            var objectToInject = target as object;

            var objectType = objectToInject.GetType();
            foreach (var field in objectType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (Attribute.GetCustomAttribute(field, typeof(InjectCommandArgumentAttribute)) is not InjectCommandArgumentAttribute argumentAttribute)
                    continue;

                var argumentToInject =
                    string.IsNullOrWhiteSpace(argumentAttribute.ArgumentName) ? field.Name : argumentAttribute.ArgumentName;

                if (!commandLineArguments.TryGetValue(argumentToInject, out var parameterList))
                    continue;

                // Field is an array.
                // TODO: Support List<T>().
                if (field.FieldType.IsArray)
                {
                    var typeToParse = field.FieldType.GetElementType();

                    // ReSharper disable once NullableWarningSuppressionIsUsed
                    if (!typeToParse!.IsPrimitive)
                    {
                        Logger.LogError($"Field '{field.FieldType.FullName}:{field.Name}' is a type of array, but it's only primitive types are supported to be parsed for arrays.");
                        continue;
                    }

                    // Crate a new array.
                    var arrayInstance = (Array)Activator.CreateInstance(typeToParse.MakeArrayType(), parameterList.Count);

                    // Fill up new array - parsing values one by one.
                    for (int parameterIndex = 0; parameterIndex < parameterList.Count; parameterIndex++)
                    {
                        var parameter = parameterList[parameterIndex];
                        if (!TryParseValue(typeToParse, parameter, out var parsedBoxedArgument))
                        {
                            Logger.LogError($"Error while injecting '{parameter}' to field '{field.FieldType.FullName}:{field.Name}'.");
                            continue;
                        }

                        // Update new array element - setting a parsed value.
                        arrayInstance!.SetValue(parsedBoxedArgument, parameterIndex);
                    }

                    // Assign array to the field.
                    field.SetValue(objectToInject, arrayInstance);
                }
                // Field is a normal value or nullable.
                else
                {
                    // Get an actual type to support nullables.
                    var targetValueType = field.FieldType;
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        targetValueType = Nullable.GetUnderlyingType(field.FieldType);

                    // Sanity check.
                    if (targetValueType == null)
                    {
                        Logger.LogError($"Error while injecting to field '{field.FieldType.FullName}:{field.Name}' as it's unable to to determine underlying type.");
                        continue;
                    }

                    // Warning - array expected (?).
                    if (parameterList.Count > 1)
                        Logger.LogWarning($"Array is expected for '{targetValueType.FullName}:{field.Name}', as there are '{parameterList.Count}' corresponded arguments.");

                    // Just grab the first parameter in this case.
                    var parameter = parameterList[0];

                    if (!TryParseValue(targetValueType, parameter, out var parsedBoxedArgument))
                    {
                        Logger.LogError($"Error while injecting '{parameter}' to field '{targetValueType.FullName}:{field.Name}'.");
                        continue;
                    }

                    field.SetValue(objectToInject, parsedBoxedArgument);
                }
            }

            // Assign it back to pointer (required for value types).
            target = (T)objectToInject;
        }

        private static bool TryParseValue(Type typeToParse, string argument, out object result)
        {
            result = null;

            try
            {
                // For string, just use parameter directly.
                if (typeToParse == typeof(string))
                {
                    result = argument;
                    return true;
                }

                // If parameter is empty then we expect that this should be a bool argument.
                if (string.IsNullOrWhiteSpace(argument))
                {
                    if (typeToParse == typeof(bool))
                    {
                        result = true;
                        return true;
                    }

                    return false;
                }

                // Special case for bool.
                if (typeToParse == typeof(bool))
                {
                    if (bool.TryParse(argument, out var value))
                    {
                        result = value;
                        return true;
                    }
                }

                // Special case for long.
                if (typeToParse == typeof(long))
                {
                    // Check for long.
                    if (long.TryParse(argument, out var parsedLong))
                    {
                        result = parsedLong;
                        return true;
                    }
                }

                if (typeToParse == typeof(ulong))
                {
                    // Check for long.
                    if (ulong.TryParse(argument, out var parsedLong))
                    {
                        result = parsedLong;
                        return true;
                    }
                }

                if (typeToParse.IsEnum && Enum.TryParse(typeToParse, argument, out var parsedEnum))
                {
                    result = parsedEnum;
                    return true;
                }

                // Check if any other numeric as a fallback. 
                if (double.TryParse(argument, out var parsedDouble))
                {
                    var convertedValue = Convert.ChangeType(parsedDouble, typeToParse);
                    result = convertedValue;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception was thrown while injecting the command line arguments: '{ex.Message}'.");
            }

            return false;
        }

        public static Dictionary<string, List<string>> ParseCommandLineArguments(string commandLine)
        {
            // Remove quotes, in case command is written like "-myCommand 500" for some reason.
            commandLine = commandLine.Replace("\"", string.Empty);

            var result = new Dictionary<string, List<string>>();

            var rawCommands = commandLine.Split(" -");
            foreach (var rawCommand in rawCommands)
            {
                try
                {
                    var splitRawCommand = rawCommand.Trim().Split(' ');
                    var command = splitRawCommand[0].Trim();
                    var argument = splitRawCommand.Length > 1 ? splitRawCommand[1].Trim(' ', '\'') : string.Empty;

                    if (!result.TryGetValue(command, out var argumentList))
                        result.Add(command, argumentList = new List<string>());

                    // 'Contains' is fine to have here, as it always only 1 or few arguments per command.
                    if (!argumentList.Contains(argument))
                        argumentList.Add(argument);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Exception was thrown while parsing the '{commandLine}' command line arguments: '{ex.Message}'.");
                }
            }

            return result;
        }
    }
}