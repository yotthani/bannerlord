# Coding Standards & Architectural Patterns

## Our Development Style Guide
*Extracted from HeirOfNumenor and related projects*

---

## 1. Project Structure

### Feature-Based Organization
```
ModName/
├── Features/
│   ├── FeatureName/
│   │   ├── FeatureNameBehavior.cs      # CampaignBehavior or MissionBehavior
│   │   ├── FeatureNamePatches.cs       # Harmony patches (if needed)
│   │   ├── FeatureNameVM.cs            # ViewModel
│   │   ├── FeatureNameManager.cs       # Business logic / state
│   │   └── Data/                       # Feature-specific data classes
│   └── AnotherFeature/
├── Common/                             # Shared utilities (or use BCL)
├── ModuleData/                         # XML data files
├── Settings.cs                         # MCM settings (single file)
├── SubModule.cs                        # Entry point
└── ModName.csproj
```

### GUI Separation
```
ModNameGUI/                             # Separate mod for assets
├── GUI/
│   ├── Prefabs/
│   ├── Brushes/
│   └── SpriteParts/
├── Assets/GauntletUI/                  # Compiled .tpac files
└── SubModule.xml
```

---

## 2. Naming Conventions

### Files
```
PascalCase.cs                           # All C# files
feature_data.xml                        # XML data files
ui_feature_name/                        # Sprite folders (snake_case)
```

### Classes
```csharp
// Behaviors
public class FeatureNameCampaignBehavior : CampaignBehaviorBase { }
public class FeatureNameMissionBehavior : MissionBehavior { }

// ViewModels
public class FeatureNameVM : ViewModel { }
public class FeatureItemVM : ViewModel { }      // List item VMs

// Patches
public static class FeatureNamePatches { }      // Static class, grouped patches

// Managers
public static class FeatureNameManager { }      // Static for global state
public class FeatureNameController { }          // Instance for scoped logic
```

### Members
```csharp
// Private fields: _camelCase with underscore prefix
private string _selectedItem;
private readonly Action _closeAction;

// Properties: PascalCase
public string SelectedItem { get; set; }

// Constants: PascalCase (not SCREAMING_CASE)
private const float RotationDuration = 0.4f;
private const int MaxEquippedRings = 1;

// Methods: PascalCase, verb-first
public void UpdateForFormation(Formation formation) { }
private bool IsMainHeroSpawn(AgentBuildData data) { }
```

---

## 3. ViewModel Patterns

### Standard Structure
```csharp
public class FeatureVM : ViewModel
{
    // Dependencies (readonly)
    private readonly Action _closeAction;
    private readonly SomeService _service;
    
    // Backing fields grouped by UI section
    // -- Header --
    private string _title;
    private string _subtitle;
    
    // -- Content --
    private MBBindingList<ItemVM> _items;
    private ItemVM _selectedItem;
    private bool _hasSelection;
    
    // -- Actions --
    private bool _canExecuteAction;
    
    // Constructor
    public FeatureVM(Action closeAction)
    {
        _closeAction = closeAction;
        _items = new MBBindingList<ItemVM>();
        Initialize();
    }
    
    // Properties with OnPropertyChanged
    [DataSourceProperty]
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChangedWithValue(value, nameof(Title));
            }
        }
    }
    
    // Shorthand for simple cases
    [DataSourceProperty]
    public bool HasSelection { get => _hasSelection; set { _hasSelection = value; OnPropertyChanged(); } }
    
    // Execute methods for buttons (named ExecuteXxx)
    public void ExecuteClose() => _closeAction?.Invoke();
    public void ExecuteSelectItem(ItemVM item) { /* ... */ }
    
    // Refresh methods
    public void RefreshData() { /* ... */ }
    private void RefreshActionStates() { /* ... */ }
}
```

### List Item VMs
```csharp
public class ItemVM : ViewModel
{
    private readonly Action<ItemVM> _onSelect;
    
    [DataSourceProperty] public string Name { get; }
    [DataSourceProperty] public string Description { get; }
    [DataSourceProperty] public bool IsSelected { get => _isSelected; set { /* ... */ } }
    
    public ItemVM(SomeData data, Action<ItemVM> onSelect)
    {
        _onSelect = onSelect;
        Name = data.Name;
        // ...
    }
    
    public void ExecuteSelect() => _onSelect?.Invoke(this);
}
```

---

## 4. Harmony Patch Patterns

