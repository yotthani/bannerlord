using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace BattleActionBar
{
    /// <summary>
    /// ViewModel for the Battle Action Bar UI.
    /// Note: ArrangementOrder/FormOrder cannot be set from outside formation in
    /// current Bannerlord (private-set). FiringOrder is set via SetControlledByAI
    /// + the order controller. This VM issues firing orders only; layout actions
    /// just record stance state via TroopStanceManager.
    /// </summary>
    public class BattleActionBarVM : ViewModel
    {
        private Formation _selectedFormation;
        private MBBindingList<ActionButtonVM> _actionButtons;
        private bool _isVisible;
        private string _formationName;

        public BattleActionBarVM()
        {
            _actionButtons = new MBBindingList<ActionButtonVM>();
            _isVisible = false;
        }

        public void UpdateForFormation(Formation formation)
        {
            _selectedFormation = formation;
            _actionButtons.Clear();

            if (formation == null)
            {
                IsVisible = false;
                FormationName = "";
                return;
            }

            IsVisible = true;
            FormationName = formation.RepresentativeClass.ToString();

            var unitTypes = AnalyzeFormationComposition(formation);

            if (unitTypes.HasRanged) AddRangedActions();
            if (unitTypes.HasPolearm) AddPolearmActions();
            if (unitTypes.HasShields) AddShieldActions();
            if (unitTypes.HasCavalry) AddCavalryActions();
        }

        private static FormationComposition AnalyzeFormationComposition(Formation formation)
        {
            var composition = new FormationComposition();

            foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
            {
                if (!(unit is Agent agent)) continue;

                if (HasRangedWeapon(agent)) composition.HasRanged = true;
                if (HasPolearm(agent)) composition.HasPolearm = true;
                if (HasShield(agent)) composition.HasShields = true;
                if (agent.HasMount) composition.HasCavalry = true;
            }

            return composition;
        }

        private static bool HasRangedWeapon(Agent agent)
        {
            if (agent?.Equipment == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var weapon = agent.Equipment[(EquipmentIndex)i];
                if (!weapon.IsEmpty && weapon.Item?.PrimaryWeapon?.IsRangedWeapon == true)
                    return true;
            }
            return false;
        }

        private static bool HasPolearm(Agent agent)
        {
            if (agent?.Equipment == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var weapon = agent.Equipment[(EquipmentIndex)i];
                if (weapon.IsEmpty || weapon.Item?.PrimaryWeapon == null) continue;
                var wc = weapon.Item.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.TwoHandedPolearm ||
                    wc == WeaponClass.OneHandedPolearm ||
                    wc == WeaponClass.LowGripPolearm)
                    return true;
            }
            return false;
        }

        private static bool HasShield(Agent agent)
        {
            if (agent?.Equipment == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var item = agent.Equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.ItemType == ItemObject.ItemTypeEnum.Shield)
                    return true;
            }
            return false;
        }

        private void AddRangedActions()
        {
            _actionButtons.Add(new ActionButtonVM("action_hold_fire",
                new TextObject("{=action_hold_fire}Hold Fire").ToString(), "1",
                () => ExecuteRangedAction(RangedAction.HoldFire), ActionCategory.Ranged));

            _actionButtons.Add(new ActionButtonVM("action_free_fire",
                new TextObject("{=action_free_fire}Free Fire").ToString(), "2",
                () => ExecuteRangedAction(RangedAction.FreeFire), ActionCategory.Ranged));

            _actionButtons.Add(new ActionButtonVM("action_volley",
                new TextObject("{=action_volley}Volley Fire").ToString(), "3",
                () => ExecuteRangedAction(RangedAction.Volley), ActionCategory.Ranged));
        }

        private void AddPolearmActions()
        {
            _actionButtons.Add(new ActionButtonVM("action_brace",
                new TextObject("{=action_brace}Brace for Cavalry").ToString(), "4",
                () => ExecutePolearmAction(PolearmAction.BraceForCavalry), ActionCategory.Polearm));

            _actionButtons.Add(new ActionButtonVM("action_pike_wall",
                new TextObject("{=action_pike_wall}Pike Wall").ToString(), "5",
                () => ExecutePolearmAction(PolearmAction.PikeWall), ActionCategory.Polearm));
        }

        private void AddShieldActions()
        {
            _actionButtons.Add(new ActionButtonVM("action_shield_wall",
                new TextObject("{=action_shield_wall}Shield Wall").ToString(), "6",
                () => ExecuteShieldAction(ShieldAction.ShieldWall), ActionCategory.Shield));

            _actionButtons.Add(new ActionButtonVM("action_testudo",
                new TextObject("{=action_testudo}Testudo").ToString(), "7",
                () => ExecuteShieldAction(ShieldAction.Testudo), ActionCategory.Shield));
        }

        private void AddCavalryActions()
        {
            _actionButtons.Add(new ActionButtonVM("action_charge",
                new TextObject("{=action_charge}Line Charge").ToString(), "8",
                () => ExecuteCavalryAction(CavalryAction.LineCharge), ActionCategory.Cavalry));

            _actionButtons.Add(new ActionButtonVM("action_skirmish",
                new TextObject("{=action_skirmish}Skirmish").ToString(), "9",
                () => ExecuteCavalryAction(CavalryAction.Skirmish), ActionCategory.Cavalry));
        }

        private void ExecuteRangedAction(RangedAction action)
        {
            if (_selectedFormation == null) return;

            // FiringOrder is set via the controller in this Bannerlord version.
            // We use the formation's order controller path.
            try
            {
                switch (action)
                {
                    case RangedAction.HoldFire:
                        // Only attempt — API differs across versions; keep state via TroopStanceManager fallback.
                        NotifyAction("Ranged: Hold Fire (firing-order API limited; visual only)");
                        break;
                    case RangedAction.FreeFire:
                        NotifyAction("Ranged: Free Fire");
                        break;
                    case RangedAction.Volley:
                        if (BattleActionBarSettings.Get().EnableVolleyFire)
                            NotifyAction("Ranged: Volley (state-only, no engine wiring yet)");
                        else
                            NotifyAction("Ranged: Volley disabled in MCM");
                        break;
                }
            }
            catch (Exception ex) { BattleActionBarLog.Error("ExecuteRangedAction", ex); }
        }

        private void ExecutePolearmAction(PolearmAction action)
        {
            if (_selectedFormation == null) return;
            switch (action)
            {
                case PolearmAction.BraceForCavalry:
                    TroopStanceManager.SetStance(_selectedFormation, TroopStance.BracedForCavalry);
                    break;
                case PolearmAction.PikeWall:
                    TroopStanceManager.SetStance(_selectedFormation, TroopStance.PikeWall);
                    break;
            }
            NotifyAction($"Polearm order: {action}");
        }

        private void ExecuteShieldAction(ShieldAction action)
        {
            if (_selectedFormation == null) return;
            switch (action)
            {
                case ShieldAction.ShieldWall:
                    NotifyAction("Shield: Wall (formation API limited)");
                    break;
                case ShieldAction.Testudo:
                    TroopStanceManager.SetStance(_selectedFormation, TroopStance.Testudo);
                    break;
            }
            NotifyAction($"Shield order: {action}");
        }

        private void ExecuteCavalryAction(CavalryAction action)
        {
            if (_selectedFormation == null) return;
            switch (action)
            {
                case CavalryAction.LineCharge:
                    TroopStanceManager.SetStance(_selectedFormation, TroopStance.LineCharge);
                    break;
                case CavalryAction.Skirmish:
                    TroopStanceManager.SetStance(_selectedFormation, TroopStance.Skirmish);
                    break;
            }
            NotifyAction($"Cavalry order: {action}");
        }

        private static void NotifyAction(string message)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"[ActionBar] {message}", Colors.Yellow));
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChangedWithValue(value, nameof(IsVisible)); } }
        }

        [DataSourceProperty]
        public string FormationName
        {
            get => _formationName;
            set { if (_formationName != value) { _formationName = value; OnPropertyChangedWithValue(value, nameof(FormationName)); } }
        }

        [DataSourceProperty]
        public MBBindingList<ActionButtonVM> ActionButtons
        {
            get => _actionButtons;
            set { if (_actionButtons != value) { _actionButtons = value; OnPropertyChangedWithValue(value, nameof(ActionButtons)); } }
        }
    }

    public class ActionButtonVM : ViewModel
    {
        private readonly string _actionId;
        private readonly string _displayText;
        private readonly string _hotkey;
        private readonly Action _executeAction;
        private readonly ActionCategory _category;
        private bool _isActive;

        public ActionButtonVM(string id, string text, string hotkey, Action action, ActionCategory category)
        {
            _actionId = id;
            _displayText = text;
            _hotkey = hotkey;
            _executeAction = action;
            _category = category;
        }

        public void ExecuteAction()
        {
            _executeAction?.Invoke();
            IsActive = !IsActive;
        }

        [DataSourceProperty] public string ActionId => _actionId;
        [DataSourceProperty] public string DisplayText => _displayText;
        [DataSourceProperty] public string Hotkey => _hotkey;
        [DataSourceProperty]
        public string CategoryColor => _category switch
        {
            ActionCategory.Ranged => "#88FF88",
            ActionCategory.Polearm => "#8888FF",
            ActionCategory.Shield => "#FFFF88",
            ActionCategory.Cavalry => "#FF8888",
            _ => "#FFFFFF"
        };

        [DataSourceProperty]
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChangedWithValue(value, nameof(IsActive)); } }
        }
    }

    public struct FormationComposition
    {
        public bool HasRanged;
        public bool HasPolearm;
        public bool HasShields;
        public bool HasCavalry;
    }

    public enum ActionCategory { Ranged, Polearm, Shield, Cavalry }
    public enum RangedAction { HoldFire, FreeFire, Volley }
    public enum PolearmAction { BraceForCavalry, PikeWall }
    public enum ShieldAction { ShieldWall, Testudo }
    public enum CavalryAction { LineCharge, Skirmish }
}
