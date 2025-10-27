#!/bin/bash
# Pre-deployment validation script for Linux
# Runs fast, WPF-free tests to catch issues before touching Windows

set -e

echo "========================================"
echo "SuperTUI Pre-Deployment Check (Linux)"
echo "========================================"
echo ""

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Start timer
START_TIME=$(date +%s)

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}✗ .NET SDK not found${NC}"
    echo "Please install .NET 8.0 SDK"
    exit 1
fi

echo -e "${GREEN}✓ .NET SDK found: $(dotnet --version)${NC}"
echo ""

# Navigate to test project
cd "$(dirname "$0")/WPF/Tests"

# Restore packages
echo "Restoring packages..."
dotnet restore --verbosity quiet
if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Package restore failed${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Packages restored${NC}"
echo ""

# Build test project
echo "Building test project..."
dotnet build --configuration Release --no-restore --verbosity quiet
if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Build succeeded${NC}"
echo ""

# Run Linux-only tests
echo "Running Linux tests (DI, services, config, security)..."
echo "--------------------------------------------"
dotnet test \
    --configuration Release \
    --no-build \
    --filter "Category=Linux" \
    --logger "console;verbosity=normal" \
    --collect:"XPlat Code Coverage"

TEST_EXIT_CODE=$?

echo ""
echo "============================================"

# Calculate elapsed time
END_TIME=$(date +%s)
ELAPSED=$((END_TIME - START_TIME))

if [ $TEST_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✅ ALL LINUX TESTS PASSED${NC}"
    echo -e "${GREEN}✓ Safe to deploy to Windows${NC}"
    echo ""
    echo "Duration: ${ELAPSED}s"
    echo ""
    echo "Next steps:"
    echo "  1. Commit your changes"
    echo "  2. Push to git"
    echo "  3. On Windows: git pull"
    echo "  4. On Windows: pwsh run-windows-tests.ps1"
    exit 0
else
    echo -e "${RED}❌ LINUX TESTS FAILED${NC}"
    echo -e "${RED}✗ DO NOT DEPLOY TO WINDOWS${NC}"
    echo ""
    echo "Fix the failing tests before deploying."
    echo "Duration: ${ELAPSED}s"
    exit 1
fi
