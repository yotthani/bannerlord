using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.CustomResourceSystem
{
    /// <summary>
    /// Loads resource definitions and culture requirements from XML configuration.
    /// </summary>
    public static class ResourceConfigLoader
    {
        private static Dictionary<string, ResourceDefinition> _resources;
        private static Dictionary<string, CultureResourceProfile> _cultureProfiles;
        private static bool _isLoaded = false;

        public static IReadOnlyDictionary<string, ResourceDefinition> Resources => _resources;
        public static IReadOnlyDictionary<string, CultureResourceProfile> CultureProfiles => _cultureProfiles;
        public static bool IsLoaded => _isLoaded;

        /// <summary>
        /// Loads all configuration from XML files.
        /// </summary>
        public static void LoadConfiguration()
        {
            _resources = new Dictionary<string, ResourceDefinition>(StringComparer.OrdinalIgnoreCase);
            _cultureProfiles = new Dictionary<string, CultureResourceProfile>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Try to load from ModuleData folder
                string modulePath = Path.Combine(BasePath.Name, "Modules", "InventoryQuickActions", "ModuleData");
                string configPath = Path.Combine(modulePath, "culture_resources.xml");

                if (File.Exists(configPath))
                {
                    LoadFromFile(configPath);
                }
                else
                {
                    // Load default configuration
                    LoadDefaultConfiguration();
                }

                // Process inheritance
                ProcessInheritance();

                _isLoaded = true;

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[CustomResource] Loaded {_resources.Count} resources, {_cultureProfiles.Count} culture profiles.",
                    Colors.Cyan));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[CustomResource] Config load error: {ex.Message}", Colors.Red));
                
                // Load defaults on error
                LoadDefaultConfiguration();
                _isLoaded = true;
            }
        }

        private static void LoadFromFile(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);

            // Load resource definitions
            var resourceNodes = doc.SelectNodes("//Resources/Resource");
            if (resourceNodes != null)
            {
                foreach (XmlNode node in resourceNodes)
                {
                    var resource = ResourceDefinition.FromXml(node);
                    _resources[resource.Id] = resource;
                }
            }

            // Load culture profiles
            var profileNodes = doc.SelectNodes("//CultureProfiles/Culture");
            if (profileNodes != null)
            {
                foreach (XmlNode node in profileNodes)
                {
                    var profile = CultureResourceProfile.FromXml(node);
                    _cultureProfiles[profile.CultureId] = profile;
                }
            }
        }

        private static void ProcessInheritance()
        {
            foreach (var profile in _cultureProfiles.Values)
            {
                if (!string.IsNullOrEmpty(profile.InheritsFrom))
                {
                    if (_cultureProfiles.TryGetValue(profile.InheritsFrom, out var parent))
                    {
                        profile.InheritFrom(parent);
                    }
                }
            }
        }

        /// <summary>
        /// Creates default configuration for LOTR cultures.
        /// </summary>
        private static void LoadDefaultConfiguration()
        {
            // ═══════════════════════════════════════════════════════════
            // RESOURCE DEFINITIONS
            // ═══════════════════════════════════════════════════════════

            // Dwarven resources
            _resources["beer"] = new ResourceDefinition
            {
                Id = "beer",
                DisplayName = "Ale & Beer",
                Description = "Dwarves need their ale!",
                Type = ResourceType.Consumable,
                Mode = SatisfactionMode.Decay,
                DailyRate = 2f,
                InitialValue = 60f,
                SatisfactionGain = 15f,
                SatisfyingItems = new List<string> { "ale", "beer", "mead", "wine" },
                SatisfyingSettlements = new List<string> { "tavern", "town" }
            };

            _resources["mountains"] = new ResourceDefinition
            {
                Id = "mountains",
                DisplayName = "Mountain Homeland",
                Description = "Dwarves feel at home in the mountains.",
                Type = ResourceType.Location,
                Mode = SatisfactionMode.DaysSince,
                DailyRate = 1f,
                InitialValue = 100f,
                SatisfactionGain = 30f,
                SatisfyingTerrains = new List<string> { "mountain", "rocky" }
            };

            // Elven resources
            _resources["forest"] = new ResourceDefinition
            {
                Id = "forest",
                DisplayName = "Forest Connection",
                Description = "Elves draw strength from the forests.",
                Type = ResourceType.Location,
                Mode = SatisfactionMode.Decay,
                DailyRate = 1.5f,
                InitialValue = 70f,
                SatisfactionGain = 25f,
                SatisfyingTerrains = new List<string> { "forest", "woodland" }
            };

            _resources["starlight"] = new ResourceDefinition
            {
                Id = "starlight",
                DisplayName = "Starlight",
                Description = "Elves are renewed under the stars.",
                Type = ResourceType.Temporal,
                Mode = SatisfactionMode.DaysSince,
                DailyRate = 1f,
                InitialValue = 100f,
                SatisfactionGain = 20f
            };

            // Orcish resources
            _resources["combat"] = new ResourceDefinition
            {
                Id = "combat",
                DisplayName = "Battle Lust",
                Description = "Orcs crave violence and conflict.",
                Type = ResourceType.Action,
                Mode = SatisfactionMode.Decay,
                DailyRate = 3f,
                InitialValue = 50f,
                SatisfactionGain = 40f,
                SatisfyingEvents = new List<string> { "battle_won", "battle_fought", "raid" }
            };

            _resources["meat"] = new ResourceDefinition
            {
                Id = "meat",
                DisplayName = "Fresh Meat",
                Description = "Orcs prefer fresh meat... any kind.",
                Type = ResourceType.Consumable,
                Mode = SatisfactionMode.Decay,
                DailyRate = 2.5f,
                InitialValue = 40f,
                SatisfactionGain = 20f,
                SatisfyingItems = new List<string> { "meat", "game", "cattle" },
                SatisfyingEvents = new List<string> { "hunt", "raid" }
            };

            // Human resources (generic)
            _resources["pay"] = new ResourceDefinition
            {
                Id = "pay",
                DisplayName = "Fair Wages",
                Description = "Soldiers expect to be paid.",
                Type = ResourceType.Temporal,
                Mode = SatisfactionMode.DaysSince,
                DailyRate = 1f,
                InitialValue = 100f,
                SatisfactionGain = 100f
            };

            _resources["rest"] = new ResourceDefinition
            {
                Id = "rest",
                DisplayName = "Rest & Recuperation",
                Description = "Troops need time to rest.",
                Type = ResourceType.Location,
                Mode = SatisfactionMode.Decay,
                DailyRate = 1f,
                InitialValue = 80f,
                SatisfactionGain = 15f,
                SatisfyingSettlements = new List<string> { "town", "castle", "village" }
            };

            // ═══════════════════════════════════════════════════════════
            // CULTURE PROFILES
            // ═══════════════════════════════════════════════════════════

            // Dwarves (Erebor)
            _cultureProfiles["erebor"] = new CultureResourceProfile
            {
                CultureId = "erebor",
                DisplayName = "Dwarves of Erebor",
                Requirements = new List<CultureResourceRequirement>
                {
                    new CultureResourceRequirement
                    {
                        ResourceId = "beer",
                        Condition = TriggerCondition.Below,
                        Threshold = 30f,
                        GracePeriodDays = 3f,
                        Effects = new List<RequirementEffect>
                        {
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Frustration,
                                DailyMagnitude = 3f,
                                TriggerMessage = "Dwarves grumble about the lack of proper ale..."
                            }
                        }
                    },
                    new CultureResourceRequirement
                    {
                        ResourceId = "mountains",
                        Condition = TriggerCondition.DaysExceeds,
                        Threshold = 14f,
                        Effects = new List<RequirementEffect>
                        {
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Frustration,
                                DailyMagnitude = 1f,
                                TriggerMessage = "Dwarves long for the mountain halls..."
                            }
                        }
                    }
                }
            };

            // Elves (Lothlórien)
            _cultureProfiles["lothlorien"] = new CultureResourceProfile
            {
                CultureId = "lothlorien",
                DisplayName = "Galadhrim of Lothlórien",
                Requirements = new List<CultureResourceRequirement>
                {
                    new CultureResourceRequirement
                    {
                        ResourceId = "forest",
                        Condition = TriggerCondition.Below,
                        Threshold = 25f,
                        GracePeriodDays = 5f,
                        Effects = new List<RequirementEffect>
                        {
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Frustration,
                                DailyMagnitude = 2f,
                                TriggerMessage = "The Elves grow weary far from the woods..."
                            },
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Bonding,
                                DailyMagnitude = -0.5f
                            }
                        }
                    }
                }
            };

            // Copy for other Elven cultures
            _cultureProfiles["rivendell"] = new CultureResourceProfile
            {
                CultureId = "rivendell",
                DisplayName = "Elves of Rivendell",
                InheritsFrom = "lothlorien"
            };

            _cultureProfiles["mirkwood"] = new CultureResourceProfile
            {
                CultureId = "mirkwood",
                DisplayName = "Wood-elves of Mirkwood",
                InheritsFrom = "lothlorien"
            };

            // Orcs (Mordor)
            _cultureProfiles["mordor"] = new CultureResourceProfile
            {
                CultureId = "mordor",
                DisplayName = "Orcs of Mordor",
                Requirements = new List<CultureResourceRequirement>
                {
                    new CultureResourceRequirement
                    {
                        ResourceId = "combat",
                        Condition = TriggerCondition.Below,
                        Threshold = 20f,
                        GracePeriodDays = 2f,
                        Effects = new List<RequirementEffect>
                        {
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Frustration,
                                DailyMagnitude = 4f,
                                TriggerMessage = "The Orcs grow restless without bloodshed!"
                            },
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Fear,
                                DailyMagnitude = -2f  // Combat reduces fear for orcs
                            }
                        }
                    },
                    new CultureResourceRequirement
                    {
                        ResourceId = "meat",
                        Condition = TriggerCondition.Below,
                        Threshold = 25f,
                        Effects = new List<RequirementEffect>
                        {
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Frustration,
                                DailyMagnitude = 2f,
                                TriggerMessage = "Orcs snarl about being fed 'maggoty bread'..."
                            }
                        }
                    }
                }
            };

            // Isengard (similar to Mordor)
            _cultureProfiles["isengard"] = new CultureResourceProfile
            {
                CultureId = "isengard",
                DisplayName = "Uruk-hai of Isengard",
                InheritsFrom = "mordor"
            };

            // Gundabad
            _cultureProfiles["gundabad"] = new CultureResourceProfile
            {
                CultureId = "gundabad",
                DisplayName = "Orcs of Gundabad",
                InheritsFrom = "mordor"
            };

            // Gondor
            _cultureProfiles["gondor"] = new CultureResourceProfile
            {
                CultureId = "gondor",
                DisplayName = "Men of Gondor",
                Requirements = new List<CultureResourceRequirement>
                {
                    new CultureResourceRequirement
                    {
                        ResourceId = "pay",
                        Condition = TriggerCondition.DaysExceeds,
                        Threshold = 30f,
                        Effects = new List<RequirementEffect>
                        {
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Frustration,
                                DailyMagnitude = 2f,
                                TriggerMessage = "The soldiers mutter about unpaid wages..."
                            }
                        }
                    },
                    new CultureResourceRequirement
                    {
                        ResourceId = "rest",
                        Condition = TriggerCondition.Below,
                        Threshold = 20f,
                        GracePeriodDays = 7f,
                        Effects = new List<RequirementEffect>
                        {
                            new RequirementEffect
                            {
                                TargetStatus = TroopStatus.HoNTroopStatusType.Frustration,
                                DailyMagnitude = 1.5f,
                                TriggerMessage = "The men are exhausted from constant marching..."
                            }
                        }
                    }
                }
            };

            // Rohan (vlandia in the mod)
            _cultureProfiles["vlandia"] = new CultureResourceProfile
            {
                CultureId = "vlandia",
                DisplayName = "Riders of Rohan",
                InheritsFrom = "gondor"
            };
        }

        /// <summary>
        /// Gets a resource definition by ID.
        /// </summary>
        public static ResourceDefinition GetResource(string resourceId)
        {
            if (!_isLoaded) LoadConfiguration();
            return _resources.TryGetValue(resourceId, out var resource) ? resource : null;
        }

        /// <summary>
        /// Gets a culture profile by ID.
        /// </summary>
        public static CultureResourceProfile GetCultureProfile(string cultureId)
        {
            if (!_isLoaded) LoadConfiguration();
            return _cultureProfiles.TryGetValue(cultureId, out var profile) ? profile : null;
        }

        /// <summary>
        /// Gets all requirements for a culture.
        /// </summary>
        public static IEnumerable<CultureResourceRequirement> GetRequirementsForCulture(string cultureId)
        {
            var profile = GetCultureProfile(cultureId);
            return profile?.Requirements ?? new List<CultureResourceRequirement>();
        }
    }
}
