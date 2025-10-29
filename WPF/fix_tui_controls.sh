#!/bin/bash

# Fix Theme property names
find Core/Controls Widgets/TUIDemoWidget.cs -name "*.cs" -type f -exec sed -i 's/theme\.Text\>/theme.Foreground/g' {} \;
find Core/Controls Widgets/TUIDemoWidget.cs -name "*.cs" -type f -exec sed -i 's/theme\.TextDim\>/theme.ForegroundSecondary/g' {} \;

echo "Fixed theme property names"
