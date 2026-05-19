using System.Reflection;
using Microsoft.ClearScript;

namespace HsWin.App;

internal static class UserFacingExceptionFormatter
{
    public static string FormatConfigReloadFailure(Exception exception)
    {
        var message = Unwrap(exception).Message.Trim();
        if (message.StartsWith("Error:", StringComparison.Ordinal))
        {
            message = message[6..].Trim();
        }

        return message;
    }

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (true)
        {
            switch (current)
            {
                case TargetInvocationException { InnerException: { } inner }:
                    current = inner;
                    continue;
                case AggregateException { InnerException: { } inner }:
                    current = inner;
                    continue;
                case ScriptEngineException scriptException when scriptException.InnerException is not null:
                    current = scriptException.InnerException;
                    continue;
            }

            return current;
        }
    }
}
