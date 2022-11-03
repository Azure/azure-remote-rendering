// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Text;
using UnityEngine;

public class LogHelper<T>
{
    private string _name;

    public static LogHelperState GlobalInformation { get; set; } = LogHelperState.Always;

    public static LogHelperState GlobalVerbose { get; set; } = LogHelperState.Default;

    public static LogHelperState GlobalWarning { get; set; } = LogHelperState.Always;

    public static LogHelperState GlobalError { get; set; } = LogHelperState.Always;

    public LogHelperState Information { get; set; } = LogHelperState.Default;

    public LogHelperState Verbose { get; set; } = LogHelperState.Default;

    public LogHelperState Warning { get; set; } = LogHelperState.Default;

    public LogHelperState Error { get; set; } = LogHelperState.Default;

    public LogOption Option { get; set; } = LogOption.NoStacktrace;

    private bool CanShowInformation =>
        (GlobalInformation == LogHelperState.Always && Information != LogHelperState.Never) || (Information == LogHelperState.Always && GlobalInformation != LogHelperState.Never);

    private bool CanShowVerbose =>
        (GlobalVerbose == LogHelperState.Always && Verbose != LogHelperState.Never) || (Verbose == LogHelperState.Always && GlobalVerbose != LogHelperState.Never);

    private bool CanShowWarning =>
        (GlobalWarning == LogHelperState.Always && Warning != LogHelperState.Never) || (Warning == LogHelperState.Always && GlobalWarning != LogHelperState.Never);

    private bool CanShowError =>
        (GlobalError == LogHelperState.Always && Error != LogHelperState.Never) || (Error == LogHelperState.Always && GlobalError != LogHelperState.Never);

    public LogHelper(string name)
    {
        _name = typeof(T).Name;
    }

    public LogHelper() : this(typeof(T).Name)
    {
    }

    /// <summary>
    /// Log a message if information logging is enabled.
    /// </summary>
    public void LogInformation(string message)
    {
        if (CanShowInformation)
        {
            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {message}");
        }
    }

    /// <summary>
    /// Log a message if information logging is enabled. 
    /// </summary>
    public void LogInformation<U>(string messageFormat, U[] array)
    {
        if (CanShowInformation)
        {
            var arrayString = ToString(array);
            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", arrayString);
        }
    }

