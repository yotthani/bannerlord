using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace HeirOfNumenor.Features.CompanionRoles
{
    /// <summary>
    /// Detects the combat role of a hero based on their equipped items.
    /// Only considers the first two weapon slots (primary weapons).
    /// </summary>
    public static class CompanionRoleDetector
    {
        public enum CombatRole
        {
            Unknown,
            Archer,         // Has bow/crossbow
            Crossbow,       // Has crossbow specifically
            ShieldInfantry, // Has shield + one-handed
            TwoHanded,      // Has two-handed weapon
            Polearm,        // Has polearm
            Cavalry,        // Mounted (has horse)
            HorseArcher,    // Mounted + bow
            Skirmisher      // Has throwing weapons
        }

        // Only check first two weapon slots (0 and 1)
        private const int MAX_SLOTS_TO_CHECK = 2;

        /// <summary>
        /// Determines the primary combat role of a hero based on equipment.
        /// Only considers weapon slots 0 and 1.
        /// </summary>
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

            // Priority-based role detection
            if (hasHorse && (hasBow || hasCrossbow))
                return CombatRole.HorseArcher;
            
            if (hasHorse)
                return CombatRole.Cavalry;
            
            if (hasBow)
                return CombatRole.Archer;
            
            if (hasCrossbow)
                return CombatRole.Crossbow;
            
            if (hasThrowing)
                return CombatRole.Skirmisher;
            
            if (hasPolearm)
                return CombatRole.Polearm;
            
            if (hasTwoHanded)
                return CombatRole.TwoHanded;
            
            if (hasShield)
                return CombatRole.ShieldInfantry;

            return CombatRole.Unknown;
        }

        /// <summary>
        /// Gets a short text indicator for the role.
        /// </summary>
        public static string GetRoleText(CombatRole role)
        {
            return role switch
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
        }

        /// <summary>
        /// Alias for GetRoleText - gets a short text indicator for the role.
        /// </summary>
        public static string GetRoleShortText(CombatRole role) => GetRoleText(role);

        /// <summary>
        /// Gets a color for the role indicator.
        /// </summary>
        public static uint GetRoleColor(CombatRole role)
        {
            return role switch
            {
                CombatRole.Archer => 0xFF90EE90,      // Light green
                CombatRole.Crossbow => 0xFF98FB98,    // Pale green
                CombatRole.ShieldInfantry => 0xFF87CEEB, // Sky blue
                CombatRole.TwoHanded => 0xFFFF6347,   // Tomato red
                CombatRole.Polearm => 0xFFDDA0DD,     // Plum
                CombatRole.Cavalry => 0xFFFFD700,     // Gold
                CombatRole.HorseArcher => 0xFFFFA500, // Orange
                CombatRole.Skirmisher => 0xFFADD8E6,  // Light blue
                _ => 0xFFFFFFFF                        // White
            };
        }

        /// <summary>
        /// Checks if hero is mounted (has horse).
        /// </summary>
        public static bool IsMounted(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null)
                return false;
            
            return !hero.BattleEquipment[EquipmentIndex.Horse].IsEmpty;
        }

        private static bool HasWeaponOfClass(Equipment equipment, WeaponClass weaponClass)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.PrimaryWeapon != null)
                {
                    if (item.Item.PrimaryWeapon.WeaponClass == weaponClass)
                        return true;
                }
            }
            return false;
        }

        private static bool HasShield(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item != null)
                {
                    if (item.Item.ItemType == ItemObject.ItemTypeEnum.Shield)
                        return true;
                }
            }
            return false;
        }

        private static bool HasTwoHandedWeapon(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.PrimaryWeapon != null)
                {
                    var wc = item.Item.PrimaryWeapon.WeaponClass;
                    if (wc == WeaponClass.TwoHandedAxe || 
                        wc == WeaponClass.TwoHandedSword ||
                        wc == WeaponClass.TwoHandedMace)
                        return true;
                }
            }
            return false;
        }

        private static bool HasPolearm(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.PrimaryWeapon != null)
                {
                    var wc = item.Item.PrimaryWeapon.WeaponClass;
                    if (wc == WeaponClass.TwoHandedPolearm || 
                        wc == WeaponClass.OneHandedPolearm ||
                        wc == WeaponClass.LowGripPolearm)
                        return true;
                }
            }
            return false;
        }

        private static bool HasThrowingWeapon(Equipment equipment)
        {
            for (int i = 0; i < MAX_SLOTS_TO_CHECK; i++)
            {
                var item = equipment[(EquipmentIndex)i];
                if (!item.IsEmpty && item.Item?.PrimaryWeapon != null)
                {
                    var wc = item.Item.PrimaryWeapon.WeaponClass;
                    if (wc == WeaponClass.ThrowingAxe || 
                        wc == WeaponClass.ThrowingKnife ||
                        wc == WeaponClass.Javelin ||
                        wc == WeaponClass.Stone)
                        return true;
                }
            }
            return false;
        }
    }
}
