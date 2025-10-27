using System;
using System.Collections.Generic;
using SuperTUI.Core.Models;
using SuperTUI.Core.Services;

namespace SuperTUI.Infrastructure
{
    /// <summary>
    /// Interface for tag management service
    /// Matches actual TagService implementation
    /// </summary>
    public interface ITagService
    {
        // Tag retrieval
        List<string> GetAllTags();
        TagInfo GetTagInfo(string tag);
        List<TagInfo> GetTagsByUsage(int limit = 20);
        List<TagInfo> GetRecentTags(int limit = 10);
        List<string> GetTagSuggestions(string prefix, int limit = 10);

        // Task operations
        List<TaskItem> GetTasksByTag(string tag);
        List<TaskItem> GetTasksByTags(List<string> tags, bool requireAll = true);
        bool AddTagToTask(Guid taskId, string tag);
        bool RemoveTagFromTask(Guid taskId, string tag);
        bool SetTaskTags(Guid taskId, List<string> tags);

        // Tag management
        string ValidateTag(string tag);
        int RenameTag(string oldTag, string newTag);
        int DeleteTag(string tag);
        int MergeTags(string sourceTag, string targetTag);
        void RebuildTagIndex();
    }
}
