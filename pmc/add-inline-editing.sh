#!/bin/bash
# Batch add inline editing to all remaining task view screens

cd /home/teej/pmc/module/Pmc.Strict/consoleui/screens

echo "Adding inline editing to OverdueViewScreen..."
pwsh -c "
\$template = Get-Content 'WeekViewScreen.ps1' -Raw
\$target = Get-Content 'OverdueViewScreen.ps1' -Raw
\$target = \$target -replace 'WeekTasks', 'OverdueTasks'
\$target = \$target -replace 'Week View', 'Overdue'
Set-Content 'OverdueViewScreen.ps1' \$target -NoNewline
"

echo "Adding inline editing to UpcomingViewScreen..."
pwsh -c "
\$template = Get-Content 'WeekViewScreen.ps1' -Raw
\$target = Get-Content 'UpcomingViewScreen.ps1' -Raw
\$target = \$target -replace 'WeekTasks', 'UpcomingTasks'
\$target = \$target -replace 'Week View', 'Upcoming'
Set-Content 'UpcomingViewScreen.ps1' \$target -NoNewline
"

echo "Done!"
