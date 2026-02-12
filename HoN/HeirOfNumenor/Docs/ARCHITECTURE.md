# Bannerlord Modding Tools - Architecture

## Mod Structure

```
Recommended Dependency Chain:
┌─────────────────────────────────────────┐
│  BannerlordCommonLib (BCL)              │  ← Shared utilities
│  - Data sharing                         │
│  - Exception capture                    │
│  - Logging                              │
│  - Common helpers                       │
└─────────────────────────────────────────┘
        ▲               ▲               ▲
        │               │               │
┌───────┴───────┐ ┌─────┴─────┐ ┌───────┴───────┐
│ BMT Capture   │ │ UI        │ │ HeirOfNumenor │
│ (Balance/Err) │ │ Designer  │ │ (Your mod)    │
└───────────────┘ └───────────┘ └───────────────┘
     For all         For            Any mod
     players        modders         needing BCL
```

## Golden Rule: No Code Duplication

When writing code, ask:
1. Is this feature-specific? → Keep in feature mod
2. Is this reusable utility? → Move to BCL
3. Does another mod need this? → Definitely BCL

## BCL Contents

| Category | Classes | Used By |
|----------|---------|---------|
| Sharing | DataSharing | BMT, any mod wanting telemetry |
| Diagnostics | ExceptionCapture | BMT, debugging |
| Utilities | Log, FileIO | All mods |
| UI | CommonBrushes | UI-heavy mods |

## Mod Separation

### BMT Capture (BannerlordModToolkit)
**Audience:** All players (opt-in data collection)
**Size:** Small
**Purpose:** Combat stats, errors, performance

### UI Designer
**Audience:** Modders only
**Size:** Medium (includes desktop app)
**Purpose:** XML editing, hot-reload preview

### HeirOfNumenor
**Audience:** LotR mod users
**Purpose:** Game features

## Adding to BCL

Before adding to BCL, code must:
- [ ] Be used by 2+ mods (or clearly will be)
- [ ] Have no feature-specific logic
- [ ] Be well-tested
- [ ] Have minimal dependencies

## Version Compatibility

BCL uses semantic versioning:
- Major: Breaking changes
- Minor: New features (backward compatible)
- Patch: Bug fixes

Mods specify minimum version:
```xml
<DependedModule Id="BannerlordCommonLib" DependentVersion="v1.0.0"/>
```
