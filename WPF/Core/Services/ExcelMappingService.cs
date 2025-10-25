using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SuperTUI.Core.Models;
using SuperTUI.Core.Infrastructure;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Service for managing Excel field mappings and profiles
    /// </summary>
    public class ExcelMappingService
    {
        private static ExcelMappingService instance;
        public static ExcelMappingService Instance => instance ??= new ExcelMappingService();

        private List<ExcelMappingProfile> profiles;
        private ExcelMappingProfile activeProfile;
        private string profilesDirectory;
        private object lockObject = new object();

        // Events
        public event Action<ExcelMappingProfile> ProfileChanged;
        public event Action<ExcelFieldMapping> MappingChanged;
        public event Action ProfilesLoaded;

        private ExcelMappingService()
        {
            profiles = new List<ExcelMappingProfile>();
        }

        /// <summary>
        /// Initialize the service and load profiles
        /// </summary>
        public void Initialize()
        {
            // Set up profiles directory
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".supertui", "data", "excel-profiles");

            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            profilesDirectory = dataDir;

            // Load existing profiles
            LoadProfiles();

            // Create default profiles if none exist
            if (profiles.Count == 0)
            {
                CreateDefaultProfiles();
            }

            // Set first profile as active if none set
            if (activeProfile == null && profiles.Count > 0)
            {
                activeProfile = profiles[0];
            }

            Logger.Instance.Info("ExcelMappingService", $"Initialized with {profiles.Count} profiles");
        }

        /// <summary>
        /// Load all profiles from disk
        /// </summary>
        public void LoadProfiles()
        {
            lock (lockObject)
            {
                profiles.Clear();

                if (!Directory.Exists(profilesDirectory))
                    return;

                var files = Directory.GetFiles(profilesDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var profile = ExcelMappingProfile.LoadFromJson(file);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("ExcelMappingService", $"Failed to load profile {file}: {ex.Message}");
                    }
                }

                ProfilesLoaded?.Invoke();
            }
        }

        /// <summary>
        /// Save a profile to disk
        /// </summary>
        public void SaveProfile(ExcelMappingProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            lock (lockObject)
            {
                profile.ModifiedDate = DateTime.Now;

                // Update in memory collection
                var existing = profiles.FirstOrDefault(p => p.Id == profile.Id);
                if (existing != null)
                {
                    profiles.Remove(existing);
                }
                profiles.Add(profile);

                // Save to disk
                string fileName = SanitizeFileName(profile.Name) + ".json";
                string filePath = Path.Combine(profilesDirectory, fileName);
                profile.SaveToJson(filePath);

                Logger.Instance.Info("ExcelMappingService", $"Saved profile: {profile.Name}");
                ProfileChanged?.Invoke(profile);
            }
        }

        /// <summary>
        /// Delete a profile
        /// </summary>
        public void DeleteProfile(Guid id)
        {
            lock (lockObject)
            {
                var profile = profiles.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                    return;

                profiles.Remove(profile);

                // Delete file
                string fileName = SanitizeFileName(profile.Name) + ".json";
                string filePath = Path.Combine(profilesDirectory, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Change active profile if deleted
                if (activeProfile?.Id == id)
                {
                    activeProfile = profiles.FirstOrDefault();
                    ProfileChanged?.Invoke(activeProfile);
                }

                Logger.Instance.Info("ExcelMappingService", $"Deleted profile: {profile.Name}");
            }
        }

        /// <summary>
        /// Set active profile
        /// </summary>
        public void SetActiveProfile(Guid id)
        {
            lock (lockObject)
            {
                var profile = profiles.FirstOrDefault(p => p.Id == id);
                if (profile != null)
                {
                    activeProfile = profile;
                    ProfileChanged?.Invoke(activeProfile);
                    Logger.Instance.Info("ExcelMappingService", $"Active profile: {profile.Name}");
                }
            }
        }

        /// <summary>
        /// Get all profiles
        /// </summary>
        public List<ExcelMappingProfile> GetAllProfiles()
        {
            lock (lockObject)
            {
                return new List<ExcelMappingProfile>(profiles);
            }
        }

        /// <summary>
        /// Get active profile
        /// </summary>
        public ExcelMappingProfile GetActiveProfile()
        {
            lock (lockObject)
            {
                return activeProfile;
            }
        }

        /// <summary>
        /// Add a mapping to active profile
        /// </summary>
        public void AddMapping(ExcelFieldMapping mapping)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            lock (lockObject)
            {
                activeProfile.Mappings.Add(mapping);
                activeProfile.ModifiedDate = DateTime.Now;
                MappingChanged?.Invoke(mapping);
            }
        }

        /// <summary>
        /// Update a mapping in active profile
        /// </summary>
        public void UpdateMapping(ExcelFieldMapping mapping)
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            lock (lockObject)
            {
                var existing = activeProfile.Mappings.FirstOrDefault(m => m.Id == mapping.Id);
                if (existing != null)
                {
                    int index = activeProfile.Mappings.IndexOf(existing);
                    activeProfile.Mappings[index] = mapping;
                    activeProfile.ModifiedDate = DateTime.Now;
                    MappingChanged?.Invoke(mapping);
                }
            }
        }

        /// <summary>
        /// Delete a mapping from active profile
        /// </summary>
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
                    activeProfile.ModifiedDate = DateTime.Now;
                    MappingChanged?.Invoke(mapping);
                }
            }
        }

        /// <summary>
        /// Reorder a mapping in active profile
        /// </summary>
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
                    mapping.ModifiedDate = DateTime.Now;
                    activeProfile.ModifiedDate = DateTime.Now;
                    MappingChanged?.Invoke(mapping);
                }
            }
        }

        /// <summary>
        /// Get mappings by category from active profile
        /// </summary>
        public List<ExcelFieldMapping> GetMappingsByCategory(string category)
        {
            if (activeProfile == null)
                return new List<ExcelFieldMapping>();

            lock (lockObject)
            {
                return activeProfile.GetMappingsByCategory(category);
            }
        }

        /// <summary>
        /// Get mappings marked for export from active profile
        /// </summary>
        public List<ExcelFieldMapping> GetExportMappings()
        {
            if (activeProfile == null)
                return new List<ExcelFieldMapping>();

            lock (lockObject)
            {
                return activeProfile.GetExportMappings();
            }
        }

        /// <summary>
        /// Import project from clipboard data using active profile
        /// </summary>
        public Project ImportProjectFromClipboard(string clipboardData, string startCell = "W3")
        {
            if (activeProfile == null)
                throw new InvalidOperationException("No active profile");

            if (string.IsNullOrEmpty(clipboardData))
                throw new ArgumentException("Clipboard data is empty");

            // Parse clipboard data
            var parsedData = ClipboardDataParser.ParseTSV(clipboardData, startCell);

            // Create new project
            var project = new Project();

            // Map each field
            foreach (var mapping in activeProfile.Mappings)
            {
                string value = ClipboardDataParser.GetCellValue(parsedData, mapping.ExcelCellRef);

                if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(mapping.DefaultValue))
                {
                    value = mapping.DefaultValue;
                }

                SetProjectProperty(project, mapping.ProjectPropertyName, value, mapping.DataType);
            }

            Logger.Instance.Info("ExcelMappingService", $"Imported project: {project.Nickname}");
            return project;
        }

        /// <summary>
        /// Import multiple projects from clipboard (multiple columns)
        /// </summary>
        public List<Project> ImportMultipleProjectsFromClipboard(string clipboardData, string startCell = "W3")
        {
            // For now, just import single project
            // TODO: Implement multi-column detection and import
            var project = ImportProjectFromClipboard(clipboardData, startCell);
            return new List<Project> { project };
        }

        /// <summary>
        /// Export projects to clipboard in specified format
        /// </summary>
        public void ExportToClipboard(List<Project> projects, string format)
        {
            string exportData = ExportToString(projects, format);

            if (!string.IsNullOrEmpty(exportData))
            {
                System.Windows.Clipboard.SetText(exportData);
                Logger.Instance.Info("ExcelMappingService", $"Exported {projects.Count} projects to clipboard as {format}");
            }
        }

        /// <summary>
        /// Export projects to file in specified format
        /// </summary>
        public void ExportToFile(List<Project> projects, string filePath, string format)
        {
            string exportData = ExportToString(projects, format);

            if (!string.IsNullOrEmpty(exportData))
            {
                File.WriteAllText(filePath, exportData, Encoding.UTF8);
                Logger.Instance.Info("ExcelMappingService", $"Exported {projects.Count} projects to {filePath} as {format}");
            }
        }

        /// <summary>
        /// Export projects to string in specified format
        /// </summary>
        public string ExportToString(List<Project> projects, string format)
        {
            var exportMappings = GetExportMappings();

            if (exportMappings.Count == 0)
            {
                Logger.Instance.Warning("ExcelMappingService", "No export mappings configured");
                return string.Empty;
            }

            switch (format.ToLower())
            {
                case "csv":
                    return ExcelExportFormatter.ToCsv(projects, exportMappings);
                case "tsv":
                    return ExcelExportFormatter.ToTsv(projects, exportMappings);
                case "json":
                    return ExcelExportFormatter.ToJson(projects, exportMappings);
                case "xml":
                    return ExcelExportFormatter.ToXml(projects, exportMappings);
                case "txt":
                    return ExcelExportFormatter.ToFixedWidth(projects, exportMappings);
                default:
                    throw new ArgumentException($"Unsupported export format: {format}");
            }
        }

        /// <summary>
        /// Create default profiles on first run
        /// </summary>
        private void CreateDefaultProfiles()
        {
            // SVI-CAS Standard profile (48 fields)
            var sviCasProfile = CreateSviCasStandardProfile();
            SaveProfile(sviCasProfile);

            // Project Essentials profile (15 fields)
            var essentialsProfile = CreateProjectEssentialsProfile();
            SaveProfile(essentialsProfile);

            Logger.Instance.Info("ExcelMappingService", "Created default profiles");
        }

        /// <summary>
        /// Create the SVI-CAS Standard profile with all 48 fields
        /// </summary>
        private ExcelMappingProfile CreateSviCasStandardProfile()
        {
            var profile = new ExcelMappingProfile
            {
                Name = "SVI-CAS Standard",
                Description = "Complete 48-field audit request form import (Service Canada Audit System)"
            };

            // Project Info category (11 fields)
            profile.Mappings.Add(CreateMapping("RequestDate", "W23", "RequestDate", "Project Info", "DateTime", true, 1, true));
            profile.Mappings.Add(CreateMapping("AuditType", "W78", "AuditType", "Project Info", "String", true, 2, true));
            profile.Mappings.Add(CreateMapping("AuditorName", "W10", "AuditorName", "Project Info", "String", true, 3, true));
            profile.Mappings.Add(CreateMapping("AuditCase", "W18", "ID1", "Project Info", "String", true, 4, true));
            profile.Mappings.Add(CreateMapping("CASCase", "W17", "ID2", "Project Info", "String", true, 5, true)); // Critical ID2
            profile.Mappings.Add(CreateMapping("AuditStartDate", "W24", "DateAssigned", "Project Info", "DateTime", true, 6, true));
            profile.Mappings.Add(CreateMapping("AuditProgram", "W72", "AuditProgram", "Project Info", "String", true, 7, true));
            profile.Mappings.Add(CreateMapping("TPNum", "W4", "ClientID", "Project Info", "String", false, 8, false));
            profile.Mappings.Add(CreateMapping("Comments", "W108", "Comments", "Project Info", "String", false, 9, false));
            profile.Mappings.Add(CreateMapping("FXInfo", "W129", "FXInfo", "Project Info", "String", false, 10, false));
            profile.Mappings.Add(CreateMapping("ShipToAddress", "W130", "ShipToAddress", "Project Info", "String", false, 11, false));

            // Contact Details category (7 fields)
            profile.Mappings.Add(CreateMapping("TPName", "W3", "FullProjectName", "Contact Details", "String", true, 12, true));
            profile.Mappings.Add(CreateMapping("Address", "W5", "Address", "Contact Details", "String", true, 13, true));
            profile.Mappings.Add(CreateMapping("City", "W6", "City", "Contact Details", "String", true, 14, true));
            profile.Mappings.Add(CreateMapping("Province", "W7", "Province", "Contact Details", "String", true, 15, true));
            profile.Mappings.Add(CreateMapping("PostalCode", "W8", "PostalCode", "Contact Details", "String", false, 16, false));
            profile.Mappings.Add(CreateMapping("Country", "W9", "Country", "Contact Details", "String", false, 17, false));
            profile.Mappings.Add(CreateMapping("TaxID", "W13", "TaxID", "Contact Details", "String", false, 18, false));

            // Audit Periods category (10 fields)
            profile.Mappings.Add(CreateMapping("AuditPeriodFrom", "W27", "AuditPeriodFrom", "Audit Periods", "DateTime", true, 19, true));
            profile.Mappings.Add(CreateMapping("AuditPeriodTo", "W28", "AuditPeriodTo", "Audit Periods", "DateTime", true, 20, true));
            profile.Mappings.Add(CreateMapping("AuditPeriod1Start", "W29", "AuditPeriod1Start", "Audit Periods", "DateTime", false, 21, false));
            profile.Mappings.Add(CreateMapping("AuditPeriod1End", "W30", "AuditPeriod1End", "Audit Periods", "DateTime", false, 22, false));
            profile.Mappings.Add(CreateMapping("AuditPeriod2Start", "W31", "AuditPeriod2Start", "Audit Periods", "DateTime", false, 23, false));
            profile.Mappings.Add(CreateMapping("AuditPeriod2End", "W32", "AuditPeriod2End", "Audit Periods", "DateTime", false, 24, false));
            profile.Mappings.Add(CreateMapping("AuditPeriod3Start", "W33", "AuditPeriod3Start", "Audit Periods", "DateTime", false, 25, false));
            profile.Mappings.Add(CreateMapping("AuditPeriod3End", "W34", "AuditPeriod3End", "Audit Periods", "DateTime", false, 26, false));
            profile.Mappings.Add(CreateMapping("AuditPeriod4Start", "W35", "AuditPeriod4Start", "Audit Periods", "DateTime", false, 27, false));
            profile.Mappings.Add(CreateMapping("AuditPeriod4End", "W36", "AuditPeriod4End", "Audit Periods", "DateTime", false, 28, false));

            // Contacts category (10 fields)
            profile.Mappings.Add(CreateMapping("Contact1Name", "W54", "Contact1Name", "Contacts", "String", true, 29, true));
            profile.Mappings.Add(CreateMapping("Contact1Phone", "W55", "Contact1Phone", "Contacts", "String", false, 30, false));
            profile.Mappings.Add(CreateMapping("Contact1Ext", "W56", "Contact1Ext", "Contacts", "String", false, 31, false));
            profile.Mappings.Add(CreateMapping("Contact1Address", "W57", "Contact1Address", "Contacts", "String", false, 32, false));
            profile.Mappings.Add(CreateMapping("Contact1Title", "W58", "Contact1Title", "Contacts", "String", false, 33, false));
            profile.Mappings.Add(CreateMapping("Contact2Name", "W59", "Contact2Name", "Contacts", "String", false, 34, false));
            profile.Mappings.Add(CreateMapping("Contact2Phone", "W60", "Contact2Phone", "Contacts", "String", false, 35, false));
            profile.Mappings.Add(CreateMapping("Contact2Ext", "W61", "Contact2Ext", "Contacts", "String", false, 36, false));
            profile.Mappings.Add(CreateMapping("Contact2Address", "W62", "Contact2Address", "Contacts", "String", false, 37, false));
            profile.Mappings.Add(CreateMapping("Contact2Title", "W63", "Contact2Title", "Contacts", "String", false, 38, false));

            // System Info category (6 fields)
            profile.Mappings.Add(CreateMapping("AccountingSoftware1", "W98", "AccountingSoftware1", "System Info", "String", false, 39, false));
            profile.Mappings.Add(CreateMapping("AccountingSoftware1Other", "W100", "AccountingSoftware1Other", "System Info", "String", false, 40, false));
            profile.Mappings.Add(CreateMapping("AccountingSoftware1Type", "W101", "AccountingSoftware1Type", "System Info", "String", false, 41, false));
            profile.Mappings.Add(CreateMapping("AccountingSoftware2", "W102", "AccountingSoftware2", "System Info", "String", false, 42, false));
            profile.Mappings.Add(CreateMapping("AccountingSoftware2Other", "W104", "AccountingSoftware2Other", "System Info", "String", false, 43, false));
            profile.Mappings.Add(CreateMapping("AccountingSoftware2Type", "W105", "AccountingSoftware2Type", "System Info", "String", false, 44, false));

            // Additional fields (4 fields)
            profile.Mappings.Add(CreateMapping("TPEmailAddress", "X3", "TPEmailAddress", "Additional", "String", false, 45, false));
            profile.Mappings.Add(CreateMapping("TPPhoneNumber", "Y3", "TPPhoneNumber", "Additional", "String", false, 46, false));
            profile.Mappings.Add(CreateMapping("CASNumber", "G17", "CASNumber", "Additional", "String", false, 47, false));
            profile.Mappings.Add(CreateMapping("EmailReference", "W106", "EmailReference", "Additional", "String", false, 48, false));

            return profile;
        }

        /// <summary>
        /// Create Project Essentials profile (15 key fields)
        /// </summary>
        private ExcelMappingProfile CreateProjectEssentialsProfile()
        {
            var profile = new ExcelMappingProfile
            {
                Name = "Project Essentials",
                Description = "15 key fields for quick project creation"
            };

            profile.Mappings.Add(CreateMapping("CASCase", "W17", "ID2", "Project Info", "String", true, 1, true));
            profile.Mappings.Add(CreateMapping("TPName", "W3", "FullProjectName", "Project Info", "String", true, 2, true));
            profile.Mappings.Add(CreateMapping("RequestDate", "W23", "RequestDate", "Project Info", "DateTime", true, 3, true));
            profile.Mappings.Add(CreateMapping("AuditType", "W78", "AuditType", "Project Info", "String", true, 4, true));
            profile.Mappings.Add(CreateMapping("AuditorName", "W10", "AuditorName", "Project Info", "String", true, 5, true));
            profile.Mappings.Add(CreateMapping("TPNum", "W4", "ClientID", "Project Info", "String", true, 6, true));
            profile.Mappings.Add(CreateMapping("Address", "W5", "Address", "Contact Details", "String", true, 7, true));
            profile.Mappings.Add(CreateMapping("City", "W6", "City", "Contact Details", "String", true, 8, true));
            profile.Mappings.Add(CreateMapping("Province", "W7", "Province", "Contact Details", "String", true, 9, true));
            profile.Mappings.Add(CreateMapping("AuditStartDate", "W24", "DateAssigned", "Project Info", "DateTime", true, 10, true));
            profile.Mappings.Add(CreateMapping("Contact1Name", "W54", "Contact1Name", "Contacts", "String", true, 11, true));
            profile.Mappings.Add(CreateMapping("Contact1Phone", "W55", "Contact1Phone", "Contacts", "String", true, 12, true));
            profile.Mappings.Add(CreateMapping("AuditCase", "W18", "ID1", "Project Info", "String", true, 13, true));
            profile.Mappings.Add(CreateMapping("AuditProgram", "W72", "AuditProgram", "Project Info", "String", true, 14, true));
            profile.Mappings.Add(CreateMapping("AuditPeriodFrom", "W27", "AuditPeriodFrom", "Audit Periods", "DateTime", true, 15, true));

            return profile;
        }

        /// <summary>
        /// Helper to create a mapping
        /// </summary>
        private ExcelFieldMapping CreateMapping(string displayName, string cellRef, string propertyName,
            string category, string dataType, bool includeExport, int sortOrder, bool required)
        {
            return new ExcelFieldMapping
            {
                DisplayName = displayName,
                ExcelCellRef = cellRef,
                ProjectPropertyName = propertyName,
                Category = category,
                DataType = dataType,
                IncludeInExport = includeExport,
                SortOrder = sortOrder,
                Required = required
            };
        }

        /// <summary>
        /// Set project property value using reflection
        /// </summary>
        private void SetProjectProperty(Project project, string propertyName, string value, string dataType)
        {
            if (string.IsNullOrEmpty(propertyName))
                return;

            var prop = typeof(ProjectModels.Project).GetProperty(propertyName);
            if (prop == null || !prop.CanWrite)
                return;

            try
            {
                object convertedValue = null;

                if (string.IsNullOrEmpty(value))
                {
                    // Leave as default
                    return;
                }

                switch (dataType)
                {
                    case "String":
                        convertedValue = value;
                        break;
                    case "DateTime":
                        if (DateTime.TryParse(value, out DateTime dt))
                            convertedValue = dt;
                        else if (double.TryParse(value, out double oadate))
                            convertedValue = DateTime.FromOADate(oadate); // Excel date format
                        break;
                    case "Int32":
                        if (int.TryParse(value, out int i))
                            convertedValue = i;
                        break;
                    case "Decimal":
                        if (decimal.TryParse(value, out decimal d))
                            convertedValue = d;
                        break;
                    default:
                        convertedValue = value;
                        break;
                }

                if (convertedValue != null)
                {
                    prop.SetValue(project, convertedValue);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning("ExcelMappingService", $"Failed to set {propertyName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sanitize filename for safe file system use
        /// </summary>
        private string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder();

            foreach (char c in name)
            {
                if (!invalid.Contains(c))
                    sanitized.Append(c);
                else
                    sanitized.Append('_');
            }

            return sanitized.ToString();
        }
    }
}