### Standard Patch Class
```csharp
[HarmonyPatch]
public static class FeaturePatches
{
    private static readonly string FEATURE_NAME = "FeatureName";  // For logging
    
    [HarmonyPatch(typeof(TargetClass), "TargetMethod")]
    [HarmonyPrefix]
    public static bool TargetMethod_Prefix(/* original params */, ref ReturnType __result)
    {
        try
        {
            // Check if feature is enabled
            if (!(Settings.Instance?.EnableFeature ?? false))
                return true;  // Run original
            
            // Do prefix logic
            // ...
            
            // return false = skip original
            // return true = run original
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Prefix failed: {ex.Message}");
            return true;  // On error, run original
        }
    }
    
    [HarmonyPatch(typeof(TargetClass), "TargetMethod")]
    [HarmonyPostfix]
    public static void TargetMethod_Postfix(/* original params */, ReturnType __result)
    {
        try
        {
            if (!(Settings.Instance?.EnableFeature ?? false))
                return;
            
            // Modify result or do post-processing
        }
        catch { }  // Silent fail for postfix
    }
    
    private static void LogError(string msg) =>
        InformationManager.DisplayMessage(new InformationMessage($"[{FEATURE_NAME}] {msg}", Colors.Red));
}
```

### Patch Safety Rules
1. **Always wrap in try-catch** - never crash the game
2. **Check settings first** - allow disabling via MCM
3. **Return true on error** - let original run if we fail
4. **Use postfix when possible** - less invasive than prefix

---

## 5. MCM Settings Patterns

### Single Settings File
```csharp
public class Settings : AttributeGlobalSettings<Settings>
{
    public override string Id => "ModName";
    public override string DisplayName => "Mod Display Name";
    public override string FolderName => "ModName";
    public override string FormatType => "json2";
    
    // Group order determines UI order
    [SettingPropertyGroup("Feature One", GroupOrder = 0)]
    [SettingPropertyBool("Enable Feature", Order = 0, RequireRestart = false,
        HintText = "Description of what this does.")]
    public bool EnableFeatureOne { get; set; } = true;
    
    [SettingPropertyGroup("Feature One", GroupOrder = 0)]
    [SettingPropertyInteger("Some Value", 1, 100, Order = 1, RequireRestart = false,
        HintText = "Description.")]
    public int SomeValue { get; set; } = 50;
    
    // Dropdowns with enum backing
    [SettingPropertyGroup("Feature Two", GroupOrder = 1)]
    [SettingPropertyDropdown("Mode Selection", Order = 0, RequireRestart = false,
        HintText = "Select the mode.")]
    public Dropdown<string> ModeDropdown { get; set; } = new(
        new[] { "Option A", "Option B", "Option C" }, 0);
    
    // Helper to convert dropdown to enum
    public FeatureMode GetFeatureMode() => (FeatureMode)ModeDropdown.SelectedIndex;
}

public enum FeatureMode { OptionA, OptionB, OptionC }
```

---

## 6. Error Handling Philosophy

### Fail Gracefully
```csharp
// BAD - crashes game
public void DoSomething()
{
    var item = GetItem();
    var name = item.Name;  // NullRef if item is null!
}

// GOOD - defensive
public void DoSomething()
{
    var item = GetItem();
    if (item == null) return;  // or log and return
    
    var name = item.Name;
}

// GOOD - null conditional
var name = GetItem()?.Name ?? "Unknown";
```

### Try-Catch Boundaries
```csharp
// Wrap at entry points (patches, event handlers, ticks)
public override void OnMissionTick(float dt)
{
    try
    {
        DoComplexLogic();
    }
    catch (Exception ex)
    {
        // Log but don't crash
        Log.Error("Feature", $"Tick failed: {ex.Message}");
    }
}

// Inner methods can throw - boundary catches
private void DoComplexLogic()
{
    // No try-catch needed here
    var x = CalculateThing();
    ApplyThing(x);
}
```

---

## 7. Localization Pattern

### C# String Definitions
```csharp
// Use TextObject with IDs
[DataSourceProperty]
public string Title => new TextObject("{=feature_title}Feature Title").ToString();

// With variables
public string GetCountText(int count)
{
    var text = new TextObject("{=item_count}{COUNT} items found");
    text.SetTextVariable("COUNT", count);
    return text.ToString();
}
```

### XML String File
```xml
<!-- ModuleData/Languages/std_module_strings_xml.xml -->
<strings>
    <string id="feature_title" text="Feature Title" />
    <string id="item_count" text="{COUNT} items found" />
</strings>
```

