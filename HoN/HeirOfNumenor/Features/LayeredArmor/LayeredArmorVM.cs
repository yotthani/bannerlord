using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.LayeredArmor
{
    public class LayeredArmorVM : ViewModel
    {
        private Hero _hero;
        private MBBindingList<ArmorSlotVM> _innerSlots;
        private MBBindingList<ArmorSlotVM> _middleSlots;
        private MBBindingList<ArmorSlotVM> _outerSlots;
        private bool _isVisible;
        
        public LayeredArmorVM(Hero hero)
        {
            _hero = hero;
            _innerSlots = new MBBindingList<ArmorSlotVM>();
            _middleSlots = new MBBindingList<ArmorSlotVM>();
            _outerSlots = new MBBindingList<ArmorSlotVM>();
            RefreshLayers();
        }
        
        public void RefreshLayers()
        {
            _innerSlots.Clear();
            _middleSlots.Clear();
            _outerSlots.Clear();
            
            var inner = LayeredArmorManager.GetArmorLayer(_hero, ArmorLayer.Inner);
            var middle = LayeredArmorManager.GetArmorLayer(_hero, ArmorLayer.Middle);
            var outer = LayeredArmorManager.GetArmorLayer(_hero, ArmorLayer.Outer);
            
            AddSlots(_innerSlots, inner, ArmorLayer.Inner);
            AddSlots(_middleSlots, middle, ArmorLayer.Middle);
            AddSlots(_outerSlots, outer, ArmorLayer.Outer);
        }
        
        private void AddSlots(MBBindingList<ArmorSlotVM> list, ArmorLayerData data, ArmorLayer layer)
        {
            list.Add(new ArmorSlotVM("Head", data?.HeadArmor ?? EquipmentElement.Invalid, layer, EquipmentIndex.Head));
            list.Add(new ArmorSlotVM("Body", data?.BodyArmor ?? EquipmentElement.Invalid, layer, EquipmentIndex.Body));
            list.Add(new ArmorSlotVM("Legs", data?.LegArmor ?? EquipmentElement.Invalid, layer, EquipmentIndex.Leg));
            list.Add(new ArmorSlotVM("Hands", data?.HandArmor ?? EquipmentElement.Invalid, layer, EquipmentIndex.Gloves));
        }
        
        [DataSourceProperty]
        public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChangedWithValue(value, nameof(IsVisible)); } }
        
        [DataSourceProperty]
        public MBBindingList<ArmorSlotVM> InnerSlots => _innerSlots;
        
        [DataSourceProperty]
        public MBBindingList<ArmorSlotVM> MiddleSlots => _middleSlots;
        
        [DataSourceProperty]
        public MBBindingList<ArmorSlotVM> OuterSlots => _outerSlots;
        
        [DataSourceProperty]
        public string InnerLabel => new TextObject("{=armor_layer_inner}Inner Layer").ToString();
        
        [DataSourceProperty]
        public string MiddleLabel => new TextObject("{=armor_layer_middle}Middle Layer").ToString();
        
        [DataSourceProperty]
        public string OuterLabel => new TextObject("{=armor_layer_outer}Outer Layer").ToString();
    }
    
    public class ArmorSlotVM : ViewModel
    {
        private string _slotName;
        private EquipmentElement _item;
        private ArmorLayer _layer;
        private EquipmentIndex _slot;
        
        public ArmorSlotVM(string name, EquipmentElement item, ArmorLayer layer, EquipmentIndex slot)
        {
            _slotName = name;
            _item = item;
            _layer = layer;
            _slot = slot;
        }
        
        [DataSourceProperty]
        public string SlotName => _slotName;
        
        [DataSourceProperty]
        public string ItemName => _item.IsEmpty ? "Empty" : _item.Item.Name.ToString();
        
        [DataSourceProperty]
        public bool HasItem => !_item.IsEmpty;
        
        [DataSourceProperty]
        public int ArmorValue => _item.IsEmpty ? 0 : (int)(_item.Item.ArmorComponent?.BodyArmor ?? 0);
    }
}
