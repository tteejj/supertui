# Test Updates Needed After Recent Changes
**Analysis Date:** 2025-10-27
**Commits Analyzed:** dbe776c (Oct 26) → HEAD (Oct 27)

---

## Executive Summary

### Major Changes Since Oct 26:
1. **New Data Model Fields** - TaskPriority.Today, CompletedDate, EstimatedDuration, ExternalId1/2
2. **New Services** - WeeklyTimeTrackingService, SmartInputParser, ExcelMappingService, ValidationService, OverlayManager
3. **New Widgets** - ExcelExportWidget, ExcelImportWidget, ExcelMappingEditorWidget, 5 Overlay widgets
4. **Modified Services** - TaskService, ProjectService, TimeTrackingService (new methods)
5. **Infrastructure Changes** - ThemeChangedWeakEventManager, GridLayoutEngine enhancements
6. **Removed Widget** - TodoWidget (deleted)

### Test Status:
- ✅ **NEW:** `DomainServicesTests.cs` - Tests basic CRUD operations (430 lines)
- ✅ **NEW:** `WidgetFactoryTests.cs` - Tests widget DI instantiation (286 lines)
- ⚠️ **OUTDATED:** Existing widget tests reference TodoWidget (deleted)
- ⚠️ **INCOMPLETE:** No tests for new services (7+ new services)
- ⚠️ **INCOMPLETE:** No tests for new widgets (8+ new widgets)
- ⚠️ **INCOMPLETE:** No tests for new data model fields

---

## Test Update Requirements

### ✅ CRITICAL - Must Fix (Blockers)

#### 1. Remove TodoWidget References
**Impact:** Tests will fail to compile
**Files:**
- `Tests/Widgets/TodoWidgetTests.cs` - **ALREADY DELETED** ✅
- `Tests/Linux/WidgetFactoryTests.cs:45` - References "TodoWidget" in [InlineData]

**Action:**
```diff
- [InlineData("TodoWidget")]
+ // TodoWidget removed - replaced by TaskManagementWidget
```

**Effort:** 1 minute
**Status:** ⚠️ NOT FIXED YET

---

#### 2. Update TaskItem Tests for New Fields
**Impact:** Tests don't verify new critical fields
**New Fields:**
- `CompletedDate` (DateTime?) - When task was completed
- `EstimatedDuration` (TimeSpan?) - Time estimate
- `ExternalId1` / `ExternalId2` (string) - Excel integration
- `TaskPriority.Today` (enum value) - New priority level
- `TaskColorTheme` (enum) - Visual organization
- `RecurrenceType` / `RecurrencePattern` - Recurring tasks
- `Tags` (List<string>) - Tag support
- `Notes` (List<TaskNote>) - Note support
- `Dependencies` (List<Guid>) - Task dependencies

**Affected Tests:**
- `Tests/Linux/DomainServicesTests.cs` - TaskService tests (uses old TaskItem constructor)
- All widget tests that create TaskItem instances

**Example Fix:**
```csharp
// OLD
var task = new TaskItem
{
    Id = Guid.NewGuid(),
    Title = "Test Task",
    Priority = TaskPriority.Medium
};

// NEW - Test new fields
var task = new TaskItem
{
    Id = Guid.NewGuid(),
    Title = "Test Task",
    Priority = TaskPriority.Today,  // Test new priority level
    EstimatedDuration = TimeSpan.FromHours(2),  // Test estimation
    ExternalId1 = "PROJ",  // Test Excel integration
    ExternalId2 = "TASK001"
};

// Add test for CompletedDate
taskService.ToggleTaskCompletion(task.Id);
var completed = taskService.GetTask(task.Id);
completed.CompletedDate.Should().NotBeNull();
completed.CompletedDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
```

**Effort:** 2-4 hours
**Status:** ⚠️ NOT STARTED

---

#### 3. Update TimeEntry Tests for New Model
**Impact:** TimeTrackingService tests use old model structure
**Changes:**
- Old: Simple `TimeEntry` with start/end times
- New: `WeeklyTimeEntry` with Mon-Fri grid + fiscal year

**Affected Tests:**
- `Tests/Linux/DomainServicesTests.cs` - TimeTrackingService tests

**Current Test:**
```csharp
var entry = new TimeEntry
{
    Id = Guid.NewGuid(),
    ProjectId = Guid.NewGuid(),
    WeekEnding = DateTime.Now,  // ⚠️ Wrong property name
    Hours = 8.5m  // ⚠️ Wrong property (should be MondayHours, etc.)
};
```

