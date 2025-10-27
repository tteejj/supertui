using System;
using System.Collections.Generic;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for Excel automation service
    /// Matches actual ExcelAutomationService implementation
    /// </summary>
    public interface IExcelAutomationService
    {
        // Events
        event Action<string> StatusChanged;
        event Action<int, int> ProgressChanged; // current, total

        // Excel operations
        bool CopyExcelToExcel(string sourceFilePath, string destFilePath, string sourceSheetName = "SVI-CAS", string destSheetName = "Output");
        int BatchCopyExcelToExcel(List<string> sourceFiles, string destFilePath, string sourceSheetName = "SVI-CAS", string destSheetName = "Output");
    }
}
