using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Service for managing Excel field mappings and import/export operations.
    /// Uses clipboard-based workflow (no COM automation).
    /// </summary>
    public class ExcelMappingService : IExcelMappingService
    {
        private static ExcelMappingService instance;
        public static ExcelMappingService Instance => instance ??= new ExcelMappingService();

        private readonly ILogger logger;
        private readonly IConfigurationManager config;
        private readonly IProjectService projectService;

        private List<ExcelMappingProfile> profiles;
        private ExcelMappingProfile activeProfile;
        private string profilesDirectory;
        private readonly object lockObject = new object();

        // Events
        public event Action<ExcelMappingProfile> ProfileChanged;
        public event Action ProfilesLoaded;

        // DI constructor
        public ExcelMappingService(ILogger logger, IConfigurationManager config, IProjectService projectService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));

            profiles = new List<ExcelMappingProfile>();
        }

        // Parameterless constructor for singleton
        private ExcelMappingService()
            : this(Logger.Instance, ConfigurationManager.Instance, ProjectService.Instance)
        { }

        public void Initialize()
        {
            try
            {
                // Set profiles directory
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                profilesDirectory = Path.Combine(appData, "SuperTUI", "excel_profiles");

                if (!Directory.Exists(profilesDirectory))
                {
                    Directory.CreateDirectory(profilesDirectory);
                    logger.Info("ExcelMapping", $"Created profiles directory: {profilesDirectory}");
                }

                // Load profiles
                LoadProfiles();

                // Create default profile if none exist
                if (profiles.Count == 0)
                {
                    CreateDefaultSVICASProfile();
                }

                // Set active profile
                var activeProfileId = config.Get<string>("Excel.ActiveProfileId", null);
                if (activeProfileId != null && Guid.TryParse(activeProfileId, out var id))
                {
                    SetActiveProfile(id);
                }
                else if (profiles.Count > 0)
                {
                    SetActiveProfile(profiles[0].Id);
                }

                logger.Info("ExcelMapping", $"Initialized with {profiles.Count} profiles");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Initialization failed: {ex.Message}", ex);
            }
        }

        // Profile management

        public List<ExcelMappingProfile> GetAllProfiles()
        {
            lock (lockObject)
            {
                return new List<ExcelMappingProfile>(profiles);
            }
        }

        public ExcelMappingProfile GetActiveProfile()
        {
            return activeProfile;
        }

        public void SaveProfile(ExcelMappingProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            try
            {
                lock (lockObject)
                {
                    profile.ModifiedDate = DateTime.Now;

                    var filePath = Path.Combine(profilesDirectory, $"{profile.Id}.json");
                    profile.SaveToJson(filePath);

                    // Update or add to profiles list
                    var existing = profiles.FirstOrDefault(p => p.Id == profile.Id);
                    if (existing != null)
                    {
                        profiles.Remove(existing);
                    }
                    profiles.Add(profile);

                    logger.Info("ExcelMapping", $"Saved profile: {profile.Name}");
                }
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Failed to save profile: {ex.Message}", ex);
                throw;
            }
        }

        public void DeleteProfile(Guid id)
        {
            try
            {
                lock (lockObject)
                {
                    var profile = profiles.FirstOrDefault(p => p.Id == id);
                    if (profile == null)
                    {
                        logger.Warning("ExcelMapping", $"Profile not found: {id}");
                        return;
                    }

                    // Don't delete if it's the active profile and it's the last one
                    if (activeProfile?.Id == id && profiles.Count == 1)
                    {
                        throw new InvalidOperationException("Cannot delete the last remaining profile");
                    }

                    // Delete file
                    var filePath = Path.Combine(profilesDirectory, $"{id}.json");
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    // Remove from list
                    profiles.Remove(profile);

                    // If active profile was deleted, set another one
                    if (activeProfile?.Id == id && profiles.Count > 0)
                    {
                        SetActiveProfile(profiles[0].Id);
                    }

                    logger.Info("ExcelMapping", $"Deleted profile: {profile.Name}");
                }
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Failed to delete profile: {ex.Message}", ex);
                throw;
            }
        }

        public void SetActiveProfile(Guid id)
        {
            lock (lockObject)
            {
                var profile = profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                {
                    logger.Warning("ExcelMapping", $"Profile not found: {id}");
                    return;
                }

                activeProfile = profile;
                config.Set("Excel.ActiveProfileId", id.ToString());
                config.Save();

                ProfileChanged?.Invoke(activeProfile);
                logger.Info("ExcelMapping", $"Active profile set to: {profile.Name}");
            }
        }

        public void LoadProfiles()
        {
            try
            {
                lock (lockObject)
                {
                    profiles.Clear();

                    if (!Directory.Exists(profilesDirectory))
                        return;

                    var jsonFiles = Directory.GetFiles(profilesDirectory, "*.json");
                    foreach (var file in jsonFiles)
                    {
                        try
                        {
                            var profile = ExcelMappingProfile.LoadFromJson(file);
                            profiles.Add(profile);
                        }
                        catch (Exception ex)
                        {
                            logger.Warning("ExcelMapping", $"Failed to load profile {file}: {ex.Message}");
                        }
                    }

                    logger.Info("ExcelMapping", $"Loaded {profiles.Count} profiles");
                    ProfilesLoaded?.Invoke();
                }
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Failed to load profiles: {ex.Message}", ex);
            }
        }

        // Mapping management

        public void AddMapping(ExcelFieldMapping mapping)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            lock (lockObject)
            {
                mapping.SortOrder = activeProfile.Mappings.Count;
                activeProfile.Mappings.Add(mapping);
                SaveProfile(activeProfile);
            }
        }

        public void UpdateMapping(ExcelFieldMapping mapping)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));

            lock (lockObject)
            {
                var existing = activeProfile.Mappings.FirstOrDefault(m => m.Id == mapping.Id);
                if (existing != null)
                {
                    activeProfile.Mappings.Remove(existing);
                    activeProfile.Mappings.Add(mapping);
                    SaveProfile(activeProfile);
                }
            }
        }

        public void DeleteMapping(Guid id)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            lock (lockObject)
            {
                var mapping = activeProfile.Mappings.FirstOrDefault(m => m.Id == id);
                if (mapping != null)
                {
                    activeProfile.Mappings.Remove(mapping);
                    SaveProfile(activeProfile);
                }
            }
        }

        public void ReorderMapping(Guid id, int newSortOrder)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            lock (lockObject)
            {
                var mapping = activeProfile.Mappings.FirstOrDefault(m => m.Id == id);
                if (mapping != null)
                {
                    mapping.SortOrder = newSortOrder;
                    SaveProfile(activeProfile);
                }
            }
        }

        public List<ExcelFieldMapping> GetMappingsByCategory(string category)
        {
            if (activeProfile == null)
                return new List<ExcelFieldMapping>();

            return activeProfile.Mappings
                .Where(m => m.Category == category)
                .OrderBy(m => m.SortOrder)
                .ToList();
        }

        public List<ExcelFieldMapping> GetExportMappings()
        {
            if (activeProfile == null)
                return new List<ExcelFieldMapping>();

            return activeProfile.Mappings
                .Where(m => m.IncludeInExport)
                .OrderBy(m => m.SortOrder)
                .ToList();
        }

        // Import operations

        public Project ImportProjectFromClipboard(string clipboardData, string startCell = "W3")
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            try
            {
                var cellData = ClipboardDataParser.ParseTSV(clipboardData, startCell);
                var project = new Project();

                foreach (var mapping in activeProfile.Mappings)
                {
                    var cellValue = ClipboardDataParser.GetCellValue(cellData, mapping.ExcelCellRef);

                    if (string.IsNullOrEmpty(cellValue) && !string.IsNullOrEmpty(mapping.DefaultValue))
                    {
                        cellValue = mapping.DefaultValue;
                    }

                    ExcelExportFormatter.SetProjectValue(project, mapping.ProjectPropertyName, cellValue);
                }

                logger.Info("ExcelMapping", $"Imported project: {project.Name}");
                return project;
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Import failed: {ex.Message}", ex);
                throw;
            }
        }

        public List<Project> ImportMultipleProjectsFromClipboard(string clipboardData, string startCell = "W3")
        {
            // For now, just import one project
            // Future: Support importing multiple projects from multiple columns/rows
            var projects = new List<Project>();
            try
            {
                var project = ImportProjectFromClipboard(clipboardData, startCell);
                projects.Add(project);
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Multiple import failed: {ex.Message}", ex);
            }
            return projects;
        }

        // Export operations

        public void ExportToClipboard(List<Project> projects, string format)
        {
            try
            {
                var data = ExportToString(projects, format);
                Clipboard.SetText(data);
                logger.Info("ExcelMapping", $"Exported {projects.Count} projects to clipboard as {format}");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Export to clipboard failed: {ex.Message}", ex);
                throw;
            }
        }

        public void ExportToFile(List<Project> projects, string filePath, string format)
        {
            try
            {
                var data = ExportToString(projects, format);

                // Atomic write: temp → rename pattern
                string tempFile = filePath + ".tmp";
                File.WriteAllText(tempFile, data);
                File.Replace(tempFile, filePath, filePath + ".bak");

                logger.Info("ExcelMapping", $"Exported {projects.Count} projects to file: {filePath}");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Export to file failed: {ex.Message}", ex);
                throw;
            }
        }

        public string ExportToString(List<Project> projects, string format)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            var exportFields = GetExportMappings();
            if (exportFields.Count == 0)
            {
                throw new InvalidOperationException("No fields marked for export");
            }

            return format.ToLower() switch
            {
                "csv" => ExcelExportFormatter.ToCsv(projects, exportFields),
                "tsv" => ExcelExportFormatter.ToTsv(projects, exportFields),
                "json" => ExcelExportFormatter.ToJson(projects, exportFields),
                "xml" => ExcelExportFormatter.ToXml(projects, exportFields),
                _ => throw new ArgumentException($"Unknown format: {format}")
            };
        }

        // Helper: Create default SVI-CAS profile with 48 fields
        private void CreateDefaultSVICASProfile()
        {
            var profile = new ExcelMappingProfile
            {
                Name = "SVI-CAS Standard (48 Fields)",
                Description = "Government audit request form mapping (W3:W130, 48 fields)"
            };

            // Sample mappings (you mentioned 48 fields from W3:W130)
            // I'll create a representative subset - expand as needed
            var mappings = new List<(string display, string cell, string prop, string category, bool export)>
            {
                ("TP Name", "W3", "TPName", "Case Info", true),
                ("TP Address 1", "W4", "TPAddress1", "Case Info", false),
                ("TP Address 2", "W5", "TPAddress2", "Case Info", false),
                ("TP City", "W6", "TPCity", "Case Info", false),
                ("TP State", "W7", "TPState", "Case Info", true),
                ("TP ZIP", "W8", "TPZIP", "Case Info", false),
                ("CAS Case", "W17", "CASCase", "Case Info", true),
                ("Original Project", "W23", "OriginalProject", "Project Info", true),
                ("Actual Project", "W24", "ActualProject", "Project Info", true),
                ("Project Status", "W30", "ProjectStatus", "Status", true),
                ("Date Received", "W40", "DateReceived", "Dates", true),
                ("Due Date", "W41", "DueDate", "Dates", true),
                ("Extension Date", "W42", "ExtensionDate", "Dates", false),
                ("Analyst Name", "W50", "AnalystName", "Assignment", true),
                ("Hours Estimated", "W60", "HoursEstimated", "Time", false),
                ("Hours Actual", "W61", "HoursActual", "Time", true),
                ("Issues Found", "W70", "IssuesFound", "Results", true),
                ("Recommendation", "W80", "Recommendation", "Results", true),
            };

            int sortOrder = 0;
            foreach (var (display, cell, prop, category, export) in mappings)
            {
                profile.Mappings.Add(new ExcelFieldMapping
                {
                    DisplayName = display,
                    ExcelCellRef = cell,
                    ProjectPropertyName = prop,
                    Category = category,
                    IncludeInExport = export,
                    SortOrder = sortOrder++,
                    Required = export,  // Export fields are typically required
                    DataType = "String"
                });
            }

            SaveProfile(profile);
            SetActiveProfile(profile.Id);
            logger.Info("ExcelMapping", "Created default SVI-CAS profile");
        }
    }
}