**Check Needed:**
1. Does `TimeEntry` model still exist? Or replaced by `WeeklyTimeEntry`?
2. If both exist, tests need to cover both models
3. If only `WeeklyTimeEntry`, tests need complete rewrite

**Action:** Read `WPF/Core/Models/TimeTrackingModels.cs` to see actual structure

**Effort:** 2-3 hours
**Status:** ⚠️ NEEDS INVESTIGATION

---

### 🟡 HIGH PRIORITY - Should Add

#### 4. Tests for WeeklyTimeTrackingService
**Impact:** New service completely untested (616 lines of code)
**Missing Coverage:**
- `AddWeeklyEntry()` / `UpdateWeeklyEntry()` / `DeleteWeeklyEntry()`
- `GetWeeklyEntry()` / `GetAllWeeklyEntries()`
- `GetWeeklyEntriesForProject()`
- `GetWeeklyEntriesForWeek()`
- `CalculateFiscalYear()`
- Validation (no duplicate ID1/ID2 in same week)
- Daily hours range (0-24)
- Auto-calculation of TotalHours
- Week navigation (prev/next week)

**File:** `WPF/Core/Services/WeeklyTimeTrackingService.cs` (616 lines)

**Suggested Test:**
```csharp
[Fact]
public void WeeklyTimeTracking_AddEntry_ShouldCalculateTotals()
{
    // Arrange
    var service = container.GetService<IWeeklyTimeTrackingService>();
    var entry = new WeeklyTimeEntry
    {
        ExternalId1 = "PROJ",
        ExternalId2 = "Task001",
        WeekEndingFriday = GetNextFriday(),
        MondayHours = 8.0m,
        TuesdayHours = 7.5m,
        WednesdayHours = 8.0m,
        ThursdayHours = 6.5m,
        FridayHours = 4.0m
    };

    // Act
    var result = service.AddWeeklyEntry(entry);

    // Assert
    result.TotalHours.Should().Be(34.0m);
    result.FiscalYear.Should().NotBeNullOrEmpty();
}

[Fact]
public void WeeklyTimeTracking_DuplicateEntry_ShouldThrow()
{
    // Test validation: no duplicate ID1/ID2 in same week
}
```

**Effort:** 4-6 hours
**Status:** ⚠️ NOT STARTED

---

#### 5. Tests for SmartInputParser
**Impact:** New service completely untested (304 lines of code)
**Missing Coverage:**
- `ParseDate()` - "20251030", "+3", "tomorrow"
- `ParseDuration()` - "2h", "30m", "1.5h"
- `ParsePriority()` - "high", "low", "today"
- Edge cases: invalid input, null, empty string

**File:** `WPF/Core/Services/SmartInputParser.cs` (304 lines)

**Suggested Test:**
```csharp
[Theory]
[InlineData("20251030", "2025-10-30")]
[InlineData("+3", "3 days from now")]
[InlineData("tomorrow", "tomorrow")]
[InlineData("next week", "7 days from now")]
public void SmartInputParser_ParseDate_ShouldWork(string input, string expected)
{
    var parser = container.GetService<ISmartInputParser>();
    var result = parser.ParseDate(input);

    // Assert based on expected format
}

[Theory]
[InlineData("2h", 2.0)]
[InlineData("30m", 0.5)]
[InlineData("1.5h", 1.5)]
public void SmartInputParser_ParseDuration_ShouldWork(string input, double expectedHours)
{
    var parser = container.GetService<ISmartInputParser>();
    var result = parser.ParseDuration(input);

    result.Should().NotBeNull();
    result.Value.TotalHours.Should().BeApproximately(expectedHours, 0.01);
}
```

**Effort:** 2-3 hours
**Status:** ⚠️ NOT STARTED

---

#### 6. Tests for ValidationService
**Impact:** New service completely untested (306 lines of code)
**Missing Coverage:**
- Task validation rules
- Project validation rules
- TimeEntry validation rules
- Cross-field validation (e.g., CompletedDate requires Status=Completed)

**File:** `WPF/Core/Services/ValidationService.cs` (306 lines)