---

## 8. Common Utility Patterns

### Logging (use BCL)
```csharp
// Simple prefixed logging
Log.Info("FeatureName", "Something happened");
Log.Error("FeatureName", "Something failed");
Log.Warning("FeatureName", "Suspicious state");
```

### Null-Safe Access
```csharp
// Chain with null conditional
var skill = agent?.Character?.GetSkillValue(DefaultSkills.OneHanded) ?? 0;

// Early return pattern
if (Mission.Current?.PlayerTeam == null) return;
var team = Mission.Current.PlayerTeam;
```

### LINQ Usage
```csharp
// Prefer method syntax
var mounted = agents.Where(a => a.HasMount).ToList();
var avgDamage = hits.Average(h => h.Damage);

// Use Any() not Count() > 0
if (items.Any()) { }  // GOOD
if (items.Count() > 0) { }  // BAD - iterates whole collection
```

---

## 9. Architectural Principles

### Single Responsibility
- One behavior per feature
- One VM per screen/panel
- One patch class per feature

### Dependency Direction
```
Settings → Features → Common/BCL
              ↓
           SubModule (registration only)
```

### State Management
- **Global state**: Static manager classes
- **Per-save state**: CampaignBehavior with SaveableTypeDefiner
- **Per-mission state**: MissionBehavior (clears on mission end)
- **UI state**: ViewModel (clears on screen close)

### Don't Repeat Yourself (DRY)
- Shared code → BCL
- Similar VMs → Extract base class
- Similar patches → Helper methods

---

## 10. Performance Considerations

### OnTick Methods
```csharp
// BAD - allocates every frame
public override void OnMissionTick(float dt)
{
    var agents = Mission.Current.Agents.Where(a => a.IsActive).ToList();
}

// GOOD - reuse collections, throttle
private List<Agent> _activeAgents = new();
private float _updateTimer = 0f;

public override void OnMissionTick(float dt)
{
    _updateTimer += dt;
    if (_updateTimer < 0.5f) return;  // Every 500ms
    _updateTimer = 0f;
    
    _activeAgents.Clear();
    foreach (var agent in Mission.Current.Agents)
    {
        if (agent.IsActive) _activeAgents.Add(agent);
    }
}
```

### String Operations
```csharp
// BAD - concatenation in loops
string result = "";
foreach (var item in items)
    result += item.Name + ", ";

// GOOD - StringBuilder
var sb = new StringBuilder();
foreach (var item in items)
    sb.Append(item.Name).Append(", ");
var result = sb.ToString();

// GOOD - string.Join
var result = string.Join(", ", items.Select(i => i.Name));
```

---

## 11. XML/Gauntlet Patterns

### Widget Binding
```xml
<!-- Property binding -->
<TextWidget Text="@PropertyName" />

<!-- List binding -->
<ListPanel DataSource="{Items}">
    <ItemTemplate>
        <Widget DataSource="{..}">
            <TextWidget Text="@ItemName" />
        </Widget>
    </ItemTemplate>
</ListPanel>

<!-- Command binding -->
<ButtonWidget Command.Click="ExecuteMethodName" />
```

### Layout Principles
- Use `HorizontalAlignment` / `VerticalAlignment` for positioning
- Use `MarginLeft/Right/Top/Bottom` for offsets from alignment
- Use `SuggestedWidth/Height` with `SizePolicy="Fixed"` for exact sizes
- Use `SizePolicy="StretchToParent"` to fill container

---

*Last updated: December 2024*
*Derived from HeirOfNumenor, BCL, BMT projects*

---

## 12. External Mod Integration

### Pattern Adoption Hierarchy

When integrating external mods (e.g., Maid Mod), follow this priority:

```
1. External Mod Patterns (highest priority when integrating)
   └── Preserve their conventions in their codebase
   
2. Our Patterns (for new code / our features)
   └── Apply our standards to new features
   
3. Hybrid (when extending external mod)
   └── Match their style in their files, ours in new files
```

### Integration Principles

**DO:**
- Study their patterns before modifying
- Document deviations from our standards
- Create adapter layers rather than rewriting
- Preserve their naming in their namespaces
- Add our features as extensions, not replacements

**DON'T:**
- Force our patterns onto their codebase
- Rename their classes/methods
- Refactor working code just for style
- Break their existing functionality

### Pattern Mapping Template

When integrating a new mod, create a mapping document:

