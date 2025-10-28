using System;
using System.Collections.Generic;
using System.Linq;
using SuperTUI.Core.Models;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Validation service for data model fields
    /// Provides centralized validation rules for new fields added in Phase 1
    /// </summary>
    public class ValidationService
    {
        private static ValidationService instance;
        public static ValidationService Instance => instance ??= new ValidationService();

        private ValidationService()
        {
        }

        #region TaskItem Validation

        /// <summary>
        /// Validate TaskItem with all new fields
        /// </summary>
        public List<string> ValidateTask(TaskItem task)
        {
            var errors = new List<string>();

            // Basic validations
            if (string.IsNullOrWhiteSpace(task.Title))
                errors.Add("Task title is required");

            if (task.Title?.Length > 500)
                errors.Add("Task title must be 500 characters or less");

            // ExternalId validations
            if (task.ExternalId1?.Length > 20)
                errors.Add("ExternalId1 must be 20 characters or less");

            if (task.ExternalId2?.Length > 20)
                errors.Add("ExternalId2 must be 20 characters or less");

            // AssignedTo validation
            if (task.AssignedTo?.Length > 100)
                errors.Add("AssignedTo must be 100 characters or less");

            // EstimatedDuration validation
            if (task.EstimatedDuration.HasValue && task.EstimatedDuration.Value < TimeSpan.Zero)
                errors.Add("EstimatedDuration cannot be negative");

            if (task.EstimatedDuration.HasValue && task.EstimatedDuration.Value > TimeSpan.FromDays(365))
                errors.Add("EstimatedDuration cannot exceed 1 year");

            // CompletedDate validation
            if (task.CompletedDate.HasValue)
            {
                if (task.CompletedDate.Value > DateTime.Now)
                    errors.Add("CompletedDate cannot be in the future");

                if (task.CompletedDate.Value < task.CreatedAt)
                    errors.Add("CompletedDate cannot be before CreatedAt");

                if (task.Status != TaskStatus.Completed)
                    errors.Add("CompletedDate should only be set when Status is Completed");
            }

            // DueDate validation
            if (task.DueDate.HasValue && task.DueDate.Value < task.CreatedAt.Date)
                errors.Add("DueDate cannot be before task creation date");

            // Tags validation
            if (task.Tags != null)
            {
                if (task.Tags.Count > 10)
                    errors.Add("Maximum 10 tags allowed per task");

                foreach (var tag in task.Tags)
                {
                    if (tag.Length > 50)
                        errors.Add($"Tag '{tag}' exceeds 50 characters");

                    if (ContainsInvalidTagCharacters(tag))
                        errors.Add($"Tag '{tag}' contains invalid characters");
                }
            }

            // Progress validation
            if (task.Progress < 0 || task.Progress > 100)
                errors.Add("Progress must be between 0 and 100");

            return errors;
        }

        /// <summary>
        /// Quick validation check (returns true if valid)
        /// </summary>
        public bool IsTaskValid(TaskItem task, out List<string> errors)
        {
            errors = ValidateTask(task);
            return errors.Count == 0;
        }

        #endregion

        #region TimeEntry Validation

        /// <summary>
        /// Validate TimeEntry with new fields
        /// </summary>
        public List<string> ValidateTimeEntry(TimeEntry entry)
        {
            var errors = new List<string>();

            // ID1 validation
            if (entry.ID1?.Length > 20)
                errors.Add("ID1 must be 20 characters or less");

            // ID2 validation
            if (entry.ID2?.Length > 20)
                errors.Add("ID2 must be 20 characters or less");

            // Hours validation
            if (entry.Hours < 0)
                errors.Add("Hours cannot be negative");

            if (entry.Hours > 168)  // 1 week
                errors.Add("Hours cannot exceed 168 (1 week)");

            // Daily hours validation
            if (entry.MondayHours.HasValue && (entry.MondayHours.Value < 0 || entry.MondayHours.Value > 24))
                errors.Add("Monday hours must be between 0 and 24");

            if (entry.TuesdayHours.HasValue && (entry.TuesdayHours.Value < 0 || entry.TuesdayHours.Value > 24))
                errors.Add("Tuesday hours must be between 0 and 24");

            if (entry.WednesdayHours.HasValue && (entry.WednesdayHours.Value < 0 || entry.WednesdayHours.Value > 24))
                errors.Add("Wednesday hours must be between 0 and 24");

            if (entry.ThursdayHours.HasValue && (entry.ThursdayHours.Value < 0 || entry.ThursdayHours.Value > 24))
                errors.Add("Thursday hours must be between 0 and 24");

            if (entry.FridayHours.HasValue && (entry.FridayHours.Value < 0 || entry.FridayHours.Value > 24))
                errors.Add("Friday hours must be between 0 and 24");

            if (entry.SaturdayHours.HasValue && (entry.SaturdayHours.Value < 0 || entry.SaturdayHours.Value > 24))
                errors.Add("Saturday hours must be between 0 and 24");

            if (entry.SundayHours.HasValue && (entry.SundayHours.Value < 0 || entry.SundayHours.Value > 24))
                errors.Add("Sunday hours must be between 0 and 24");

            // Description validation
            if (entry.Description?.Length > 500)
                errors.Add("Description must be 500 characters or less");

            return errors;
        }

        /// <summary>
        /// Quick validation check for TimeEntry
        /// </summary>
        public bool IsTimeEntryValid(TimeEntry entry, out List<string> errors)
        {
            errors = ValidateTimeEntry(entry);
            return errors.Count == 0;
        }

        #endregion

        #region Tag Validation

        /// <summary>
        /// Validate a single tag
        /// </summary>
        public List<string> ValidateTag(string tag)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(tag))
            {
                errors.Add("Tag cannot be empty");
                return errors;
            }

            if (tag.Length > 50)
                errors.Add("Tag must be 50 characters or less");

            if (ContainsInvalidTagCharacters(tag))
                errors.Add("Tag contains invalid characters (spaces, tabs, commas, semicolons, pipes, slashes)");

            return errors;
        }

        /// <summary>
        /// Check if tag contains invalid characters
        /// </summary>
        private bool ContainsInvalidTagCharacters(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;

            char[] invalidChars = { ' ', '\t', '\n', '\r', ',', ';', '|', '/', '\\' };
            return tag.IndexOfAny(invalidChars) >= 0;
        }

        /// <summary>
        /// Quick tag validation check
        /// </summary>
        public bool IsTagValid(string tag, out List<string> errors)
        {
            errors = ValidateTag(tag);
            return errors.Count == 0;
        }

        #endregion

        #region Field-Specific Validation

        /// <summary>
        /// Validate ExternalId fields
        /// </summary>
        public bool ValidateExternalId(string externalId, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(externalId))
                return true;  // Null/empty is valid (optional field)

            if (externalId.Length > 20)
            {
                error = "External ID must be 20 characters or less";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate estimated duration
        /// </summary>
        public bool ValidateEstimatedDuration(TimeSpan? duration, out string error)
        {
            error = null;

            if (!duration.HasValue)
                return true;  // Null is valid (optional field)

            if (duration.Value < TimeSpan.Zero)
            {
                error = "Estimated duration cannot be negative";
                return false;
            }

            if (duration.Value > TimeSpan.FromDays(365))
            {
                error = "Estimated duration cannot exceed 1 year";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate daily hours (0-24 range)
        /// </summary>
        public bool ValidateDailyHours(decimal hours, out string error)
        {
            error = null;

            if (hours < 0)
            {
                error = "Hours cannot be negative";
                return false;
            }

            if (hours > 24)
            {
                error = "Hours cannot exceed 24 per day";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate AssignedTo field
        /// </summary>
        public bool ValidateAssignedTo(string assignedTo, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(assignedTo))
                return true;  // Null/empty is valid (optional field)

            if (assignedTo.Length > 100)
            {
                error = "AssignedTo must be 100 characters or less";
                return false;
            }

            return true;
        }

        #endregion
    }
}
