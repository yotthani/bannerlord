# Bannerlord Modding Knowledge Base

## Compiled from GUI files and Assembly analysis - December 2025

---

## 1. NAMESPACE REFERENCE

### Core Namespaces
```csharp
// Campaign System
using TaleWorlds.CampaignSystem;                           // Core campaign classes
using TaleWorlds.CampaignSystem.Actions;                   // Campaign actions (kill, join, etc.)
using TaleWorlds.CampaignSystem.CampaignBehaviors;         // Behavior base classes
using TaleWorlds.CampaignSystem.CharacterDevelopment;      // Skills, perks, traits
using TaleWorlds.CampaignSystem.ComponentInterfaces;       // Model interfaces
using TaleWorlds.CampaignSystem.Conversation;              // Dialogue system
using TaleWorlds.CampaignSystem.CraftingSystem;            // Smithing
using TaleWorlds.CampaignSystem.Election;                  // Kingdom decisions
using TaleWorlds.CampaignSystem.Encounters;                // Map encounters
using TaleWorlds.CampaignSystem.Encyclopedia;              // Encyclopedia pages
using TaleWorlds.CampaignSystem.Extensions;                // Extension methods
using TaleWorlds.CampaignSystem.GameComponents;            // Game models
using TaleWorlds.CampaignSystem.GameMenus;                 // Game menus
using TaleWorlds.CampaignSystem.GameState;                 // Game states
using TaleWorlds.CampaignSystem.Inventory;                 // Inventory logic
using TaleWorlds.CampaignSystem.Issues;                    // Quest issues
using TaleWorlds.CampaignSystem.LogEntries;                // Campaign log
using TaleWorlds.CampaignSystem.MapEvents;                 // Battle events
using TaleWorlds.CampaignSystem.MapNotificationTypes;      // Notifications
using TaleWorlds.CampaignSystem.Overlay;                   // Menu overlays (may not exist in newer versions)
using TaleWorlds.CampaignSystem.Party;                     // MobileParty, PartyBase
using TaleWorlds.CampaignSystem.Party.PartyComponents;     // Party component types
using TaleWorlds.CampaignSystem.Roster;                    // TroopRoster, TroopRosterElement
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.CampaignSystem.Settlements;               // Settlement, Town, Village
using TaleWorlds.CampaignSystem.Settlements.Buildings;     // Building system
using TaleWorlds.CampaignSystem.Settlements.Workshops;     // Workshop system
using TaleWorlds.CampaignSystem.Siege;                     // Siege mechanics

// ViewModels
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

// Core Systems
using TaleWorlds.Core;                                     // Game, ItemObject, CharacterObject
using TaleWorlds.Core.ViewModelCollection;                 // Core VMs
using TaleWorlds.Library;                                  // MBBindingList, Color, Vec2, MathF
using TaleWorlds.Localization;                             // TextObject
using TaleWorlds.ObjectSystem;                             // MBObjectManager
using TaleWorlds.SaveSystem;                               // SaveableTypeDefiner, etc.

// IMPORTANT: ExplainedNumber is in TaleWorlds.CampaignSystem, NOT TaleWorlds.Library!
// Widget is in TaleWorlds.GauntletUI.BaseTypes, NOT TaleWorlds.GauntletUI!

// UI/Screen Systems
using TaleWorlds.ScreenSystem;                             // ScreenBase, ScreenManager, ScreenLayer
using TaleWorlds.Engine.GauntletUI;                        // GauntletLayer, IGauntletMovie
using TaleWorlds.GauntletUI;                               // Widget base classes
using TaleWorlds.GauntletUI.Data;                          // GauntletMovie, ViewModel binding
using TaleWorlds.GauntletUI.BaseTypes;                     // Widget, BrushWidget, etc.
using TaleWorlds.GauntletUI.PrefabSystem;                  // Prefab loading
using TaleWorlds.InputSystem;                              // Input handling

// MountAndBlade
using TaleWorlds.MountAndBlade;                            // Mission, Agent, etc.
using TaleWorlds.MountAndBlade.View;                       // View system base
using TaleWorlds.MountAndBlade.View.Screens;               // GameStateScreenManager
using TaleWorlds.MountAndBlade.GauntletUI;                 // Gauntlet integration

// SandBox (Campaign-specific)
using SandBox;
using SandBox.GauntletUI;                                  // GauntletMapBarGlobalLayer, etc.
using SandBox.GauntletUI.Map;                              // GauntletMapBar
using SandBox.View;
using SandBox.View.Map;                                    // MapScreen
using SandBox.ViewModelCollection;
```

---

## 2. SCREEN/STATE PATTERN

### Important: [GameStateScreen] attribute does NOT work in mods!
The framework cannot find the attribute in mod assemblies. Use Harmony patch instead:

```csharp
// WRONG - Will not work in mods:
[GameStateScreen(typeof(MyCustomState))]
public class MyCustomScreen : ScreenBase { }

// CORRECT - Use Harmony patch:
[HarmonyPatch(typeof(GameStateScreenManager), "CreateScreen")]
public static class CreateScreen_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(GameState state, ref ScreenBase __result)
    {
        if (state is MyCustomState myState)
        {
            __result = new MyCustomScreen(myState);
            return false; // Skip original
        }
        return true; // Let original handle other states
    }
}
```

