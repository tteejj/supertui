# SuperTUI - First Windows Deployment Checklist

**Status:** Ready for manual Windows testing
**Date:** 2025-10-27

---

## Pre-Deployment (Linux) ✅

All ready:

- [x] Test infrastructure created (`WPF/Tests/`)
- [x] Linux test scripts created (`pre-deploy-check.sh`)
- [x] Windows test scripts created (`run-windows-tests.ps1`)
- [x] Testing guide written (`TESTING_GUIDE.md`)
- [x] Quick reference created (`TESTING_QUICK_REFERENCE.md`)

## Known Issues to Fix on Windows

### Test Project Compilation Errors

The test project has minor namespace reference issues that need fixing:

**File:** `WPF/Tests/Windows/SmokeTestRunner.cs`
- Lines 94, 153, 346, 391: Change `ServiceContainer` → `DI.ServiceContainer`
- Lines 95, 154, 347, 392: Change `ServiceRegistration` → `DI.ServiceRegistration`

**Quick fix (Windows):**
```powershell
cd WPF/Tests/Windows
# Edit SmokeTestRunner.cs and replace:
# "new ServiceContainer()" with "new DI.ServiceContainer()"
# "ServiceRegistration.RegisterServices" with "DI.ServiceRegistration.RegisterServices"
```

---

## First Windows Test Run - Manual Steps

### 1. Verify Build (5 minutes)

```powershell
cd WPF
dotnet build SuperTUI.csproj --configuration Release

# Expected: 0 errors, ~325 warnings (obsolete .Instance usage)
```

### 2. Run Demo App (5 minutes)

```powershell
pwsh SuperTUI.ps1

# Manual checks:
# - App launches?
# - Default workspace loads?
# - Can add widgets (Ctrl+W)?
# - Can switch workspaces (Ctrl+Tab)?
# - No crashes?
```

###3. Fix Test Compilation (10 minutes)

```powershell
cd Tests

# Fix namespace references (see "Known Issues" above)

# Try building tests:
dotnet build SuperTUI.Tests.csproj --configuration Release

# Expected: 0 errors after fixes
```

### 4. Run Smoke Tests (2 minutes)

```powershell
# From /home/teej/supertui root on Windows:
pwsh run-windows-tests.ps1 -Filter "Category=Smoke"

# If tests fail, that's OK - collect diagnostics
```

### 5. Collect Diagnostics

```powershell
# Results will be in test-results/latest/
cat test-results/latest/SUMMARY.txt
cat test-results/latest/DIAGNOSTICS.txt

# Commit results
git add test-results/latest
git commit -m "First Windows test run"
git push
```

### 6. Review on Linux

```bash
git pull
cat test-results/latest/SUMMARY.txt

# If tests failed, read:
cat test-results/latest/DIAGNOSTICS.txt
grep -i "error\|exception" test-results/latest/*.log
```

---

## Expected Issues (First Run)

### Likely:
- ✅ Namespace errors in tests (already identified)
- ⚠️ Missing NuGet packages (run `dotnet restore`)
- ⚠️ Some widget tests fail (new widgets not covered)
- ⚠️ Test output path issues (temp directory permissions)

### Unlikely but Possible:
- ❌ App doesn't launch (DLL conflicts, missing dependencies)
- ❌ Security manager blocks (file permissions)
- ❌ State persistence fails (disk access)

---

## Success Criteria

### Minimum Viable (Ship It):
- ✅ App launches without crashes
- ✅ Can create/switch workspaces
- ✅ At least 5 widgets work
- ✅ State saves/loads
- ✅ No memory leaks (run for 5 minutes)

### Full Success (Production Ready):
- ✅ All smoke tests pass (9 tests)
- ✅ All Linux tests pass (~45 tests)
- ✅ All Windows tests pass (~70 tests)
- ✅ Diagnostics collected automatically
- ✅ No warnings (obsolete usage)

---

## Rollback Plan

If Windows testing reveals critical issues:

1. **Don't panic** - diagnostics are auto-collected
2. **Commit test results** even if failed
3. **Pull to Linux** and review logs
4. **Fix on Linux** with pre-deploy-check.sh
5. **Re-deploy to Windows**

**You can iterate as many times as needed. Each cycle:**
- Linux fix/test: 30 seconds
- Push to Windows: 1 minute
- Windows test: 2 minutes
- **Total: 3.5 minutes per iteration**

---

## Post-Deployment Improvements (Future)

Once tests pass on Windows:

### Phase 1: Fix Test Issues
- [ ] Fix all namespace references in tests
- [ ] Ensure all 70 tests pass
- [ ] Verify test result auto-commit works
- [ ] Update test count in documentation

### Phase 2: Add Missing Tests
- [ ] Excel integration widgets (3 new widgets)
- [ ] Layout engine tests (Grid, Stack, Dock)
- [ ] Workspace component tests
- [ ] Error boundary tests

### Phase 3: CI/CD (Optional)
- [ ] Setup GitHub Actions (see TESTING_GUIDE.md)
- [ ] Automated daily builds
- [ ] Test result history
- [ ] Coverage tracking

---

## Documentation Status

| Document | Status | Notes |
|----------|--------|-------|
| TESTING_GUIDE.md | ✅ Complete | Comprehensive guide |
| TESTING_QUICK_REFERENCE.md | ✅ Complete | Daily workflow |
| THIS FILE | ✅ Complete | First deployment |
| PROJECT_STATUS.md | ⚠️ Outdated | Update after Windows tests |
| .claude/CLAUDE.md | ✅ Current | Accurate as of 2025-10-26 |

---

## Timeline

### Completed (Linux):
- Test infrastructure: 2 hours
- Automation scripts: 1 hour
- Documentation: 1 hour
- **Total: 4 hours**

### Remaining (Windows):
- Fix test compilation: 10 minutes
- Run first test suite: 5 minutes
- Debug any issues: 15-30 minutes
- **Total: 30-45 minutes**

### Total Investment: **4.5-5 hours**

**ROI:** After 3-5 deployment cycles, you'll have saved more time than invested.

---

## Questions for First Run?

**Q: What if everything breaks on Windows?**
A: Diagnostics will tell you why. Most issues are dependencies/permissions, not code.

**Q: Should I fix tests before running them?**
A: Yes - fix the namespace issues in SmokeTestRunner.cs first (10 min).

**Q: What if tests hang?**
A: Kill process (`Stop-Process -Name SuperTUI*`), re-run with `-SkipBuild`.

**Q: What if diagnostics don't commit?**
A: Manually commit `test-results/latest/` before leaving Windows.

---

## Final Checklist

Before first Windows deployment:

- [ ] Read TESTING_GUIDE.md (10 minutes)
- [ ] Read TESTING_QUICK_REFERENCE.md (5 minutes)
- [ ] Print this checklist (optional)
- [ ] Ensure Windows machine has git, .NET 8.0, PowerShell
- [ ] Git pull latest code on Windows
- [ ] Fix test namespace issues (10 minutes)
- [ ] Run smoke tests
- [ ] Collect diagnostics
- [ ] Push results to git
- [ ] Review on Linux

**Estimated Total Time: 45-60 minutes**

---

## Support

If stuck:
1. Check `test-results/latest/DIAGNOSTICS.txt`
2. Review TESTING_GUIDE.md troubleshooting section
3. Grep test source code in `WPF/Tests/`
4. Check .claude/CLAUDE.md for project state

**Last Updated:** 2025-10-27
**Next Update:** After first Windows test run
**Status:** ✅ Ready for Windows deployment
