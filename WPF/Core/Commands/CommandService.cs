// CommandService.cs - Service for managing command library
// Handles JSON storage, CRUD operations, and advanced search functionality

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using SuperTUI.Core.Infrastructure;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Commands
{
    /// <summary>
    /// Service for managing a library of reusable command strings.
    /// Provides CRUD operations, JSON persistence, and advanced search with tag syntax.
    /// </summary>
    public class CommandService
    {
        private readonly string _dataPath;
        private readonly List<Command> _commands;
        private readonly Logger _logger;

        /// <summary>
        /// Initializes a new instance of CommandService and loads commands from disk
        /// </summary>
        public CommandService(Logger logger = null)
        {
            _logger = logger;

            // Setup data directory using DirectoryHelper for consistent fallback behavior
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".supertui"
            );

            try
            {
                appDataDir = SuperTUI.Extensions.DirectoryHelper.CreateDirectoryWithFallback(appDataDir, "Commands");
                _dataPath = Path.Combine(appDataDir, "commands.json");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to create data directory: {ex.Message}");
                // Last resort fallback - should rarely happen with DirectoryHelper
                var currentDir = Directory.GetCurrentDirectory();
                _dataPath = Path.Combine(currentDir, ".supertui", "commands.json");
            }

            _commands = new List<Command>();
            LoadCommands();
        }

        /// <summary>
        /// Get all commands in the library
        /// </summary>
        public List<Command> GetAllCommands() => new List<Command>(_commands);

        /// <summary>
        /// Get command by ID
        /// </summary>
        public Command GetCommand(string id)
        {
            return _commands.FirstOrDefault(c => c.Id == id);
        }

        /// <summary>
        /// Load commands from JSON file
        /// </summary>
        public void LoadCommands()
        {
            try
            {
                if (!File.Exists(_dataPath))
                {
                    _logger?.Info($"No existing commands file found at {_dataPath}, starting with empty library");
                    return;
                }

                var json = File.ReadAllText(_dataPath);
                var commands = JsonSerializer.Deserialize<List<Command>>(json);

                _commands.Clear();
                if (commands != null)
                {
                    _commands.AddRange(commands);
                }

                _logger?.Info($"Loaded {_commands.Count} commands from {_dataPath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to load commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Save commands to JSON file
        /// </summary>
        public void SaveCommands()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_commands, options);
                File.WriteAllText(_dataPath, json);

                _logger?.Debug($"Saved {_commands.Count} commands to {_dataPath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to save commands: {ex.Message}");
            }
        }

        /// <summary>
        /// Add new command to library
        /// </summary>
        public Command AddCommand(Command command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!command.IsValid)
                throw new ArgumentException("Command text is required", nameof(command));

            _commands.Add(command);
            SaveCommands();

            _logger?.Info($"Added command: {command.Title}");

            return command;
        }

        /// <summary>
        /// Update existing command
        /// </summary>
        public bool UpdateCommand(Command command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (!command.IsValid)
                throw new ArgumentException("Command text is required", nameof(command));

            var index = _commands.FindIndex(c => c.Id == command.Id);
            if (index < 0)
            {
                _logger?.Warning($"Command not found for update: {command.Id}");
                return false;
            }

            _commands[index] = command;
            SaveCommands();

            _logger?.Info($"Updated command: {command.Title}");

            return true;
        }

        /// <summary>
        /// Delete command from library
        /// </summary>
        public bool DeleteCommand(string id)
        {
            var command = _commands.FirstOrDefault(c => c.Id == id);
            if (command == null)
            {
                _logger?.Warning($"Command not found for deletion: {id}");
                return false;
            }

            _commands.Remove(command);
            SaveCommands();

            _logger?.Info($"Deleted command: {command.Title}");

            return true;
        }

        /// <summary>
        /// Copy command to clipboard and record usage
        /// </summary>
        public void CopyToClipboard(string id)
        {
            var command = GetCommand(id);
            if (command == null)
            {
                _logger?.Warning($"Command not found: {id}");
                return;
            }

            try
            {
                Clipboard.SetText(command.CommandText);
                command.RecordUsage();
                SaveCommands();

                _logger?.Debug($"Copied command to clipboard: {command.Title}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to copy to clipboard: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Search commands with advanced syntax support.
        /// Syntax:
        ///   - "docker"         : Substring search in all fields
        ///   - "t:docker"       : Search in tags
        ///   - "t:docker,admin" : Search for docker OR admin tag
        ///   - "+docker +restart" : AND search (must contain both)
        ///   - "docker|podman"  : OR search (contains either)
        /// </summary>
        public List<Command> SearchCommands(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAllCommands();

            var criteria = ParseSearchQuery(query);
            return _commands.Where(cmd => MatchesSearchCriteria(cmd, criteria)).ToList();
        }

        /// <summary>
        /// Get all unique tags from all commands
        /// </summary>
        public string[] GetTags()
        {
            var tags = new HashSet<string>();
            foreach (var command in _commands)
            {
                foreach (var tag in command.Tags)
                {
                    if (!string.IsNullOrWhiteSpace(tag))
                        tags.Add(tag);
                }
            }
            return tags.OrderBy(t => t).ToArray();
        }

        #region Search Implementation

        private class SearchCriteria
        {
            public List<string> DefaultSearch { get; set; } = new List<string>();
            public List<string> TagSearch { get; set; } = new List<string>();
            public bool AndMode { get; set; } = false;
        }

        private SearchCriteria ParseSearchQuery(string query)
        {
            var criteria = new SearchCriteria();

            // Check for AND mode (+)
            if (query.Contains('+'))
            {
                criteria.AndMode = true;
            }

            // Split by spaces and process each term
            var terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var term in terms)
            {
                // Check for tag search (t:tag or t:tag1,tag2)
                if (term.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
                {
                    var tagPart = term.Substring(2);
                    // Handle OR within tags (t:docker,podman)
                    var tags = tagPart.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
                    criteria.TagSearch.AddRange(tags);
                }
                else
                {
                    // Default search (remove leading +)
                    var cleanTerm = term.TrimStart('+');
                    // Handle OR in default search (docker|podman)
                    var orTerms = cleanTerm.Split('|');
                    criteria.DefaultSearch.AddRange(orTerms);
                }
            }

            return criteria;
        }

        private bool MatchesSearchCriteria(Command command, SearchCriteria criteria)
        {
            var matches = new List<bool>();

            // Default search (all fields)
            if (criteria.DefaultSearch.Count > 0)
            {
                var searchableText = command.GetSearchableText().ToLowerInvariant();
                var anyMatch = false;

                foreach (var term in criteria.DefaultSearch)
                {
                    if (searchableText.Contains(term.ToLowerInvariant()))
                    {
                        anyMatch = true;
                        break;
                    }
                }

                matches.Add(anyMatch);
            }

            // Tag search
            if (criteria.TagSearch.Count > 0)
            {
                var commandTags = command.Tags.Select(t => t.ToLowerInvariant()).ToArray();
                var anyMatch = false;

                foreach (var searchTag in criteria.TagSearch)
                {
                    var searchTagLower = searchTag.ToLowerInvariant();
                    if (commandTags.Any(t => t.Contains(searchTagLower)))
                    {
                        anyMatch = true;
                        break;
                    }
                }

                matches.Add(anyMatch);
            }

            // Return based on AND/OR logic
            if (matches.Count == 0)
                return true; // No criteria, match all

            if (criteria.AndMode)
            {
                // AND: all criteria must match
                return matches.All(m => m);
            }
            else
            {
                // OR: any criteria can match
                return matches.Any(m => m);
            }
        }

        #endregion
    }
}