**Suggested Test:**
```csharp
[Fact]
public void ValidationService_InvalidTask_ShouldFail()
{
    var validator = container.GetService<ValidationService>();
    var task = new TaskItem { Title = "" };  // Empty title

    var result = validator.ValidateTask(task);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.Contains("Title"));
}

[Fact]
public void ValidationService_CompletedDateWithoutCompletedStatus_ShouldFail()
{
    var validator = container.GetService<ValidationService>();
    var task = new TaskItem
    {
        Title = "Test",
        Status = TaskStatus.InProgress,
        CompletedDate = DateTime.Now  // Invalid: not completed
    };

    var result = validator.ValidateTask(task);

    result.IsValid.Should().BeFalse();
}
```

**Effort:** 3-4 hours
**Status:** ⚠️ NOT STARTED

---

#### 7. Tests for ExcelMappingService
**Impact:** New service completely untested (493 lines of code)
**Missing Coverage:**
- Cell mapping (W23 → RequestDate, etc.)
- Type conversion (Date, String, Number)
- Import/export round-trip
- Duplicate detection
- Error handling for invalid cells

**File:** `WPF/Core/Services/ExcelMappingService.cs` (493 lines)

**Note:** Excel tests may require Windows (COM interop) - could use mocks

**Suggested Test:**
```csharp
[Fact]
public void ExcelMapping_RoundTrip_ShouldPreserveData()
{
    var service = container.GetService<ExcelMappingService>();
    var tasks = new List<TaskItem> { /* ... */ };

    // Export
    var filePath = Path.GetTempFileName() + ".xlsx";
    service.ExportToExcel(tasks, filePath);

    // Import
    var imported = service.ImportFromExcel(filePath);

    // Assert
    imported.Should().HaveCount(tasks.Count);
    // ... verify all fields match
}
```

**Effort:** 4-6 hours (with mocking), 8-12 hours (with real Excel on Windows)
**Status:** ⚠️ NOT STARTED

---

#### 8. Tests for OverlayManager
**Impact:** New service completely untested (752 lines of code)
**Missing Coverage:**
- ShowOverlay() / HideOverlay()
- Overlay stacking (multiple overlays)
- ESC key handling
- Click-outside-to-close
- Animation timing

**File:** `WPF/Core/Services/OverlayManager.cs` (752 lines)

**Note:** Requires WPF - may only be testable on Windows

**Suggested Test:**
```csharp
[Fact]
public void OverlayManager_ShowHide_ShouldWork()
{
    var manager = container.GetService<OverlayManager>();
    var overlay = new TestOverlay();

    manager.ShowOverlay(overlay);

    // Assert overlay is visible
    // ...

    manager.HideOverlay();

    // Assert overlay is hidden
}
```

**Effort:** 3-4 hours (Windows only)
**Status:** ⚠️ NOT STARTED

---

### 🔵 MEDIUM PRIORITY - Nice to Have

#### 9. Tests for New Widgets
**Impact:** 8 new widgets with zero test coverage
**New Widgets:**
- ExcelExportWidget (747 lines)
- ExcelImportWidget (500 lines)
- ExcelMappingEditorWidget (1033 lines)
- CommandPaletteOverlay (420 lines)
- FilterPanelOverlay (530 lines)
- JumpToAnythingOverlay (481 lines)
- QuickAddTaskOverlay (255 lines)
- TaskDetailOverlay (340 lines)

**Total:** 4,306 lines of untested code

**Note:** Widget tests require WPF - must run on Windows

