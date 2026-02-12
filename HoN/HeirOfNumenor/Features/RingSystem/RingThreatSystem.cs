using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Manages threat level and visibility based on ring ownership.
    /// Rings draw the attention of dark forces!
    /// </summary>
    public class RingThreatSystem
    {
        private const string FEATURE_NAME = "RingThreat";
        
        private static RingThreatSystem _instance;
        public static RingThreatSystem Instance => _instance ??= new RingThreatSystem();

        /// <summary>
        /// Check if Ring Threats are enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try 
                { 
                    var settings = ModSettings.Get();
                    return settings.EnableRingSystem && settings.EnableRingThreats; 
                }
                catch { return true; }
            }
        }

        private float _accumulatedThreat = 0f;

        public RingThreatSystem()
        {
            _instance = this;
        }

        public void AddThreat(float amount)
        {
            SafeExecutor.Execute(FEATURE_NAME, "AddThreat", () =>
            {
                if (!IsEnabled) return;
                _accumulatedThreat += amount * ModSettings.Get().ThreatRateMultiplier;
            });
        }

        public void ProcessDailyThreat()
        {
            SafeExecutor.Execute(FEATURE_NAME, "DailyThreat", () =>
            {
                if (!IsEnabled) return;
                
                // Trigger events based on accumulated threat
                if (_accumulatedThreat >= 100f)
                {
                    TriggerThreatEvent();
                    _accumulatedThreat -= 100f;
                }
            });
        }

        private void TriggerThreatEvent()
        {
            // Placeholder for actual threat events (enemy spawns, etc.)
            InformationManager.DisplayMessage(new InformationMessage(
                "Dark forces stir... You feel eyes watching you from the shadows.",
                Colors.Red));
        }

        public enum ThreatLevel
        {
            None = 0,
            Low = 1,      // 1-3 rings
            Medium = 2,   // 4-7 rings
            High = 3,     // 8+ rings
            Extreme = 4   // The One Ring
        }

        /// <summary>
        /// Gets current threat level based on owned rings.
        /// </summary>
        public static ThreatLevel GetThreatLevel()
        {
            try
            {
                if (!IsEnabled) return ThreatLevel.None;
                
                var behavior = RingSystemCampaignBehavior.Instance;
                if (behavior == null) return ThreatLevel.None;

                var ownedRings = behavior.OwnedRingIds;
                if (ownedRings == null || ownedRings.Count == 0)
                    return ThreatLevel.None;

                // The One Ring = Extreme threat, always
                if (ownedRings.Contains("hon_ring_one_ring"))
                    return ThreatLevel.Extreme;

                // Count rings for threat level
                int count = ownedRings.Count;
                
                if (count >= 8) return ThreatLevel.High;
                if (count >= 4) return ThreatLevel.Medium;
                if (count >= 1) return ThreatLevel.Low;

                return ThreatLevel.None;
            }
            catch
            {
                return ThreatLevel.None;
            }
        }

        /// <summary>
        /// Gets the visibility multiplier for the player party.
        /// Higher = easier for enemies to spot you.
        /// </summary>
        public static float GetVisibilityMultiplier()
        {
            try
            {
                if (!IsEnabled) return 1.0f;
                
                return GetThreatLevel() switch
                {
                    ThreatLevel.None => 1.0f,
                    ThreatLevel.Low => 1.15f,      // 15% more visible
                    ThreatLevel.Medium => 1.35f,   // 35% more visible
                    ThreatLevel.High => 1.6f,      // 60% more visible
                    ThreatLevel.Extreme => 2.0f,   // 100% more visible (One Ring!)
                    _ => 1.0f
                };
            }
            catch
            {
                return 1.0f;
            }
        }

        /// <summary>
        /// Gets the aggression multiplier for enemy parties based on their culture.
        /// Evil cultures are much more aggressive toward ring bearers!
        /// Orcish cultures (Mordor, Isengard, etc.) are especially savage.
        /// </summary>
        public static float GetAggressionMultiplier(BasicCultureObject attackerCulture = null)
        {
            try
            {
                if (!IsEnabled) return 1.0f;
                
                var threatLevel = GetThreatLevel();
                if (threatLevel == ThreatLevel.None) return 1.0f;

                // Base aggression multiplier
                float baseMultiplier = threatLevel switch
                {
                    ThreatLevel.Low => 1.2f,
                    ThreatLevel.Medium => 1.5f,
                    ThreatLevel.High => 2.0f,
                    ThreatLevel.Extreme => 3.0f,
                    _ => 1.0f
                };

                if (attackerCulture == null) return baseMultiplier;

                // Orcish cultures get MASSIVE aggression bonus
                if (IsOrcishCulture(attackerCulture))
                {
                    float orcBonus = threatLevel switch
                    {
                        ThreatLevel.Low => 2.0f,       // 100% more aggressive
                        ThreatLevel.Medium => 2.5f,    // 150% more
                        ThreatLevel.High => 3.0f,      // 200% more
                        ThreatLevel.Extreme => 5.0f,   // One Ring = absolute priority!
                        _ => 1.0f
                    };
                    return baseMultiplier * orcBonus;
                }
                
                // Other evil cultures (Umbar corsairs, etc.) get moderate bonus
                if (IsEvilCulture(attackerCulture))
                {
                    float evilBonus = threatLevel switch
                    {
                        ThreatLevel.Low => 1.3f,
                        ThreatLevel.Medium => 1.5f,
                        ThreatLevel.High => 2.0f,
                        ThreatLevel.Extreme => 3.0f,
                        _ => 1.0f
                    };
                    return baseMultiplier * evilBonus;
                }

                // Neutral cultures might be tempted by the One Ring
                if (IsNeutralCulture(attackerCulture) && threatLevel == ThreatLevel.Extreme)
                {
                    return baseMultiplier * 1.5f;  // Even neutrals want the One Ring
                }

                return baseMultiplier;
            }
            catch
            {
                return 1.0f;
            }
        }

        /// <summary>
        /// Gets chance for a "hunter party" to spawn each day.
        /// These are special parties that actively seek the ring bearer.
        /// </summary>
        public static float GetHunterSpawnChance()
        {
            return GetThreatLevel() switch
            {
                ThreatLevel.None => 0f,
                ThreatLevel.Low => 0.01f,      // 1% per day
                ThreatLevel.Medium => 0.03f,   // 3% per day
                ThreatLevel.High => 0.07f,     // 7% per day
                ThreatLevel.Extreme => 0.15f,  // 15% per day (One Ring!)
                _ => 0f
            };
        }

        /// <summary>
        /// Gets the relation penalty with evil factions for owning rings.
        /// </summary>
        public static int GetEvilFactionRelationPenalty()
        {
            return GetThreatLevel() switch
            {
                ThreatLevel.None => 0,
                ThreatLevel.Low => -10,
                ThreatLevel.Medium => -25,
                ThreatLevel.High => -50,
                ThreatLevel.Extreme => -100,   // Permanent enemy with all evil!
                _ => 0
            };
        }

        /// <summary>
        /// Checks if a party should be hostile to the ring bearer based on threat.
        /// Evil parties become hostile even without normal war declaration.
        /// Orcish cultures are especially aggressive!
        /// </summary>
        public static bool ShouldBeHostileToRingBearer(MobileParty party)
        {
            if (party == null) return false;
            
            var threatLevel = GetThreatLevel();
            if (threatLevel == ThreatLevel.None) return false;

            // Get party's culture
            var culture = party.Party?.Culture;
            if (culture == null) return false;

            // Orcish cultures (Mordor, Isengard, Gundabad, Dol Guldur) are VERY aggressive
            if (IsOrcishCulture(culture))
            {
                return threatLevel switch
                {
                    ThreatLevel.Low => MBRandom.RandomFloat < 0.1f,     // 10% chance
                    ThreatLevel.Medium => MBRandom.RandomFloat < 0.4f,  // 40% chance
                    ThreatLevel.High => MBRandom.RandomFloat < 0.7f,    // 70% chance
                    ThreatLevel.Extreme => true,                        // One Ring = ALWAYS hostile!
                    _ => false
                };
            }
            
            // Other evil cultures (Umbar, etc.) are less aggressive but still dangerous
            if (IsEvilCulture(culture))
            {
                return threatLevel switch
                {
                    ThreatLevel.Low => false,
                    ThreatLevel.Medium => MBRandom.RandomFloat < 0.15f,  // 15% chance
                    ThreatLevel.High => MBRandom.RandomFloat < 0.35f,    // 35% chance
                    ThreatLevel.Extreme => MBRandom.RandomFloat < 0.6f,  // 60% chance - corsairs might help Sauron
                    _ => false
                };
            }

            // Neutral cultures (Harad, Rhûn, Dunland) - only at extreme threat
            if (IsNeutralCulture(culture))
            {
                return threatLevel == ThreatLevel.Extreme && MBRandom.RandomFloat < 0.2f;  // 20% if One Ring
            }

            return false;
        }

        /// <summary>
        /// Checks if player owns The One Ring.
        /// </summary>
        public static bool HasTheOneRing()
        {
            var behavior = RingSystemCampaignBehavior.Instance;
            return behavior?.OwnedRingIds?.Contains("hon_ring_one_ring") ?? false;
        }

        /// <summary>
        /// Checks if player is wearing The One Ring (equipped).
        /// </summary>
        public static bool IsWearingTheOneRing()
        {
            var behavior = RingSystemCampaignBehavior.Instance;
            return behavior?.EquippedRingIds?.Contains("hon_ring_one_ring") ?? false;
        }

        /// <summary>
        /// Gets a description of current threat status for UI.
        /// </summary>
        public static string GetThreatDescription()
        {
            var level = GetThreatLevel();
            
            return level switch
            {
                ThreatLevel.None => "You carry no rings. The shadows do not notice you.",
                ThreatLevel.Low => "A faint presence... Dark forces sense something.",
                ThreatLevel.Medium => "The Enemy's eye turns toward you. Beware.",
                ThreatLevel.High => "You are marked! Servants of darkness hunt for you.",
                ThreatLevel.Extreme => "THE EYE IS UPON YOU! Sauron knows you bear the One Ring!",
                _ => ""
            };
        }

        /// <summary>
        /// Gets color for threat level display.
        /// </summary>
        public static Color GetThreatColor()
        {
            return GetThreatLevel() switch
            {
                ThreatLevel.None => Colors.Gray,
                ThreatLevel.Low => Colors.Yellow,
                ThreatLevel.Medium => new Color(1f, 0.5f, 0f),  // Orange
                ThreatLevel.High => Colors.Red,
                ThreatLevel.Extreme => new Color(0.5f, 0f, 0.5f),  // Purple/Dark
                _ => Colors.White
            };
        }

        /// <summary>
        /// Gets a warning message when acquiring a new ring.
        /// </summary>
        public static string GetAcquisitionWarning(string ringId)
        {
            if (ringId == "hon_ring_one_ring")
            {
                return "You have claimed The One Ring! Its power is immense... but now ALL of Sauron's forces will hunt you relentlessly. There is no hiding from the Eye.";
            }

            var newLevel = GetThreatLevelWithRing(ringId);
            var currentLevel = GetThreatLevel();

            if (newLevel > currentLevel)
            {
                return newLevel switch
                {
                    ThreatLevel.Low => "You feel a cold presence watching... The shadows have noticed you.",
                    ThreatLevel.Medium => "A chill runs down your spine. Dark forces grow aware of your collection.",
                    ThreatLevel.High => "The Enemy now actively seeks you. Powerful servants are dispatched to claim the rings.",
                    _ => ""
                };
            }

            return "";
        }

        private static ThreatLevel GetThreatLevelWithRing(string newRingId)
        {
            var behavior = RingSystemCampaignBehavior.Instance;
            if (behavior == null) return ThreatLevel.None;

            var ownedRings = behavior.OwnedRingIds?.ToList() ?? new List<string>();
            if (!ownedRings.Contains(newRingId))
                ownedRings.Add(newRingId);

            if (ownedRings.Contains("hon_ring_one_ring"))
                return ThreatLevel.Extreme;

            int count = ownedRings.Count;
            if (count >= 8) return ThreatLevel.High;
            if (count >= 4) return ThreatLevel.Medium;
            if (count >= 1) return ThreatLevel.Low;

            return ThreatLevel.None;
        }

        #region Culture Helpers
        
        // These mirror the mod's CultureObjectExtensions and CampaignStartGlobals
        // In actual integration, we'd reference the mod's class directly
        
        /// <summary>
        /// Evil cultures - servants of Sauron and dark forces
        /// </summary>
        private static readonly HashSet<string> EvilCultureIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "mordor",       // Orcs, Uruks, Evil Men
            "isengard",     // Uruk-hai, Berserkers
            "gundabad",     // Pale Uruks, Goblins, Orcs
            "dolguldur",    // Dol Guldur forces
            "umbar",        // Corsairs of Umbar
        };

        /// <summary>
        /// Good cultures - Free Peoples of Middle-earth
        /// </summary>
        private static readonly HashSet<string> GoodCultureIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Elven cultures
            "rivendell",    // Elves of Rivendell
            "mirkwood",     // Wood-elves of Mirkwood
            "lothlorien",   // Galadhrim of Lothlórien
            
            // Dwarven cultures
            "erebor",       // Dwarves of Erebor/Iron Hills
            
            // Human good cultures
            "gondor",       // Men of Gondor
            "vlandia",      // Rohirrim (Rohan)
            "battania",     // (mapped to good humans)
            "sturgia",      // (mapped to good humans)
        };

        /// <summary>
        /// Neutral/ambiguous cultures
        /// </summary>
        private static readonly HashSet<string> NeutralCultureIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "aserai",       // Haradrim - can go either way
            "empire",       // Dunlendings
            "khuzait",      // Easterlings of Rhûn
        };

        /// <summary>
        /// Race types for ring affinity
        /// </summary>
        public enum RaceType
        {
            Human,
            Elf,
            Dwarf,
            Orc,      // Includes Uruks, Goblins
            Unknown
        }

        /// <summary>
        /// Gets the primary race type for a culture
        /// </summary>
        public static RaceType GetCultureRace(string cultureId)
        {
            if (string.IsNullOrEmpty(cultureId)) return RaceType.Unknown;
            
            var id = cultureId.ToLowerInvariant();
            
            // Elven cultures
            if (id == "rivendell" || id == "mirkwood" || id == "lothlorien")
                return RaceType.Elf;
            
            // Dwarven cultures
            if (id == "erebor")
                return RaceType.Dwarf;
            
            // Orcish cultures
            if (id == "mordor" || id == "isengard" || id == "gundabad" || id == "dolguldur")
                return RaceType.Orc;
            
            // Default to human
            return RaceType.Human;
        }

        /// <summary>
        /// Gets ring affinity bonus for a race with a specific ring type.
        /// Elves with Elven rings, Dwarves with Dwarf rings, etc.
        /// </summary>
        public static float GetRingAffinityBonus(RaceType race, string ringId)
        {
            var ringRace = RingAttributes.GetRingRace(ringId);
            
            // Perfect match bonuses
            if (race == RaceType.Elf && ringRace == RingAttributes.RingRace.Elven)
                return 1.5f;  // 50% bonus - Elves made these rings
            
            if (race == RaceType.Dwarf && ringRace == RingAttributes.RingRace.Dwarf)
                return 1.3f;  // 30% bonus - made for Dwarf-lords
            
            if (race == RaceType.Human && ringRace == RingAttributes.RingRace.Mortal)
                return 1.2f;  // 20% bonus - made for mortal men
            
            // The One Ring - no affinity bonus, it corrupts all equally
            if (ringRace == RingAttributes.RingRace.OneRing)
                return 1.0f;
            
            // Orcs/Evil beings get penalty with good rings
            if (race == RaceType.Orc && (ringRace == RingAttributes.RingRace.Elven || ringRace == RingAttributes.RingRace.Dwarf))
                return 0.5f;  // 50% penalty - rings resist evil bearers
            
            return 1.0f;
        }

        /// <summary>
        /// Checks if a culture is evil (orcs, evil men, etc.)
        /// </summary>
        public static bool IsEvilCulture(BasicCultureObject culture)
        {
            if (culture == null) return false;
            return EvilCultureIds.Contains(culture.StringId);
        }

        /// <summary>
        /// Checks if a culture is good (elves, dwarves, free peoples)
        /// </summary>
        public static bool IsGoodCulture(BasicCultureObject culture)
        {
            if (culture == null) return false;
            return GoodCultureIds.Contains(culture.StringId);
        }

        /// <summary>
        /// Checks if a culture is neutral
        /// </summary>
        public static bool IsNeutralCulture(BasicCultureObject culture)
        {
            if (culture == null) return false;
            // Neutral if explicitly listed OR not in good/evil
            return NeutralCultureIds.Contains(culture.StringId) || 
                   (!IsEvilCulture(culture) && !IsGoodCulture(culture));
        }

        /// <summary>
        /// Checks if a culture is orcish (for specific behaviors like hunting ring-bearers)
        /// </summary>
        public static bool IsOrcishCulture(BasicCultureObject culture)
        {
            if (culture == null) return false;
            var id = culture.StringId.ToLowerInvariant();
            return id == "mordor" || id == "isengard" || id == "gundabad" || id == "dolguldur";
        }

        #endregion
    }
}
