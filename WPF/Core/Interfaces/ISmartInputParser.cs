using System;

namespace SuperTUI.Core.Interfaces
{
    /// <summary>
    /// Interface for smart input parsing service
    /// Parses natural language dates and durations
    /// </summary>
    public interface ISmartInputParser
    {
        /// <summary>
        /// Parse date from natural language input
        /// Supports: "tomorrow", "+3", "20251030", "next week", "today", etc.
        /// </summary>
        /// <param name="input">Date string to parse</param>
        /// <returns>Parsed date or null if cannot parse</returns>
        DateTime? ParseDate(string input);

        /// <summary>
        /// Parse duration from input
        /// Supports: "2h", "30m", "2.5h", "1d", "90m", etc.
        /// </summary>
        /// <param name="input">Duration string to parse</param>
        /// <returns>Parsed TimeSpan or null if cannot parse</returns>
        TimeSpan? ParseDuration(string input);

        /// <summary>
        /// Try parse date with out parameter
        /// </summary>
        bool TryParseDate(string input, out DateTime result);

        /// <summary>
        /// Try parse duration with out parameter
        /// </summary>
        bool TryParseDuration(string input, out TimeSpan result);
    }
}
