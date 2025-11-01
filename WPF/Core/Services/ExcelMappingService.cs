using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using SuperTUI.Core.Models;
using SuperTUI.Core.Infrastructure;
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
        private readonly ExcelComReader excelComReader;

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
            this.excelComReader = new ExcelComReader(this.logger);

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

                // Validate all profile mappings reference valid properties
                foreach (var profile in profiles)
                {
                    foreach (var mapping in profile.Mappings)
                    {
                        var prop = typeof(Project).GetProperty(mapping.ProjectPropertyName);
                        if (prop == null)
                        {
                            logger.Error("ExcelMapping", $"Profile '{profile.Name}' has invalid property '{mapping.ProjectPropertyName}'");
                            throw new InvalidOperationException(
                                $"Excel mapping profile '{profile.Name}' references non-existent property '{mapping.ProjectPropertyName}'. " +
                                $"Please fix the profile or update the Project model.");
                        }
                    }
                }
                logger.Info("ExcelMapping", $"All profile mappings validated successfully");

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
                var project = MapCellDataToProject(cellData);

                logger.Info("ExcelMapping", $"Imported project from clipboard: {project.Name}");
                return project;
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Clipboard import failed: {ex.Message}", ex);
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

        /// <summary>
        /// Import project from running Excel instance via COM
        /// </summary>
        public (Project project, string errorMessage) ImportProjectFromExcelCOM(string startCell = "W3")
        {
            if (activeProfile == null)
            {
                return (null, "No active profile selected");
            }

            try
            {
                var (success, cellData, error) = excelComReader.TryReadFromRunningExcel(startCell);

                if (!success)
                {
                    return (null, error);
                }

                // Use existing mapping logic (same as clipboard import)
                var project = MapCellDataToProject(cellData);

                logger.Info("ExcelMapping", $"Imported project from Excel COM: {project.Name}");
                return (project, null);
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"COM import failed: {ex.Message}", ex);
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Import project from Excel file via COM
        /// </summary>
        public (Project project, string errorMessage) ImportProjectFromExcelFile(string filePath, string startCell = "W3", int rowCount = 128)
        {
            if (activeProfile == null)
            {
                return (null, "No active profile selected");
            }

            try
            {
                var (success, cellData, error) = excelComReader.ReadFromFile(filePath, startCell, rowCount);

                if (!success)
                {
                    return (null, error);
                }

                var project = MapCellDataToProject(cellData);

                logger.Info("ExcelMapping", $"Imported project from file: {project.Name}");
                return (project, null);
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"File import failed: {ex.Message}", ex);
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Import project directly from cellData dictionary (useful when data already read via COM)
        /// </summary>
        public Project ImportProjectFromCellData(Dictionary<string, string> cellData)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            if (cellData == null || cellData.Count == 0)
                throw new ArgumentException("Cell data is empty", nameof(cellData));

            try
            {
                var project = MapCellDataToProject(cellData);
                logger.Info("ExcelMapping", $"Imported project from cell data: {project.Name}");
                return project;
            }
            catch (Exception ex)
            {
                logger.Error("ExcelMapping", $"Cell data import failed: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Shared mapping logic: Dictionary<cellRef, value> -> Project
        /// Refactored from ImportProjectFromClipboard
        /// </summary>
        private Project MapCellDataToProject(Dictionary<string, string> cellData)
        {
            var project = new Project();

            foreach (var mapping in activeProfile.Mappings)
            {
                var cellValue = cellData.ContainsKey(mapping.ExcelCellRef)
                    ? cellData[mapping.ExcelCellRef]
                    : "";

                if (string.IsNullOrEmpty(cellValue) && !string.IsNullOrEmpty(mapping.DefaultValue))
                {
                    cellValue = mapping.DefaultValue;
                }

                ExcelExportFormatter.SetProjectValue(project, mapping.ProjectPropertyName, cellValue);
            }

            return project;
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

            // Mappings using ACTUAL Project model property names
            // Based on ProjectModels.cs lines 133-220
            var mappings = new List<(string display, string cell, string prop, string category, bool export)>
            {
                ("TP Name", "W3", "FullProjectName", "Case Info", true),
                ("TP Address", "W4", "Address", "Case Info", false),
                ("TP City", "W6", "City", "Case Info", false),
                ("TP Province/State", "W7", "Province", "Case Info", true),
                ("TP Postal Code", "W8", "PostalCode", "Case Info", false),
                ("Country", "W9", "Country", "Case Info", false),
                ("CAS Case Number", "W17", "ID2", "Case Info", true),
                ("Client ID", "W18", "ClientID", "Case Info", true),
                ("Tax ID", "W19", "TaxID", "Project Info", false),
                ("Audit Type", "W23", "AuditType", "Project Info", true),
                ("Audit Program", "W24", "AuditProgram", "Project Info", true),
                ("Date Received", "W40", "RequestDate", "Dates", true),
                ("Date Assigned", "W41", "DateAssigned", "Dates", true),
                ("Audit Period From", "W42", "AuditPeriodFrom", "Dates", false),
                ("Audit Period To", "W43", "AuditPeriodTo", "Dates", false),
                ("Auditor Name", "W50", "AuditorName", "Assignment", true),
                ("Contact 1 Name", "W60", "Contact1Name", "Contacts", false),
                ("Contact 1 Phone", "W61", "Contact1Phone", "Contacts", false),
                ("Contact 1 Title", "W62", "Contact1Title", "Contacts", false),
                ("Contact 2 Name", "W70", "Contact2Name", "Contacts", false),
                ("Contact 2 Phone", "W71", "Contact2Phone", "Contacts", false),
                ("Accounting Software 1", "W80", "AccountingSoftware1", "Systems", false),
                ("Accounting Software 2", "W81", "AccountingSoftware2", "Systems", false),
                ("TP Email", "W90", "TPEmailAddress", "Contacts", false),
                ("TP Phone", "W91", "TPPhoneNumber", "Contacts", false),
                ("Comments", "W100", "Comments", "Notes", false),
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
