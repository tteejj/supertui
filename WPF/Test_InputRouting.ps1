#!/usr/bin/env pwsh
# Test input routing with context awareness

Write-Host "=== Testing Input Routing ===" -ForegroundColor Cyan
Write-Host ""

# Build first
Write-Host "Building SuperTUI..." -ForegroundColor Yellow
dotnet build SuperTUI.csproj -nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Test scenarios to verify manually
Write-Host "Test scenarios to verify:" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. ShortcutManager Context Awareness:" -ForegroundColor Yellow
Write-Host "   - Type 'test' in any TextBox - letters should appear, not trigger shortcuts"
Write-Host "   - While typing, Ctrl+S should still work (allowed shortcut)"
Write-Host "   - While typing, Ctrl+Z/Y/X/C/V/A should work (editing shortcuts)"
Write-Host "   - While typing, single keys (A/D/E/S) should NOT trigger pane actions"
Write-Host ""

Write-Host "2. TaskListPane:" -ForegroundColor Yellow
Write-Host "   - Focus task list, press 'A' - should open Quick Add"
Write-Host "   - Type in Quick Add field - 'A' should type letter, not open another add"
Write-Host "   - Focus task list, press 'E' - should start inline edit"
Write-Host "   - While editing task title, all keys should work for typing"
Write-Host ""

Write-Host "3. NotesPane:" -ForegroundColor Yellow
Write-Host "   - Focus notes list, press 'A' - should create new note"
Write-Host "   - In note editor, all keys should work for typing (no vim modes)"
Write-Host "   - Arrow keys should move cursor normally"
Write-Host "   - No H/J/K/L navigation - these should type letters"
Write-Host "   - Escape should close editor, Ctrl+S should save"
Write-Host "   - Search box should accept all typed characters"
Write-Host ""

Write-Host "4. Global Shortcuts:" -ForegroundColor Yellow
Write-Host "   - Ctrl+Shift+T should open TaskListPane from anywhere"
Write-Host "   - Ctrl+Shift+N should open NotesPane from anywhere"
Write-Host "   - Ctrl+1-9 should switch workspaces"
Write-Host "   - These should work even when typing (different modifier keys)"
Write-Host ""

Write-Host "5. Focus Management:" -ForegroundColor Yellow
Write-Host "   - Each pane should remember what had focus"
Write-Host "   - Switching panes should restore focus to last element"
Write-Host "   - Alt+Tab away and back should restore focus correctly"
Write-Host ""

Write-Host "Running SuperTUI for manual testing..." -ForegroundColor Green
Write-Host "Please test the scenarios above." -ForegroundColor Cyan
Write-Host ""

# Run the app
& dotnet run --project SuperTUI.csproj