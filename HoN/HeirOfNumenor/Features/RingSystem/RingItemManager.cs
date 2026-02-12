using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Manages the Rings of Power items.
    /// Provides methods to give rings to the player and query ring status.
    /// </summary>
    public static class RingItemManager
    {
        /// <summary>
        /// All ring item IDs in the game.
        /// </summary>
        public static readonly string[] AllRingIds = new string[]
        {
            // The One Ring
            "hon_ring_one_ring",
            
            // Three Elven Rings
            "hon_ring_narya",
            "hon_ring_nenya",
            "hon_ring_vilya",
            
            // Seven Dwarf Rings
            "hon_ring_dwarf_1",
            "hon_ring_dwarf_2",
            "hon_ring_dwarf_3",
            "hon_ring_dwarf_4",
            "hon_ring_dwarf_5",
            "hon_ring_dwarf_6",
            "hon_ring_dwarf_7",
            
            // Nine Rings for Mortal Men
            "hon_ring_nazgul_1",
            "hon_ring_nazgul_2",
            "hon_ring_nazgul_3",
            "hon_ring_nazgul_4",
            "hon_ring_nazgul_5",
            "hon_ring_nazgul_6",
            "hon_ring_nazgul_7",
            "hon_ring_nazgul_8",
            "hon_ring_nazgul_9"
        };

        /// <summary>
        /// Gets ring item by ID.
        /// </summary>
        public static ItemObject GetRing(string ringId)
        {
            try
            {
                return MBObjectManager.Instance?.GetObject<ItemObject>(ringId);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gives a specific ring to the player.
        /// </summary>
        public static bool GiveRingToPlayer(string ringId)
        {
            try
            {
                var ring = GetRing(ringId);
                if (ring == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Ring '{ringId}' not found", Colors.Red));
                    return false;
                }

                var partyInventory = MobileParty.MainParty?.ItemRoster;
                if (partyInventory == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Cannot access inventory", Colors.Red));
                    return false;
                }

                partyInventory.AddToCounts(ring, 1);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Received: {ring.Name}", Colors.Green));
                return true;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Error: {ex.Message}", Colors.Red));
                return false;
            }
        }

        /// <summary>
        /// Gives all rings to the player.
        /// </summary>
        public static void GiveAllRingsToPlayer()
        {
            int count = 0;
            foreach (var ringId in AllRingIds)
            {
                if (GiveRingToPlayer(ringId))
                {
                    count++;
                }
            }
            InformationManager.DisplayMessage(new InformationMessage(
                $"Received {count} Rings of Power", Colors.Cyan));
        }

        /// <summary>
        /// Gets all rings currently in player inventory.
        /// </summary>
        public static List<ItemObject> GetPlayerRings()
        {
            var rings = new List<ItemObject>();
            try
            {
                var partyInventory = MobileParty.MainParty?.ItemRoster;
                if (partyInventory == null) return rings;

                foreach (var ringId in AllRingIds)
                {
                    var ring = GetRing(ringId);
                    if (ring != null)
                    {
                        int count = partyInventory.GetItemNumber(ring);
                        if (count > 0)
                        {
                            rings.Add(ring);
                        }
                    }
                }
            }
            catch { }
            return rings;
        }

        /// <summary>
        /// Checks if player has a specific ring.
        /// </summary>
        public static bool PlayerHasRing(string ringId)
        {
            try
            {
                var ring = GetRing(ringId);
                if (ring == null) return false;

                var partyInventory = MobileParty.MainParty?.ItemRoster;
                if (partyInventory == null) return false;

                return partyInventory.GetItemNumber(ring) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if player has The One Ring.
        /// </summary>
        public static bool PlayerHasTheOneRing()
        {
            return PlayerHasRing("hon_ring_one_ring");
        }

        /// <summary>
        /// Gets ring info by ID. Returns the ItemObject or null.
        /// </summary>
        public static ItemObject GetRingById(string ringId)
        {
            return GetRing(ringId);
        }

        /// <summary>
        /// Gets all ring IDs organized by category.
        /// </summary>
        public static Dictionary<string, string[]> GetRingsByCategory()
        {
            return new Dictionary<string, string[]>
            {
                { "The One Ring", new[] { "hon_ring_one_ring" } },
                { "Elven Rings", new[] { "hon_ring_narya", "hon_ring_nenya", "hon_ring_vilya" } },
                { "Dwarf Rings", new[] { "hon_ring_dwarf_1", "hon_ring_dwarf_2", "hon_ring_dwarf_3", "hon_ring_dwarf_4", 
                                         "hon_ring_dwarf_5", "hon_ring_dwarf_6", "hon_ring_dwarf_7" } },
                { "Rings for Men", new[] { "hon_ring_nazgul_1", "hon_ring_nazgul_2", "hon_ring_nazgul_3", 
                                           "hon_ring_nazgul_4", "hon_ring_nazgul_5", "hon_ring_nazgul_6",
                                           "hon_ring_nazgul_7", "hon_ring_nazgul_8", "hon_ring_nazgul_9" } }
            };
        }

        #region Ring Property Helpers

        /// <summary>
        /// Checks if ring ID is the One Ring.
        /// </summary>
        public static bool IsOneRing(string ringId)
        {
            return ringId == "hon_ring_one_ring";
        }

        /// <summary>
        /// Gets corruption rate for a ring by ID.
        /// </summary>
        public static float GetCorruptionRate(string ringId)
        {
            if (IsOneRing(ringId)) return 5.0f;
            if (ringId?.StartsWith("hon_ring_nazgul") == true) return 2.0f;
            if (ringId?.StartsWith("hon_ring_dwarf") == true) return 0.5f;
            if (ringId == "hon_ring_narya" || ringId == "hon_ring_nenya" || ringId == "hon_ring_vilya") return 0.0f;
            return 0.0f;
        }

        /// <summary>
        /// Gets threat level for a ring by ID.
        /// </summary>
        public static float GetThreatLevel(string ringId)
        {
            if (IsOneRing(ringId)) return 10.0f;
            if (ringId?.StartsWith("hon_ring_nazgul") == true) return 3.0f;
            if (ringId?.StartsWith("hon_ring_dwarf") == true) return 1.0f;
            if (ringId == "hon_ring_narya" || ringId == "hon_ring_nenya" || ringId == "hon_ring_vilya") return 0.5f;
            return 0.0f;
        }

        /// <summary>
        /// Gets skill bonuses for a ring by ID.
        /// </summary>
        public static Dictionary<string, int> GetSkillBonuses(string ringId)
        {
            var bonuses = new Dictionary<string, int>();
            
            if (IsOneRing(ringId))
            {
                bonuses["Charm"] = 20;
                bonuses["Roguery"] = 15;
                bonuses["Stealth"] = 30;
            }
            else if (ringId == "hon_ring_narya")
            {
                bonuses["Leadership"] = 15;
                bonuses["Charm"] = 10;
            }
            else if (ringId == "hon_ring_nenya")
            {
                bonuses["Medicine"] = 15;
                bonuses["Steward"] = 10;
            }
            else if (ringId == "hon_ring_vilya")
            {
                bonuses["Tactics"] = 15;
                bonuses["Scouting"] = 10;
            }
            else if (ringId?.StartsWith("hon_ring_dwarf") == true)
            {
                bonuses["Trade"] = 10;
                bonuses["Smithing"] = 5;
            }
            else if (ringId?.StartsWith("hon_ring_nazgul") == true)
            {
                bonuses["Riding"] = 10;
                bonuses["OneHanded"] = 5;
            }

            return bonuses;
        }

        #endregion
    }
}
