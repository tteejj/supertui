using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace SuperTUI.Core.Models
{
    /// <summary>
    /// Represents a single field mapping from Excel to Project model
    /// </summary>
    public class ExcelFieldMapping
    {
        public Guid Id { get; set; }
        public string DisplayName { get; set; }
        public string ExcelCellRef { get; set; }
        public string ProjectPropertyName { get; set; }
        public string Category { get; set; }
        public string DataType { get; set; }
        public bool IncludeInExport { get; set; }
        public int SortOrder { get; set; }
        public bool Required { get; set; }
        public string DefaultValue { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        [JsonIgnore]
        public string PreviewValue { get; set; }

        public ExcelFieldMapping()
        {
            Id = Guid.NewGuid();
            DisplayName = string.Empty;
            ExcelCellRef = string.Empty;
            ProjectPropertyName = string.Empty;
            Category = "General";
            DataType = "String";
            IncludeInExport = false;
            SortOrder = 0;
            Required = false;
            DefaultValue = null;
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
            PreviewValue = string.Empty;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(DisplayName) &&
                   !string.IsNullOrEmpty(ExcelCellRef) &&
                   !string.IsNullOrEmpty(ProjectPropertyName);
        }

        public ExcelFieldMapping Clone()
        {
            return new ExcelFieldMapping
            {
                Id = Guid.NewGuid(),
                DisplayName = this.DisplayName,
                ExcelCellRef = this.ExcelCellRef,
                ProjectPropertyName = this.ProjectPropertyName,
                Category = this.Category,
                DataType = this.DataType,
                IncludeInExport = this.IncludeInExport,
                SortOrder = this.SortOrder,
                Required = this.Required,
                DefaultValue = this.DefaultValue,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            };
        }
    }

    /// <summary>
    /// Profile containing a set of field mappings for import/export
    /// </summary>
    public class ExcelMappingProfile
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<ExcelFieldMapping> Mappings { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        public ExcelMappingProfile()
        {
            Id = Guid.NewGuid();
            Name = "Untitled Profile";
            Description = string.Empty;
            Mappings = new List<ExcelFieldMapping>();
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
        }

        public void SaveToJson(string filePath)
        {
            ModifiedDate = DateTime.Now;
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        public static ExcelMappingProfile LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<ExcelMappingProfile>(json);
        }

        public ExcelMappingProfile Clone()
        {
            return new ExcelMappingProfile
            {
                Id = Guid.NewGuid(),
                Name = this.Name + " (Copy)",
                Description = this.Description,
                Mappings = this.Mappings.Select(m => m.Clone()).ToList(),
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            };
        }

        public List<ExcelFieldMapping> GetExportMappings()
        {
            return Mappings.Where(m => m.IncludeInExport).OrderBy(m => m.SortOrder).ToList();
        }

        public List<ExcelFieldMapping> GetMappingsByCategory(string category)
        {
            return Mappings.Where(m => m.Category == category).OrderBy(m => m.SortOrder).ToList();
        }

        public List<string> GetCategories()
        {
            return Mappings.Select(m => m.Category).Distinct().OrderBy(c => c).ToList();
        }
    }

    /// <summary>
    /// Utility class for parsing clipboard data from Excel (TSV format)
    /// </summary>
    public static class ClipboardDataParser
    {
        /// <summary>
        /// Parse TSV clipboard data into a dictionary keyed by cell reference
        /// </summary>
        public static Dictionary<string, string> ParseTSV(string clipboardText, string startCell = "W1")
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(clipboardText))
                return result;

            var (startCol, startRow) = ParseCellReference(startCell);
            var lines = clipboardText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            for (int rowOffset = 0; rowOffset < lines.Length; rowOffset++)
            {
                var cells = lines[rowOffset].Split('\t');
                for (int colOffset = 0; colOffset < cells.Length; colOffset++)
                {
                    int col = startCol + colOffset;
                    int row = startRow + rowOffset;
                    string cellRef = GetCellReference(col, row);
                    result[cellRef] = cells[colOffset];
                }
            }

            return result;
        }

        /// <summary>
        /// Get value for specific cell reference from parsed data
        /// </summary>
        public static string GetCellValue(Dictionary<string, string> data, string cellRef)
        {
            return data.ContainsKey(cellRef) ? data[cellRef] : string.Empty;
        }

        /// <summary>
        /// Parse cell reference like "W17" into (column=23, row=17)
        /// </summary>
        public static (int col, int row) ParseCellReference(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef))
                return (1, 1);

            int col = 0;
            int row = 0;
            int i = 0;

            // Parse column letters (A=1, B=2, ... Z=26, AA=27, etc.)
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
            {
                col = col * 26 + (char.ToUpper(cellRef[i]) - 'A' + 1);
                i++;
            }

            // Parse row number
            if (i < cellRef.Length)
            {
                int.TryParse(cellRef.Substring(i), out row);
            }

            return (col, row);
        }

        /// <summary>
        /// Convert column and row numbers to cell reference (23, 17 -> "W17")
        /// </summary>
        public static string GetCellReference(int col, int row)
        {
            string colStr = string.Empty;

            while (col > 0)
            {
                int rem = (col - 1) % 26;
                colStr = (char)('A' + rem) + colStr;
                col = (col - 1) / 26;
            }

            return colStr + row;
        }

        /// <summary>
        /// Validate cell reference format
        /// </summary>
        public static bool IsValidCellReference(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef))
                return false;

            int i = 0;
            bool hasLetters = false;
            bool hasNumbers = false;

            // Check letters
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
            {
                hasLetters = true;
                i++;
            }

            // Check numbers
            while (i < cellRef.Length && char.IsDigit(cellRef[i]))
            {
                hasNumbers = true;
                i++;
            }

            return hasLetters && hasNumbers && i == cellRef.Length;
        }
    }

    /// <summary>
    /// Utility class for formatting export data in various formats
    /// </summary>
    public static class ExcelExportFormatter
    {
        /// <summary>
        /// Generate CSV with proper escaping
        /// </summary>
        public static string ToCsv(List<Project> projects, List<ExcelFieldMapping> fields)
        {
            var sb = new StringBuilder();

            // Header row
            sb.AppendLine(string.Join(",", fields.Select(f => EscapeCsv(f.DisplayName))));

            // Data rows
            foreach (var project in projects)
            {
                var values = fields.Select(f => EscapeCsv(GetProjectValue(project, f.ProjectPropertyName)));
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate TSV (Tab-Separated Values) - Excel native paste format
        /// </summary>
        public static string ToTsv(List<Project> projects, List<ExcelFieldMapping> fields)
        {
            var sb = new StringBuilder();

            // Header row
            sb.AppendLine(string.Join("\t", fields.Select(f => f.DisplayName)));

            // Data rows
            foreach (var project in projects)
            {
                var values = fields.Select(f => GetProjectValue(project, f.ProjectPropertyName));
                sb.AppendLine(string.Join("\t", values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate JSON array
        /// </summary>
        public static string ToJson(List<Project> projects, List<ExcelFieldMapping> fields)
        {
            var list = new List<Dictionary<string, string>>();

            foreach (var project in projects)
            {
                var dict = new Dictionary<string, string>();
                foreach (var field in fields)
                {
                    dict[field.DisplayName] = GetProjectValue(project, field.ProjectPropertyName);
                }
                list.Add(dict);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(list, options);
        }

        /// <summary>
        /// Generate XML
        /// </summary>
        public static string ToXml(List<Project> projects, List<ExcelFieldMapping> fields)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<Projects>");

            foreach (var project in projects)
            {
                sb.AppendLine("  <Project>");
                foreach (var field in fields)
                {
                    string value = GetProjectValue(project, field.ProjectPropertyName);
                    sb.AppendLine($"    <{SanitizeXmlTag(field.DisplayName)}>{EscapeXml(value)}</{SanitizeXmlTag(field.DisplayName)}>");
                }
                sb.AppendLine("  </Project>");
            }

            sb.AppendLine("</Projects>");
            return sb.ToString();
        }

        /// <summary>
        /// Generate fixed-width text
        /// </summary>
        public static string ToFixedWidth(List<Project> projects, List<ExcelFieldMapping> fields)
        {
            var sb = new StringBuilder();

            // Calculate column widths
            var widths = new int[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                widths[i] = Math.Max(fields[i].DisplayName.Length, 20);
            }

            // Header row
            for (int i = 0; i < fields.Count; i++)
            {
                sb.Append(fields[i].DisplayName.PadRight(widths[i]));
                sb.Append(" | ");
            }
            sb.AppendLine();

            // Separator
            for (int i = 0; i < fields.Count; i++)
            {
                sb.Append(new string('-', widths[i]));
                sb.Append("-+-");
            }
            sb.AppendLine();

            // Data rows
            foreach (var project in projects)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    string value = GetProjectValue(project, fields[i].ProjectPropertyName);
                    if (value.Length > widths[i])
                        value = value.Substring(0, widths[i] - 3) + "...";

                    sb.Append(value.PadRight(widths[i]));
                    sb.Append(" | ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get property value from Project object using reflection
        /// </summary>
        private static string GetProjectValue(Project project, string propertyName)
        {
            if (project == null || string.IsNullOrEmpty(propertyName))
                return string.Empty;

            var prop = typeof(ProjectModels.Project).GetProperty(propertyName);
            if (prop == null)
                return string.Empty;

            var value = prop.GetValue(project);
            if (value == null)
                return string.Empty;

            // Format dates
            if (value is DateTime dt)
                return dt.ToString("yyyy-MM-dd");

            // Format lists
            if (value is System.Collections.IEnumerable && !(value is string))
            {
                var items = ((System.Collections.IEnumerable)value).Cast<object>();
                return string.Join("; ", items);
            }

            return value.ToString();
        }

        /// <summary>
        /// Escape CSV value (handle commas, quotes, newlines)
        /// </summary>
        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');

            if (needsQuotes)
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return value;
        }

        /// <summary>
        /// Escape XML special characters
        /// </summary>
        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Sanitize XML tag name (remove spaces, special chars)
        /// </summary>
        private static string SanitizeXmlTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return "Field";

            // Remove spaces and non-alphanumeric chars
            var result = new StringBuilder();
            foreach (char c in tag)
            {
                if (char.IsLetterOrDigit(c))
                    result.Append(c);
            }

            return result.Length > 0 ? result.ToString() : "Field";
        }
    }
}
