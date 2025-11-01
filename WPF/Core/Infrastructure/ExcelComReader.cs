using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Excel;
using SuperTUI.Infrastructure;
using SuperTUI.Core.Models;

namespace SuperTUI.Core.Infrastructure
{
    /// <summary>
    /// Reads Excel cells via COM automation (no clipboard needed)
    /// Supports reading from running Excel instance or opening files directly
    /// </summary>
    public class ExcelComReader : IDisposable
    {
        // P/Invoke for GetActiveObject (not available in .NET Core by default)
        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        private readonly ILogger logger;
        private Application excelApp;
        private Workbook workbook;
        private bool ownsExcelInstance;

        public ExcelComReader(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.ownsExcelInstance = false;
        }

        /// <summary>
        /// Attempt to read from currently running Excel instance
        /// Returns cell data as Dictionary<"W3", "value"> (same format as TSV parser)
        /// </summary>
        public (bool success, Dictionary<string, string> cellData, string errorMessage)
            TryReadFromRunningExcel(string startCell)
        {
            try
            {
                // Get running Excel instance using P/Invoke
                Guid excelClsid = new Guid("00024500-0000-0000-C000-000000000046"); // Excel.Application CLSID
                GetActiveObject(ref excelClsid, IntPtr.Zero, out object excelObj);
                excelApp = (Application)excelObj;
                ownsExcelInstance = false; // We don't own this instance

                // Get active selection
                var selection = excelApp.Selection as Microsoft.Office.Interop.Excel.Range;
                if (selection == null)
                {
                    return (false, null, "No cells selected in Excel");
                }

                // Read cells starting from startCell reference
                var cellData = ReadRangeToDict(selection, startCell);

                logger.Info("ExcelComReader", $"Read {cellData.Count} cells from running Excel");
                return (true, cellData, null);
            }
            catch (COMException ex) when (ex.HResult == -2147221021) // 0x800401E3 - Excel not running
            {
                return (false, null, "Excel is not running");
            }
            catch (Exception ex)
            {
                logger.Error("ExcelComReader", $"Failed to read from Excel: {ex.Message}", ex);
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Open Excel file and read specified range
        /// </summary>
        public (bool success, Dictionary<string, string> cellData, string errorMessage)
            ReadFromFile(string filePath, string startCell, int rowCount)
        {
            try
            {
                // Create new Excel instance
                excelApp = new Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;
                ownsExcelInstance = true; // We own this instance

                // Open workbook
                workbook = excelApp.Workbooks.Open(filePath, ReadOnly: true);
                var worksheet = (Worksheet)workbook.Sheets[1];

                // Parse start cell (e.g., "W3" -> column 23, row 3)
                var (col, row) = ClipboardDataParser.ParseCellReference(startCell);

                // Read cells W3:W130 (or specified range)
                var range = worksheet.Range[worksheet.Cells[row, col], worksheet.Cells[row + rowCount - 1, col]];

                var cellData = ReadRangeToDict(range, startCell);

                logger.Info("ExcelComReader", $"Read {cellData.Count} cells from file: {filePath}");
                return (true, cellData, null);
            }
            catch (Exception ex)
            {
                logger.Error("ExcelComReader", $"Failed to read Excel file: {ex.Message}", ex);
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Open Excel file and read specified rectangular range
        /// </summary>
        public (bool success, Dictionary<string, string> cellData, string errorMessage)
            ReadRangeFromFile(string filePath, string startCell, string endCell)
        {
            try
            {
                // Create new Excel instance
                excelApp = new Application();
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;
                ownsExcelInstance = true;

                // Open workbook
                workbook = excelApp.Workbooks.Open(filePath, ReadOnly: true);
                var worksheet = (Worksheet)workbook.Sheets[1];

                // Parse cell references
                var (startCol, startRow) = ClipboardDataParser.ParseCellReference(startCell);
                var (endCol, endRow) = ClipboardDataParser.ParseCellReference(endCell);

                // Read range
                var range = worksheet.Range[worksheet.Cells[startRow, startCol], worksheet.Cells[endRow, endCol]];

                var cellData = ReadRangeToDict(range, startCell);

                logger.Info("ExcelComReader", $"Read {cellData.Count} cells from file: {filePath}");
                return (true, cellData, null);
            }
            catch (Exception ex)
            {
                logger.Error("ExcelComReader", $"Failed to read Excel file: {ex.Message}", ex);
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Convert Excel Range to Dictionary<cellRef, value>
        /// </summary>
        private Dictionary<string, string> ReadRangeToDict(Microsoft.Office.Interop.Excel.Range range, string startCell)
        {
            var result = new Dictionary<string, string>();
            var (startCol, startRow) = ClipboardDataParser.ParseCellReference(startCell);

            // Range is 1-indexed
            for (int rowOffset = 0; rowOffset < range.Rows.Count; rowOffset++)
            {
                for (int colOffset = 0; colOffset < range.Columns.Count; colOffset++)
                {
                    var cell = range.Cells[rowOffset + 1, colOffset + 1] as Microsoft.Office.Interop.Excel.Range;
                    var value = cell?.Value2?.ToString() ?? "";

                    int col = startCol + colOffset;
                    int row = startRow + rowOffset;
                    string cellRef = ClipboardDataParser.GetCellReference(col, row);

                    result[cellRef] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Cleanup COM objects
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Close workbook if we opened one
                if (workbook != null)
                {
                    workbook.Close(false);
                    Marshal.ReleaseComObject(workbook);
                    workbook = null;
                }

                // Quit Excel if we created the instance
                if (excelApp != null && ownsExcelInstance)
                {
                    excelApp.Quit();
                    Marshal.ReleaseComObject(excelApp);
                    excelApp = null;
                }

                // Force garbage collection to release COM objects
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch (Exception ex)
            {
                logger?.Error("ExcelComReader", $"Error during disposal: {ex.Message}", ex);
            }
        }
    }
}
