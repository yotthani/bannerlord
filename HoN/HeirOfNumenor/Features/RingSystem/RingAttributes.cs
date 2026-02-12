using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Defines the Ring Power and Corruption attributes and their associated skills.
    /// </summary>
    public static class RingAttributes
    {
        // Attribute IDs
        public const string RingPowerAttributeId = "ring_power";
        public const string CorruptionAttributeId = "ring_corruption";

        // Elven Skills (Ring Power)
        public const string ElvenGraceId = "elven_grace";           // Archery/Ranged
        public const string ElvenStarlightId = "elven_starlight";   // Charm/Influence
        public const string ElvenSwiftnessId = "elven_swiftness";   // Athletics/Speed

        // Dwarf Skills (Ring Power)
        public const string DwarfFortitudeId = "dwarf_fortitude";   // Toughness/Endurance
        public const string DwarfStonecraftId = "dwarf_stonecraft"; // Smithing/Crafting
        public const string DwarfGoldlustId = "dwarf_goldlust";     // Trade/Barter

        // Mortal Skills (Ring Power)
        public const string MortalDominionId = "mortal_dominion";   // Leadership
        public const string MortalCommandId = "mortal_command";     // Tactics
        public const string MortalPresenceId = "mortal_presence";   // Charm/Intimidation

        // Corruption Skills (shared across ring types but with different intensities)
        public const string CorruptionFadingId = "corruption_fading";       // Health drain
        public const string CorruptionGreedId = "corruption_greed";         // Economic/Relations
        public const string CorruptionShadowId = "corruption_shadow";       // Detection/Visibility

        // Skill bonuses at each tier
        public static readonly int[] BonusTiers = { 40, 25, 15 };  // Tier 1, 2, 3 (decreasing)
        public static readonly int[] CurseTiers = { 10, 20, 35 };  // Tier 1, 2, 3 (increasing)

        // Days to unlock each tier
        public static readonly float[] UnlockDays = { 0f, 7f, 30f };  // Immediate, 1 week, 1 month

        /// <summary>
        /// Ring race types for categorizing skills
        /// </summary>
        public enum RingRace
        {
            None,
            Elven,
            Dwarf,
            Mortal,
            OneRing  // Special case - uses Mortal skills but with extreme values
        }

        /// <summary>
        /// Gets the race of a ring by its ID
        /// </summary>
        public static RingRace GetRingRace(string ringId)
        {
            if (string.IsNullOrEmpty(ringId)) return RingRace.None;

            // The One Ring
            if (ringId == "hon_ring_one_ring") return RingRace.OneRing;
            
            // Elven Rings
            if (ringId == "hon_ring_narya" || ringId == "hon_ring_nenya" || ringId == "hon_ring_vilya")
                return RingRace.Elven;
            
            // Dwarf Rings
            if (ringId.StartsWith("hon_ring_dwarf_")) return RingRace.Dwarf;
            
            // Mortal (Nazgul) Rings
            if (ringId.StartsWith("hon_ring_nazgul_")) return RingRace.Mortal;

            return RingRace.None;
        }

        /// <summary>
        /// Gets the Ring Power skill IDs for a given race
        /// </summary>
        public static string[] GetPowerSkillsForRace(RingRace race)
        {
            return race switch
            {
                RingRace.Elven => new[] { ElvenGraceId, ElvenStarlightId, ElvenSwiftnessId },
                RingRace.Dwarf => new[] { DwarfFortitudeId, DwarfStonecraftId, DwarfGoldlustId },
                RingRace.Mortal => new[] { MortalDominionId, MortalCommandId, MortalPresenceId },
                RingRace.OneRing => new[] { MortalDominionId, MortalCommandId, MortalPresenceId },
                _ => System.Array.Empty<string>()
            };
        }

        /// <summary>
        /// Gets the Corruption skill IDs (same for all races, but intensity varies)
        /// </summary>
        public static string[] GetCorruptionSkills()
        {
            return new[] { CorruptionFadingId, CorruptionGreedId, CorruptionShadowId };
        }

        /// <summary>
        /// Progression speed multipliers by race
        /// </summary>
        public static class ProgressionSpeed
        {
            // Bonus buildup multiplier (higher = faster)
            public static float GetBonusBuildup(RingRace race) => race switch
            {
                RingRace.Elven => 1.0f,
                RingRace.Dwarf => 1.0f,
                RingRace.Mortal => 1.5f,    // Fast buildup
                RingRace.OneRing => 100f,   // Instant
                _ => 1.0f
            };

            // Curse buildup multiplier (higher = faster corruption)
            public static float GetCurseBuildup(RingRace race) => race switch
            {
                RingRace.Elven => 0.5f,     // Slow corruption
                RingRace.Dwarf => 1.0f,
                RingRace.Mortal => 1.5f,    // Fast corruption
                RingRace.OneRing => 2.0f,   // Accelerating (handled specially)
                _ => 1.0f
            };

            // Decay speed multiplier (higher = faster decay when unequipped)
            public static float GetBonusDecay(RingRace race) => race switch
            {
                RingRace.Elven => 2.0f,     // Fast decay
                RingRace.Dwarf => 1.0f,
                RingRace.Mortal => 0.5f,    // Slow decay
                RingRace.OneRing => 0.25f,  // Very slow decay
                _ => 1.0f
            };

            public static float GetCurseDecay(RingRace race) => race switch
            {
                RingRace.Elven => 1.5f,     // Moderate decay
                RingRace.Dwarf => 1.0f,
                RingRace.Mortal => 0.5f,    // Slow decay
                RingRace.OneRing => 0.1f,   // Extremely slow decay
                _ => 1.0f
            };
        }

        /// <summary>
        /// Skill display information
        /// </summary>
        public static class SkillInfo
        {
            public static TextObject GetSkillName(string skillId) => skillId switch
            {
                // Elven
                ElvenGraceId => new TextObject("{=elven_grace}Elven Grace"),
                ElvenStarlightId => new TextObject("{=elven_starlight}Starlight"),
                ElvenSwiftnessId => new TextObject("{=elven_swiftness}Swiftness"),
                // Dwarf
                DwarfFortitudeId => new TextObject("{=dwarf_fortitude}Dwarven Fortitude"),
                DwarfStonecraftId => new TextObject("{=dwarf_stonecraft}Stonecraft"),
                DwarfGoldlustId => new TextObject("{=dwarf_goldlust}Gold-sense"),
                // Mortal
                MortalDominionId => new TextObject("{=mortal_dominion}Dominion"),
                MortalCommandId => new TextObject("{=mortal_command}Command"),
                MortalPresenceId => new TextObject("{=mortal_presence}Dark Presence"),
                // Corruption
                CorruptionFadingId => new TextObject("{=corruption_fading}Fading"),
                CorruptionGreedId => new TextObject("{=corruption_greed}Greed"),
                CorruptionShadowId => new TextObject("{=corruption_shadow}Shadow"),
                _ => new TextObject(skillId)
            };

            public static TextObject GetSkillDescription(string skillId) => skillId switch
            {
                // Elven
                ElvenGraceId => new TextObject("{=elven_grace_desc}Enhances archery and ranged combat abilities."),
                ElvenStarlightId => new TextObject("{=elven_starlight_desc}Increases charm and diplomatic influence."),
                ElvenSwiftnessId => new TextObject("{=elven_swiftness_desc}Improves movement speed and athletics."),
                // Dwarf
                DwarfFortitudeId => new TextObject("{=dwarf_fortitude_desc}Grants exceptional toughness and endurance."),
                DwarfStonecraftId => new TextObject("{=dwarf_stonecraft_desc}Enhances smithing and crafting abilities."),
                DwarfGoldlustId => new TextObject("{=dwarf_goldlust_desc}Improves trade deals and gold acquisition."),
                // Mortal
                MortalDominionId => new TextObject("{=mortal_dominion_desc}Greatly enhances leadership over armies."),
                MortalCommandId => new TextObject("{=mortal_command_desc}Improves tactical abilities in battle."),
                MortalPresenceId => new TextObject("{=mortal_presence_desc}Instills fear and respect in others."),
                // Corruption
                CorruptionFadingId => new TextObject("{=corruption_fading_desc}Your life force slowly drains away."),
                CorruptionGreedId => new TextObject("{=corruption_greed_desc}An insatiable hunger for wealth corrupts your relationships."),
                CorruptionShadowId => new TextObject("{=corruption_shadow_desc}Dark forces become aware of your presence."),
                _ => new TextObject("")
            };
        }
    }
}
