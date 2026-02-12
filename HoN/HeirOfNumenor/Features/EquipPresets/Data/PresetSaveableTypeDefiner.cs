using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.EquipPresets.Data
{
    /// <summary>
    /// Defines saveable types for the Bannerlord save system.
    /// Required for custom classes to be serialized with SyncData.
    /// </summary>
    public class PresetSaveableTypeDefiner : SaveableTypeDefiner
    {
        // Use a unique base ID for this mod
        public const int SaveBaseId = 726900501;

        public PresetSaveableTypeDefiner() : base(SaveBaseId) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HoNPresetItemReference), 101);
            AddClassDefinition(typeof(HoNEquipmentPreset), 102);
        }

        protected override void DefineContainerDefinitions()
        {
            // Must register containers that use our custom types
            ConstructContainerDefinition(typeof(List<HoNPresetItemReference>));
            ConstructContainerDefinition(typeof(List<HoNEquipmentPreset>));
            ConstructContainerDefinition(typeof(Dictionary<string, List<HoNEquipmentPreset>>));
        }
    }
}
