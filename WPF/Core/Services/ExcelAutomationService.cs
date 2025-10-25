using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SuperTUI.Core.Models;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Service for automated Excel-to-Excel data copying using COM automation
    /// </summary>
    public class ExcelAutomationService
    {
        private static ExcelAutomationService instance;
        public static ExcelAutomationService Instance => instance ??= new ExcelAutomationService();

        // Events for progress reporting
        public event Action<string> StatusChanged;
        public event Action<int, int> ProgressChanged; // current, total

        private ExcelAutomationService()
        {
        }

        /// <summary>
        /// Copy data from source Excel file to destination Excel file using active profile mappings
        /// </summary>
        public bool CopyExcelToExcel(string sourceFilePath, string destFilePath, string sourceSheetName = "SVI-CAS", string destSheetName = "Output")
        {
            object excelApp = null;
            object sourceWorkbook = null;
            object destWorkbook = null;

            try
            {
                ReportStatus("Initializing Excel...");

                // Create Excel application instance
                Type excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    ReportStatus("ERROR: Excel is not installed on this system");
                    return false;
                }

                excelApp = Activator.CreateInstance(excelType);
                excelType.InvokeMember("Visible", System.Reflection.BindingFlags.SetProperty, null, excelApp, new object[] { false });

                ReportStatus($"Opening source file: {System.IO.Path.GetFileName(sourceFilePath)}");

                // Open source workbook (read-only)
                object workbooks = excelType.InvokeMember("Workbooks", System.Reflection.BindingFlags.GetProperty, null, excelApp, null);
                sourceWorkbook = workbooks.GetType().InvokeMember("Open",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    workbooks,
                    new object[] { sourceFilePath, Type.Missing, true }); // true = read-only

                // Get source worksheet
                object sourceSheets = sourceWorkbook.GetType().InvokeMember("Worksheets", System.Reflection.BindingFlags.GetProperty, null, sourceWorkbook, null);
                object sourceSheet = null;

                try
                {
                    sourceSheet = sourceSheets.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, sourceSheets, new object[] { sourceSheetName });
                }
                catch
                {
                    ReportStatus($"ERROR: Worksheet '{sourceSheetName}' not found in source file");
                    return false;
                }

                ReportStatus($"Opening destination file: {System.IO.Path.GetFileName(destFilePath)}");

                // Open destination workbook
                destWorkbook = workbooks.GetType().InvokeMember("Open",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    workbooks,
                    new object[] { destFilePath, Type.Missing, false }); // false = not read-only

                // Get destination worksheet
                object destSheets = destWorkbook.GetType().InvokeMember("Worksheets", System.Reflection.BindingFlags.GetProperty, null, destWorkbook, null);
                object destSheet = null;

                try
                {
                    destSheet = destSheets.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, destSheets, new object[] { destSheetName });
                }
                catch
                {
                    ReportStatus($"ERROR: Worksheet '{destSheetName}' not found in destination file");
                    return false;
                }

                // Get active profile mappings
                var profile = ExcelMappingService.Instance.GetActiveProfile();
                if (profile == null)
                {
                    ReportStatus("ERROR: No active profile selected");
                    return false;
                }

                ReportStatus($"Using profile: {profile.Name} ({profile.Mappings.Count} field mappings)");
                ReportStatus("Copying data...");

                // Copy each field according to mappings
                int current = 0;
                int total = profile.Mappings.Count;
                int copiedCount = 0;

                foreach (var mapping in profile.Mappings)
                {
                    current++;
                    ReportProgress(current, total);

                    try
                    {
                        // Read value from source cell
                        string value = GetCellValue(sourceSheet, mapping.ExcelCellRef);

                        // Write to destination cell (assuming sequential output A1, A2, A3...)
                        string destCell = $"A{mapping.SortOrder}";
                        SetCellValue(destSheet, destCell, value);

                        copiedCount++;
                    }
                    catch (Exception ex)
                    {
                        ReportStatus($"Warning: Failed to copy {mapping.DisplayName} from {mapping.ExcelCellRef}: {ex.Message}");
                    }
                }

                ReportStatus($"Saving destination file...");

                // Save destination workbook
                destWorkbook.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, destWorkbook, null);

                ReportStatus($"SUCCESS: Copied {copiedCount} of {total} fields");

                SuperTUI.Infrastructure.Logger.Instance.Info("ExcelAutomationService", $"Copied {copiedCount} fields from {sourceFilePath} to {destFilePath}");

                return true;
            }
            catch (Exception ex)
            {
                ReportStatus($"ERROR: {ex.Message}");
                SuperTUI.Infrastructure.Logger.Instance.Error("ExcelAutomationService", $"Excel automation failed: {ex.Message}");
                return false;
            }
            finally
            {
                // Close workbooks and release COM objects
                try
                {
                    if (sourceWorkbook != null)
                    {
                        sourceWorkbook.GetType().InvokeMember("Close", System.Reflection.BindingFlags.InvokeMethod, null, sourceWorkbook, new object[] { false });
                        Marshal.ReleaseComObject(sourceWorkbook);
                    }

                    if (destWorkbook != null)
                    {
                        destWorkbook.GetType().InvokeMember("Close", System.Reflection.BindingFlags.InvokeMethod, null, destWorkbook, new object[] { true }); // save changes
                        Marshal.ReleaseComObject(destWorkbook);
                    }

                    if (excelApp != null)
                    {
                        excelApp.GetType().InvokeMember("Quit", System.Reflection.BindingFlags.InvokeMethod, null, excelApp, null);
                        Marshal.ReleaseComObject(excelApp);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// Batch process multiple source files to single destination
        /// </summary>
        public int BatchCopyExcelToExcel(List<string> sourceFiles, string destFilePath, string sourceSheetName = "SVI-CAS", string destSheetName = "Output")
        {
            int successCount = 0;
            int fileCount = 0;

            foreach (var sourceFile in sourceFiles)
            {
                fileCount++;
                ReportStatus($"Processing file {fileCount} of {sourceFiles.Count}: {System.IO.Path.GetFileName(sourceFile)}");

                bool success = CopyExcelToExcel(sourceFile, destFilePath, sourceSheetName, destSheetName);
                if (success)
                {
                    successCount++;
                }

                // Brief pause between files
                System.Threading.Thread.Sleep(500);
            }

            ReportStatus($"Batch complete: {successCount} of {sourceFiles.Count} files processed successfully");
            return successCount;
        }

        /// <summary>
        /// Get cell value from worksheet
        /// </summary>
        private string GetCellValue(object worksheet, string cellRef)
        {
            try
            {
                object range = worksheet.GetType().InvokeMember("Range", System.Reflection.BindingFlags.GetProperty, null, worksheet, new object[] { cellRef });
                object value = range.GetType().InvokeMember("Value", System.Reflection.BindingFlags.GetProperty, null, range, null);

                Marshal.ReleaseComObject(range);

                if (value == null)
                    return string.Empty;

                // Handle Excel date values (OADate format)
                if (value is double doubleValue)
                {
                    try
                    {
                        DateTime date = DateTime.FromOADate(doubleValue);
                        return date.ToString("yyyy-MM-dd");
                    }
                    catch
                    {
                        return doubleValue.ToString();
                    }
                }

                return value.ToString();
            }
            catch (Exception ex)
            {
                SuperTUI.Infrastructure.Logger.Instance.Warning("ExcelAutomationService", $"Failed to read {cellRef}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Set cell value in worksheet
        /// </summary>
        private void SetCellValue(object worksheet, string cellRef, string value)
        {
            try
            {
                object range = worksheet.GetType().InvokeMember("Range", System.Reflection.BindingFlags.GetProperty, null, worksheet, new object[] { cellRef });
                range.GetType().InvokeMember("Value", System.Reflection.BindingFlags.SetProperty, null, range, new object[] { value });
                Marshal.ReleaseComObject(range);
            }
            catch (Exception ex)
            {
                SuperTUI.Infrastructure.Logger.Instance.Warning("ExcelAutomationService", $"Failed to write {cellRef}: {ex.Message}");
            }
        }

        /// <summary>
        /// Report status change
        /// </summary>
        private void ReportStatus(string status)
        {
            StatusChanged?.Invoke(status);
            SuperTUI.Infrastructure.Logger.Instance.Info("ExcelAutomation", status);
        }

        /// <summary>
        /// Report progress change
        /// </summary>
        private void ReportProgress(int current, int total)
        {
            ProgressChanged?.Invoke(current, total);
        }
    }
}
