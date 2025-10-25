// Command.cs - Data model for storing reusable command strings
// Used in Command Library for quick access and clipboard copying

using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace SuperTUI.Core.Commands
{
    /// <summary>
    /// Represents a stored command string with metadata for organization and usage tracking.
    /// Commands are simple text strings to be copied to clipboard and pasted elsewhere.
    /// </summary>
    public class Command
    {
        /// <summary>
        /// Unique identifier for the command
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Short title for display in lists
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Optional longer description of what the command does
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Tags for categorization and filtering (e.g., "docker", "git", "admin")
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// The actual command text to be copied to clipboard (single line)
        /// </summary>
        public string CommandText { get; set; } = "";

        /// <summary>
        /// Timestamp when the command was created
        /// </summary>
        public DateTime Created { get; set; } = DateTime.Now;

        /// <summary>
        /// Timestamp when the command was last used (copied to clipboard)
        /// </summary>
        public DateTime LastUsed { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Number of times this command has been copied to clipboard
        /// </summary>
        public int UseCount { get; set; } = 0;

        /// <summary>
        /// Validation - CommandText is required
        /// </summary>
        [JsonIgnore]
        public bool IsValid => !string.IsNullOrWhiteSpace(CommandText);

        /// <summary>
        /// Update usage statistics when command is copied to clipboard
        /// </summary>
        public void RecordUsage()
        {
            LastUsed = DateTime.Now;
            UseCount++;
        }

        /// <summary>
        /// Get display text for lists (title with tags)
        /// </summary>
        public string GetDisplayText()
        {
            var display = string.IsNullOrWhiteSpace(Title)
                ? TruncateCommand(CommandText, 50)
                : Title;

            if (Tags.Length > 0)
            {
                display += $"  t:{string.Join(",", Tags)}";
            }

            return display;
        }

        /// <summary>
        /// Get all searchable text (title, description, tags, command text)
        /// </summary>
        public string GetSearchableText()
        {
            var parts = new[]
            {
                Title,
                Description,
                string.Join(" ", Tags),
                CommandText
            };

            return string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        /// <summary>
        /// Get detailed text for display in detail panel
        /// </summary>
        public string GetDetailText()
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"Command: {CommandText}",
                ""
            };

            if (!string.IsNullOrWhiteSpace(Description))
                lines.Add($"Description: {Description}");

            if (Tags.Length > 0)
                lines.Add($"Tags: {string.Join(", ", Tags)}");

            if (UseCount > 0)
            {
                var lastUsedStr = LastUsed == DateTime.MinValue
                    ? "never"
                    : LastUsed.ToString("yyyy-MM-dd HH:mm");
                lines.Add($"Used: {UseCount} times, last: {lastUsedStr}");
            }

            return string.Join("\n", lines);
        }

        private static string TruncateCommand(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }
    }
}
