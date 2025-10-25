# Test_CommandService.ps1 - Test the Command Library data layer

# Load the core types
Add-Type -Path "$PSScriptRoot/Core/Commands/Command.cs" -ReferencedAssemblies @(
    "System.Text.Json",
    "PresentationFramework",
    "WindowsBase"
)

Add-Type -Path "$PSScriptRoot/Core/Infrastructure/Logger.cs" -ReferencedAssemblies @(
    "System.Collections"
)

Add-Type -Path "$PSScriptRoot/Core/Commands/CommandService.cs" -ReferencedAssemblies @(
    "System.Text.Json",
    "PresentationFramework",
    "WindowsBase"
)

Write-Host "`n=== Command Library Service Test ===" -ForegroundColor Cyan
Write-Host ""

# Create logger
$logger = [SuperTUI.Core.Infrastructure.Logger]::new()

# Create service
Write-Host "Creating CommandService..." -ForegroundColor Yellow
$service = [SuperTUI.Core.Commands.CommandService]::new($logger)

# Test 1: Add commands
Write-Host "`n--- Test 1: Adding Commands ---" -ForegroundColor Cyan
$cmd1 = [SuperTUI.Core.Commands.Command]::new()
$cmd1.Title = "Docker list containers"
$cmd1.Description = "Show all containers including stopped ones"
$cmd1.Tags = @("docker", "admin")
$cmd1.CommandText = "docker ps -a"

$cmd2 = [SuperTUI.Core.Commands.Command]::new()
$cmd2.Title = "Git status"
$cmd2.Description = "Show working tree status"
$cmd2.Tags = @("git")
$cmd2.CommandText = "git status"

$cmd3 = [SuperTUI.Core.Commands.Command]::new()
$cmd3.Title = "Docker restart all"
$cmd3.Description = "Restart all running containers"
$cmd3.Tags = @("docker", "admin")
$cmd3.CommandText = "docker restart `$(docker ps -q)"

$service.AddCommand($cmd1) | Out-Null
$service.AddCommand($cmd2) | Out-Null
$service.AddCommand($cmd3) | Out-Null

Write-Host "Added 3 commands" -ForegroundColor Green
Write-Host "Total commands: $($service.GetAllCommands().Count)"

# Test 2: Search - simple substring
Write-Host "`n--- Test 2: Search (simple substring) ---" -ForegroundColor Cyan
$results = $service.SearchCommands("docker")
Write-Host "Search 'docker': Found $($results.Count) commands"
foreach ($cmd in $results) {
    Write-Host "  - $($cmd.Title)"
}

# Test 3: Search - tag syntax
Write-Host "`n--- Test 3: Search (tag syntax) ---" -ForegroundColor Cyan
$results = $service.SearchCommands("t:admin")
Write-Host "Search 't:admin': Found $($results.Count) commands"
foreach ($cmd in $results) {
    Write-Host "  - $($cmd.Title) [tags: $($cmd.Tags -join ', ')]"
}

# Test 4: Search - AND mode
Write-Host "`n--- Test 4: Search (AND mode) ---" -ForegroundColor Cyan
$results = $service.SearchCommands("+docker +restart")
Write-Host "Search '+docker +restart': Found $($results.Count) commands"
foreach ($cmd in $results) {
    Write-Host "  - $($cmd.Title)"
}

# Test 5: Get all tags
Write-Host "`n--- Test 5: Get All Tags ---" -ForegroundColor Cyan
$tags = $service.GetTags()
Write-Host "All tags: $($tags -join ', ')"

# Test 6: Update command
Write-Host "`n--- Test 6: Update Command ---" -ForegroundColor Cyan
$cmd = $service.GetAllCommands()[0]
$cmd.Description = "Updated description"
$service.UpdateCommand($cmd) | Out-Null
Write-Host "Updated: $($cmd.Title)" -ForegroundColor Green

# Test 7: Copy to clipboard (this will actually copy!)
Write-Host "`n--- Test 7: Copy to Clipboard ---" -ForegroundColor Cyan
$cmd = $service.GetAllCommands()[0]
Write-Host "Before copy: UseCount = $($cmd.UseCount)"
$service.CopyToClipboard($cmd.Id)
$cmd = $service.GetCommand($cmd.Id) # Reload to see updated stats
Write-Host "After copy: UseCount = $($cmd.UseCount)" -ForegroundColor Green
Write-Host "Clipboard contains: $(Get-Clipboard)" -ForegroundColor Yellow

# Test 8: Display formatting
Write-Host "`n--- Test 8: Display Formatting ---" -ForegroundColor Cyan
$cmd = $service.GetAllCommands()[0]
Write-Host "Display text: $($cmd.GetDisplayText())"
Write-Host "Detail text:`n$($cmd.GetDetailText())"

# Test 9: Delete command
Write-Host "`n--- Test 9: Delete Command ---" -ForegroundColor Cyan
$cmdToDelete = $service.GetAllCommands()[1]
Write-Host "Deleting: $($cmdToDelete.Title)"
$service.DeleteCommand($cmdToDelete.Id) | Out-Null
Write-Host "Total commands after delete: $($service.GetAllCommands().Count)" -ForegroundColor Green

# Test 10: JSON persistence check
Write-Host "`n--- Test 10: JSON Persistence ---" -ForegroundColor Cyan
$jsonPath = Join-Path $env:USERPROFILE ".supertui\commands.json"
if (Test-Path $jsonPath) {
    Write-Host "JSON file exists at: $jsonPath" -ForegroundColor Green
    $jsonContent = Get-Content $jsonPath -Raw
    Write-Host "File size: $($jsonContent.Length) bytes"
    Write-Host "`nFirst 200 chars:"
    Write-Host $jsonContent.Substring(0, [Math]::Min(200, $jsonContent.Length)) -ForegroundColor DarkGray
} else {
    Write-Host "JSON file not found!" -ForegroundColor Red
}

Write-Host "`n=== All Tests Complete ===" -ForegroundColor Cyan
Write-Host ""
