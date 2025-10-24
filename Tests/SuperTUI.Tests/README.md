# SuperTUI Unit Tests

Comprehensive test suite for SuperTUI framework using xUnit, FluentAssertions, and Moq.

## Test Coverage

### Layout Tests (`Layout/`)
- ✅ **GridLayoutEngineTests** - Grid layout creation, validation, child management
  - Row/column bounds validation
  - Span validation
  - Splitter creation
  - Child add/remove operations

### Infrastructure Tests (`Infrastructure/`)
- ✅ **SecurityManagerTests** - Path validation and security
  - Allowed directory validation
  - Path traversal prevention
  - Directory name similarity attacks
  - Subdirectory access

### State Management Tests (`Extensions/`)
- ✅ **StatePersistenceTests** - State save/load, undo/redo
  - Save and load state
  - Undo/redo history
  - Backup creation
  - Version compatibility

## Running Tests

### On Windows (with .NET SDK):
```powershell
cd Tests/SuperTUI.Tests
dotnet test
```

### With Visual Studio:
Open the solution and use Test Explorer.

### With Rider:
Right-click project → Run Unit Tests

## Test Structure

```
Tests/SuperTUI.Tests/
├── Layout/
│   └── GridLayoutEngineTests.cs
├── Infrastructure/
│   └── SecurityManagerTests.cs
├── Extensions/
│   └── StatePersistenceTests.cs
└── SuperTUI.Tests.csproj
```

## Dependencies

- **xUnit** - Test framework
- **FluentAssertions** - Fluent assertion library
- **Moq** - Mocking framework
- **Microsoft.NET.Test.Sdk** - Test SDK

## Writing New Tests

### Test Class Template

```csharp
using System;
using FluentAssertions;
using SuperTUI.Core;
using Xunit;

namespace SuperTUI.Tests.YourNamespace
{
    public class YourClassTests : IDisposable
    {
        private readonly YourClass sut; // System Under Test

        public YourClassTests()
        {
            // Setup
            sut = new YourClass();
        }

        public void Dispose()
        {
            // Cleanup
        }

        [Fact]
        public void Method_Scenario_ExpectedBehavior()
        {
            // Arrange
            var input = "test";

            // Act
            var result = sut.Method(input);

            // Assert
            result.Should().Be("expected");
        }
    }
}
```

### Naming Conventions

- **Test Class**: `{ClassName}Tests`
- **Test Method**: `{MethodName}_{Scenario}_{ExpectedBehavior}`
- Examples:
  - `AddChild_WithValidRowAndColumn_AddsToGrid`
  - `ValidateFileAccess_WithPathTraversal_ReturnsFalse`
  - `Undo_ReturnsNullWhenNoHistory`

### FluentAssertions Examples

```csharp
// Equality
result.Should().Be(expected);
result.Should().NotBe(unexpected);

// Nullability
result.Should().BeNull();
result.Should().NotBeNull();

// Collections
list.Should().HaveCount(5);
list.Should().Contain(item);
list.Should().NotContain(item);
list.Should().BeEmpty();

// Exceptions
Action act = () => method(invalidInput);
act.Should().Throw<ArgumentException>();
act.Should().Throw<ArgumentException>()
   .WithMessage("*invalid*");

// Booleans
result.Should().BeTrue();
result.Should().BeFalse();

// Strings
str.Should().StartWith("Hello");
str.Should().Contain("world");
str.Should().Match("*pattern*");
```

## TODO: Additional Test Coverage Needed

### Phase 1 (Immediate):
- [ ] DockLayoutEngineTests
- [ ] StackLayoutEngineTests
- [ ] LayoutParamsTests
- [ ] WorkspaceTests
- [ ] WorkspaceManagerTests

### Phase 2 (Infrastructure):
- [ ] LoggerTests
- [ ] ConfigurationManagerTests
- [ ] ThemeManagerTests
- [ ] ErrorHandlerTests
- [ ] PluginManagerTests
- [ ] PerformanceMonitorTests

### Phase 3 (Widgets):
- [ ] WidgetBaseTests
- [ ] ClockWidgetTests
- [ ] CounterWidgetTests
- [ ] NotesWidgetTests
- [ ] TaskSummaryWidgetTests

### Phase 4 (Integration):
- [ ] End-to-end workspace switching tests
- [ ] State persistence integration tests
- [ ] Theme switching integration tests
- [ ] Widget lifecycle tests

## Test Principles

1. **AAA Pattern**: Arrange, Act, Assert
2. **One assertion per test** (when possible)
3. **Test behavior, not implementation**
4. **Use descriptive names**
5. **Clean up resources** (implement IDisposable)
6. **Isolate tests** (no shared state)
7. **Fast tests** (avoid Thread.Sleep, use Task.Delay with cancellation)

## Continuous Integration

Tests should run on:
- Every commit
- Pull requests
- Release builds

Configure GitHub Actions or Azure Pipelines to run:
```yaml
- name: Run tests
  run: dotnet test --configuration Release --no-build --verbosity normal
```

## Code Coverage

Aim for:
- **Core logic**: 90%+ coverage
- **Infrastructure**: 80%+ coverage
- **UI code**: 60%+ coverage (WPF is hard to test)

Use Coverlet for coverage:
```powershell
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Troubleshooting

### "Window requires STA thread"
WPF tests must run on STA thread. xUnit does this automatically for `[Fact]` tests.

### "Assembly not found"
Ensure all source files are included in `.csproj` with correct `Link` attributes.

### "Configuration not found"
Some tests initialize ConfigurationManager. If it fails, check temp directory permissions.

## Contributing

When adding new features:
1. ✅ Write tests first (TDD)
2. ✅ Run all tests before committing
3. ✅ Add tests to cover new code paths
4. ✅ Update this README if adding new test categories
