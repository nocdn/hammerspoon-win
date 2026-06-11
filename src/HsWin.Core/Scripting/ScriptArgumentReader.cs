using System.Collections;
using System.Globalization;
using Microsoft.ClearScript;

namespace HsWin.Core.Scripting;

public static class ScriptArgumentReader
{
    public static bool IsMissing(object? value)
    {
        return value is null || ReferenceEquals(value, Undefined.Value);
    }

    public static bool IsOptionsObject(object? value)
    {
        return value is ScriptObject or IReadOnlyDictionary<string, object?> or IDictionary;
    }

    public static bool HasIndexedValues(object? value)
    {
        if (IsMissing(value))
        {
            return false;
        }

        if (value is ScriptObject scriptObject)
        {
            return scriptObject.PropertyIndices.Any();
        }

        if (value is IDictionary)
        {
            return false;
        }

        return value is IEnumerable and not string;
    }

    public static object? GetPropertyValue(object? value, params string[] names)
    {
        if (IsMissing(value))
        {
            return null;
        }

        if (value is ScriptObject scriptObject)
        {
            var propertyNames = scriptObject.PropertyNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                var actualName = propertyNames.FirstOrDefault(propertyName => string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase));
                if (actualName is not null)
                {
                    return scriptObject.GetProperty(actualName);
                }
            }

            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var name in names)
            {
                foreach (var item in readOnlyDictionary)
                {
                    if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return item.Value;
                    }
                }
            }

            return null;
        }

        if (value is IDictionary dictionary)
        {
            foreach (var name in names)
            {
                foreach (DictionaryEntry item in dictionary)
                {
                    if (item.Key is string key && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return item.Value;
                    }
                }
            }
        }

        return null;
    }

    public static IEnumerable<object?> EnumerateIndexedValues(object? value)
    {
        if (IsMissing(value))
        {
            yield break;
        }

        if (value is ScriptObject scriptObject)
        {
            foreach (var index in scriptObject.PropertyIndices.Order())
            {
                yield return scriptObject.GetProperty(index);
            }

            yield break;
        }

        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }

    public static IReadOnlyDictionary<string, string> ReadStringMap(object? value, string argumentName)
    {
        if (IsMissing(value))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (value is ScriptObject scriptObject)
        {
            foreach (var propertyName in scriptObject.PropertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                var propertyValue = scriptObject.GetProperty(propertyName);
                if (!IsMissing(propertyValue))
                {
                    result[propertyName] = RequireText(propertyValue, propertyName);
                }
            }

            return result;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            foreach (var item in readOnlyDictionary)
            {
                if (!IsMissing(item.Value))
                {
                    result[item.Key] = RequireText(item.Value, item.Key);
                }
            }

            return result;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry item in dictionary)
            {
                if (item.Key is string key && !IsMissing(item.Value))
                {
                    result[key] = RequireText(item.Value, key);
                }
            }

            return result;
        }

        throw new ArgumentException($"{argumentName} must be an object.", argumentName);
    }

    public static string RequireText(object? value, string argumentName)
    {
        if (IsMissing(value))
        {
            throw new ArgumentException($"{argumentName} is required.", argumentName);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static string RequireNonWhiteSpaceString(object? value, string argumentName)
    {
        var text = RequireText(value, argumentName);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{argumentName} is required.", argumentName);
        }

        return text;
    }

    public static string? OptionalString(object? value)
    {
        if (IsMissing(value))
        {
            return null;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static bool RequireBoolean(object? value, string argumentName)
    {
        if (IsMissing(value))
        {
            throw new ArgumentException($"{argumentName} is required.", argumentName);
        }

        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    public static int RequireInt32(object? value, string argumentName, string expectedDescription)
    {
        if (IsMissing(value))
        {
            throw new ArgumentException($"{argumentName} is required.", argumentName);
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException($"{argumentName} must be {expectedDescription}.", argumentName, exception);
        }
        catch (InvalidCastException exception)
        {
            throw new ArgumentException($"{argumentName} must be {expectedDescription}.", argumentName, exception);
        }
    }

    public static double RequireDouble(object? value, string argumentName, string expectedDescription)
    {
        if (IsMissing(value))
        {
            throw new ArgumentException($"{argumentName} is required.", argumentName);
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException($"{argumentName} must be {expectedDescription}.", argumentName, exception);
        }
        catch (InvalidCastException exception)
        {
            throw new ArgumentException($"{argumentName} must be {expectedDescription}.", argumentName, exception);
        }
    }
}
