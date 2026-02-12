using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace HeirOfNumenor.Features.LayeredArmor
{
    public enum ArmorLayer
    {
        Inner = 0,   // Gambeson, padding
        Middle = 1,  // Chainmail, scale
        Outer = 2    // Plate, brigandine
    }
    
    public class ArmorLayerData
    {
        public EquipmentElement HeadArmor;
        public EquipmentElement BodyArmor;
        public EquipmentElement LegArmor;
        public EquipmentElement HandArmor;
        public EquipmentElement Cape;
    }
    
    public static class LayeredArmorManager
    {
        private static Dictionary<Hero, Dictionary<ArmorLayer, ArmorLayerData>> _heroArmorLayers 
            = new Dictionary<Hero, Dictionary<ArmorLayer, ArmorLayerData>>();
        
        public static void SetArmorLayer(Hero hero, ArmorLayer layer, ArmorLayerData data)
        {
            if (hero == null) return;
            
            if (!_heroArmorLayers.ContainsKey(hero))
                _heroArmorLayers[hero] = new Dictionary<ArmorLayer, ArmorLayerData>();
            
            _heroArmorLayers[hero][layer] = data;
        }
        
        public static ArmorLayerData GetArmorLayer(Hero hero, ArmorLayer layer)
        {
            if (hero == null) return null;
            
            if (_heroArmorLayers.TryGetValue(hero, out var layers))
            {
                if (layers.TryGetValue(layer, out var data))
                    return data;
            }
            return null;
        }
        
        public static float CalculateCombinedArmor(Hero hero, EquipmentIndex slot)
        {
            if (hero == null) return 0f;
            
            float baseArmor = GetArmorValue(hero.BattleEquipment[slot]);
            
            if (!(Settings.Instance?.EnableLayeredArmor ?? false))
                return baseArmor;
            
            if (!_heroArmorLayers.TryGetValue(hero, out var layers))
                return baseArmor;
            
            float innerArmor = 0f, middleArmor = 0f;
            
            if (layers.TryGetValue(ArmorLayer.Inner, out var inner))
                innerArmor = GetArmorValueFromLayer(inner, slot);
            
            if (layers.TryGetValue(ArmorLayer.Middle, out var middle))
                middleArmor = GetArmorValueFromLayer(middle, slot);
            
            var mode = Settings.Instance?.ArmorLayerCalculationDropdown?.SelectedIndex ?? 1;
            
            return mode switch
            {
                0 => baseArmor + middleArmor + innerArmor, // Additive
                1 => baseArmor + (middleArmor + innerArmor) * (Settings.Instance?.UnderArmorBonusPercent ?? 0.15f), // Highest + Bonus
                2 => baseArmor * 1.0f + middleArmor * 0.5f + innerArmor * 0.3f, // Weighted
                _ => baseArmor
            };
        }
        
        private static float GetArmorValue(EquipmentElement element)
        {
            if (element.IsEmpty || element.Item?.ArmorComponent == null)
                return 0f;
            return element.Item.ArmorComponent.BodyArmor;
        }
        
        private static float GetArmorValueFromLayer(ArmorLayerData layer, EquipmentIndex slot)
        {
            if (layer == null) return 0f;
            
            var element = slot switch
            {
                EquipmentIndex.Head => layer.HeadArmor,
                EquipmentIndex.Body => layer.BodyArmor,
                EquipmentIndex.Leg => layer.LegArmor,
                EquipmentIndex.Gloves => layer.HandArmor,
                EquipmentIndex.Cape => layer.Cape,
                _ => EquipmentElement.Invalid
            };
            
            return GetArmorValue(element);
        }
        
        public static void ClearHeroLayers(Hero hero)
        {
            _heroArmorLayers.Remove(hero);
        }
        
        public static void ClearAll()
        {
            _heroArmorLayers.Clear();
        }
    }
}
