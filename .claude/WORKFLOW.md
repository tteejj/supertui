# SuperTUI Development Workflow

## For Claude Code: How to Work on This Project

### Initial Context

Every time you start working on SuperTUI:

1. **Read the design:** Start with `DESIGN_DIRECTIVE.md`
2. **Check status:** Read `PROJECT_STATUS.md` and `TASKS.md`
3. **Load memory:** Review `.claude/CLAUDE.md` for context
4. **Understand goals:** Remember the three rules and success metrics

### When Starting a Task

1. **Create todos:** Use TodoWrite to track multi-step tasks
2. **Plan approach:** Consider if sub-agents would help
3. **Reference code:** Look at existing TUI implementations when needed
4. **Update status:** Keep PROJECT_STATUS.md current

### Implementation Guidelines

#### For C# Code
- Use XML documentation comments for public APIs
- Follow Microsoft C# naming conventions
- Keep methods focused and single-purpose
- Use LINQ where appropriate
- Optimize for readability first, performance second

#### For PowerShell Code
- Use approved verbs (Get-, New-, Set-, etc.)
- Add parameter validation
- Include comment-based help
- Use pipeline-friendly designs
- Follow PowerShell naming conventions

### Using Sub-Agents Effectively

**When to use sub-agents:**
- Multi-file refactoring
- Complex codebase exploration
- Parallel independent tasks
- Large file analysis

**How to use:**
```
Use Task tool with:
- subagent_type: "general-purpose" or "Explore"
- description: Brief task description
- prompt: Detailed instructions with expected output
```

**Launch in parallel:**
Use multiple Task tool calls in single message for independent work.

### Testing Strategy

**After each component:**
1. Write unit test
2. Create visual test screen
3. Test manually
4. Document any issues

**Before committing:**
1. All tests pass
2. No regressions
3. Code reviewed
4. Documentation updated

### Documentation Updates

**Keep these updated:**
- `.claude/CLAUDE.md` - Decisions, learnings, open questions
- `PROJECT_STATUS.md` - Phase progress, next steps
- `TASKS.md` - Task completion status
- Code comments - Why, not what

### Common Patterns

#### Creating a New Component (C#)

```csharp
/// <summary>
/// Brief description
/// </summary>
public class MyComponent : UIElement {
    // Properties
    public string Text { get; set; }

    // Constructor
    public MyComponent() {
        CanFocus = true;
    }

    // Rendering
    public override string Render(RenderContext ctx) {
        // Implementation
    }
}
```

#### Creating a Builder (PowerShell)

```powershell
function New-MyComponent {
    <#
    .SYNOPSIS
    Creates a new MyComponent instance.

    .DESCRIPTION
    Brief description of component.

    .PARAMETER Text
    The text to display.

    .EXAMPLE
    $comp = New-MyComponent -Text "Hello"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Text
    )

    $component = [SuperTUI.MyComponent]::new()
    $component.Text = $Text
    return $component
}
```

#### Creating a Screen (PowerShell)

```powershell
class MyScreen : SuperTUI.Screen {
    [object]$_service

    MyScreen() {
        $this.Title = "My Screen"

        # Get services
        $this._service = Get-Service "MyService"

        # Create layout
        $layout = New-GridLayout -Rows "*" -Columns "*"

        # Add components
        # ...

        $this.Children.Add($layout)

        # Register keys
        $this.RegisterKey("Escape", { Pop-Screen })

        # Subscribe to events
        [SuperTUI.EventBus]::Instance.add_DataChanged({
            # Handle event
        })
    }
}
```

### Problem-Solving Approach

1. **Understand the problem:** What needs to be solved?
2. **Check references:** Has this been solved in other TUIs?
3. **Design solution:** How does it fit the architecture?
4. **Implement incrementally:** Small testable steps
5. **Test thoroughly:** Unit + integration + visual
6. **Document:** Why this approach?

### Performance Considerations

**Always consider:**
- String allocations (use StringBuilder for loops)
- Collection operations (prefer List<T> over arrays for mutations)
- Layout calculations (cache when possible)
- Rendering (only redraw dirty regions)

**Profile if:**
- Rendering < 30 FPS
- Compilation > 2 seconds
- Memory growing over time

### Error Handling

**C# code:**
- Validate parameters (throw ArgumentException)
- Handle edge cases gracefully
- Use try-catch only where needed
- Log errors for debugging

**PowerShell code:**
- Validate parameters (ValidateNotNull, etc.)
- Use -ErrorAction appropriately
- Provide helpful error messages
- Don't swallow exceptions silently

### Commit Strategy

**Good commit:**
- Single logical change
- Tests pass
- Documentation updated
- Clear commit message

**Commit message format:**
```
[Component] Brief description

- Detailed point 1
- Detailed point 2

Refs #issue
```

### Code Review Checklist

Before considering code complete:

- [ ] Follows architectural principles
- [ ] Tests written and passing
- [ ] Documentation complete
- [ ] No TODO comments without issues
- [ ] Performance acceptable
- [ ] Error handling appropriate
- [ ] Code is readable
- [ ] No obvious bugs

### Getting Unstuck

If stuck:

1. **Check references:** Look at similar code in TUI implementations
2. **Review design:** Is this aligned with architecture?
3. **Simplify:** Can you make it simpler?
4. **Ask questions:** Update CLAUDE.md with open questions
5. **Try alternatives:** Test different approaches
6. **Take a break:** Come back with fresh perspective

### Success Indicators

You're on track when:
- ✅ Code is getting simpler, not more complex
- ✅ Tests are easy to write
- ✅ Components compose naturally
- ✅ Documentation writes itself
- ✅ Performance is good without heroics

### Red Flags

Watch out for:
- ❌ Lots of special cases
- ❌ Hard to test
- ❌ Tight coupling
- ❌ Performance requires hacks
- ❌ Documentation is confusing

### Resources Quick Reference

**Design:**
- `DESIGN_DIRECTIVE.md` - Complete architecture
- `.claude/CLAUDE.md` - Project memory
- `README.md` - Quick overview

**Planning:**
- `TASKS.md` - Task list
- `PROJECT_STATUS.md` - Current status

**Reference Code:**
- `/home/teej/_tui/praxis-main/` - Advanced patterns
- `/home/teej/_tui/alcar/` - Clean architecture
- `/home/teej/_tui/_R2/` - Services
- `/home/teej/pmc/ConsoleUI.Core.ps1` - Current implementation

**Tools:**
- TodoWrite - Task tracking
- Task tool - Sub-agents
- Slash commands - /implement, /status, /test

### Remember

**The three rules:**
1. Infrastructure in C#, Logic in PowerShell
2. Declarative over Imperative
3. Convention over Configuration

**The goal:**
70-80% code reduction with better maintainability and performance.

**The metric:**
New screen in <50 lines, zero manual positioning, zero manual refresh.

---

Keep this workflow in mind while implementing. Update it if you discover better patterns!
