using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ArmorCosmetics
{
    /// <summary>
    /// State for armor cosmetic overrides — visual replacement of armor (transmog)
    /// or hiding individual slots, while keeping original stats. State only;
    /// applying the visual on the agent mesh requires AgentVisuals access which
    /// is not currently implemented (see ArmorCosmeticsPatches notes).
    /// </summary>
    public static class ArmorCosmeticsManager
    {
        private static readonly Dictionary<Hero, Dictionary<EquipmentIndex, bool>> _slotVisibility
            = new Dictionary<Hero, Dictionary<EquipmentIndex, bool>>();

        private static readonly Dictionary<Hero, Dictionary<EquipmentIndex, ItemObject>> _cosmeticOverrides
            = new Dictionary<Hero, Dictionary<EquipmentIndex, ItemObject>>();

        public static readonly EquipmentIndex[] CosmeticSlots =
        {
            EquipmentIndex.Head,
            EquipmentIndex.Cape,
            EquipmentIndex.Body,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg
        };

        #region Visibility

        public static void HideSlot(Hero hero, EquipmentIndex slot)
        {
            if (!IsValidCosmeticSlot(slot)) return;
            EnsureHeroEntries(hero);
            _slotVisibility[hero][slot] = false;
        }

        public static void ShowSlot(Hero hero, EquipmentIndex slot)
        {
            if (!IsValidCosmeticSlot(slot)) return;
            EnsureHeroEntries(hero);
            _slotVisibility[hero][slot] = true;
        }

        public static void ToggleSlotVisibility(Hero hero, EquipmentIndex slot)
        {
            if (IsSlotVisible(hero, slot))
                HideSlot(hero, slot);
            else
                ShowSlot(hero, slot);
        }

        public static bool IsSlotVisible(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return true;
            if (!_slotVisibility.TryGetValue(hero, out var slots)) return true;
            if (!slots.TryGetValue(slot, out var visible)) return true;
            return visible;
        }

        #endregion

        #region Cosmetic Overrides (Transmog)

        public static void SetCosmeticOverride(Hero hero, EquipmentIndex slot, ItemObject cosmeticItem)
        {
            if (!IsValidCosmeticSlot(slot)) return;
            if (cosmeticItem == null) return;

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

        public static void ClearCosmeticOverride(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return;
            if (_cosmeticOverrides.TryGetValue(hero, out var overrides))
            {
                overrides.Remove(slot);
            }
        }

        /// <summary>Returns the cosmetic override item for the slot, or null.</summary>
        public static ItemObject GetCosmeticOverride(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return null;
            if (!_cosmeticOverrides.TryGetValue(hero, out var overrides)) return null;
            return overrides.TryGetValue(slot, out var item) ? item : null;
        }

        public static ItemObject GetVisualItem(Hero hero, EquipmentIndex slot)
        {
            if (!IsSlotVisible(hero, slot))
                return null;

            var cosmetic = GetCosmeticOverride(hero, slot);
            if (cosmetic != null) return cosmetic;

            return hero?.BattleEquipment?[slot].Item;
        }

        public static bool HasCosmeticOverride(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return false;
            if (!_cosmeticOverrides.TryGetValue(hero, out var overrides)) return false;
            return overrides.ContainsKey(slot);
        }

        #endregion

        #region Helpers

        public static bool IsValidCosmeticSlot(EquipmentIndex slot)
        {
            return slot == EquipmentIndex.Head ||
                   slot == EquipmentIndex.Cape ||
                   slot == EquipmentIndex.Body ||
                   slot == EquipmentIndex.Gloves ||
                   slot == EquipmentIndex.Leg;
        }

        /// <summary>True if the candidate item can serve as a cosmetic for the given slot.</summary>
        public static bool IsValidCosmeticForSlot(ItemObject item, EquipmentIndex slot)
        {
            if (item == null) return false;
            return slot switch
            {
                EquipmentIndex.Head => item.ItemType == ItemObject.ItemTypeEnum.HeadArmor,
                EquipmentIndex.Cape => item.ItemType == ItemObject.ItemTypeEnum.Cape,
                EquipmentIndex.Body => item.ItemType == ItemObject.ItemTypeEnum.BodyArmor,
                EquipmentIndex.Gloves => item.ItemType == ItemObject.ItemTypeEnum.HandArmor,
                EquipmentIndex.Leg => item.ItemType == ItemObject.ItemTypeEnum.LegArmor,
                _ => false
            };
        }

        private static bool IsSameArmorSlot(ItemObject original, ItemObject cosmetic, EquipmentIndex slot)
        {
            if (original.ItemType != cosmetic.ItemType) return false;
            return IsValidCosmeticForSlot(cosmetic, slot);
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
            if (ArmorCosmeticsSettings.Get().DebugMode)
                InformationManager.DisplayMessage(new InformationMessage($"[Cosmetics] {msg}", Colors.Magenta));
        }

        public static void ClearAllForHero(Hero hero)
        {
            _slotVisibility.Remove(hero);
            _cosmeticOverrides.Remove(hero);
        }

        #endregion
    }
}
