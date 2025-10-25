# Cyberpunk Themes - Quick Start Guide

## TL;DR

You now have **4 neon cyberpunk themes** with glow effects, CRT scanlines, and bloom. Default theme is **Cyberpunk** (cyan/magenta neon).

**To test:**
```powershell
cd /home/teej/supertui/WPF
./SuperTUI.ps1
```

**What you'll see:**
- ✨ Neon glows around widgets (cyan normally, magenta when focused)
- 📺 CRT scanlines across the screen
- 🌟 Bloom effect on bright elements
- 🪟 Slightly transparent window (96% opacity)

---

## Available Themes

### 1. Cyberpunk (Default) 🔷
**Colors:** Cyan + Magenta + Purple
**Vibe:** Blade Runner, high-tech noir
**Glow:** Cyan (always) → Magenta (focus) → Purple (hover)
**Effects:** Strong scanlines (12% opacity), heavy bloom (70%)

**Best for:** Showcasing all effects, maximum cyberpunk aesthetic

---

### 2. Synthwave 💗
**Colors:** Hot Pink + Neon Cyan + Purple
**Vibe:** Retro 80s, outrun, vaporwave
**Glow:** MAXIMUM! Pink (always, 20px radius, 90% opacity) → Cyan (hover)
**Effects:** Subtle scanlines (5%), INTENSE bloom (80%)

**Best for:** Maximum color and brightness, "over the top" aesthetic
**Warning:** Very bright! May be overwhelming for long sessions

---

