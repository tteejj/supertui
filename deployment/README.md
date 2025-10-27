# SuperTUI Deployment Guide

This directory contains tools for deploying SuperTUI to machines with limited internet access (email-only).

## Prerequisites

### Development Machine (Linux)
- .NET 8 SDK
- PowerShell 7+
- tar

### Target Machine (Windows)
- .NET 8 Desktop Runtime (NOT SDK)
- PowerShell 7+
- tar (usually included with Windows 10+)

## Deployment Process

### Step 1: Build SuperTUI (on Linux)

```bash
cd ../WPF
dotnet publish SuperTUI.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=false
```

**Note:** Using `--self-contained false` creates a much smaller deployment (~2-5 MB) but requires .NET 8 runtime on target.

### Step 2: Encode for Transfer (on Linux)

```bash
cd ../deployment
pwsh email-packaging-tool.ps1
```

This will:
1. Create a tar.gz archive of the published files
2. Encode it to base64
3. Split into 20MB chunks (email-friendly)
4. Save as `.txt` files in `./encoded/`

### Step 3: Email the Files

Email all `supertui_part_*.txt` files as attachments to the target machine.

Also send `email-unpacking-tool.ps1` (small script, can be inline or attachment).

### Step 4: Decode on Target (on Windows)

```powershell
# Place all .txt files in a directory
# Place email-unpacking-tool.ps1 in the same directory
pwsh email-unpacking-tool.ps1 -InputDir . -OutputDir C:\SuperTUI
```

This will:
1. Reassemble the base64 chunks
2. Decode to tar.gz
3. Extract to `C:\SuperTUI`

### Step 5: Run SuperTUI (on Windows)

```powershell
cd C:\SuperTUI
pwsh SuperTUI.ps1
```

## Options

### Self-Contained Build (Larger but No Runtime Needed)

If the target machine does NOT have .NET 8 runtime:

```bash
dotnet publish SuperTUI.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

This creates a single ~100MB executable. Encode and transfer the same way.

### Custom Chunk Size

For email providers with different attachment limits:

```bash
pwsh email-packaging-tool.ps1 -ChunkSizeMB 10  # 10MB chunks
pwsh email-packaging-tool.ps1 -ChunkSizeMB 50  # 50MB chunks
```

## Troubleshooting

### "tar: command not found" on Windows

Install tar (usually included with Windows 10+):
```powershell
winget install GnuWin32.tar
```

Or use 7-Zip as alternative:
```powershell
7z x supertui.tar.gz
7z x supertui.tar
```

### ".NET runtime not found"

Install .NET 8 Desktop Runtime on target:
```powershell
# Check current runtimes
dotnet --list-runtimes

# Download installer (if internet available briefly)
# https://dotnet.microsoft.com/download/dotnet/8.0
# Get "Desktop Runtime" (NOT SDK)
```

### Email Blocks .txt Attachments

Rename files to `.pdf` or `.docx`:
```bash
for f in supertui_part_*.txt; do mv "$f" "${f%.txt}.pdf"; done
```

On target, rename back before decoding:
```powershell
Get-ChildItem *.pdf | Rename-Item -NewName { $_.Name -replace '\.pdf$', '.txt' }
```

### Base64 Decode Fails

Ensure all part files were received and are in correct order:
```powershell
Get-ChildItem supertui_part_*.txt | Sort-Object Name | Select-Object Name, Length
```

## File Sizes (Approximate)

| Build Type | Archive Size | Base64 Size | # of 20MB Chunks |
|------------|--------------|-------------|------------------|
| Framework-Dependent | 2-5 MB | 3-7 MB | 1 chunk |
| Self-Contained Single File | 70-100 MB | 95-135 MB | 5-7 chunks |
| Self-Contained Extracted | 60-80 MB | 80-110 MB | 4-6 chunks |

## Security Notes

- Base64 encoding is NOT encryption (just binary-to-text conversion)
- Files are transferred as plain text
- If security is required, encrypt the archive before encoding
- Email scanners may still inspect content

## Alternative: PowerShell Script Embedding

For very small deployments, you can embed the base64 directly in a PowerShell script:

```powershell
# See email-packaging-tool.ps1 for example
# Creates a single .ps1 file that extracts itself
```

This works well for framework-dependent builds (<10 MB).