```markdown
# [ModName] Integration Patterns

## Their Conventions
- Naming: [describe]
- File structure: [describe]
- ViewModel pattern: [describe]
- Patch style: [describe]

## Differences from Our Standards
| Aspect | Theirs | Ours | Decision |
|--------|--------|------|----------|
| Field naming | camelCase | _camelCase | Use theirs in their files |
| Settings | [their approach] | MCM single file | Adapter pattern |
| ... | ... | ... | ... |

## Integration Approach
- [ ] Keep their core untouched
- [ ] Add extension points via Harmony
- [ ] Create adapter classes in our namespace
- [ ] Bridge their events to our systems
```

### Namespace Strategy

```csharp
// Their code - preserve namespace
namespace MaidMod.Features.SomeFeature
{
    // Don't modify unless necessary
}

// Our extensions - separate namespace
namespace HeirOfNumenor.Integration.MaidMod
{
    // Our adapters, extensions, bridges
    public class MaidModBridge { }
}

// Shared interfaces - in BCL if truly shared
namespace BannerlordCommonLib.Integration
{
    public interface IModBridge { }
}
```

### When to Adopt Their Patterns

**Adopt theirs when:**
- Working in their files/namespaces
- Extending their classes
- Their pattern is objectively better
- Consistency within their module matters more

**Keep ours when:**
- Writing new features
- Creating integration adapters
- Their pattern has clear issues
- It's purely our code

### Integration Checklist

Before integrating external mod:
- [ ] Read their code, understand patterns
- [ ] Document their conventions
- [ ] Identify integration points
- [ ] Plan adapter architecture
- [ ] Decide pattern adoption per-area
- [ ] Create mapping document
- [ ] Get their code working first, style later

### Learning from External Mods

When their patterns are better than ours:

```markdown
## Pattern Upgrade Proposal

**Source:** [ModName]
**Pattern:** [Description]
**Their approach:** [Code example]
**Our current:** [Code example]
**Recommendation:** Adopt / Adapt / Ignore
**Migration effort:** Low / Medium / High
```

If adopted, update this standards doc and gradually migrate.

---

## 13. Documentation Requirements

### Per-Feature Documentation
Each feature should have a brief header comment:

```csharp
/// <summary>
/// Brief description of what this feature does.
/// </summary>
/// <remarks>
/// Integration notes:
/// - Dependencies: [list any]
/// - Settings: [MCM group name]
/// - Patches: [what we patch]
/// - External mod compat: [any notes]
/// </remarks>
```

### Integration Documentation
For external mod integrations, maintain in `/Docs/Integration/`:

```
Docs/
└── Integration/
    ├── MaidMod_Patterns.md       # Their patterns
    ├── MaidMod_Integration.md    # How we integrate
    └── MaidMod_API.md            # Their public API we use
```

---

*Standards are guidelines, not laws. Use judgment.*
*When in doubt, prioritize: Working > Readable > Consistent > Pretty*

---

## 14. Pattern Context Hierarchy

### Three-Tier Pattern System

```
┌─────────────────────────────────────────┐
│  OURS (HeirOfNumenor)                   │  ← New features, our style
│  Apply our full standards               │
├─────────────────────────────────────────┤
│  BASE MOD (Maid Mod / etc)              │  ← Preserve their patterns
│  Match their conventions                │
├─────────────────────────────────────────┤
│  BANNERLORD ORIGINAL (TaleWorlds)       │  ← Reference only
│  Don't modify, learn from               │
└─────────────────────────────────────────┘
```

### When Sharing Code - Always Specify Context

**Claude will ask:** "Which pattern context is this?"

Specify one of:
- `[BL]` - Bannerlord original (TaleWorlds code)
- `[BASE]` - Base mod we're integrating (Maid Mod, etc.)
- `[OURS]` - Our code (HeirOfNumenor, BCL, BMT)
- `[NEW]` - New code to write (use our patterns)

### Context Determines Response

| Context | Claude's Approach |
|---------|-------------------|
| `[BL]` | Analyze, don't suggest changes to their code |
| `[BASE]` | Preserve their patterns, minimal changes |
| `[OURS]` | Apply our standards, can refactor |
| `[NEW]` | Full our standards, clean implementation |

### Example Usage

```
User: Here's some code [BASE]
```csharp
// Maid Mod's approach
public class someFeature {
    string myField;  // their naming
}
```

Claude: I see this is BASE mod context. I'll preserve their naming 
convention (no underscore prefix) when working in their files.
```

---

*REMINDER: Always specify pattern context when sharing code!*