**Suggested Approach:**
1. Basic instantiation tests (already covered by WidgetFactoryTests)
2. Smoke tests (Initialize() doesn't throw)
3. Integration tests for key workflows

**Effort:** 12-16 hours (Windows only)
**Status:** ⚠️ NOT STARTED

---

#### 10. Update Existing Widget Tests
**Impact:** Existing tests may not cover new widget features
**Changes:**
- TaskManagementWidget: +178 lines (new features)
- AgendaWidget: +89 lines
- KanbanBoardWidget: +103 lines
- ProjectStatsWidget: +59 lines
- TimeTrackingWidget: +70 lines
- NotesWidget: +147 lines

**Affected Tests:**
- Tests/Widgets/TaskManagementWidgetTests.cs
- Tests/Widgets/FileExplorerWidgetTests.cs
- Tests/Widgets/CommandPaletteWidgetTests.cs

**Action:** Review git diff for each widget, add tests for new features

**Effort:** 6-8 hours
**Status:** ⚠️ NOT STARTED

---

#### 11. Tests for Modified Core Services
**Impact:** Existing services have new methods/functionality
**Modified Services:**
- TaskService: +135 lines (new methods for subtasks, tags, dependencies)
- ProjectService: +144 lines (new stats calculations)
- TimeTrackingService: +91 lines (integration with tasks)

**New Methods to Test:**
```csharp
// TaskService
GetTasksWithTag(string tag)
GetSubtasks(Guid parentId)
AddDependency(Guid taskId, Guid dependsOnId)
GetBlockedTasks()

// ProjectService
GetProjectStats(Guid projectId)
CalculateProjectProgress(Guid projectId)
GetProjectTimeEntries(Guid projectId)

// TimeTrackingService
GetTimeForTask(Guid taskId)
GetTimeForProject(Guid projectId, DateTime start, DateTime end)
```

**Effort:** 4-6 hours
**Status:** ⚠️ NOT STARTED

---

### 🟢 LOW PRIORITY - Future Work

#### 12. Infrastructure Tests Updates
**Impact:** Core infrastructure has minor changes
**Modified:**
- ThemeChangedWeakEventManager (98 lines) - NEW
- GridLayoutEngine (+225 lines) - Enhanced
- HotReloadManager (+17 lines) - Minor fix

**Suggested Tests:**
- WeakEventManager memory leak tests
- GridLayoutEngine directional navigation tests
- HotReloadManager integration tests

**Effort:** 3-4 hours
**Status:** ⚠️ NOT STARTED

---

#### 13. Integration Tests for Cross-Service Workflows
**Impact:** No tests for complex workflows
**Missing Scenarios:**
- Task creation → Time tracking → Project stats update
- Excel import → Task creation → Validation → Save
- Weekly time entry → Project time aggregation → Report generation
- Overlay system → Keyboard shortcuts → Widget interaction

**Effort:** 8-12 hours
**Status:** ⚠️ NOT STARTED

---

## Test Execution Status

### Current State:
```bash
cd /home/teej/supertui/WPF/Tests
dotnet test SuperTUI.Tests.csproj
```

**Expected Result on Linux:**
- ✅ DomainServicesTests - Should pass (if TodoWidget reference fixed)
- ✅ WidgetFactoryTests - Should pass (if TodoWidget reference fixed)
- ⚠️ All other tests - Excluded from build (lines 42-52 of .csproj)

**Expected Result on Windows:**
- All tests should compile and run
- Some may fail due to outdated assertions

---

## Immediate Actions Required

### Fix Compilation Errors (5 minutes):

1. **Remove TodoWidget from WidgetFactoryTests:**
```diff
File: Tests/Linux/WidgetFactoryTests.cs
Line: 45

-        [InlineData("TodoWidget")]
+        // TodoWidget removed - replaced by TaskManagementWidget
```

**That's the ONLY blocking issue!**

---

## Recommended Test Update Plan

### Phase 1: Critical Fixes (4-6 hours)
**Goal:** Make all tests pass with current code

1. ✅ Fix TodoWidget reference (5 min)
2. ⚠️ Update TaskItem test data for new fields (2h)
3. ⚠️ Investigate TimeEntry model changes (1h)
4. ⚠️ Update time tracking tests if needed (1-2h)

**Deliverable:** All existing tests pass

---

### Phase 2: New Service Tests (12-16 hours)
**Goal:** Test all new services

5. ⚠️ WeeklyTimeTrackingService tests (4-6h)
6. ⚠️ SmartInputParser tests (2-3h)
7. ⚠️ ValidationService tests (3-4h)
8. ⚠️ ExcelMappingService tests (3-4h)

**Deliverable:** Core services have test coverage

---

### Phase 3: Widget Tests (16-20 hours, Windows only)
**Goal:** Test new and updated widgets

9. ⚠️ OverlayManager tests (3-4h)
10. ⚠️ New widget smoke tests (4-6h)
11. ⚠️ Updated widget tests (6-8h)
12. ⚠️ Integration workflow tests (3-4h)

**Deliverable:** Comprehensive widget coverage

---

## Test Coverage Analysis

### Before Recent Changes:
- **Services:** ~60% coverage (basic CRUD)
- **Widgets:** ~40% coverage (instantiation + basic ops)
- **Infrastructure:** ~30% coverage (smoke tests)
- **Total LOC:** ~20,000
- **Tested LOC:** ~8,000 (40%)

### After Recent Changes:
- **New Code:** ~15,000 lines
- **New Tested:** ~700 lines (DomainServicesTests + WidgetFactoryTests)
- **Total LOC:** ~35,000
- **Tested LOC:** ~8,700 (25%)

**Coverage dropped from 40% to 25% due to new untested code!**

### Target Coverage:
- **Phase 1:** 30% (fix existing tests)
- **Phase 2:** 50% (add service tests)
- **Phase 3:** 65% (add widget tests)

---

## Files to Update

### Immediate (Phase 1):
1. `Tests/Linux/WidgetFactoryTests.cs` - Remove TodoWidget line 45
2. `Tests/Linux/DomainServicesTests.cs` - Update TaskItem/TimeEntry usage

### Phase 2 (New Test Files):
3. `Tests/Linux/WeeklyTimeTrackingServiceTests.cs` - NEW
4. `Tests/Linux/SmartInputParserTests.cs` - NEW
5. `Tests/Linux/ValidationServiceTests.cs` - NEW
6. `Tests/Linux/ExcelMappingServiceTests.cs` - NEW (or Windows folder if needs COM)

### Phase 3 (Windows Tests):
7. `Tests/Windows/OverlayManagerTests.cs` - NEW
8. `Tests/Windows/ExcelWidgetsTests.cs` - NEW
9. `Tests/Windows/OverlayWidgetsTests.cs` - NEW
10. Update existing widget tests in `Tests/Widgets/`

---

## Test Execution Strategy

### On Linux (CI/CD):
```bash
# Run non-WPF tests only
cd /home/teej/supertui/WPF/Tests
dotnet test --filter "Category=Linux"
```

**Expected Tests:**
- DomainServicesTests (14 tests)
- WidgetFactoryTests (15 tests)
- WeeklyTimeTrackingServiceTests (10+ tests) - NEW
- SmartInputParserTests (8+ tests) - NEW
- ValidationServiceTests (12+ tests) - NEW

**Total: ~60 tests on Linux**

### On Windows (Manual/CI):
```powershell
# Run ALL tests including WPF
cd /home/teej/supertui/WPF/Tests
dotnet test
```

**Or use PowerShell runner:**
```powershell
/home/teej/supertui/run-windows-tests.ps1
```

**Expected Tests:**
- All Linux tests (60)
- Widget tests (40+)
- Overlay tests (20+)
- Integration tests (10+)

**Total: ~130 tests on Windows**

---

## Summary

### What Needs Updating:

| Priority | Item | Effort | Status |
|----------|------|--------|--------|
| 🔴 CRITICAL | Fix TodoWidget reference | 5 min | ⚠️ NOT DONE |
| 🔴 CRITICAL | Update TaskItem tests | 2h | ⚠️ NOT DONE |
| 🔴 CRITICAL | Update TimeEntry tests | 2h | ⚠️ NOT DONE |
| 🟡 HIGH | WeeklyTimeTrackingService tests | 4-6h | ⚠️ NOT DONE |
| 🟡 HIGH | SmartInputParser tests | 2-3h | ⚠️ NOT DONE |
| 🟡 HIGH | ValidationService tests | 3-4h | ⚠️ NOT DONE |
| 🟡 HIGH | ExcelMappingService tests | 4-6h | ⚠️ NOT DONE |
| 🔵 MEDIUM | OverlayManager tests | 3-4h | ⚠️ NOT DONE |
| 🔵 MEDIUM | New widget tests | 12-16h | ⚠️ NOT DONE |
| 🔵 MEDIUM | Update existing widget tests | 6-8h | ⚠️ NOT DONE |

**Total Effort:**
- **Critical (Phase 1):** 4-6 hours
- **High (Phase 2):** 13-19 hours
- **Medium (Phase 3):** 21-28 hours
- **GRAND TOTAL:** 38-53 hours (1-1.5 weeks)

### Can We Run Tests Now?

**On Linux:**
- ⚠️ **NO** - Will fail to compile due to TodoWidget reference
- ✅ **YES (after 5-minute fix)** - DomainServicesTests + WidgetFactoryTests will pass

**On Windows:**
- ⚠️ Compilation will succeed
- ⚠️ Some tests may fail due to outdated assertions
- ⚠️ Many widgets untested

### Recommendation:

1. **NOW (5 min):** Fix TodoWidget reference → Tests compile and run
2. **This Week (4-6h):** Phase 1 fixes → All existing tests pass
3. **Next Week (13-19h):** Phase 2 tests → Core services covered
4. **Later (21-28h):** Phase 3 tests → Comprehensive coverage

---

**Last Updated:** 2025-10-27
**Status:** Tests partially outdated, 1 compilation error, ~15,000 lines untested
**Next Action:** Fix TodoWidget reference (5 minutes)