### Basic Screen Structure
```csharp
// 1. Game State
public class MyScreenState : GameState
{
    public override bool IsMenuState => true;
}

// 2. Screen Class
public class GauntletMyScreen : ScreenBase, IGameStateListener
{
    private MyScreenState _state;
    private MyScreenVM _dataSource;
    private GauntletLayer _gauntletLayer;
    private IGauntletMovie _movie;

    public GauntletMyScreen(MyScreenState state)
    {
        _state = state;
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();
        _dataSource = new MyScreenVM(OnClose);
        _gauntletLayer = new GauntletLayer("LayerName", 100, false);  // (name, localOrder, shouldClear)
        _movie = _gauntletLayer.LoadMovie("MyScreen", _dataSource);
        _gauntletLayer.InputRestrictions.SetInputRestrictions();
        AddLayer(_gauntletLayer);
    }

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);
        if (_gauntletLayer.Input.IsKeyReleased(InputKey.Escape))
        {
            OnClose();
        }
    }

    private void OnClose()
    {
        Game.Current.GameStateManager.PopState(0);
    }

    protected override void OnFinalize()
    {
        _gauntletLayer.ReleaseMovie(_movie);
        RemoveLayer(_gauntletLayer);
        _dataSource?.OnFinalize();
        base.OnFinalize();
    }

    // IGameStateListener
    void IGameStateListener.OnActivate() { }
    void IGameStateListener.OnDeactivate() { }
    void IGameStateListener.OnInitialize() { }
    void IGameStateListener.OnFinalize() { }
}

// 3. ViewModel
public class MyScreenVM : ViewModel
{
    private Action _onClose;
    
    public MyScreenVM(Action onClose)
    {
        _onClose = onClose;
    }

    [DataSourceProperty]
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
    private string _title = "My Screen";

    [DataSourceProperty]
    public MBBindingList<MyItemVM> Items { get; } = new MBBindingList<MyItemVM>();

    public void ExecuteClose() => _onClose?.Invoke();
}
```

### Opening a Custom Screen
```csharp
public static void OpenMyScreen()
{
    var state = Game.Current.GameStateManager.CreateState<MyScreenState>();
    Game.Current.GameStateManager.PushState(state, 0);
}
```

---

## 3. GUI/XML PREFAB STRUCTURE

### File Location
```
YourMod/
├── GUI/
│   ├── Prefabs/
│   │   └── MyScreen.xml           # Screen layout
│   ├── Brushes/
│   │   └── MyBrushes.xml          # Styling
│   └── SpriteParts/
│       └── ui_mymod/
│           ├── Config.xml         # Sprite category config
│           └── *.png              # Individual sprites
```

### Basic XML Prefab Structure
```xml
<Prefab>
  <Constants>
    <Constant Name="Panel.Width" Value="400" />
    <Constant Name="Button.Height" BrushName="ButtonBrush1" BrushLayer="Default" BrushValueType="Height" />
  </Constants>
  
  <VisualDefinitions>
    <VisualDefinition Name="FadeIn" EaseType="EaseOut" EaseFunction="Quint" TransitionDuration="0.3">
      <VisualState State="Default" AlphaFactor="1" />
    </VisualDefinition>
  </VisualDefinitions>
  
  <Window>
    <!-- Root widget, stretches to fill parent -->
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
      <Children>
        
        <!-- Background overlay -->
        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" 
                Sprite="BlankWhiteSquare_9" Color="#000000FF" AlphaFactor="0.5" />
        
        <!-- Main panel -->
        <Widget Id="MainPanel" 
                WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                SuggestedWidth="800" SuggestedHeight="600"
                HorizontalAlignment="Center" VerticalAlignment="Center"
                Brush="Frame1Brush">
          <Children>
            
            <!-- Title -->
            <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed"
                        SuggestedHeight="50" HorizontalAlignment="Center"
                        Brush="TitleText.Brush" Text="@Title" />
            
            <!-- List with data binding -->
            <ListPanel DataSource="{Items}"
                       WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                       StackLayout.LayoutMethod="VerticalBottomToTop">
              <ItemTemplate>
                <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="40">
                  <Children>
                    <TextWidget Text="@Name" Brush="ItemText.Brush" />
                  </Children>
                </Widget>
              </ItemTemplate>
            </ListPanel>
            
            <!-- Button -->
            <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed"
                          SuggestedWidth="200" SuggestedHeight="50"
                          HorizontalAlignment="Center" VerticalAlignment="Bottom"
                          Brush="ButtonBrush1" Command.Click="ExecuteClose">
              <Children>
                <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent"
                            Text="Close" Brush="Button.Text.Brush" />
              </Children>
            </ButtonWidget>
            
          </Children>
        </Widget>
        
      </Children>
    </Widget>
  </Window>
</Prefab>
```

### Common Widget Types
```xml
<!-- Basic Widgets -->
Widget                  - Base container
BrushWidget            - Styled container with brush
TextWidget             - Text display
RichTextWidget         - Formatted text with tags
ImageWidget            - Image display
ButtonWidget           - Clickable button

<!-- Layout Widgets -->
ListPanel              - Vertical/horizontal list (use with DataSource for binding)
NavigatableListPanel   - List with gamepad navigation
GridWidget             - Grid layout
ScrollablePanel        - Scrollable content

<!-- Specialized Widgets -->
HintWidget             - Tooltip trigger
InputKeyVisualWidget   - Key binding display
CharacterTableauWidget - 3D character preview
```

### Size Policies
```xml
WidthSizePolicy="Fixed"           <!-- Use SuggestedWidth -->
WidthSizePolicy="StretchToParent" <!-- Fill parent -->
WidthSizePolicy="CoverChildren"   <!-- Fit to content -->
```

### Alignment
```xml
HorizontalAlignment="Left|Center|Right"
VerticalAlignment="Top|Center|Bottom"
```

### Data Binding
```xml
<!-- Property binding -->
Text="@PropertyName"
IsVisible="@BoolProperty"
IntText="@IntProperty"

<!-- Collection binding -->
DataSource="{CollectionProperty}"

<!-- Nested ViewModel binding -->
DataSource="{NestedVM}"
```