    /// <summary>
    /// Log a message if information logging is enabled. 
    /// </summary>
    public void LogInformation(string messageFormat, params object[] args)
    {
        if (CanShowInformation)
        {
            // expand arrays and hashtables to strings
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = ToString(args[i]);
            }

            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", args);
        }
    }

    /// <summary>
    /// Log a message if verbose logging is enabled.
    /// </summary>
    public void LogVerbose(string message)
    {
        if (CanShowVerbose)
        {
            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {message}");
        }
    }

    /// <summary>
    /// Log a message if verbose logging is enabled. 
    /// </summary>
    public void LogVerbose<U>(string messageFormat, U[] array)
    {
        if (CanShowVerbose)
        {
            var arrayString = ToString(array);
            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", arrayString);
        }
    }

    /// <summary>
    /// Log a message if verbose logging is enabled. 
    /// </summary>
    public void LogVerbose(string messageFormat, params object[] args)
    {
        if (CanShowVerbose)
        {
            // expand arrays and hashtables to strings
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = ToString(args[i]);
            }

            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", args);
        }
    }

    /// <summary>
    /// Log an assert failure
    /// </summary>
    /// <param name="test"></param>
    /// <param name="message"></param>
    public void LogAssert(bool test, string message)
    {
        if (CanShowError)
        {
            Debug.Assert(test, $"[{TimeStamp()}] [{_name}] {message}");
        }
    }

    /// <summary>
    /// Log a message if warning logging is enabled.
    /// </summary>
    public void LogWarning(string message)
    {
        if (CanShowWarning)
        {
            SafeLogFormat(LogType.Warning, Option, null, $"[{TimeStamp()}] [{_name}] {message}");
        }
    }

    /// <summary>
    /// Log a message if warning logging is enabled. 
    /// </summary>
    public void LogWarning<U>(string messageFormat, U[] array)
    {
        if (CanShowWarning)
        {
            var arrayString = ToString(array);
            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", arrayString);
        }
    }

    /// <summary>
    /// Log a message if warning logging is enabled. 
    /// </summary>
    public void LogWarning(string messageFormat, params object[] args)
    {
        if (CanShowWarning)
        {
            // expand arrays and hashtables to strings
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = ToString(args[i]);
            }
            SafeLogFormat(LogType.Warning, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", args);
        }
    }

    /// <summary>
    /// Log a message if error logging is enabled.
    /// </summary>
    public void LogError(string message)
    {
        if (CanShowError)
        {
            SafeLogFormat(LogType.Error, Option, null, $"[{TimeStamp()}] [{_name}] {message}");
        }
    }

    /// <summary>
    /// Log a message if error logging is enabled. 
    /// </summary>
    public void LogError<U>(string messageFormat, U[] array)
    {
        if (CanShowError)
        {
            var arrayString = ToString(array);
            SafeLogFormat(LogType.Log, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", arrayString);
        }
    }

    /// <summary>
    /// Log a message if error logging is enabled. 
    /// </summary>
    public void LogError(string messageFormat, params object[] args)
    {
        if (CanShowError)
        {
            // expand arrays and hashtables to strings
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = ToString(args[i]);
            }
            SafeLogFormat(LogType.Error, Option, null, $"[{TimeStamp()}] [{_name}] {messageFormat}", args);
        }
    }

    /// <summary>
    /// Protect against badly formatted strings.
    /// </summary>
    private void SafeLogFormat(LogType logType, LogOption logOption, object context, string format, params object[] args)
    {
        try
        {
            Debug.LogFormat(logType, logOption, null, format, args);
        }
        catch (FormatException)
        {
            Debug.LogFormat(logType, logOption, null, $"[{TimeStamp()}] [{_name}] The following message was not formatted correctly (args = {(args?.Length ?? 0)})");
            Debug.Log(format);
        }
    }

    /// <summary>
    /// Create a time stamp
    /// </summary>
    /// <returns></returns>
    private string TimeStamp()
    {
        return DateTime.UtcNow.ToString();
    }

    /// <summary>
    /// Convert a value to string. If the value is a hashtable or an array, each entry will be converted to a string.
    /// </summary>
    private string ToString(object value)
    {
        if (value is IDictionary)
        {
            return ToString((IDictionary)value, maxEntries: 10);
        }
        else if (value is ICollection)
        {
            return ToString((ICollection)value, maxEntries: 10);
        }
        else
        {
            return value?.ToString() ?? "NULL";
        }
    }

    /// <summary>
    /// Expand a hashtable to single string. Adding only a max number of entries the string.
    /// </summary>
    private string ToString(IDictionary table, int maxEntries)
    {
        int count = Math.Min(maxEntries, table.Count);
        int currnet = 0;
        StringBuilder sb = new StringBuilder();

        sb.Append("[");
        foreach (var key in table.Keys)
        {
            if (currnet >= count)
            {
                break;
            }

            if (currnet > 0)
            {
                sb.Append(", ");
            }

            sb.Append(key.ToString());
            sb.Append(" = ");
            sb.Append(ToString(table[key]));

            currnet++;
        }

        if (count < table.Count)
        {
            sb.Append("...");
        }
        sb.Append("]");

        return sb.ToString();
    }

    /// <summary>
    /// Expand a collection to single string. Adding only a max number of entries the string. 
    /// </summary>
    private string ToString(ICollection collection, int maxEntries)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append("[");
        int entry = 0;
        foreach (object value in collection)
        {
            if (entry > maxEntries)
            {
                sb.Append("...");
                break;
            }

            if (entry > 0)
            {
                sb.Append(", ");
            }

            sb.Append(value != null ? value.ToString() : "NULL");
            entry++;
        }
        sb.Append("]");

        return sb.ToString();
    }
}

public class LogHelper : LogHelper<object>
{
    public LogHelper(string name) : base(name)
    {
    }
}

/// <summary>
/// Enum to control when to log a message of certain type
/// </summary>
public enum LogHelperState
{
    Always,
    Never,
    Default
}
