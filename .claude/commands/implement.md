You are implementing the SuperTUI project. Follow these guidelines:

1. **Read the design directive first:** Always reference DESIGN_DIRECTIVE.md for architectural decisions

2. **Use sub-agents:** For complex tasks, use the Task tool with appropriate agent types:
   - general-purpose: Multi-step refactoring
   - Explore: Codebase investigation

3. **Update memory:** Keep .claude/CLAUDE.md updated with:
   - Architectural decisions
   - Implementation progress
   - Lessons learned
   - Open questions

4. **Follow the three rules:**
   - Infrastructure in C#, Logic in PowerShell
   - Declarative over Imperative
   - Convention over Configuration

5. **Test as you go:** Create test screens after implementing components

6. **Track progress:** Use TodoWrite for multi-step tasks

7. **Document:** Add comments for complex C# code, XML docs for public APIs

8. **Reference existing code:** Look at /home/teej/_tui/ implementations and /home/teej/pmc/ConsoleUI.Core.ps1 for patterns

Current phase: {{Determine from TASKS.md}}
Next task: {{Determine from TASKS.md}}