---

## 4. BRUSH STRUCTURE

```xml
<Brushes>
  <Brush Name="MyButton.Brush">
    <Layers>
      <BrushLayer Name="Default" Sprite="button_background" />
      <BrushLayer Name="Frame" Sprite="button_frame" ExtendTop="5" ExtendBottom="5" ExtendLeft="5" ExtendRight="5" />
    </Layers>
    <Styles>
      <Style Name="Default">
        <BrushLayer Name="Default" ColorFactor="1.0" />
        <BrushLayer Name="Frame" ColorFactor="1.0" />
      </Style>
      <Style Name="Hovered">
        <BrushLayer Name="Default" ColorFactor="1.3" />
        <BrushLayer Name="Frame" ColorFactor="1.5" />
      </Style>
      <Style Name="Pressed">
        <BrushLayer Name="Default" ColorFactor="0.8" />
      </Style>
      <Style Name="Disabled">
        <BrushLayer Name="Default" ColorFactor="0.5" AlphaFactor="0.5" />
      </Style>
    </Styles>
    <SoundProperties>
      <EventSounds>
        <EventSound EventName="Click" Audio="button_click" />
      </EventSounds>
    </SoundProperties>
  </Brush>
</Brushes>
```

---

## 5. SPRITE DATA CONFIGURATION

### SpriteParts/ui_mymod/Config.xml
```xml
<Config>
  <SpriteCategory Name="ui_mymod">
    <AlwaysLoad/>  <!-- Optional: Load with game start -->
  </SpriteCategory>
</Config>
```

### SpriteData registration in SubModule.xml or code
The game auto-discovers sprite parts from `GUI/SpriteParts/` folders.

---

## 6. SAVE SYSTEM

### SaveableTypeDefiner
```csharp
public class MySaveableTypeDefiner : SaveableTypeDefiner
{
    public MySaveableTypeDefiner() : base(YOUR_UNIQUE_ID) { }
    
    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(MyDataClass), 1);
        AddClassDefinition(typeof(MyOtherClass), 2);
    }
    
    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<string, MyDataClass>));
        ConstructContainerDefinition(typeof(List<MyOtherClass>));
    }
}

// Data class
public class MyDataClass
{
    [SaveableField(1)] public string Id;
    [SaveableField(2)] public int Value;
    [SaveableField(3)] public float Progress;
}
```

### CampaignBehavior Save/Load
```csharp
public class MyCampaignBehavior : CampaignBehaviorBase
{
    private Dictionary<string, MyDataClass> _data = new();
    
    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("my_data_key", ref _data);
    }
}
```

---

## 7. COMMON PATTERNS

### Accessing Main Hero
```csharp
Hero mainHero = Hero.MainHero;
MobileParty mainParty = MobileParty.MainParty;
Clan playerClan = Clan.PlayerClan;
```

### Iterating Settlements
```csharp
foreach (var settlement in Settlement.All)
{
    if (settlement.IsTown) { }
    if (settlement.IsCastle) { }
    if (settlement.IsVillage) { }
    if (settlement.OwnerClan == Clan.PlayerClan) { }
}
```

### Party Roster Operations
```csharp
TroopRoster roster = MobileParty.MainParty.MemberRoster;
foreach (TroopRosterElement element in roster.GetTroopRoster())
{
    CharacterObject troop = element.Character;
    int count = element.Number;
    int wounded = element.WoundedNumber;
}
```

### Showing Messages
```csharp
// Simple message
InformationManager.DisplayMessage(new InformationMessage("Text", Colors.Green));

// Inquiry (Yes/No dialog)
InformationManager.ShowInquiry(new InquiryData(
    "Title",
    "Message text",
    true, true,        // affirmative, negative buttons
    "Yes", "No",
    () => { /* Yes action */ },
    () => { /* No action */ }
));

// Multi-selection
InformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
    "Title",
    "Message",
    elements,          // List<InquiryElement>
    true,              // Can select multiple
    1, 5,              // Min/max selection
    "Confirm", "Cancel",
    (selected) => { },
    (selected) => { }
));
```

### Getting Items
```csharp
ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>("item_string_id");
```

### Creating/Finding Characters
```csharp
CharacterObject character = MBObjectManager.Instance.GetObject<CharacterObject>("character_id");
```

---

## 8. COMMON ISSUES & SOLUTIONS

### Issue: CS0246 - Type not found
**Cause:** Missing using directive
**Solution:** Add appropriate namespace (see Section 1)

### Issue: [GameStateScreen] not working
**Cause:** Attribute not discovered in mod assemblies
**Solution:** Use Harmony patch on `GameStateScreenManager.CreateScreen`

### Issue: Overlay namespace not found
**Cause:** `TaleWorlds.CampaignSystem.Overlay` may not exist in newer versions
**Solution:** Use `GameMenuInitializationHandler` patterns instead, or omit overlay parameter

### Issue: Sprites not showing
**Causes & Solutions:**
1. Check sprite category name matches folder name
2. Ensure Config.xml exists in SpriteParts folder
3. Check sprite is referenced correctly in XML (path from SpriteParts folder)

### Issue: ViewModel property not updating UI
**Solution:** Always call `OnPropertyChanged()` or `OnPropertyChangedWithValue(value, nameof(Property))`

### Issue: Movie not loading
**Solutions:**
1. Ensure XML file is in `GUI/Prefabs/` folder
2. Check XML is well-formed (no syntax errors)
3. Verify movie name matches file name (without .xml)

---

## 9. USEFUL EXISTING BRUSHES (from Native)

