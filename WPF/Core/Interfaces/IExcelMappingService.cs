using System;
using System.Collections.Generic;
using SuperTUI.Core.Models;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for Excel mapping service
    /// Matches actual ExcelMappingService implementation
    /// </summary>
    public interface IExcelMappingService
    {
        // Events
        event Action<ExcelMappingProfile> ProfileChanged;
        event Action ProfilesLoaded;

        // Initialization
        void Initialize();

        // Profile management
        List<ExcelMappingProfile> GetAllProfiles();
        ExcelMappingProfile GetActiveProfile();
        void SaveProfile(ExcelMappingProfile profile);
        void DeleteProfile(Guid id);
        void SetActiveProfile(Guid id);
        void LoadProfiles();

        // Mapping management
        void AddMapping(ExcelFieldMapping mapping);
        void UpdateMapping(ExcelFieldMapping mapping);
        void DeleteMapping(Guid id);
        void ReorderMapping(Guid id, int newSortOrder);
        List<ExcelFieldMapping> GetMappingsByCategory(string category);
        List<ExcelFieldMapping> GetExportMappings();

        // Import operations
        Project ImportProjectFromClipboard(string clipboardData, string startCell = "W3");
        List<Project> ImportMultipleProjectsFromClipboard(string clipboardData, string startCell = "W3");

        // Export operations
        void ExportToClipboard(List<Project> projects, string format);
        void ExportToFile(List<Project> projects, string filePath, string format);
        string ExportToString(List<Project> projects, string format);
    }
}
