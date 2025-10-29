#!/bin/bash
cd /home/teej/supertui/WPF
echo "Cleaning..."
dotnet clean SuperTUI.csproj
echo "Building Release..."
dotnet build SuperTUI.csproj --configuration Release
echo "Done. DLL at: bin/Release/net8.0-windows/SuperTUI.dll"
ls -lh bin/Release/net8.0-windows/SuperTUI.dll
