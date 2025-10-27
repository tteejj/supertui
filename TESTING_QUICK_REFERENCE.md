# SuperTUI Testing - Quick Reference Card

## Solo Developer Workflow

```
┌─────────────────────────────────────────────────────────┐
│                   LINUX (Dev Machine)                   │
├─────────────────────────────────────────────────────────┤
│  1. Make code changes                                   │
│  2. ./pre-deploy-check.sh                    [30s]     │
│  3. git add . && git commit && git push                 │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│                 WINDOWS (Test Machine)                  │
├─────────────────────────────────────────────────────────┤
│  4. git pull                                            │
│  5. pwsh run-windows-tests.ps1               [2min]    │
│     → Auto-commits results to git                       │
└─────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────┐
│              LINUX (Check Results)                      │
├─────────────────────────────────────────────────────────┤
│  6. git pull                                            │
│  7. cat test-results/latest/SUMMARY.txt                 │
│  8. ✅ Deploy or 🐛 Fix and repeat                      │
└─────────────────────────────────────────────────────────┘
```

**Total Time:** ~2.5 minutes per iteration

---

## Commands Cheat Sheet

### Linux (Before Windows Testing)

```bash
# Pre-deployment validation (ALWAYS RUN THIS FIRST)
./pre-deploy-check.sh

# Manual test run
cd WPF/Tests && dotnet test --filter "Category=Linux"

# Specific test
dotnet test --filter "FullyQualifiedName~WidgetFactoryTests"

# Build only
cd WPF && dotnet build SuperTUI.csproj
```

### Windows (Full Testing)

```powershell
# Full test suite with auto-commit (DEFAULT)
pwsh run-windows-tests.ps1

# Skip build (faster, if already built)
pwsh run-windows-tests.ps1 -SkipBuild

# Skip git commit
pwsh run-windows-tests.ps1 -SkipCommit

# Smoke tests only (fast)
pwsh run-windows-tests.ps1 -Filter "Category=Smoke"

# Manual test run
cd WPF\Tests
dotnet test --filter "Category=Windows"
```

### Debugging Failed Tests

```bash
# On Linux after Windows test run:
git pull
cat test-results/latest/SUMMARY.txt
cat test-results/latest/DIAGNOSTICS.txt
grep -i "error" test-results/latest/*.log

# On Windows:
cat test-results/latest/errors.log
ls test-results/latest/app-logs/
```

---

## What Gets Tested Where

### Linux Tests (30s) - Catch 70% of Issues

| Component | What's Tested |
|-----------|---------------|
| **DI Container** | Service resolution, widget creation, lifetimes |
| **Domain Services** | TaskService, ProjectService, TimeTracking, Tags |
| **Configuration** | Load/save, validation, type safety |
| **Security** | Path validation, traversal blocking |
| **State** | Persistence, checksums, backups |

**NO UI/WPF** - Pure logic tests

### Windows Tests (2min) - Full Integration

| Component | What's Tested |
|-----------|---------------|
| **Everything from Linux** | + WPF/UI-specific tests |
| **Widget UI** | Rendering, theme application, focus |
| **Workspace** | Lifecycle, switching, state preservation |
| **Application** | Launch, initialization, shutdown |
| **Performance** | Speed, memory leaks, disposal |

---

## Test Results Structure

```
test-results/
├── latest/                    # ← Always check this (git tracked)
│   ├── SUMMARY.txt            # Quick pass/fail status
│   ├── DIAGNOSTICS.txt        # Full system info
│   ├── test-results.trx       # Machine-readable results
│   ├── errors.log             # Error details (if failed)
│   └── app-logs/              # Application logs
│
└── YYYY-MM-DD_HH-mm-ss/       # Timestamped runs (not tracked)
    └── (same structure)
```

---

## Troubleshooting Decision Tree

