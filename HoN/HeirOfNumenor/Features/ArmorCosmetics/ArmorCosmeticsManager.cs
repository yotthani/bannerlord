using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.ArmorCosmetics
{
    /// <summary>
    /// Manages armor cosmetic overrides - hide or replace visual appearance
    /// while keeping original stats. RPG transmog-style system.
    /// </summary>
    public static class ArmorCosmeticsManager
    {
        // Slot visibility (true = visible, false = hidden)
        private static Dictionary<Hero, Dictionary<EquipmentIndex, bool>> _slotVisibility = new();
        
        // Cosmetic overrides (visual item different from stat item)
        private static Dictionary<Hero, Dictionary<EquipmentIndex, ItemObject>> _cosmeticOverrides = new();
        
        // Slots that can be customized
        public static readonly EquipmentIndex[] CosmeticSlots = new[]
        {
            EquipmentIndex.Head,
            EquipmentIndex.Cape,
            EquipmentIndex.Body,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg
        };
        
        #region Visibility
        
        /// <summary>
        /// Hide a specific armor slot visually (stats remain).
        /// </summary>
        public static void HideSlot(Hero hero, EquipmentIndex slot)
        {
            if (!IsValidCosmeticSlot(slot)) return;
            EnsureHeroEntries(hero);
            _slotVisibility[hero][slot] = false;
        }
        
        /// <summary>
        /// Show a previously hidden slot.
        /// </summary>
        public static void ShowSlot(Hero hero, EquipmentIndex slot)
        {
            if (!IsValidCosmeticSlot(slot)) return;
            EnsureHeroEntries(hero);
            _slotVisibility[hero][slot] = true;
        }
        
        /// <summary>
        /// Toggle slot visibility.
        /// </summary>
        public static void ToggleSlotVisibility(Hero hero, EquipmentIndex slot)
        {
            if (IsSlotVisible(hero, slot))
                HideSlot(hero, slot);
            else
                ShowSlot(hero, slot);
        }
        
        /// <summary>
        /// Check if slot is visible.
        /// </summary>
        public static bool IsSlotVisible(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return true;
            if (!_slotVisibility.TryGetValue(hero, out var slots)) return true;
            if (!slots.TryGetValue(slot, out var visible)) return true;
            return visible;
        }
        
        #endregion
        
        #region Cosmetic Overrides (Transmog)
        
        /// <summary>
        /// Set a cosmetic override - show different item visually.
        /// The original item's stats are kept.
        /// </summary>
        public static void SetCosmeticOverride(Hero hero, EquipmentIndex slot, ItemObject cosmeticItem)
        {
            if (!IsValidCosmeticSlot(slot)) return;
            if (cosmeticItem == null) return;
            
            // Validate same slot type
            var originalItem = hero?.BattleEquipment?[slot].Item;
            if (originalItem == null) return;
            
            if (!IsSameArmorSlot(originalItem, cosmeticItem, slot))
            {
                Log($"Cannot apply {cosmeticItem.Name} as cosmetic - wrong slot type");
                return;
            }
            
            EnsureHeroEntries(hero);
            _cosmeticOverrides[hero][slot] = cosmeticItem;
            Log($"Applied cosmetic: {cosmeticItem.Name} over {originalItem.Name}");
        }
        
        /// <summary>
        /// Remove cosmetic override, show original item.
        /// </summary>
        public static void ClearCosmeticOverride(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return;
            if (_cosmeticOverrides.TryGetValue(hero, out var overrides))
            {
                overrides.Remove(slot);
            }
        }
        
        /// <summary>
        /// Get the visual item for a slot (cosmetic if set, otherwise original).
        /// </summary>
        public static ItemObject GetVisualItem(Hero hero, EquipmentIndex slot)
        {
            // If hidden, return null (no visual)
            if (!IsSlotVisible(hero, slot))
                return null;
            
            // Check for cosmetic override
            if (_cosmeticOverrides.TryGetValue(hero, out var overrides))
            {
                if (overrides.TryGetValue(slot, out var cosmetic))
                    return cosmetic;
            }
            
            // Return original
            return hero?.BattleEquipment?[slot].Item;
        }
        
        /// <summary>
        /// Check if slot has cosmetic override.
        /// </summary>
        public static bool HasCosmeticOverride(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return false;
            if (!_cosmeticOverrides.TryGetValue(hero, out var overrides)) return false;
            return overrides.ContainsKey(slot);
        }
        
        #endregion
        
        #region Helpers
        
        private static bool IsValidCosmeticSlot(EquipmentIndex slot)
        {
            return slot == EquipmentIndex.Head ||
                   slot == EquipmentIndex.Cape ||
                   slot == EquipmentIndex.Body ||
                   slot == EquipmentIndex.Gloves ||
                   slot == EquipmentIndex.Leg;
        }
        
        private static bool IsSameArmorSlot(ItemObject original, ItemObject cosmetic, EquipmentIndex slot)
        {
            // Both must be armor
            if (original.ItemType != cosmetic.ItemType) return false;
            
            // Must be wearable in same slot
            return slot switch
            {
                EquipmentIndex.Head => cosmetic.ItemType == ItemObject.ItemTypeEnum.HeadArmor,
                EquipmentIndex.Cape => cosmetic.ItemType == ItemObject.ItemTypeEnum.Cape,
                EquipmentIndex.Body => cosmetic.ItemType == ItemObject.ItemTypeEnum.BodyArmor,
                EquipmentIndex.Gloves => cosmetic.ItemType == ItemObject.ItemTypeEnum.HandArmor,
                EquipmentIndex.Leg => cosmetic.ItemType == ItemObject.ItemTypeEnum.LegArmor,
                _ => false
            };
        }
        
        private static void EnsureHeroEntries(Hero hero)
        {
            if (hero == null) return;
            if (!_slotVisibility.ContainsKey(hero))
                _slotVisibility[hero] = new Dictionary<EquipmentIndex, bool>();
            if (!_cosmeticOverrides.ContainsKey(hero))
                _cosmeticOverrides[hero] = new Dictionary<EquipmentIndex, ItemObject>();
        }
        
        private static void Log(string msg)
        {
            if (Settings.Instance?.DebugMode ?? false)
                InformationManager.DisplayMessage(new InformationMessage($"[Cosmetics] {msg}", Colors.Magenta));
        }
        
        /// <summary>
        /// Clear all cosmetics for hero (e.g., on death/retirement).
        /// </summary>
        public static void ClearAllForHero(Hero hero)
        {
            _slotVisibility.Remove(hero);
            _cosmeticOverrides.Remove(hero);
        }
        
        #endregion
    }
}