### 3. Matrix 🟢
**Colors:** Bright Green (#00FF41)
**Vibe:** Classic "Matrix" falling code aesthetic
**Glow:** Green (always, 15px radius) → Bright green (focus)
**Effects:** Strong scanlines (15% opacity), moderate bloom (50%)

**Best for:** Classic hacker/terminal aesthetic, green phosphor CRT

---

### 4. Amber Terminal 🟠
**Colors:** Amber (#FFB000) + Orange
**Vibe:** Retro 70s/80s terminal, warm nostalgia
**Glow:** Orange (always, 12px radius) → Amber (focus)
**Effects:** Subtle scanlines (8%), moderate bloom (40%)

**Best for:** Warm retro aesthetic, easy on eyes for long sessions

---

### 5. Dark (Classic) 🌑
**Colors:** Teal accent on dark gray
**Vibe:** Modern VS Code / minimal
**Glow:** Focus only (no always-on glow)
**Effects:** None (no scanlines, no bloom)

**Best for:** Serious work, minimal distractions, classic look

---

### 6. Light (Classic) ☀️
**Colors:** Blue accent on white
**Vibe:** Clean, professional
**Glow:** Focus only
**Effects:** None

**Best for:** Daytime use, well-lit environments

---

## How to Switch Themes

### Method 1: Edit Config (Persistent)
```powershell
# Edit SuperTUI.ps1 line 226
$savedThemeName = $configManager.Get("UI.Theme", "Synthwave")  # Change here
```

### Method 2: PowerShell at Runtime (Temporary)
```powershell
# After SuperTUI launches, in PowerShell:
$themeManager.ApplyTheme("Synthwave")
$themeManager.ApplyTheme("Matrix")
$themeManager.ApplyTheme("Amber Terminal")
$themeManager.ApplyTheme("Cyberpunk")
```

### Method 3: Use ThemeEditorWidget (Future)
- Add `ThemeEditorWidget` to workspace
- Live theme switching + customization UI
- Save custom themes

---

## Effect Descriptions

### Glow (Neon Effect)
**What it is:** Soft colored glow around widget borders
**How it works:** WPF `DropShadowEffect` with `ShadowDepth=0` (true glow, not shadow)
**Modes:**
- **Always:** Widgets glow constantly (Cyberpunk, Synthwave, Matrix, Amber)
- **OnFocus:** Widgets only glow when focused (Dark, Light themes)

**Colors:**
- **Normal glow:** When widget is not focused (e.g., cyan in Cyberpunk)
- **Focus glow:** When widget has focus (e.g., magenta in Cyberpunk)
- **Hover glow:** When mouse hovers over widget (not implemented yet)

**Performance:** Minimal impact on modern GPUs

---

### Scanlines
**What it is:** Horizontal dark lines across the entire window
**How it works:** Canvas overlay with thin Rectangle elements
**Purpose:** Simulates CRT monitor phosphor gaps
**Spacing:** 2-3 pixels typical
**Opacity:** 5-15% typical (subtle darkening effect)

**Visibility:**
- Matrix: Very visible (15% opacity, black lines)
- Cyberpunk: Visible (12% opacity)
- Amber: Subtle (8% opacity)
- Synthwave: Very subtle (5% opacity)

**Performance:** Excellent (static rectangles, no animation)

---

### Bloom
**What it is:** Gaussian blur applied to entire overlay
**How it works:** WPF `BlurEffect` on CRT overlay canvas
**Purpose:** Creates soft glow on bright UI elements
**Intensity:** 0.0-1.0 (typical 0.3-0.8)

**Intensity by theme:**
- Synthwave: 80% (MAXIMUM bloom)
- Cyberpunk: 70% (heavy bloom)
- Matrix: 50% (moderate bloom)
- Amber: 40% (subtle bloom)

**Performance:** Moderate (GPU accelerated, single effect on overlay)

---

### Opacity
**What it is:** Window transparency
**How it works:** Native WPF `Window.Opacity` property
**Typical values:** 0.95-0.98 (2-5% transparency)

**Why transparent?**
- Cyberpunk aesthetic (see desktop wallpaper through terminal)
- Layered effect (terminal feels "holographic")
- Not too transparent (still readable)

**Performance:** Excellent (native Windows compositor)

---

## Testing Checklist

### Visual Verification
1. **Launch SuperTUI** - Should see cyan/magenta Cyberpunk theme
2. **Look for glow** - Widget borders should have cyan glow
3. **Press Tab** - Focus should move, glow should turn magenta
4. **Look for scanlines** - Horizontal lines across window (may be subtle)
5. **Check opacity** - Window should be slightly transparent (look for desktop behind it)
6. **Look for bloom** - Bright text should have soft glow around it

### Theme Testing
```powershell
# Try each theme in PowerShell:
$themeManager.ApplyTheme("Synthwave")   # MAXIMUM NEON!
$themeManager.ApplyTheme("Matrix")      # Green phosphor
$themeManager.ApplyTheme("Amber Terminal")  # Warm amber
$themeManager.ApplyTheme("Dark")        # Minimal (no effects)
```

### Expected Results
- **Glow color changes** with theme (pink in Synthwave, green in Matrix)
- **Scanlines rebuild** when theme switches
- **Window opacity changes** with theme
- **All widgets update** immediately (no restart needed)

---

## Customization

### Adjust Glow Intensity
```csharp
// In ThemeManager.cs, edit theme definition:
Glow = new GlowSettings
{
    Mode = GlowMode.Always,
    GlowRadius = 20.0,      // Change: 0-30 (higher = bigger glow)
    GlowOpacity = 0.9,      // Change: 0.0-1.0 (higher = brighter)
    GlowColor = neonPink,
    FocusGlowColor = neonCyan
}
```

### Adjust Scanlines
```csharp
CRTEffects = new CRTEffectSettings
{
    EnableScanlines = true,
    ScanlineOpacity = 0.15,  // Change: 0.0-0.3 (higher = darker lines)
    ScanlineSpacing = 2,     // Change: 1-6 (higher = more spaced)
    ScanlineColor = Colors.Black
}
```

### Adjust Bloom
```csharp
CRTEffects = new CRTEffectSettings
{
    EnableBloom = true,
    BloomIntensity = 0.8  // Change: 0.0-1.0 (higher = more blur)
}
```

### Adjust Window Opacity
```csharp
Opacity = new OpacitySettings
{
    WindowOpacity = 0.96  // Change: 0.5-1.0 (lower = more transparent)
}
```

---

## Troubleshooting

### "I don't see any glow!"
**Check:**
1. Theme has `GlowMode.Always` or widget is focused
2. Window is running on Windows (WPF only)
3. Graphics drivers are up to date

**Test:**
```powershell
$themeManager.ApplyTheme("Synthwave")  # This has MAXIMUM glow
```

### "I don't see scanlines!"
**Check:**
1. Scanlines may be very subtle (5-8% opacity)
2. Try Matrix theme (15% opacity, very visible)
3. Look closely at solid color areas (easier to see)

**Test:**
```powershell
$themeManager.ApplyTheme("Matrix")  # Strong scanlines
```

### "Effects are too intense!"
**Switch to a subtler theme:**
```powershell
$themeManager.ApplyTheme("Amber Terminal")  # Subtle effects
$themeManager.ApplyTheme("Dark")  # No effects
```

### "Performance is slow!"
**Reduce effects:**
1. Switch to Dark theme (no effects)
2. Disable bloom: Set `EnableBloom = false` in theme
3. Reduce glow radius: Set `GlowRadius = 5.0` (from 20.0)

---

## Theme Comparison Table

| Theme | Glow Mode | Glow Radius | Scanlines | Bloom | Opacity | Best For |
|-------|-----------|-------------|-----------|-------|---------|----------|
| **Cyberpunk** | Always | 18px (85%) | 12% opacity | 70% | 96% | Showcasing all effects |
| **Synthwave** | Always | 20px (90%) | 5% opacity | 80% | 98% | Maximum neon aesthetic |
| **Matrix** | Always | 15px (70%) | 15% opacity | 50% | 95% | Classic hacker aesthetic |
| **Amber Terminal** | Always | 12px (60%) | 8% opacity | 40% | 98% | Retro warm aesthetic |
| **Dark** | Focus only | 10px (60%) | None | None | 100% | Minimal, serious work |
| **Light** | Focus only | 10px (60%) | None | None | 100% | Daytime use |

---

## Next Steps

1. **Test on Windows** - Run `.\SuperTUI.ps1` and verify effects
2. **Try all themes** - See which aesthetic you prefer
3. **Customize** - Adjust glow/scanlines/bloom to taste
4. **Integrate typography** - Update widgets to use theme fonts (optional)
5. **Create custom theme** - Copy existing theme, modify colors/effects
6. **Use ThemeEditorWidget** - Live theme editing (if added to workspace)

---

## Files to Review

- **THEME_SYSTEM_INTEGRATION_COMPLETE.md** - Full technical documentation
- **CYBERPUNK_THEME_SYSTEM_COMPLETE.md** - Original implementation summary
- **Core/Infrastructure/ThemeManager.cs:249-565** - Theme definitions
- **Core/Effects/GlowEffectHelper.cs** - Glow implementation
- **Core/Components/CRTEffectsOverlay.cs** - Scanline/bloom implementation
- **SuperTUI.ps1:267-316** - Integration code

---

**Enjoy your neon cyberpunk terminal!** ✨🔷💗🟢
