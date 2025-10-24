# Script to split Framework.cs into multiple files
# Run from WPF directory: ./split_framework.ps1

$frameworkFile = "Core/Framework.cs"
$lines = Get-Content $frameworkFile

# Common using statements for all files
$commonUsings = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SuperTUI.Infrastructure;

namespace SuperTUI.Core
{
"@

$footer = "}"

# Define class extraction rules
$classes = @(
    @{
        Name = "GridLayoutEngine"
        Start = 391
        End = 558
        Dir = "Layout"
    },
    @{
        Name = "DockLayoutEngine"
        Start = 563
        End = 601
        Dir = "Layout"
    },
    @{
        Name = "StackLayoutEngine"
        Start = 603
        End = 643
        Dir = "Layout"
    },
    @{
        Name = "Workspace"
        Start = 645
        End = 808
        Dir = "Infrastructure"
    },
    @{
        Name = "WorkspaceManager"
        Start = 810
        End = 896
        Dir = "Infrastructure"
    },
    @{
        Name = "ServiceContainer"
        Start = 898
        End = 942
        Dir = "Infrastructure"
    },
    @{
        Name = "EventBus"
        Start = 944
        End = 984
        Dir = "Infrastructure"
    },
    @{
        Name = "ShortcutManager"
        Start = 986 # Includes KeyboardShortcut struct
        End = 1067
        Dir = "Infrastructure"
    }
)

Write-Host "Extracting classes from Framework.cs..." -ForegroundColor Cyan

foreach ($class in $classes) {
    Write-Host "  Extracting $($class.Name)..." -NoNewline

    # Extract lines (PowerShell arrays are 0-indexed)
    $classLines = $lines[($class.Start - 1)..($class.End - 1)]

    # Build complete file content
    $content = $commonUsings + "`n" + ($classLines -join "`n") + "`n" + $footer

    # Create target directory if needed
    $targetDir = "Core/$($class.Dir)"
    if (!(Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    # Write file
    $targetFile = "$targetDir/$($class.Name).cs"
    Set-Content -Path $targetFile -Value $content -Encoding UTF8

    Write-Host " Done" -ForegroundColor Green
}

Write-Host "`nAll classes extracted successfully!" -ForegroundColor Green
Write-Host "`nFiles created:" -ForegroundColor Yellow
Get-ChildItem -Path "Core/Components", "Core/Layout", "Core/Infrastructure" -Filter *.cs -Recurse | ForEach-Object {
    Write-Host "  $($_.FullName.Replace((Get-Location).Path + '\', ''))"
}

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Verify compilation (PowerShell loads the new files)"
Write-Host "2. Test demo: ./SuperTUI_Demo.ps1"
Write-Host "3. If successful, rename Framework.cs to Framework.cs.deprecated"
Write-Host "4. Update .claude/CLAUDE.md with new structure"
