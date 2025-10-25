#!/usr/bin/env python3
"""
Automated DI migration script for SuperTUI widgets
Adds DI constructors and replaces .Instance usages with injected dependencies
"""

import re
import os
import sys
from pathlib import Path

# Widget files to process (excluding already migrated ones)
WIDGETS_TO_MIGRATE = [
    "SettingsWidget.cs",
    "ShortcutHelpWidget.cs",
    "TodoWidget.cs",
    "CommandPaletteWidget.cs",
    "SystemMonitorWidget.cs",
    "TaskManagementWidget.cs",
    "GitStatusWidget.cs",
    "FileExplorerWidget.cs",
    "AgendaWidget.cs",
    "ProjectStatsWidget.cs",
    "KanbanBoardWidget.cs",
]

DI_CONSTRUCTOR_TEMPLATE = '''
        private readonly ILogger logger;
        private readonly IThemeManager themeManager;
        private readonly IConfigurationManager config;

        /// <summary>
        /// DI constructor - preferred for new code
        /// </summary>
        public {class_name}(
            ILogger logger,
            IThemeManager themeManager,
            IConfigurationManager config)
        {{
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            this.config = config ?? throw new ArgumentNullException(nameof(config));

{initialization}
        }}

        /// <summary>
        /// Parameterless constructor for backward compatibility
        /// </summary>
        public {class_name}()
            : this(Logger.Instance, ThemeManager.Instance, ConfigurationManager.Instance)
        {{
        }}
'''


def migrate_widget(file_path):
    """Migrate a single widget file to use DI"""
    print(f"\nProcessing: {file_path.name}")

    content = file_path.read_text()
    original_content = content

    # Extract class name
    class_match = re.search(r'public class (\w+Widget)', content)
    if not class_match:
        print(f"  ❌ Could not find class name")
        return False

    class_name = class_match.group(1)
    print(f"  Class: {class_name}")

    # Check if already migrated
    if 'private readonly ILogger logger;' in content:
        print(f"  ⏭️  Already migrated - skipping")
        return False

    # Count .Instance usages
    instance_count = len(re.findall(r'\.(Instance)(?!\w)', content))
    print(f"  Found {instance_count} .Instance usages")

    # Find the parameterless constructor
    # Pattern: public ClassName() { ... }
    ctor_pattern = rf'public {class_name}\s*\([^)]*\)\s*{{([^{{}}]*(?:{{[^{{}}]*}}[^{{}}]*)*?)}}'
    ctor_match = re.search(ctor_pattern, content, re.DOTALL)

    if not ctor_match:
        print(f"  ❌ Could not find parameterless constructor")
        return False

    ctor_body = ctor_match.group(1)
    print(f"  Found constructor")

    # Extract initialization code from constructor body
    initialization = '\n'.join(f'            {line}' for line in ctor_body.strip().split('\n'))

    # Generate DI constructors
    di_code = DI_CONSTRUCTOR_TEMPLATE.format(
        class_name=class_name,
        initialization=initialization
    )

    # Find the position to insert DI fields and constructors
    # Insert after class opening brace
    class_start_pattern = rf'public class {class_name}[^{{]*{{([^{{]*)(?=private|public|protected|internal)'

    def replace_constructor(match):
        prefix = match.group(1)
        return f'public class {class_name} : WidgetBase, IThemeable\n    {{{di_code}\n\n{prefix[prefix.find("private"):]}'

    # Replace the old constructor pattern with DI constructors
    content = re.sub(class_start_pattern, replace_constructor, content, count=1, flags=re.DOTALL)

    # Remove the old parameterless constructor
    content = re.sub(ctor_pattern, '', content, count=1, flags=re.DOTALL)

    # Replace .Instance usages
    replacements = [
        (r'Logger\.Instance', 'logger'),
        (r'ThemeManager\.Instance', 'themeManager'),
        (r'ConfigurationManager\.Instance', 'config'),
    ]

    for pattern, replacement in replacements:
        old_count = len(re.findall(pattern, content))
        content = re.sub(pattern, replacement, content)
        new_count = len(re.findall(pattern, content))
        if old_count > 0:
            print(f"  Replaced {old_count - new_count} {pattern}")

    # Write back
    if content != original_content:
        file_path.write_text(content)
        print(f"  ✅ Migrated successfully")
        return True
    else:
        print(f"  ⚠️  No changes made")
        return False


def main():
    """Main migration function"""
    widgets_dir = Path(__file__).parent / "Widgets"

    if not widgets_dir.exists():
        print(f"Error: Widgets directory not found: {widgets_dir}")
        return 1

    print("=" * 60)
    print("SuperTUI Widget DI Migration")
    print("=" * 60)

    migrated_count = 0
    failed_count = 0

    for widget_file in WIDGETS_TO_MIGRATE:
        file_path = widgets_dir / widget_file

        if not file_path.exists():
            print(f"\n❌ File not found: {widget_file}")
            failed_count += 1
            continue

        try:
            if migrate_widget(file_path):
                migrated_count += 1
        except Exception as e:
            print(f"  ❌ Error: {e}")
            failed_count += 1

    print("\n" + "=" * 60)
    print(f"Migration complete:")
    print(f"  Migrated: {migrated_count}")
    print(f"  Failed: {failed_count}")
    print(f"  Skipped: {len(WIDGETS_TO_MIGRATE) - migrated_count - failed_count}")
    print("=" * 60)

    return 0 if failed_count == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
