# SuperTUI Security Model

**Version:** 1.0
**Last Updated:** 2025-10-24
**Status:** Production Ready (with Phase 1 fixes)

---

## Overview

SuperTUI implements a multi-layered security model designed to protect against common attack vectors while maintaining usability. This document describes the security architecture, features, and best practices for deploying SuperTUI securely.

**Security Philosophy:** Defense in depth with fail-safe defaults.

---

## Security Modes

SuperTUI supports three security modes, set at initialization and **immutable** thereafter:

### Strict Mode (Production Default)

**Use Case:** Production deployments, untrusted users, public systems

**Features:**
- Full path validation with canonicalization
- Symlink resolution to prevent symlink attacks
- Path traversal attack prevention (`../` blocked)
- Directory allowlisting (default deny)
- File extension allowlisting (optional)
- File size limits enforced
- UNC paths rejected
- Comprehensive audit logging

**Recommended For:**
- Production environments
- Multi-user systems
- Systems handling sensitive data
- Internet-facing applications

**Initialization:**
```csharp
SecurityManager.Instance.Initialize(SecurityMode.Strict);
```

### Permissive Mode (Trusted Environments)

**Use Case:** Enterprise environments with network shares, trusted users

**Features:**
- Same validation as Strict mode
- **UNC paths allowed** (`\\server\share`)
- Larger file size limits (100MB vs 10MB)
- All access still logged

**Recommended For:**
- Enterprise internal applications
- Environments requiring network share access
- Systems with trusted users only

**Initialization:**
```csharp
SecurityManager.Instance.Initialize(SecurityMode.Permissive);
```

### Development Mode (Testing Only)

**Use Case:** Development, debugging, testing

**⚠️ WARNING:** Never use in production!

**Features:**
- Validation **bypassed** (logs warnings only)
- All paths allowed
- Loud warnings in logs
- Useful for debugging path issues

**Recommended For:**
- Local development only
- Unit/integration testing
- Debugging security issues

**Initialization:**
```csharp
SecurityManager.Instance.Initialize(SecurityMode.Development);
```

**Log Output:**
```
WARNING: !!! DEVELOPMENT MODE ACTIVE !!! Security validation is minimal. DO NOT USE IN PRODUCTION.
WARNING: DEV MODE: Allowing file access (validation bypassed): 'C:\Windows\System32\evil.exe'
```

---

## Security Features

### 1. Path Validation

**What It Protects Against:**
- Path traversal attacks (`../../../etc/passwd`)
- Invalid path characters (`<>:|?*`)
- Null byte injection (`file.txt\0.exe`)
- Malformed paths

**How It Works:**
```csharp
public bool ValidateFileAccess(string path, bool checkWrite = false)
{
    // Step 1: Validate path format (no invalid chars, null bytes)
    if (!ValidationHelper.IsValidPath(path, allowUncPaths))
        return false;

    // Step 2: Resolve symlinks (prevents symlink attacks)
    string resolvedPath = ValidationHelper.ResolveSymlinks(path);

    // Step 3: Canonicalize path (prevents ../ bypasses)
    string fullPath = Path.GetFullPath(resolvedPath);

    // Step 4: Check allowed directories
    // Step 5: Check file extension allowlist
    // Step 6: Check file size limits
    // Step 7: Write-specific checks

    return true;
}
```

**Example Attack Prevention:**
```csharp
// Attack: Path traversal
ValidateFileAccess("/allowed/../../etc/passwd")
// Result: FALSE - canonicalizes to "/etc/passwd", not in allowed dirs

// Attack: Null byte injection
ValidateFileAccess("/allowed/file.txt\0.exe")
// Result: FALSE - null byte detected

// Attack: Symlink to restricted directory
ValidateFileAccess("/allowed/link_to_system")
// Result: FALSE - resolves to "/system", not in allowed dirs
```

### 2. Symlink Resolution

**What It Protects Against:**
- Symlink attacks (symbolic links to restricted directories)
- Junction point attacks (Windows directory junctions)

**How It Works:**
```csharp
public static string ResolveSymlinks(string path)
{
    var fileInfo = new FileInfo(path);

    // Resolve symlink (Windows 10+ / .NET 6+)
    if (fileInfo.LinkTarget != null)
    {
        return Path.GetFullPath(fileInfo.LinkTarget);
    }

    return Path.GetFullPath(path);
}
```

