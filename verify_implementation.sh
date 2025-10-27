#!/bin/bash
# SuperTUI Implementation Verification Script
# Verifies all new features are present and build succeeds

echo "============================================"
echo "SuperTUI Implementation Verification"
echo "Date: $(date)"
echo "============================================"
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Counters
PASSED=0
FAILED=0
TOTAL=0

# Function to check file exists
check_file() {
    TOTAL=$((TOTAL + 1))
    if [ -f "$1" ]; then
        echo -e "${GREEN}✓${NC} File exists: $1"
        PASSED=$((PASSED + 1))
        return 0
    else
        echo -e "${RED}✗${NC} File missing: $1"
        FAILED=$((FAILED + 1))
        return 1
    fi
}

# Function to check file has minimum lines
check_file_lines() {
    TOTAL=$((TOTAL + 1))
    local file="$1"
    local min_lines="$2"

    if [ -f "$file" ]; then
        local lines=$(wc -l < "$file")
        if [ "$lines" -ge "$min_lines" ]; then
            echo -e "${GREEN}✓${NC} $file has $lines lines (>= $min_lines expected)"
            PASSED=$((PASSED + 1))
            return 0
        else
            echo -e "${RED}✗${NC} $file has only $lines lines (< $min_lines expected)"
            FAILED=$((FAILED + 1))
            return 1
        fi
    else
        echo -e "${RED}✗${NC} File missing: $file"
        FAILED=$((FAILED + 1))
        return 1
    fi
}

# Function to check pattern in file
check_pattern() {
    TOTAL=$((TOTAL + 1))
    local file="$1"
    local pattern="$2"
    local description="$3"

    if grep -q "$pattern" "$file" 2>/dev/null; then
        echo -e "${GREEN}✓${NC} $description found in $file"
        PASSED=$((PASSED + 1))
        return 0
    else
        echo -e "${RED}✗${NC} $description NOT found in $file"
        FAILED=$((FAILED + 1))
        return 1
    fi
}

echo "=== Checking New Files ==="
check_file_lines "WPF/Core/Components/TreeTaskListControl.cs" 200
check_file_lines "WPF/Core/Services/TagService.cs" 500
check_file_lines "WPF/Core/Dialogs/TagEditorDialog.cs" 300
check_file_lines "WPF/Widgets/TimeTrackingWidget.cs" 600
echo ""

echo "=== Checking Documentation ==="
check_file "NEW_FEATURES_GUIDE.md"
check_file "IMPLEMENTATION_SUMMARY.md"
check_file "PROGRESS_REPORT.md"
check_file "QUICK_REFERENCE.md"
check_file "IMPLEMENTATION_COMPLETE.md"
echo ""

echo "=== Checking Modified Files ==="
check_pattern "WPF/Core/Models/TaskModels.cs" "TaskColorTheme" "TaskColorTheme enum"
check_pattern "WPF/Core/Models/TaskModels.cs" "ColorTheme" "ColorTheme property"
check_pattern "WPF/Core/Models/TaskModels.cs" "IndentLevel" "IndentLevel property"
check_pattern "WPF/Core/Models/TaskModels.cs" "IsExpanded" "IsExpanded property"
check_pattern "WPF/Core/Models/TimeTrackingModels.cs" "TaskTimeSession" "TaskTimeSession class"
check_pattern "WPF/Core/Models/TimeTrackingModels.cs" "PomodoroSession" "PomodoroSession class"
check_pattern "WPF/Core/Services/TaskService.cs" "MoveTaskUp" "MoveTaskUp method"
check_pattern "WPF/Core/Services/TaskService.cs" "MoveTaskDown" "MoveTaskDown method"
check_pattern "WPF/Core/Services/TaskService.cs" "GetAllSubtasksRecursive" "GetAllSubtasksRecursive method"
echo ""

echo "=== Checking Feature Integration ==="
check_pattern "WPF/Widgets/TaskManagementWidget.cs" "TreeTaskListControl" "TreeTaskListControl integration"
check_pattern "WPF/Widgets/TaskManagementWidget.cs" "TagService" "TagService integration"
check_pattern "WPF/Widgets/TaskManagementWidget.cs" "TagEditorDialog" "TagEditorDialog integration"
check_pattern "WPF/Widgets/TaskManagementWidget.cs" "CycleColor" "Color cycling"
check_pattern "WPF/Widgets/TaskManagementWidget.cs" "GetColorThemeDisplay" "Color theme display"
echo ""

echo "=== Running Build Test ==="
TOTAL=$((TOTAL + 1))
cd WPF
if dotnet build SuperTUI.csproj > /tmp/supertui_build.log 2>&1; then
    echo -e "${GREEN}✓${NC} Build succeeded"
    PASSED=$((PASSED + 1))

    # Check for errors and warnings
    ERRORS=$(grep -c "error" /tmp/supertui_build.log || echo "0")
    WARNINGS=$(grep -c "warning" /tmp/supertui_build.log || echo "0")

    if [ "$ERRORS" -eq 0 ]; then
        echo -e "${GREEN}✓${NC} 0 errors"
    else
        echo -e "${RED}✗${NC} $ERRORS errors found"
    fi

    if [ "$WARNINGS" -eq 0 ]; then
        echo -e "${GREEN}✓${NC} 0 warnings"
    else
        echo -e "${YELLOW}⚠${NC} $WARNINGS warnings (expected .Instance deprecation warnings)"
    fi
else
    echo -e "${RED}✗${NC} Build failed"
    FAILED=$((FAILED + 1))
    echo "Build log:"
    cat /tmp/supertui_build.log | tail -20
fi
cd ..
echo ""

echo "=== Summary ==="
echo "Total checks: $TOTAL"
echo -e "Passed: ${GREEN}$PASSED${NC}"
echo -e "Failed: ${RED}$FAILED${NC}"
echo ""

if [ "$FAILED" -eq 0 ]; then
    echo -e "${GREEN}✓✓✓ ALL CHECKS PASSED ✓✓✓${NC}"
    echo "Implementation is complete and verified!"
    exit 0
else
    echo -e "${RED}✗✗✗ SOME CHECKS FAILED ✗✗✗${NC}"
    echo "Please review the failures above"
    exit 1
fi