```
Tests failed?
├─ On Linux? → Fix on Linux, re-run pre-deploy-check.sh
├─ On Windows?
│  ├─ Check test-results/latest/SUMMARY.txt
│  ├─ Check test-results/latest/DIAGNOSTICS.txt
│  ├─ Pull results to Linux: git pull
│  ├─ Fix on Linux
│  └─ Re-deploy: push → Windows: pull + test
└─ Can't figure it out?
   ├─ Search test source: WPF/Tests/
   └─ Grep logs: grep -r "ERROR" test-results/
```

---

## Expected Test Counts

| Test Suite | Tests | Duration | Pass Criteria |
|------------|-------|----------|---------------|
| Linux (Critical) | ~45 | 25-35s | 100% pass |
| Windows (Smoke) | ~9 | 30-45s | 100% pass |
| Windows (Full) | ~70 | 90-120s | 100% pass |

If counts differ significantly, check:
- Did you add/remove widgets?
- Did you exclude tests?
- Did tests error out early?

---

## Common Issues

### Linux: "dotnet not found"
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh && ./dotnet-install.sh --channel 8.0
```

### Linux: "EnableWindowsTargeting warning"
**Ignore** - This is normal. Tests still run.

### Windows: "pwsh not found"
```powershell
powershell -File run-windows-tests.ps1  # Use built-in PowerShell
```

### Windows: Tests hang
```powershell
Get-Process | Where-Object {$_.Name -like "*SuperTUI*"} | Stop-Process -Force
pwsh run-windows-tests.ps1
```

### Results not committing
**OK** - Results are in `test-results/latest/` even if git commit fails.

---

## When to Run Tests

### ALWAYS (Before Windows)
- ✅ Before git push
- ✅ After changing DI/services
- ✅ After adding/removing widgets

### SOMETIMES (Manual)
- ⚠️ After changing config schema
- ⚠️ After security changes
- ⚠️ Before "stable" releases

### NEVER SKIP
- ❌ Don't skip Linux tests to save 30s
- ❌ Don't commit without running tests
- ❌ Don't deploy to production without Windows tests

**Why?** 30s now saves 30-60min later.

---

## Test Writing Rules

### ✅ Write Linux Tests For
- Business logic
- Data transformations
- Service interactions
- Configuration handling
- State management
- Algorithms

### ❌ Write Windows Tests For
- UI rendering
- Theme visual changes
- Layout behavior
- User input handling
- Window management

**Golden Rule:** If it doesn't touch WPF types, write a Linux test.

---

## Success Indicators

### Healthy Test Run (Linux)
```
✅ ALL LINUX TESTS PASSED
✓ Safe to deploy to Windows
Duration: 28s
```

### Healthy Test Run (Windows)
```
✅ ALL TESTS PASSED
Duration: 118s
✓ Test results committed to git
```

### Unhealthy Test Run
```
❌ TESTS FAILED
  Failed: TaskServiceTests.AddTask_ShouldSucceed
  Failed: WidgetFactoryTests.CreateWidget_...

✗ DO NOT DEPLOY
```

**Action:** Fix, re-run, don't proceed until green.

---

## Pro Tips

1. **Always run Linux tests first** - Catches 70% of issues in 30s
2. **Commit test results** - Future you will thank you
3. **Check SUMMARY.txt** - Don't dig into logs unless needed
4. **Git pull after Windows tests** - Results auto-commit
5. **Don't skip tests to "save time"** - You'll lose more time debugging

---

## Emergency Commands

```bash
# Linux: Kill all .NET processes
pkill -9 dotnet

# Linux: Clean test artifacts
cd WPF/Tests && dotnet clean && rm -rf bin obj

# Windows: Nuclear option
Remove-Item -Recurse -Force WPF\Tests\bin, WPF\Tests\obj, test-results
dotnet clean WPF/SuperTUI.csproj
pwsh run-windows-tests.ps1

# Both: Fresh clone
cd .. && rm -rf supertui && git clone <repo> && cd supertui
```

---

**Last Updated:** 2025-10-27
**Maintained By:** You (solo dev)
**Print this or pin it** - You'll reference it daily
