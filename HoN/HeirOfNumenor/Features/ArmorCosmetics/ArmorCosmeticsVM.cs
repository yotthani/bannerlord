using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.ArmorCosmetics
{
    public class ArmorCosmeticsVM : ViewModel
    {
        private readonly Action _closeAction;
        private readonly Hero _hero;
        private MBBindingList<ArmorSlotVM> _armorSlots;
        
        public ArmorCosmeticsVM(Hero hero, Action closeAction)
        {
            _hero = hero ?? Hero.MainHero;
            _closeAction = closeAction;
            _armorSlots = new MBBindingList<ArmorSlotVM>();
            RefreshSlots();
        }
        
        private void RefreshSlots()
        {
            _armorSlots.Clear();
            foreach (var slot in ArmorCosmeticsManager.CosmeticSlots)
            {
                _armorSlots.Add(new ArmorSlotVM(_hero, slot, RefreshSlots));
            }
        }
        
        public void ExecuteClose() => _closeAction?.Invoke();
        
        [DataSourceProperty]
        public string TitleText => new TextObject("{=armor_cosmetics_title}Armor Cosmetics").ToString();
        
        [DataSourceProperty]
        public string CloseText => new TextObject("{=close_button}Close").ToString();
        
        [DataSourceProperty]
        public MBBindingList<ArmorSlotVM> ArmorSlots
        {
            get => _armorSlots;
            set { _armorSlots = value; OnPropertyChangedWithValue(value); }
        }
    }
    
    public class ArmorSlotVM : ViewModel
    {
        private readonly Hero _hero;
        private readonly EquipmentIndex _slot;
        private readonly Action _refreshCallback;
        
        public ArmorSlotVM(Hero hero, EquipmentIndex slot, Action refreshCallback)
        {
            _hero = hero;
            _slot = slot;
            _refreshCallback = refreshCallback;
        }
        
        public void ExecuteToggleVisibility()
        {
            ArmorCosmeticsManager.ToggleSlotVisibility(_hero, _slot);
            OnPropertyChanged(nameof(IsVisible));
            OnPropertyChanged(nameof(VisibilityIcon));
            OnPropertyChanged(nameof(VisibilityBrush));
        }
        
        public void ExecuteSelectCosmetic()
        {
            if (!(Settings.Instance?.EnableCosmeticPicker ?? false))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Cosmetic picker disabled in settings", Colors.Yellow));
                return;
            }
            
            // Get available items from inventory that match this slot
            var inventory = _hero?.PartyBelongedTo?.ItemRoster;
            if (inventory == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("No inventory available", Colors.Red));
                return;
            }
            
            // Find matching items for this slot
            var matchingItems = new List<ItemObject>();
            for (int i = 0; i < inventory.Count; i++)
            {
                var item = inventory.GetItemAtIndex(i);
                if (item != null && ArmorCosmeticsManager.IsValidCosmeticForSlot(item, _slot))
                {
                    matchingItems.Add(item);
                }
            }
            
            if (matchingItems.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"No items in inventory for {SlotName} slot", Colors.Yellow));
                return;
            }
            
            // For now, cycle through available items
            var currentCosmetic = ArmorCosmeticsManager.GetCosmeticOverride(_hero, _slot);
            int currentIndex = currentCosmetic != null ? matchingItems.IndexOf(currentCosmetic) : -1;
            int nextIndex = (currentIndex + 1) % matchingItems.Count;
            
            var newCosmetic = matchingItems[nextIndex];
            ArmorCosmeticsManager.SetCosmeticOverride(_hero, _slot, newCosmetic);
            
            InformationManager.DisplayMessage(new InformationMessage(
                $"Cosmetic set to: {newCosmetic.Name}", Colors.Green));
            _refreshCallback?.Invoke();
        }
        
        [DataSourceProperty]
        public string SlotName => _slot switch
        {
            EquipmentIndex.Head => "Head",
            EquipmentIndex.Cape => "Cape",
            EquipmentIndex.Body => "Body",
            EquipmentIndex.Gloves => "Gloves",
            EquipmentIndex.Leg => "Legs",
            _ => _slot.ToString()
        };
        
        [DataSourceProperty]
        public string CurrentItemName
        {
            get
            {
                var item = _hero?.BattleEquipment?[_slot].Item;
                return item?.Name?.ToString() ?? "Empty";
            }
        }
        
        [DataSourceProperty]
        public bool IsVisible => ArmorCosmeticsManager.IsSlotVisible(_hero, _slot);
        
        [DataSourceProperty]
        public string VisibilityIcon => IsVisible ? "👁" : "✕";
        
        [DataSourceProperty]
        public string VisibilityBrush => IsVisible ? "ButtonBrush1" : "ButtonBrush2";
    }
}