```
Frame1Brush              - Standard frame
Frame1.Broken.Left       - Broken frame (left side)
Frame1.Broken.Right      - Broken frame (right side)
Panel.Background         - Panel background
Panel.Dent               - Indented panel
ButtonBrush1             - Standard button
ButtonBrush2             - Alternative button
Header1                  - Header style 1
Header2                  - Header style 2
ButtonLeftArrowBrush1    - Left arrow button
ButtonRightArrowBrush1   - Right arrow button
InventoryBackground      - Inventory screen background
```

---

## 10. TIPS FOR MOD DEVELOPMENT

1. **Always wrap in try-catch** - Campaign behaviors run every tick
2. **Use SafeExecutor pattern** - Centralized error handling
3. **Cache MBObjectManager lookups** - They're expensive
4. **Use MBBindingList** - Not regular List for UI binding
5. **Test save/load early** - Save system issues are hard to debug later
6. **Log liberally during development** - Use debug flag to toggle
7. **Check for null everywhere** - Campaign objects can be null at various times
8. **Use Harmony responsibly** - Prefix returns false = skip original method

---

## 11. API REFERENCE (From Decompiled Code)

### Colors Class (TaleWorlds.Library.Colors)
Available colors:
- `Colors.Gray` - (0.65, 0.65, 0.65)
- `Colors.Red` - (1, 0, 0)
- `Colors.Yellow` - (1, 1, 0)
- `Colors.Green` - (0, 1, 0)
- `Colors.Blue` - (0, 0, 1)
- `Colors.Cyan` - (0, 1, 1)
- `Colors.Magenta` - (1, 0, 1)
- `Colors.Black` - (0, 0, 0)
- `Colors.White` - (1, 1, 1)

**NOTE:** `Colors.Gold` does NOT exist! Use `Colors.Yellow` or create custom:
```csharp
Color goldColor = Color.FromUint(4294957447u); // Gold from UIColors
```

### MathF Class (TaleWorlds.Library.MathF)
Use instead of System.Math for floats:
```csharp
MathF.Clamp(value, min, max)
MathF.Sqrt(f)
MathF.Abs(f)
MathF.Floor(f)
MathF.Cos(radian)
MathF.Sin(radian)
MathF.Pow(x, y)
MathF.Min(a, b)
MathF.Max(a, b)
```

### GauntletLayer Constructor
```csharp
// CORRECT: 3 parameters (name, localOrder, shouldClear)
_gauntletLayer = new GauntletLayer("MyLayerName", 100, false);

// WRONG: Single int parameter does not exist
_gauntletLayer = new GauntletLayer(100); // COMPILE ERROR
```

### CampaignTime
```csharp
// Get current campaign time
CampaignTime now = CampaignTime.Now;

// Get values
double totalDays = now.ToDays;
int dayOfYear = now.GetDayOfYear;
int year = now.GetYear;

// Compare times
if (CampaignTime.Now > someOtherTime) { }
```

### InformationManager
```csharp
// Simple inquiry (Yes/No)
InformationManager.ShowInquiry(new InquiryData(
    "Title", "Message",
    isAffirmativeOptionShown: true,
    isNegativeOptionShown: true,
    "Yes", "No",
    affirmativeAction: () => { },
    negativeAction: () => { },
    closeAction: () => { },
    pauseGameActiveState: false
));

// Multi-selection exists but may require specific namespace
// Simpler to use ShowInquiry for most cases
```

### TextObject
```csharp
// Create text
TextObject text = new TextObject("{=text_id}Display Text");

// Empty text (TextObject.Empty may not exist)
TextObject empty = new TextObject("");

// Get string value
string str = textObject.ToString();

// CANNOT use ?? with TextObject and string directly
// WRONG: text?.Name ?? "default"
// RIGHT: text?.Name?.ToString() ?? "default"
```

### CampaignEvents (Common Events)
```csharp
CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, OnHeroLevelledUp);
CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);

// NOTE: OnTroopDismissalEvent does NOT exist
```

---

## 12. NATIVE INTEGRATION PATTERNS

### Custom Model Pattern (Best Practice)
Override native models to integrate custom effects into game tooltips:

```csharp
// PartyMoraleModel - effects show in morale tooltip
public class MyPartyMoraleModel : PartyMoraleModel
{
    public override ExplainedNumber GetEffectivePartyMorale(MobileParty party, bool includeDescription = false)
    {
        ExplainedNumber result = BaseModel.GetEffectivePartyMorale(party, includeDescription);
        
        // Add custom effects
        result.Add(-5f, new TextObject("{=my_effect}My Custom Effect"));
        
        return result;
    }
    
    // Must implement all abstract members
    public override float HighMoraleValue => BaseModel.HighMoraleValue;
    public override int GetDailyStarvationMoralePenalty(PartyBase party) => BaseModel.GetDailyStarvationMoralePenalty(party);
    // ... etc
}

// Register in OnGameStart
protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
{
    if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
    {
        starter.AddModel(new MyPartyMoraleModel());
    }
}
```

### ExplainedNumber Pattern
Use for ANY calculation that might show in tooltips:

```csharp
public ExplainedNumber CalculateSomething(bool includeDescription = false)
{
    var result = new ExplainedNumber(50f, includeDescription, 
        new TextObject("{=base}Base Value"));
    
    // Flat additions
    result.Add(10f, new TextObject("{=bonus}Some Bonus"));
    result.Add(-5f, new TextObject("{=penalty}Some Penalty"));
    
    // Percentage factors
    result.AddFactor(0.05f, new TextObject("{=percent}5% Bonus"));
    result.AddFactor(-0.10f, new TextObject("{=neg_percent}-10% Penalty"));
    
    // Limits
    result.LimitMin(0f);
    result.LimitMax(100f);
    
    return result;
}
```

