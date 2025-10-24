# TaskService - Manages tasks with observable collections for auto-updating UI
# Demonstrates: ObservableCollection, INotifyPropertyChanged integration

using namespace System.Collections.ObjectModel
using namespace System.ComponentModel

# Task model
class Task {
    [int]$Id
    [string]$Title
    [string]$Description
    [string]$Status  # Pending, InProgress, Completed
    [string]$Priority  # Low, Medium, High
    [DateTime]$CreatedDate
    [DateTime]$DueDate
    [int]$ProjectId
    [string]$ProjectName

    Task([int]$id, [string]$title) {
        $this.Id = $id
        $this.Title = $title
        $this.Status = "Pending"
        $this.Priority = "Medium"
        $this.CreatedDate = Get-Date
        $this.DueDate = (Get-Date).AddDays(7)
        $this.ProjectId = 0
        $this.ProjectName = "General"
    }
}

# TaskStatistics - Updates trigger UI refresh automatically
class TaskStatistics {
    [int]$TotalTasks = 0
    [int]$CompletedTasks = 0
    [int]$PendingTasks = 0
    [int]$InProgressTasks = 0
    [int]$TodayTasks = 0
    [int]$WeekTasks = 0
    [int]$OverdueTasks = 0

    [void] Update([ObservableCollection[Task]]$tasks) {
        $today = (Get-Date).Date
        $weekEnd = $today.AddDays(7)

        $this.TotalTasks = $tasks.Count
        $this.CompletedTasks = ($tasks | Where-Object { $_.Status -eq "Completed" }).Count
        $this.PendingTasks = ($tasks | Where-Object { $_.Status -eq "Pending" }).Count
        $this.InProgressTasks = ($tasks | Where-Object { $_.Status -eq "InProgress" }).Count
        $this.TodayTasks = ($tasks | Where-Object { $_.DueDate.Date -eq $today -and $_.Status -ne "Completed" }).Count
        $this.WeekTasks = ($tasks | Where-Object { $_.DueDate.Date -le $weekEnd -and $_.DueDate.Date -ge $today -and $_.Status -ne "Completed" }).Count
        $this.OverdueTasks = ($tasks | Where-Object { $_.DueDate.Date -lt $today -and $_.Status -ne "Completed" }).Count
    }
}

# Main TaskService
class TaskService {
    [ObservableCollection[Task]]$Tasks
    [TaskStatistics]$Statistics
    [int]$NextId = 1

    TaskService() {
        $this.Tasks = [ObservableCollection[Task]]::new()
        $this.Statistics = [TaskStatistics]::new()

        # Load sample data
        $this.LoadSampleData()
    }

    [void] LoadSampleData() {
        # Add some sample tasks for demo
        $task1 = [Task]::new($this.NextId++, "Review SuperTUI documentation")
        $task1.Status = "InProgress"
        $task1.Priority = "High"
        $task1.DueDate = (Get-Date).Date
        $this.Tasks.Add($task1)

        $task2 = [Task]::new($this.NextId++, "Implement DashboardScreen")
        $task2.Status = "Completed"
        $task2.Priority = "High"
        $task2.DueDate = (Get-Date).Date.AddDays(-1)
        $this.Tasks.Add($task2)

        $task3 = [Task]::new($this.NextId++, "Create TaskService")
        $task3.Status = "InProgress"
        $task3.Priority = "High"
        $task3.DueDate = (Get-Date).Date
        $this.Tasks.Add($task3)

        $task4 = [Task]::new($this.NextId++, "Add keyboard navigation")
        $task4.Status = "Pending"
        $task4.Priority = "Medium"
        $task4.DueDate = (Get-Date).Date
        $this.Tasks.Add($task4)

        $task5 = [Task]::new($this.NextId++, "Test complete workflow")
        $task5.Status = "Pending"
        $task5.Priority = "Medium"
        $task5.DueDate = (Get-Date).Date.AddDays(1)
        $this.Tasks.Add($task5)

        $task6 = [Task]::new($this.NextId++, "Implement TaskListScreen")
        $task6.Status = "Pending"
        $task6.Priority = "High"
        $task6.DueDate = (Get-Date).Date.AddDays(2)
        $this.Tasks.Add($task6)

        $task7 = [Task]::new($this.NextId++, "Create ProjectService")
        $task7.Status = "Pending"
        $task7.Priority = "Medium"
        $task7.DueDate = (Get-Date).Date.AddDays(3)
        $this.Tasks.Add($task7)

        $task8 = [Task]::new($this.NextId++, "Port remaining screens")
        $task8.Status = "Pending"
        $task8.Priority = "Low"
        $task8.DueDate = (Get-Date).Date.AddDays(7)
        $this.Tasks.Add($task8)

        # Update statistics
        $this.RefreshStatistics()
    }

    [void] AddTask([Task]$task) {
        $task.Id = $this.NextId++
        $this.Tasks.Add($task)
        $this.RefreshStatistics()
    }

    [void] UpdateTask([Task]$task) {
        $existing = $this.Tasks | Where-Object { $_.Id -eq $task.Id } | Select-Object -First 1
        if ($existing) {
            $index = $this.Tasks.IndexOf($existing)
            $this.Tasks[$index] = $task
            $this.RefreshStatistics()
        }
    }

    [void] DeleteTask([int]$id) {
        $task = $this.Tasks | Where-Object { $_.Id -eq $id } | Select-Object -First 1
        if ($task) {
            $this.Tasks.Remove($task)
            $this.RefreshStatistics()
        }
    }

    [void] CompleteTask([int]$id) {
        $task = $this.Tasks | Where-Object { $_.Id -eq $id } | Select-Object -First 1
        if ($task) {
            $task.Status = "Completed"
            $this.RefreshStatistics()
        }
    }

    [void] RefreshStatistics() {
        $this.Statistics.Update($this.Tasks)
    }

    [Task[]] GetTasksDueToday() {
        $today = (Get-Date).Date
        return $this.Tasks | Where-Object { $_.DueDate.Date -eq $today -and $_.Status -ne "Completed" }
    }

    [Task[]] GetTasksDueThisWeek() {
        $today = (Get-Date).Date
        $weekEnd = $today.AddDays(7)
        return $this.Tasks | Where-Object { $_.DueDate.Date -le $weekEnd -and $_.DueDate.Date -ge $today -and $_.Status -ne "Completed" }
    }

    [Task[]] GetOverdueTasks() {
        $today = (Get-Date).Date
        return $this.Tasks | Where-Object { $_.DueDate.Date -lt $today -and $_.Status -ne "Completed" }
    }
}

# Helper function to create and register TaskService
function New-TaskService {
    <#
    .SYNOPSIS
    Creates a new TaskService instance

    .DESCRIPTION
    Creates a TaskService with sample data and registers it in the service container

    .EXAMPLE
    $taskSvc = New-TaskService
    Register-Service "TaskService" -Instance $taskSvc
    #>

    return [TaskService]::new()
}
