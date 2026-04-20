using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace CompanionRoles
{
    /// <summary>
    /// Detects the combat role of a hero based on their equipped items
    /// (first two weapon slots only).
    /// </summary>
    public static class CompanionRoleDetector
    {
        public enum CombatRole
        {
            Unknown,
            Archer,
            Crossbow,
            ShieldInfantry,
            TwoHanded,
            Polearm,
            Cavalry,
            HorseArcher,
            Skirmisher
        }

        private const int MAX_SLOTS_TO_CHECK = 2;

        public static CombatRole GetPrimaryRole(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null)
                return CombatRole.Unknown;

            var equipment = hero.BattleEquipment;

            bool hasHorse = !equipment[EquipmentIndex.Horse].IsEmpty;
            bool hasBow = HasWeaponOfClass(equipment, WeaponClass.Bow);
            bool hasCrossbow = HasWeaponOfClass(equipment, WeaponClass.Crossbow);
            bool hasShield = HasShield(equipment);
            bool hasTwoHanded = HasTwoHandedWeapon(equipment);
            bool hasPolearm = HasPolearm(equipment);
            bool hasThrowing = HasThrowingWeapon(equipment);

            if (hasHorse && (hasBow || hasCrossbow)) return CombatRole.HorseArcher;
            if (hasHorse) return CombatRole.Cavalry;
            if (hasBow) return CombatRole.Archer;
            if (hasCrossbow) return CombatRole.Crossbow;
            if (hasThrowing) return CombatRole.Skirmisher;
            if (hasPolearm) return CombatRole.Polearm;
            if (hasTwoHanded) return CombatRole.TwoHanded;
            if (hasShield) return CombatRole.ShieldInfantry;

            return CombatRole.Unknown;
        }

        public static string GetRoleText(CombatRole role) => role switch
        {
            CombatRole.Archer => "BOW",
            CombatRole.Crossbow => "XBW",
            CombatRole.ShieldInfantry => "INF",
            CombatRole.TwoHanded => "2H",
            CombatRole.Polearm => "POL",
            CombatRole.Cavalry => "CAV",
            CombatRole.HorseArcher => "H.AR",
            CombatRole.Skirmisher => "SKR",
            _ => ""
        };

        public static string GetRoleShortText(CombatRole role) => GetRoleText(role);

        public static uint GetRoleColor(CombatRole role) => role switch
        {
            CombatRole.Archer => 0xFF90EE90,
            CombatRole.Crossbow => 0xFF98FB98,
            CombatRole.ShieldInfantry => 0xFF87CEEB,
            CombatRole.TwoHanded => 0xFFFF6347,
            CombatRole.Polearm => 0xFFDDA0DD,
            CombatRole.Cavalry => 0xFFFFD700,
            CombatRole.HorseArcher => 0xFFFFA500,
            CombatRole.Skirmisher => 0xFFADD8E6,
            _ => 0xFFFFFFFF
        };

        public static bool IsMounted(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null) return false;
            return !hero.BattleEquipment[EquipmentIndex.Horse].IsEmpty;
        }

        private static bool HasWeaponOfClass(Equipment equipment, WeaponClass weaponClass)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.PrimaryWeapon?.WeaponClass == weaponClass)
                    return true;
            }
            return false;
        }

        private static bool HasShield(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.ItemType == ItemObject.ItemTypeEnum.Shield)
                    return true;
            }
            return false;
        }

        private static bool HasTwoHandedWeapon(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (item.IsEmpty || item.Item?.PrimaryWeapon == null) continue;
                var wc = item.Item.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.TwoHandedAxe ||
                    wc == WeaponClass.TwoHandedSword ||
                    wc == WeaponClass.TwoHandedMace)
                    return true;
            }
            return false;
        }

        private static bool HasPolearm(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (item.IsEmpty || item.Item?.PrimaryWeapon == null) continue;
                var wc = item.Item.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.TwoHandedPolearm ||
                    wc == WeaponClass.OneHandedPolearm ||
                    wc == WeaponClass.LowGripPolearm)
                    return true;
            }
            return false;
        }

        private static bool HasThrowingWeapon(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (item.IsEmpty || item.Item?.PrimaryWeapon == null) continue;
                var wc = item.Item.PrimaryWeapon.WeaponClass;
                if (wc == WeaponClass.ThrowingAxe ||
                    wc == WeaponClass.ThrowingKnife ||
                    wc == WeaponClass.Javelin ||
                    wc == WeaponClass.Stone)
                    return true;
            }
            return false;
        }
    }
}
