using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for logging service - enables testing and mocking
    /// </summary>
    public interface ILogger
    {
        void Trace(string category, string message);
        void Debug(string category, string message);
        void Info(string category, string message);
        void Warning(string category, string message);
        void Error(string category, string message, Exception ex = null);
        void Critical(string category, string message, Exception ex = null);

        void Log(LogLevel level, string category, string message, Exception exception = null, Dictionary<string, object> properties = null);

        void SetMinLevel(LogLevel level);
        void AddSink(ILogSink sink);
        void Flush();

        void EnableCategory(string category);
        void DisableCategory(string category);
    }
}
