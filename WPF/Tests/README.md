# SuperTUI Test Suite

## Overview

This test suite provides comprehensive unit tests for the SuperTUI WPF framework using xUnit and Moq.

## Test Coverage

### Infrastructure Tests
- **ConfigurationManagerTests** - Configuration management, persistence, validation
- **SecurityManagerTests** - File access validation, path traversal prevention
- **ThemeManagerTests** - Theme loading, switching, and persistence
- **ErrorHandlerTests** - Error handling, retry logic (sync/async)

### Layout Tests
- **GridLayoutEngineTests** - Grid layout, row/column validation, splitters

### Component Tests
- **WorkspaceTests** - Widget management, focus handling, state persistence

## Running Tests

### Windows (Visual Studio)
```powershell
dotnet test
```

### Command Line
```powershell
cd /home/teej/supertui/WPF/Tests
dotnet test --logger "console;verbosity=detailed"
```

### With Coverage
```powershell
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Structure

Each test class follows the Arrange-Act-Assert pattern:

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and dependencies
    var sut = new SystemUnderTest();

    // Act - Execute the method being tested
    var result = sut.MethodToTest();

    // Assert - Verify the expected outcome
    Assert.Equal(expected, result);
}
```

## Mocking

Tests use Moq for creating mock objects:

```csharp
var mockWidget = new Mock<IWidget>();
mockWidget.Setup(w => w.WidgetId).Returns(Guid.NewGuid());
mockWidget.Verify(w => w.Initialize(), Times.Once);
```

## Test Categories

### Unit Tests
- Test individual classes in isolation
- Mock all dependencies
- Fast execution (<100ms per test)

### Integration Tests (Future)
- Test component interactions
- Use real dependencies where appropriate
- May require WPF dispatcher

## Known Limitations

- **WPF Dependency**: Some tests require Windows and WPF runtime
- **UI Thread**: Tests involving WPF UI elements may need STA thread
- **File System**: Some tests use temp directories and files

## Adding New Tests

1. Create test class in appropriate folder (Infrastructure/, Layout/, Components/)
2. Follow naming convention: `{ClassUnderTest}Tests.cs`
3. Use `[Fact]` for individual tests, `[Theory]` for parameterized tests
4. Implement `IDisposable` if cleanup is needed
5. Ensure test names describe the scenario: `Method_Scenario_Expected`

## Test Naming Examples

✅ Good:
- `AddWidget_ShouldInitializeWidget`
- `ValidateFileAccess_PathTraversalAttempt_ShouldReturnFalse`
- `ExecuteWithRetryAsync_ShouldNotBlockUIThread`

❌ Bad:
- `Test1`
- `AddWidgetTest`
- `ItWorks`

## CI/CD Integration

Tests are designed to run in CI/CD pipelines. Ensure:
- All tests are deterministic (no random values without seeds)
- Tests clean up resources (temp files, directories)
- Tests don't depend on external services
- Tests complete within reasonable time (<5 seconds each)

## Future Enhancements

- [ ] Integration tests for widget interactions
- [ ] Performance benchmarks
- [ ] Snapshot testing for themes
- [ ] End-to-end UI automation tests
- [ ] Code coverage reporting (target: 80%+)
