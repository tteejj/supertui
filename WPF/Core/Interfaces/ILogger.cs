using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for logging service - enables testing and mocking
    /// </summary>
    public interface ILogger
    {
        void Trace(string category, string message, Dictionary<string, object> properties = null);
        void Debug(string category, string message, Dictionary<string, object> properties = null);
        void Info(string category, string message, Dictionary<string, object> properties = null);
        void Warning(string category, string message, Dictionary<string, object> properties = null);
        void Error(string category, string message, Exception exception = null, Dictionary<string, object> properties = null);
        void Critical(string category, string message, Exception exception = null, Dictionary<string, object> properties = null);

        void SetMinLevel(LogLevel level);
        void AddSink(ILogSink sink);
        void RemoveSink(ILogSink sink);
        void Flush();
    }
}