### ICraftingCampaignBehavior Interface
Proper way to modify smithing stamina:

```csharp
var behavior = Campaign.Current.GetCampaignBehavior<ICraftingCampaignBehavior>();

// Read
int current = behavior.GetHeroCraftingStamina(hero);
int max = behavior.GetMaxHeroCraftingStamina(hero);

// Write
behavior.SetHeroCraftingStamina(hero, newValue);

// Patch max stamina properly
[HarmonyPatch(typeof(CraftingCampaignBehavior), "GetMaxHeroCraftingStamina")]
[HarmonyPostfix]
public static void GetMaxStamina_Postfix(Hero hero, ref int __result)
{
    // Increase result - don't modify internal fields
    __result += hero.GetSkillValue(DefaultSkills.Crafting) / 25 * 5;
}
```

### ItemModifier and ItemModifierGroup
Native way to add item quality/bonuses:

```xml
<!-- In ModuleData/my_modifiers.xml -->
<ItemModifiers>
  <ItemModifierGroup id="my_modifier_group" no_modifier_loot_score="100" no_modifier_production_score="100">
    <ItemModifier id="my_fine" name="{=my_fine}Fine" damage="2" speed="1" armor="0"
                  price_factor="1.3" loot_drop_score="40" production_drop_score="40" quality="fine" />
    <ItemModifier id="my_masterwork" name="{=my_master}Masterwork" damage="4" speed="2" armor="0"
                  price_factor="2.0" loot_drop_score="15" production_drop_score="15" quality="masterwork" />
  </ItemModifierGroup>
</ItemModifiers>
```

```csharp
// Use in code
var modifierGroup = item?.ItemComponent?.ItemModifierGroup;
var modifiers = modifierGroup?.GetModifiersBasedOnQuality(ItemQuality.Masterwork);
if (modifiers?.Count > 0)
{
    var modifier = modifiers[0];
    var element = new EquipmentElement(item, modifier);
}
```

### Native SettlementProjectSelectionVM
For building queue management:

```csharp
// Use native VM instead of custom queue handling
_projectSelection = new SettlementProjectSelectionVM(settlement, OnBuildingQueueChanged);
_governorSelection = new SettlementGovernorSelectionVM(settlement, OnGovernorSelected);

// Native handles all the queue logic
private void OnBuildingQueueChanged()
{
    // Sync from _projectSelection.LocalDevelopmentList back to settlement
}
```

### CampaignEvents - Extended List
```csharp
// Hero events
CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, OnHeroLevelledUp);
CampaignEvents.HeroWounded.AddNonSerializedListener(this, OnHeroWounded);
CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);

// Army events  
CampaignEvents.ArmyCreated.AddNonSerializedListener(this, OnArmyCreated);
CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);

// Settlement events
CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);

// Party events
CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, OnUnitRecruited);

// Battle events (campaign level)
CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
```

---

## 13. FEATURE IMPLEMENTATION PATTERNS

### Equipment System
```csharp
// Equipment copying
Equipment equipment = character.Equipment.Clone();

// Equipment element access
EquipmentElement element = hero.BattleEquipment[EquipmentIndex.Weapon0];

// Slot iteration
for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
{
    hero.BattleEquipment[(EquipmentIndex)i] = savedEquipment[(EquipmentIndex)i];
}

// Civilian vs Battle equipment
Equipment battleEquip = hero.BattleEquipment;
Equipment civilianEquip = hero.CivilianEquipment;
```

### Native Inventory Search (Hidden Feature)
The native inventory has a hidden search feature controlled by `SPInventoryVM.IsSearchAvailable`:
```csharp
// In GauntletInventoryScreen, the _dataSource field holds SPInventoryVM
var field = typeof(GauntletInventoryScreen).GetField("_dataSource", 
    BindingFlags.Instance | BindingFlags.NonPublic);
var vm = field.GetValue(screen) as SPInventoryVM;

// Enable search boxes
vm.IsSearchAvailable = true;
```

### Formation Classes
```csharp
// All native formation types
FormationClass.Infantry
FormationClass.Ranged
FormationClass.Cavalry
FormationClass.HorseArcher
FormationClass.Skirmisher
FormationClass.HeavyInfantry
FormationClass.LightCavalry
FormationClass.HeavyCavalry

// Get troop's default formation
FormationClass formation = character.DefaultFormationClass;
```

### Item Quality System
```csharp
public enum ItemQuality
{
    Poor,
    Inferior,
    Common,
    Fine,
    Masterwork,
    Legendary
}

// Get modifiers by quality
var modifierGroup = item.ItemComponent?.ItemModifierGroup;
var modifiers = modifierGroup?.GetModifiersBasedOnQuality(ItemQuality.Masterwork);
if (modifiers?.Count > 0)
{
    var element = new EquipmentElement(item, modifiers[0]);
}
```

### Native Economy Actions
```csharp
// Gold changes
GiveGoldAction.ApplyBetweenCharacters(from, to, amount);
GiveGoldAction.ApplyForPartyToSettlement(party, settlement, amount);

// Renown/Influence
GainRenownAction.Apply(hero, amount);
ChangeClanInfluenceAction.Apply(clan, amount);

// Trait changes
TraitLevelingHelper.OnIssueSolvedThroughQuest(hero, trait, amount);
```

### Native Save Pattern (TypeDefiner)
```csharp
public class MyTypeDefiner : SaveableTypeDefiner
{
    public MyTypeDefiner() : base(MY_UNIQUE_ID) { }
    
    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(MyData), 10);
    }
    
    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<string, MyData>));
    }
}
```

