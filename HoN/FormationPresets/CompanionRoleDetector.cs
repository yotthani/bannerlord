using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace FormationPresets
{
    /// <summary>
    /// Inlined minimal copy of the CompanionRoleDetector from the standalone
    /// CompanionRoles module. Duplicated here so FormationPresets has zero
    /// inter-module dependency. If you load both modules, both definitions
    /// coexist in different namespaces — there's no clash.
    /// </summary>
    internal static class CompanionRoleDetector
    {
        public enum CombatRole
        {
            Unknown, Archer, Crossbow, ShieldInfantry, TwoHanded,
            Polearm, Cavalry, HorseArcher, Skirmisher
        }

        private const int MAX_SLOTS_TO_CHECK = 2;

        public static CombatRole GetPrimaryRole(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null) return CombatRole.Unknown;
            var eq = hero.BattleEquipment;

            bool hasHorse = !eq[EquipmentIndex.Horse].IsEmpty;
            bool hasBow = HasClass(eq, WeaponClass.Bow);
            bool hasCrossbow = HasClass(eq, WeaponClass.Crossbow);
            bool hasShield = HasShield(eq);
            bool hasTwoH = HasTwoHanded(eq);
            bool hasPole = HasPolearm(eq);
            bool hasThrow = HasThrowing(eq);

            if (hasHorse && (hasBow || hasCrossbow)) return CombatRole.HorseArcher;
            if (hasHorse) return CombatRole.Cavalry;
            if (hasBow) return CombatRole.Archer;
            if (hasCrossbow) return CombatRole.Crossbow;
            if (hasThrow) return CombatRole.Skirmisher;
            if (hasPole) return CombatRole.Polearm;
            if (hasTwoH) return CombatRole.TwoHanded;
            if (hasShield) return CombatRole.ShieldInfantry;
            return CombatRole.Unknown;
        }

        public static bool IsMounted(Hero hero) =>
            hero?.BattleEquipment != null && !hero.BattleEquipment[EquipmentIndex.Horse].IsEmpty;

        private static bool HasClass(Equipment eq, WeaponClass wc)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = eq[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.PrimaryWeapon?.WeaponClass == wc) return true;
            }
            return false;
        }

        private static bool HasShield(Equipment eq)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = eq[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.ItemType == ItemObject.ItemTypeEnum.Shield) return true;
            }
            return false;
        }

        private static bool HasTwoHanded(Equipment eq)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = eq[(EquipmentIndex)i];
                if (item.IsEmpty || item.Item?.PrimaryWeapon == null) continue;
                var wc = item.Item.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.TwoHandedAxe || wc == WeaponClass.TwoHandedSword || wc == WeaponClass.TwoHandedMace) return true;
            }
            return false;
        }

        private static bool HasPolearm(Equipment eq)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = eq[(EquipmentIndex)i];
                if (item.IsEmpty || item.Item?.PrimaryWeapon == null) continue;
                var wc = item.Item.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.TwoHandedPolearm || wc == WeaponClass.OneHandedPolearm || wc == WeaponClass.LowGripPolearm) return true;
            }
            return false;
        }

        private static bool HasThrowing(Equipment eq)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = eq[(EquipmentIndex)i];
                if (item.IsEmpty || item.Item?.PrimaryWeapon == null) continue;
                var wc = item.Item.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.ThrowingAxe || wc == WeaponClass.ThrowingKnife ||
                    wc == WeaponClass.Javelin || wc == WeaponClass.Stone) return true;
            }
            return false;
        }
    }
}
