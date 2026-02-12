using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.EquipPresets.Data
{
    /// <summary>
    /// Represents a saved equipment loadout with a name and item references for each slot.
    /// Now supports both Battle and Civilian equipment.
    /// </summary>
    public class HoNEquipmentPreset
    {
        [SaveableProperty(1)]
        public string Name { get; set; }

        [SaveableProperty(2)]
        public string Id { get; set; }

        [SaveableProperty(3)]
        public List<HoNPresetItemReference> Items { get; set; }

        [SaveableProperty(4)]
        public List<HoNPresetItemReference> CivilianItems { get; set; }

        [SaveableProperty(5)]
        public bool IncludesMount { get; set; }

        [SaveableProperty(6)]
        public bool IncludesCivilianEquipment { get; set; }

        // Equipment slots we care about (body equipment, not horse)
        public static readonly EquipmentIndex[] EquipmentSlots = new[]
        {
            EquipmentIndex.Head,
            EquipmentIndex.Cape,
            EquipmentIndex.Body,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg,
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3
        };

        // Mount slots
        public static readonly EquipmentIndex[] MountSlots = new[]
        {
            EquipmentIndex.Horse,
            EquipmentIndex.HorseHarness
        };

        public HoNEquipmentPreset()
        {
            Items = new List<HoNPresetItemReference>();
            CivilianItems = new List<HoNPresetItemReference>();
        }

        public HoNEquipmentPreset(string name) : this()
        {
            Name = name;
            Id = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Creates a preset from the hero's current equipment.
        /// </summary>
        public static HoNEquipmentPreset FromEquipment(string name, Equipment equipment)
        {
            var preset = new HoNEquipmentPreset(name);

            foreach (var slot in EquipmentSlots)
            {
                var element = equipment[slot];
                preset.Items.Add(new HoNPresetItemReference(element, slot));
            }

            // Check for mount
            var horseElement = equipment[EquipmentIndex.Horse];
            preset.IncludesMount = horseElement.Item != null;

            return preset;
        }

        /// <summary>
        /// Creates a preset from a hero, including both Battle and Civilian equipment.
        /// </summary>
        public static HoNEquipmentPreset FromHero(Hero hero, string name, bool includeCivilian = true, bool includeMount = true)
        {
            var preset = new HoNEquipmentPreset(name);

            // Save Battle equipment
            var battleEquip = hero.BattleEquipment;
            foreach (var slot in EquipmentSlots)
            {
                var element = battleEquip[slot];
                preset.Items.Add(new HoNPresetItemReference(element, slot));
            }

            // Check for mount
            if (includeMount)
            {
                foreach (var slot in MountSlots)
                {
                    var element = battleEquip[slot];
                    preset.Items.Add(new HoNPresetItemReference(element, slot));
                }
                preset.IncludesMount = battleEquip[EquipmentIndex.Horse].Item != null;
            }

            // Save Civilian equipment if requested
            if (includeCivilian)
            {
                var civilianEquip = hero.CivilianEquipment;
                foreach (var slot in EquipmentSlots)
                {
                    var element = civilianEquip[slot];
                    preset.CivilianItems.Add(new HoNPresetItemReference(element, slot));
                }
                preset.IncludesCivilianEquipment = true;
            }

            return preset;
        }

        /// <summary>
        /// Applies this preset to a hero's battle equipment.
        /// </summary>
        public void ApplyTo(Hero hero, bool applyCivilian = false)
        {
            if (hero == null) return;

            var targetEquip = applyCivilian ? hero.CivilianEquipment : hero.BattleEquipment;
            var sourceItems = applyCivilian ? CivilianItems : Items;

            foreach (var slot in EquipmentSlots)
            {
                var itemRef = sourceItems.FirstOrDefault(i => i.Slot == slot);
                if (itemRef != null)
                {
                    var element = itemRef.ToEquipmentElement();
                    targetEquip[slot] = element;
                }
            }

            // Apply mount if this preset includes it and we're not doing civilian
            if (!applyCivilian && IncludesMount)
            {
                foreach (var slot in MountSlots)
                {
                    var itemRef = Items.FirstOrDefault(i => i.Slot == slot);
                    if (itemRef != null)
                    {
                        var element = itemRef.ToEquipmentElement();
                        targetEquip[slot] = element;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the item reference for a specific slot.
        /// </summary>
        public HoNPresetItemReference GetItemForSlot(EquipmentIndex slot)
        {
            return Items.FirstOrDefault(i => i.Slot == slot);
        }

        /// <summary>
        /// Gets all non-empty item StringIds in this preset (for lock management).
        /// </summary>
        public IEnumerable<string> GetItemStringIds()
        {
            var battleIds = Items
                .Where(i => !i.IsEmpty)
                .Select(i => i.ItemStringId);

            var civilianIds = CivilianItems
                .Where(i => !i.IsEmpty)
                .Select(i => i.ItemStringId);

            return battleIds.Union(civilianIds).Distinct();
        }

        /// <summary>
        /// Checks if this preset contains a reference to the given item.
        /// </summary>
        public bool ContainsItem(EquipmentElement element)
        {
            return Items.Any(i => i.Matches(element)) || 
                   CivilianItems.Any(i => i.Matches(element));
        }

        /// <summary>
        /// Checks if this preset contains a reference to an item by StringId.
        /// </summary>
        public bool ContainsItemById(string itemStringId)
        {
            return Items.Any(i => i.ItemStringId == itemStringId) ||
                   CivilianItems.Any(i => i.ItemStringId == itemStringId);
        }

        public override string ToString()
        {
            int itemCount = Items.Count(i => !i.IsEmpty);
            int civilianCount = CivilianItems.Count(i => !i.IsEmpty);
            string mountStr = IncludesMount ? " +Mount" : "";
            string civilianStr = civilianCount > 0 ? $" +{civilianCount}civ" : "";
            return $"{Name} ({itemCount} items{mountStr}{civilianStr})";
        }
    }
}