### Native Property Pattern (ViewModels)
```csharp
// Native uses OnPropertyChangedWithValue
private string _name;
public string Name
{
    get => _name;
    set
    {
        if (_name != value)
        {
            _name = value;
            OnPropertyChangedWithValue(value, nameof(Name));
        }
    }
}
```

---

## 14. COMPLETED FEATURE CHECKLIST

All features implemented with native patterns:

### Core Features
- ✅ Equipment Presets (Battle + Civilian, mount support)
- ✅ Formation Presets (all 8 formation classes)
- ✅ Companion Role Indicators
- ✅ Inventory Search Enabler (native hidden feature)

### Advanced Systems
- ✅ Ring System (20 rings, power progression, corruption)
- ✅ Troop Status (Fear, Frustration, Bonding, Experience, Loyalty)
- ✅ Memory System (Virtual Captains with skills/traits)
- ✅ Custom Resource System (culture-specific needs)
- ✅ Fief Management (remote building queue)
- ✅ Smithing Extended (armor crafting, unique items)

### Native Integration
- ✅ HeirOfNumenorPartyMoraleModel (TroopStatus in tooltips)
- ✅ HeirOfNumenorPartySpeedModel (Ring effects in tooltips)
- ✅ HeirOfNumenorClanTierModel (companion limit)
- ✅ ItemModifier for unique items and rings
- ✅ XML-defined ring modifier groups
- ✅ All relevant CampaignEvents hooked

---

## 15. OVERTURNED ASSUMPTIONS & DECISIONS

### Things Claude Initially Said Were "Impossible" (But We Found Workarounds)

#### ❌ WRONG: "Custom screens require [GameStateScreen] attribute"
**Reality**: The attribute DOES NOT WORK in mod assemblies. The framework only scans game assemblies.
**Solution**: Harmony patch on `GameStateScreenManager.CreateScreen`:
```csharp
[HarmonyPatch(typeof(GameStateScreenManager), "CreateScreen")]
public static class CreateScreen_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(GameState state, ref ScreenBase __result)
    {
        if (state is RingScreenState ringState)
        {
            __result = new GauntletRingScreen(ringState);
            return false; // Skip original
        }
        return true;
    }
}
```
**Lesson**: When framework features don't work, check if they scan only game assemblies.

#### ❌ WRONG: "You can add buttons to MapBar by appending XML"
**Reality**: Bannerlord REPLACES brush files by name, it doesn't merge them.
**Solution**: Override the ENTIRE MapBar.xml brush file with your additions included.
**Lesson**: XML "modding" is actually replacement, not patching.

#### ❌ WRONG: "Sprite categories should be unloaded when screens close"
**Reality**: Sprite categories are GLOBALLY shared. Unloading breaks other UI.
**Solution**: Load categories but NEVER unload them. Let the game manage lifecycle.
**Lesson**: Test UI changes extensively; visual bugs may appear in unrelated screens.

#### ❌ WRONG: "Base64 embedding is fine for images in artifacts"
**Reality**: Large base64 strings crash context compaction in Claude.
**Solution**: Reference external files or generate SVG inline.
**Lesson**: Keep artifacts lightweight; avoid embedding binary data.

### Key Architecture Decisions

| Decision | Why | Alternative Considered |
|----------|-----|----------------------|
| Harmony patches over attributes | Attributes don't work in mods | Tried [GameStateScreen] first |
| Single MapBar.xml override | Merging doesn't work | Tried XML append |
| SVG table in mock vs image | Avoids file/base64 issues | Tried image reference |
| SaveableTypeDefiner base 8675309 | Unique ID avoids conflicts | Random IDs |
| Float margins in XML | Int crashes the game | Discovered via crash |
| Dual-layer button injection | Single injection unreliable | Constructor-only failed |

---

## 16. ISSUE HANDLING STRATEGIES

### Debugging Workflow

#### Step 1: Identify the Layer
```
┌─────────────────────────────────────────────────────────┐
│ ERROR TYPE              │ CHECK FIRST                   │
├─────────────────────────────────────────────────────────┤
│ Game won't start        │ SubModule.xml, dependencies   │
│ UI doesn't appear       │ XML path, Movie name match    │
│ Button doesn't work     │ Brush StringId, VM method     │
│ Save corruption         │ SaveableTypeDefiner IDs       │
│ Visual glitches         │ Sprite category, Brush states │
│ Null reference          │ Event timing, initialization  │
└─────────────────────────────────────────────────────────┘
```

#### Step 2: Check Common Gotchas
1. **XML Margins**: Must be FLOAT (`MarginLeft="10.0"` not `MarginLeft="10"`)
2. **Movie name**: Must match XML filename WITHOUT extension
3. **Brush StringId**: Must match layer name EXACTLY (case-sensitive)
4. **DataSource binding**: `{Property}` for collections, `@Property` for strings
5. **Harmony timing**: Some patches need [HarmonyPriority]

#### Step 3: Isolate and Test
```csharp
// Wrap risky code in SafeExecutor
SafeExecutor.ExecuteSafe("FeatureName", () =>
{
    // Risky code here
}, logOnSuccess: true);
```

### Successful Fix Patterns