**Platform Support:**
- Windows 10+ with .NET 6+: Full support
- Older platforms: Graceful degradation (returns original path)

### 3. Directory Allowlisting

**What It Protects Against:**
- Unauthorized file access
- Accidental system file modification
- Data exfiltration

**Default Allowed Directories:**
- User's Documents folder (`%USERPROFILE%\Documents`)
- Application data folder (`%LOCALAPPDATA%\SuperTUI`)

**Adding Custom Directories:**
```csharp
SecurityManager.Instance.AddAllowedDirectory(@"C:\MyApp\Data");
SecurityManager.Instance.AddAllowedDirectory(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
```

**Best Practices:**
- Use specific directories, not broad paths (avoid `C:\`)
- Add only necessary directories
- Prefer user-specific locations over system-wide
- Document all allowed directories in deployment guide

### 4. File Extension Allowlisting

**What It Protects Against:**
- Execution of malicious file types
- Accidental system file modification

**Configuration:**
```json
{
  "Security.AllowedExtensions": [
    ".txt", ".md", ".log", ".json", ".xml", ".csv"
  ]
}
```

**Behavior:**
- Empty list = all extensions allowed (extension checking disabled)
- Non-empty list = only listed extensions allowed
- Case-insensitive matching

**Recommended Extensions:**
```json
{
  "Security.AllowedExtensions": [
    // Documents
    ".txt", ".md", ".pdf", ".docx", ".xlsx",
    // Data
    ".json", ".xml", ".csv", ".yaml", ".toml",
    // Logs
    ".log", ".txt"
  ]
}
```

### 5. File Size Limits

**What It Protects Against:**
- Denial of service (loading huge files)
- Memory exhaustion
- Disk space exhaustion

**Configuration:**
```json
{
  "Security.MaxFileSize": 10  // MB
}
```

**Mode-Specific Defaults:**
- Strict: 10 MB
- Permissive: 100 MB
- Development: 10 MB (logged only)

**Enforcement:**
```csharp
if (fileInfo.Length > maxFileSizeBytes)
{
    Logger.Instance.Warning("Security",
        $"SECURITY VIOLATION: File exceeds size limit\n" +
        $"  Path: '{path}'\n" +
        $"  Size: {fileInfo.Length:N0} bytes\n" +
        $"  Limit: {maxFileSizeBytes:N0} bytes");
    return false;
}
```

---

## FileExplorer Security

### File Classification

**Safe Extensions (No Warning):**
```
Documents:  .txt, .md, .pdf, .docx, .xlsx, .pptx
Images:     .jpg, .png, .gif, .svg, .bmp
Media:      .mp3, .mp4, .avi, .mov
Code:       .cs, .py, .js, .html, .css (read-only)
Archives:   .zip, .rar, .7z (view only)
```

**Dangerous Extensions (Warning + Confirmation Required):**
```
Executables: .exe, .com, .bat, .cmd, .msi, .scr
Scripts:     .ps1, .vbs, .js, .wsf, .wsh
System:      .sys, .dll, .drv
Other:       .reg, .hta, .cpl, .jar
```

**Unknown Extensions:**
- Any extension not in safe or dangerous lists
- Shows generic warning
- Requires user confirmation

### User Warnings

**Dangerous File Warning:**
```
⚠️ SECURITY WARNING ⚠️

File: installer.exe
Type: EXE (Executable/Script)
Size: 5.2 MB
Path: C:\Downloads

This file type can execute code on your computer.
Only open files from trusted sources.

Do you want to open this file?

[No]  [Yes]  ← Default: No
```

**Design Decisions:**
- **Default to No:** Prevents accidental execution
- **Clear language:** No technical jargon
- **File details:** User can verify it's the right file
- **Warning icon:** Visual security indicator

**Unknown File Warning:**
```
Unknown File Type

File: data.xyz
Extension: .xyz
Size: 1.2 KB

This file type is not recognized as safe.
Opening it may be unsafe.

Do you want to open this file?

[No]  [Yes]  ← Default: No
```

### Security Flow

```
User double-clicks file
    ↓
1. SecurityManager.ValidateFileAccess()
    ├─ Outside allowed directories? → DENY
    ├─ Invalid path? → DENY
    ├─ Symlink to restricted path? → DENY
    └─ Passes validation → Continue
    ↓
2. File Type Classification
    ├─ Safe extension (.txt, .pdf) → Open directly
    ├─ Dangerous extension (.exe, .bat) → Show warning dialog
    └─ Unknown extension (.xyz) → Show confirmation dialog
    ↓
3. User Confirmation (if needed)
    ├─ User clicks "No" → Cancel (logged)
    └─ User clicks "Yes" → Continue (logged as WARNING)
    ↓
4. Open File
    └─ Log all opens for audit
```

### Audit Logging

All file operations are logged:

**Successful Open (Safe File):**
```
INFO [FileExplorer] Successfully opened file: C:\Users\me\Documents\report.txt
```

**Dangerous File Confirmed:**
```
WARNING [FileExplorer] User confirmed opening dangerous file: C:\Downloads\installer.exe (extension: .exe)
```

**Access Denied:**
```
WARNING [FileExplorer] File access denied by security policy: C:\Windows\System32\config.sys
```

**User Cancelled:**
```
INFO [FileExplorer] User cancelled opening dangerous file: C:\Downloads\setup.exe
```

---

## Plugin Security

### Limitations

**⚠️ CRITICAL:** Plugins **cannot** be unloaded once loaded!

**Why:**
- .NET Framework doesn't support assembly unloading
- `Assembly.LoadFrom()` loads into default AppDomain
- Repeatedly loading/unloading accumulates assemblies in memory

**Implications:**
- Malicious plugin = permanent compromise until restart
- Memory leaks if plugins loaded repeatedly
- Plugins have full access to SuperTUI internals

### Security Best Practices

**DO:**
- ✅ Only load plugins from trusted sources
- ✅ Code review all plugins before deployment
- ✅ Require signed assemblies in production (`Security.RequireSignedPlugins=true`)
- ✅ Monitor plugin directory for unauthorized changes
- ✅ Log all plugin load attempts
- ✅ Use SecurityManager.ValidateFileAccess() before loading

**DON'T:**
- ❌ Load plugins from untrusted sources
- ❌ Load plugins downloaded from internet without verification
- ❌ Allow users to upload/install plugins
- ❌ Repeatedly load/unload plugins (memory leak)

### Plugin Manifest (Recommended)

```json
{
  "name": "MyPlugin",
  "version": "1.0.0",
  "author": "Your Name",
  "signature": "SHA256:abcd1234...",
  "permissions": [
    "FileAccess:Documents",
    "Network:None"
  ],
  "dependencies": [
    "SuperTUI.Core >= 1.0"
  ]
}
```

**Note:** Manifest validation is optional but recommended.

### Future Improvements

- Migrate to .NET 6+ with `AssemblyLoadContext` for unloadability
- Implement plugin permission model (file access, network, etc.)
- Add plugin sandboxing via separate process
- Add automated malware scanning
- Add plugin signing verification

**See:** [PLUGIN_GUIDE.md](PLUGIN_GUIDE.md) for development best practices.

---

## Audit Logging

### What Is Logged

**Security Events (WARNING level):**
- Path traversal attempts
- Access to files outside allowed directories
- Disallowed file extensions
- Files exceeding size limits
- Invalid path formats
- Symlink resolution failures
- User confirmations for dangerous files

**Informational Events (INFO level):**
- Successful file access
- Security mode initialization
- Allowed directory additions
- Configuration changes

**Critical Events (ERROR level):**
- Security validation exceptions
- Initialization failures
- Plugin loading failures

### Log Format

```
2025-10-24 14:32:15.123 [WARNING ] [Security] SECURITY VIOLATION: Path outside allowed directories
  Original path: 'C:\Windows\System32\config.sys'
  Normalized path: 'C:\Windows\System32\config.sys'
  Mode: Strict
  Allowed directories: C:\Users\me\Documents, C:\Users\me\AppData\Local\SuperTUI
```

### Log Locations

**File Logs:**
- Default: `%TEMP%\supertui_YYYYMMDD_HHmmss.log`
- Configurable: `ConfigurationManager.Instance.Get<string>("App.LogDirectory")`
- Rotation: 10 MB max per file, 5 files retained

**Log Levels:**
- Development: Debug and above
- Production: Info and above
- Recommended: Warning and above for security audit

### Monitoring Security Events

```powershell
# PowerShell: Find security violations
Get-Content C:\Temp\supertui_*.log | Select-String "SECURITY VIOLATION"

# PowerShell: Find dangerous file confirmations
Get-Content C:\Temp\supertui_*.log | Select-String "User confirmed opening dangerous file"

# PowerShell: Count security events
(Get-Content C:\Temp\supertui_*.log | Select-String "SECURITY").Count
```

---

## Deployment Checklist

### Pre-Deployment

- [ ] Review security mode (use Strict for production)
- [ ] Configure allowed directories (minimal necessary)
- [ ] Configure file extension allowlist (if applicable)
- [ ] Set appropriate file size limits
- [ ] Test with representative data
- [ ] Review audit logs for false positives
- [ ] Document security configuration

### Production Configuration

```csharp
// Initialization
SecurityManager.Instance.Initialize(SecurityMode.Strict);

// Add only necessary directories
SecurityManager.Instance.AddAllowedDirectory(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyApp"));
SecurityManager.Instance.AddAllowedDirectory(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyApp"));

// Configure logging
Logger.Instance.SetMinLevel(LogLevel.Warning);  // Capture security events
```

```json
// Configuration file
{
  "Security.MaxFileSize": 10,
  "Security.AllowedExtensions": [".txt", ".md", ".json", ".csv"],
  "Security.RequireSignedPlugins": true,
  "App.LogLevel": "Warning",
  "App.LogDirectory": "C:\\MyApp\\Logs"
}
```

### Post-Deployment

- [ ] Monitor audit logs for security violations
- [ ] Review user-reported access denials (false positives)
- [ ] Adjust allowed directories if needed (minimally)
- [ ] Regular security log reviews
- [ ] Keep SuperTUI updated
- [ ] Test disaster recovery (corrupted config, etc.)

---

## Threat Model

### In-Scope Threats

**Protected Against:**
- ✅ Path traversal attacks
- ✅ Symlink attacks
- ✅ Arbitrary code execution (via file opening)
- ✅ Accidental system file modification
- ✅ Social engineering (dangerous file warnings)
- ✅ Configuration tampering (immutable security mode)
- ✅ Unauthorized file access

### Out-of-Scope Threats

**NOT Protected Against:**
- ❌ Malicious plugins (user must trust plugins)
- ❌ Memory corruption exploits
- ❌ Privilege escalation (OS-level)
- ❌ Network-based attacks
- ❌ Physical access attacks
- ❌ Supply chain attacks

### Assumptions

- Operating system is not compromised
- User has appropriate OS-level permissions
- Filesystem permissions are correctly configured
- Plugins are from trusted sources
- Audit logs are monitored regularly

---

## Reporting Security Issues

**DO NOT** create public GitHub issues for security vulnerabilities.

**Instead:**
1. Email security concerns to: [your-security-email]
2. Include: Description, steps to reproduce, impact assessment
3. Allow 90 days for fix before public disclosure
4. We will acknowledge within 48 hours

**Responsible Disclosure Appreciated!**

---

## Compliance

### Relevant Standards

- **OWASP Top 10:** Path Traversal (A01), Broken Access Control (A01)
- **CWE-22:** Path Traversal
- **CWE-59:** Improper Link Resolution ('Link Following')
- **CWE-73:** External Control of File Name or Path

### Audit Recommendations

- Regular penetration testing
- Code review of custom plugins
- Security log analysis (monthly minimum)
- User training on file security warnings
- Incident response plan for security violations

---

## Version History

**v1.0 (2025-10-24)** - Initial security model
- SecurityMode enum (Strict/Permissive/Development)
- Path validation with symlink resolution
- FileExplorer dangerous file warnings
- Comprehensive audit logging
- Plugin security documentation

---

## References

- [REMEDIATION_PLAN.md](REMEDIATION_PLAN.md) - Security fix implementation
- [CRITICAL_ANALYSIS_REPORT.md](CRITICAL_ANALYSIS_REPORT.md) - Security audit
- [PLUGIN_GUIDE.md](PLUGIN_GUIDE.md) - Plugin development security
- [OWASP Path Traversal](https://owasp.org/www-community/attacks/Path_Traversal)
- [CWE-22: Path Traversal](https://cwe.mitre.org/data/definitions/22.html)

---

**Document Owner:** SuperTUI Development Team
**Review Cycle:** Quarterly or after security updates
**Last Review:** 2025-10-24
