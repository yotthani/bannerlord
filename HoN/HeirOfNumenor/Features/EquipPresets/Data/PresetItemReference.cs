using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.EquipPresets.Data
{
    /// <summary>
    /// Stores a reference to an item that can be matched against inventory items.
    /// Uses StringId and modifier data to find the "same" item later.
    /// </summary>
    public class HoNPresetItemReference
    {
        [SaveableProperty(1)]
        public string ItemStringId { get; set; }

        [SaveableProperty(2)]
        public EquipmentIndex Slot { get; set; }

        [SaveableProperty(3)]
        public string ItemModifierStringId { get; set; }

        public HoNPresetItemReference() { }

        public HoNPresetItemReference(EquipmentElement element, EquipmentIndex slot)
        {
            Slot = slot;
            
            if (!element.IsEmpty && element.Item != null)
            {
                ItemStringId = element.Item.StringId;
                ItemModifierStringId = element.ItemModifier?.StringId;
            }
        }

        public bool IsEmpty => string.IsNullOrEmpty(ItemStringId);

        /// <summary>
        /// Converts this reference back to an EquipmentElement.
        /// Returns Invalid if item cannot be found.
        /// </summary>
        public EquipmentElement ToEquipmentElement()
        {
            if (IsEmpty)
                return EquipmentElement.Invalid;

            try
            {
                var item = MBObjectManager.Instance?.GetObject<ItemObject>(ItemStringId);
                if (item == null)
                    return EquipmentElement.Invalid;

                ItemModifier modifier = null;
                if (!string.IsNullOrEmpty(ItemModifierStringId))
                {
                    modifier = MBObjectManager.Instance?.GetObject<ItemModifier>(ItemModifierStringId);
                }

                return new EquipmentElement(item, modifier);
            }
            catch
            {
                return EquipmentElement.Invalid;
            }
        }

        /// <summary>
        /// Checks if an EquipmentElement matches this reference.
        /// </summary>
        public bool Matches(EquipmentElement element)
        {
            if (IsEmpty)
                return element.IsEmpty;
            
            if (element.IsEmpty || element.Item == null)
                return false;

            if (element.Item.StringId != ItemStringId)
                return false;

            // Match modifier (both null = match, or same StringId)
            string elementModifier = element.ItemModifier?.StringId;
            return elementModifier == ItemModifierStringId;
        }

        public override string ToString()
        {
            string name = ItemStringId ?? "Empty";
            string mod = ItemModifierStringId != null ? $" ({ItemModifierStringId})" : "";
            return $"{name} @ {Slot}{mod}";
        }
    }
}
