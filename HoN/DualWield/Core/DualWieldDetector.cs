using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DualWield.Core
{
    /// <summary>
    /// Detects whether an agent's equipment qualifies for dual wielding.
    /// Requirement: one-handed weapon in slot 0 AND one-handed weapon in slot 1 (no shield).
    /// </summary>
    public static class DualWieldDetector
    {
        private static readonly WeaponClass[] ValidWeaponClasses = new[]
        {
            WeaponClass.OneHandedSword,
            WeaponClass.Dagger,
            WeaponClass.OneHandedAxe,
            WeaponClass.Mace
        };

        /// <summary>
        /// Checks spawn equipment (Equipment, not MissionEquipment) for dual wield eligibility.
        /// </summary>
        public static bool IsDualWieldEquipped(Equipment spawnEquipment)
        {
            if (spawnEquipment == null) return false;

            var slot0 = spawnEquipment[EquipmentIndex.Weapon0];
            var slot1 = spawnEquipment[EquipmentIndex.Weapon1];

            return IsValidOneHandedWeapon(slot0) && IsValidOneHandedWeapon(slot1);
        }

        /// <summary>
        /// Checks runtime mission equipment for dual wield eligibility.
        /// </summary>
        public static bool IsDualWieldEquipped(MissionEquipment equipment)
        {
            if (equipment == null) return false;

            var slot0 = equipment[EquipmentIndex.Weapon0];
            var slot1 = equipment[EquipmentIndex.Weapon1];

            return IsValidOneHandedMissionWeapon(slot0) && IsValidOneHandedMissionWeapon(slot1);
        }

        private static bool IsValidOneHandedWeapon(EquipmentElement element)
        {
            if (element.IsEmpty || element.Item?.PrimaryWeapon == null)
                return false;

            var weaponData = element.Item.PrimaryWeapon;

            if (weaponData.IsShield)
                return false;

            foreach (var valid in ValidWeaponClasses)
            {
                if (weaponData.WeaponClass == valid)
                    return true;
            }
            return false;
        }

        private static bool IsValidOneHandedMissionWeapon(MissionWeapon weapon)
        {
            if (weapon.IsEmpty || weapon.CurrentUsageItem == null)
                return false;

            if (weapon.CurrentUsageItem.IsShield)
                return false;

            foreach (var valid in ValidWeaponClasses)
            {
                if (weapon.CurrentUsageItem.WeaponClass == valid)
                    return true;
            }
            return false;
        }
    }
}