#### Pattern: "Feature Works in Vanilla But Not in Mod"
1. Check if feature uses attributes (they often don't work in mods)
2. Look for Harmony patch opportunity
3. Check if feature scans specific assemblies only
4. **Example**: [GameStateScreen] → Harmony CreateScreen patch

#### Pattern: "UI Element Doesn't Appear"
1. Verify XML is in correct folder (GUI/Prefabs/)
2. Check Movie name matches filename
3. Verify DataSource is set before LoadMovie
4. Check brush file is loading (may need full replacement)
5. **Example**: MapBar button → full brush file override

#### Pattern: "Click/Hover Doesn't Work"
1. Check brush has all states (Default, Hovered, Pressed, Disabled)
2. Verify Command binding syntax: `Command.Click="ExecuteMethodName"`
3. Check if widget has `IsEnabled="true"` and `IsVisible="true"`
4. Verify zIndex if overlapping elements exist

#### Pattern: "Save/Load Crashes"
1. Verify SaveableTypeDefiner has unique base ID
2. Check all saved fields have [SaveableField(n)] with unique n
3. Ensure SyncData uses unique string key
4. Test with fresh save (old saves may have incompatible data)

#### Pattern: "Works First Time, Fails on Reload"
1. Check for static state that isn't reset
2. Verify event subscriptions are cleaned up in OnFinalize
3. Look for cached references to destroyed objects
4. **Example**: ViewModel caching → recreate on each screen open

### Mock-to-Implementation Translation

When building UI mocks (React/JSX) for Bannerlord:

| Mock Element | Bannerlord Equivalent |
|--------------|----------------------|
| `<div style={{position: 'absolute'}}` | `<Widget PositionType="Absolute"` |
| `onClick={handler}` | `Command.Click="ExecuteHandler"` |
| `transform: scale()` | `ScaledPositionXOffset/YOffset` |
| SVG shapes | Brush with sprite layers |
| CSS transitions | `VisualDefinition` with `TransitionDuration` |
| State hooks | `[DataSourceProperty]` + OnPropertyChanged |
| Conditional render | `IsVisible="@PropertyName"` |
| `requestAnimationFrame` | `OnFrameTick(float dt)` in Screen class |
| Dynamic positioning | Margin bindings `MarginLeft="@PosX"` |

### Bannerlord Animation System

**VisualDefinitions** - Built-in state-based transitions:
```xml
<VisualDefinitions>
  <VisualDefinition Name="FadeSlide" TransitionDuration="0.3" EaseType="EaseOut" EaseFunction="Quint">
    <VisualState State="Default" MarginTop="0" AlphaFactor="1" />
    <VisualState State="Pressed" MarginTop="5" AlphaFactor="0.8" />
    <VisualState State="Selected" MarginTop="10" AlphaFactor="1" />
  </VisualDefinition>
</VisualDefinitions>

<!-- Apply to widget -->
<Widget VisualDefinition="FadeSlide" ... />
```

Properties that can be animated via VisualState:
- `MarginTop`, `MarginLeft`, `MarginRight`, `MarginBottom`
- `SuggestedWidth`, `SuggestedHeight`
- `AlphaFactor` (opacity)

**Code-Driven Animation** - For continuous motion like orbital rotation:
```csharp
// In Screen class
protected override void OnFrameTick(float dt)
{
    base.OnFrameTick(dt);
    _dataSource?.UpdateAnimations(dt);
}

// In ViewModel
public void UpdateAnimations(float dt)
{
    if (_isRotating)
    {
        _rotationProgress += dt / _animationDuration;
        float t = EaseInOutCubic(_rotationProgress);
        // Update positions via property bindings
        foreach (var ring in _rings)
        {
            ring.PosX = Lerp(startX, endX, t);
            ring.PosY = Lerp(startY, endY, t);
        }
    }
}

private static float EaseInOutCubic(float t)
{
    return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
}
```

**Hybrid Approach** - Best for complex UI like Ring Orbital:
1. Use `OnFrameTick` for smooth continuous animations (rotation)
2. Use `VisualDefinitions` for discrete state changes (hover, selected, equipped glow)
3. Bind position properties (`MarginLeft="@PosX"`) updated from code

### Error Recovery Checklist

When something breaks badly:
- [ ] Check rgl_log.txt for native errors
- [ ] Verify SubModule.xml loads in launcher
- [ ] Test with minimal mod (comment out features)
- [ ] Compare working vanilla examples
- [ ] Check Bannerlord version compatibility
- [ ] Verify Harmony isn't conflicting with other mods
- [ ] Test on fresh save file

---

## 12. SUBMODULE.XML STRUCTURE

### DependedModules Format

**CRITICAL**: Use the correct version format and module IDs:

```xml
<DependedModules>
  <!-- Vanilla modules - use "v" prefix for version -->
  <DependedModule Id="Native" DependentVersion="v1.3.0" Optional="false"/>
  <DependedModule Id="SandBoxCore" DependentVersion="v1.3.0" Optional="false"/>
  <DependedModule Id="Sandbox" DependentVersion="v1.3.0" Optional="false"/>
  <DependedModule Id="StoryMode" DependentVersion="v1.3.0" Optional="true"/>
  
  <!-- Workshop/External mods -->
  <DependedModule Id="Bannerlord.MBOptionScreen" DependentVersion="v5.0.0" Optional="true"/>
  <DependedModule Id="Bannerlord.Harmony" DependentVersion="v2.0.0" Optional="false"/>
</DependedModules>
```

**Common Mistakes**:
- ❌ `DependentVersion="e1.0.0"` - Wrong prefix (e vs v)
- ❌ `Id="SandBox"` - Wrong case (should be `Sandbox` for the dependency)
- ❌ Missing `Optional` attribute

### ItemModifiers vs ItemModifierGroups

Item modifiers and modifier groups must be in **separate files** with different root elements:

**ring_modifiers.xml** (flat list of modifiers):
```xml
<?xml version="1.0" encoding="utf-8"?>
<ItemModifiers>
  <ItemModifier id="my_modifier" name="My Modifier" damage="5" speed="0" armor="0" price_factor="1.5" />
</ItemModifiers>
```

**ring_modifier_groups.xml** (groups referencing modifiers):
```xml
<?xml version="1.0" encoding="utf-8"?>
<ItemModifierGroups>
  <ItemModifierGroup id="my_modifier_group" no_modifier_loot_score="100" no_modifier_production_score="100">
    <ItemModifier id="my_modifier" loot_drop_score="50" production_drop_score="50" />
  </ItemModifierGroup>
</ItemModifierGroups>
```

**SubModule.xml registration**:
```xml
<Xmls>
  <XmlNode>
    <XmlName id="ItemModifiers" path="ring_modifiers"/>
    <IncludedGameTypes>
      <GameType value="Campaign"/>
      <GameType value="CampaignStoryMode"/>
    </IncludedGameTypes>
  </XmlNode>
  <XmlNode>
    <XmlName id="ItemModifierGroups" path="ring_modifier_groups"/>
    <IncludedGameTypes>
      <GameType value="Campaign"/>
      <GameType value="CampaignStoryMode"/>
    </IncludedGameTypes>
  </XmlNode>
</Xmls>
```

**Error if combined**: `Error: The element 'ItemModifiers' has invalid child element 'ItemModifierGroup'`

### SpriteData XML Structure

The `<ModuleName>SpriteData.xml` file must be placed in the `GUI/` folder (not inside SpriteParts):

```
Module/
├── GUI/
│   ├── HeirOfNumenorSpriteData.xml    ← Sprite definitions
│   └── SpriteParts/
│       └── ui_ring_system/
│           └── ui_ring_system_1.tpac  ← Compiled sprite sheet
```

File naming convention: `<ModuleName>SpriteData.xml` (e.g., `NativeSpriteData.xml`, `HeirOfNumenorSpriteData.xml`)

## Gauntlet Click Priority & Z-Order

### Problem
Gauntlet renders widgets in XML declaration order, not by dynamic z-position. Later-declared widgets render on top AND receive clicks first.

### Solutions
1. **Declare order matters:** For overlapping widgets, declare back items first, front items last
2. **Anti-alignment logic:** Prevent overlaps by nudging positions when items would align
3. **IsHitTestVisible="false":** For decorative overlays (glows, shadows) that shouldn't intercept clicks
4. **AcceptDrop="false":** For wrapper widgets that shouldn't capture events

### Ring Screen Z-Order (correct)
- Back rings: Mortal → Dwarven → Elven → One Ring (inner on top at back)
- Front rings: Mortal → Dwarven → Elven (inner declared LAST = clickable on top)

## Localization Pattern (Bannerlord Standard)

### C# Usage
```csharp
using TaleWorlds.Localization;

// Simple text
string text = new TextObject("{=string_id}Default English Text").ToString();

// With variables
var text = new TextObject("{=ring_count}{OWNED} of 20 Rings Found");
text.SetTextVariable("OWNED", count);
return text.ToString();

// VM property for XML binding
[DataSourceProperty]
public string LabelName => new TextObject("{=label_id}Label Text").ToString();
```

### XML Usage
```xml
<!-- Bind to localized VM property -->
<TextWidget Text="@LabelName" />
```

### Localization File (ModuleData/Languages/std_module_strings_xml.xml)
```xml
<string id="string_id" text="English text here" />
<string id="ring_count" text="{OWNED} of 20 Rings Found" />
```

### Adding Translations
1. Create folder: `ModuleData/Languages/DE/` (German), `FR/` (French), etc.
2. Copy `std_module_strings_xml.xml` to new folder
3. Change `<tag language="English" />` to target language
4. Translate `text="..."` values, keep `id="..."` unchanged

## Glow/Shadow Effects Pattern

### Dual Glow System
```csharp
// Table glow: brightens when ring is closer (bobbing down)
float tableOpacity = 0.25f + (-bobOffset / 20f);  // Inverted

// Ring aura: brightens when ring is higher (bobbing up)  
float auraOpacity = 0.08f + (bobOffset / 22f);  // Not inverted
```

### XML Structure
```xml
<Widget> <!-- Container -->
  <Widget IsHitTestVisible="false" Color="@TableGlowColor" AlphaFactor="@ShadowOpacityValue" /> <!-- Table glow -->
  <ButtonWidget> <!-- Clickable ring -->
    <Widget IsHitTestVisible="false" Color="@TableGlowColor" AlphaFactor="@RingAuraOpacity" /> <!-- Ring aura -->
    <Widget Color="#RingColor" /> <!-- Ring visual -->
  </ButtonWidget>
</Widget>
```

---

## 13. COMMON LIBRARY PATTERN

### Architecture Principle
**Never duplicate code between mods.** Extract shared utilities to BannerlordCommonLib (BCL).

### When to Use BCL
- Data sharing (Gist, Discord, Paste uploads)
- Exception/error capture
- Logging utilities
- Common UI helpers
- File I/O wrappers

### Dependency Setup
```xml
<!-- SubModule.xml -->
<DependedModule Id="BannerlordCommonLib" DependentVersion="v1.0.0" Optional="false"/>
```

```csharp
// C# usage
using BannerlordCommonLib.Sharing;
using BannerlordCommonLib.Diagnostics;
using BannerlordCommonLib.Utilities;

// Logging
Log.Info("MyMod", "Something happened");
Log.Error("MyMod", "Something failed");

// Data sharing
var url = await DataSharing.UploadToGist(json, "data.json");
await DataSharing.SendToDiscord(json, webhookUrl, "New data!");

// Exception capture
ExceptionCapture.Initialize();
ExceptionCapture.LogMisbehavior("Combat", "Invalid state detected");
```

### What NOT to Put in BCL
- Feature-specific logic
- UI prefabs (belong in their mods)
- Game balance data
- Mod-specific settings
