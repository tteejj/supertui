using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Represents a saved workspace template that can be loaded and exported.
    /// Templates include layout configuration and widget definitions.
    /// </summary>
    public class WorkspaceTemplate
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Version { get; set; }
        public LayoutType LayoutType { get; set; }
        public Dictionary<string, object> LayoutConfig { get; set; }
        public List<WidgetDefinition> Widgets { get; set; }
        public Dictionary<string, string> Metadata { get; set; }

        public WorkspaceTemplate()
        {
            LayoutConfig = new Dictionary<string, object>();
            Widgets = new List<WidgetDefinition>();
            Metadata = new Dictionary<string, string>();
            CreatedAt = DateTime.Now;
            Version = "1.0";
        }
    }

    /// <summary>
    /// Defines a widget instance in a template
    /// </summary>
    public class WidgetDefinition
    {
        public string WidgetType { get; set; }
        public string Name { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, object> LayoutParameters { get; set; }

        public WidgetDefinition()
        {
            Parameters = new Dictionary<string, object>();
            LayoutParameters = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Layout type enumeration
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LayoutType
    {
        Grid,
        Dock,
        Stack
    }

    /// <summary>
    /// Manages workspace templates - save, load, export, import
    /// </summary>
    public class WorkspaceTemplateManager
    {
        private static WorkspaceTemplateManager instance;
        private static readonly object lockObject = new object();

        private string templatesDirectory;
        private Dictionary<string, WorkspaceTemplate> loadedTemplates;

        public static WorkspaceTemplateManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new WorkspaceTemplateManager();
                        }
                    }
                }
                return instance;
            }
        }

        private WorkspaceTemplateManager()
        {
            templatesDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SuperTUI", "Templates");

            if (!Directory.Exists(templatesDirectory))
                Directory.CreateDirectory(templatesDirectory);

            loadedTemplates = new Dictionary<string, WorkspaceTemplate>();
            LoadAllTemplates();
        }

        /// <summary>
        /// Save a workspace template to disk
        /// </summary>
        public void SaveTemplate(WorkspaceTemplate template)
        {
            try
            {
                var fileName = SanitizeFileName(template.Name) + ".json";
                var filePath = Path.Combine(templatesDirectory, fileName);

                var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                File.WriteAllText(filePath, json);
                loadedTemplates[template.Name] = template;

                Logger.Instance.Info("Templates", $"Saved template: {template.Name}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to save template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Load a workspace template by name
        /// </summary>
        public WorkspaceTemplate LoadTemplate(string name)
        {
            if (loadedTemplates.TryGetValue(name, out var template))
                return template;

            var fileName = SanitizeFileName(name) + ".json";
            var filePath = Path.Combine(templatesDirectory, fileName);

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Template not found: {name}");

            try
            {
                var json = File.ReadAllText(filePath);
                template = JsonSerializer.Deserialize<WorkspaceTemplate>(json);
                loadedTemplates[name] = template;

                Logger.Instance.Info("Templates", $"Loaded template: {name}");
                return template;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to load template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Export a template to a specific file path
        /// </summary>
        public void ExportTemplate(string templateName, string exportPath)
        {
            var template = LoadTemplate(templateName);

            try
            {
                var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                File.WriteAllText(exportPath, json);
                Logger.Instance.Info("Templates", $"Exported template to: {exportPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to export template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Import a template from a file
        /// </summary>
        public WorkspaceTemplate ImportTemplate(string importPath)
        {
            try
            {
                var json = File.ReadAllText(importPath);
                var template = JsonSerializer.Deserialize<WorkspaceTemplate>(json);

                // Save to templates directory
                SaveTemplate(template);

                Logger.Instance.Info("Templates", $"Imported template: {template.Name}");
                return template;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to import template: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Delete a template
        /// </summary>
        public void DeleteTemplate(string name)
        {
            var fileName = SanitizeFileName(name) + ".json";
            var filePath = Path.Combine(templatesDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                loadedTemplates.Remove(name);
                Logger.Instance.Info("Templates", $"Deleted template: {name}");
            }
        }

        /// <summary>
        /// List all available templates
        /// </summary>
        public List<WorkspaceTemplate> ListTemplates()
        {
            return loadedTemplates.Values.OrderBy(t => t.Name).ToList();
        }

        /// <summary>
        /// Check if a template exists
        /// </summary>
        public bool TemplateExists(string name)
        {
            return loadedTemplates.ContainsKey(name);
        }

        /// <summary>
        /// Load all templates from disk
        /// </summary>
        private void LoadAllTemplates()
        {
            try
            {
                var files = Directory.GetFiles(templatesDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var template = JsonSerializer.Deserialize<WorkspaceTemplate>(json);
                        if (template != null)
                        {
                            loadedTemplates[template.Name] = template;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("Templates", $"Failed to load template file {file}: {ex.Message}");
                    }
                }

                Logger.Instance.Info("Templates", $"Loaded {loadedTemplates.Count} templates");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Templates", $"Failed to load templates: {ex.Message}");
            }
        }

        private string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", name.Split(invalidChars));
            return sanitized;
        }

        /// <summary>
        /// Create built-in example templates
        /// </summary>
        public void CreateBuiltInTemplates()
        {
            // Developer workspace template
            var devTemplate = new WorkspaceTemplate
            {
                Name = "Developer",
                Description = "Development workspace with terminal, git, and file explorer",
                Author = "SuperTUI",
                LayoutType = LayoutType.Grid,
                LayoutConfig = new Dictionary<string, object>
                {
                    ["Rows"] = 2,
                    ["Columns"] = 3,
                    ["Splitters"] = true
                },
                Widgets = new List<WidgetDefinition>
                {
                    new WidgetDefinition
                    {
                        WidgetType = "GitStatusWidget",
                        Name = "Git Status",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 0
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "FileExplorerWidget",
                        Name = "Files",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 1
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "SystemMonitorWidget",
                        Name = "System",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 2
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "TerminalWidget",
                        Name = "Terminal",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 1,
                            ["Column"] = 0,
                            ["ColumnSpan"] = 3
                        }
                    }
                }
            };

            // Productivity workspace template
            var productivityTemplate = new WorkspaceTemplate
            {
                Name = "Productivity",
                Description = "Todo list, notes, and clock for task management",
                Author = "SuperTUI",
                LayoutType = LayoutType.Grid,
                LayoutConfig = new Dictionary<string, object>
                {
                    ["Rows"] = 2,
                    ["Columns"] = 2,
                    ["Splitters"] = true
                },
                Widgets = new List<WidgetDefinition>
                {
                    new WidgetDefinition
                    {
                        WidgetType = "TodoWidget",
                        Name = "Tasks",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 0
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "ClockWidget",
                        Name = "Clock",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 0,
                            ["Column"] = 1
                        }
                    },
                    new WidgetDefinition
                    {
                        WidgetType = "NotesWidget",
                        Name = "Notes",
                        LayoutParameters = new Dictionary<string, object>
                        {
                            ["Row"] = 1,
                            ["Column"] = 0,
                            ["ColumnSpan"] = 2
                        }
                    }
                }
            };

            // Save built-in templates
            if (!TemplateExists("Developer"))
                SaveTemplate(devTemplate);

            if (!TemplateExists("Productivity"))
                SaveTemplate(productivityTemplate);
        }
    }
}
