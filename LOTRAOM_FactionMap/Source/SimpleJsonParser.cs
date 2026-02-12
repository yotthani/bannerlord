using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LOTRAOM.FactionMap
{
    /// <summary>
    /// JSON parser for regions.json and factions.json using Newtonsoft.Json (bundled with Bannerlord).
    /// </summary>
    public static class SimpleJsonParser
    {
        /// <summary>
        /// Parse regions.json → Dictionary keyed by region ID (e.g. "kingdom_of_rohan").
        /// </summary>
        public static Dictionary<string, RegionData> ParseRegions(string json)
        {
            var result = new Dictionary<string, RegionData>();
            if (string.IsNullOrEmpty(json)) return result;

            var root = JObject.Parse(json);
            foreach (var prop in root.Properties())
            {
                var obj = prop.Value as JObject;
                if (obj == null) continue;

                var bbox = obj["norm_bbox"]?.ToObject<float[]>();

                var capitalPos = obj["capital_pos"]?.ToObject<float[]>();

                var region = new RegionData
                {
                    FactionId = obj.Value<string>("faction"),
                    BBoxX = bbox != null && bbox.Length >= 1 ? bbox[0] : 0f,
                    BBoxY = bbox != null && bbox.Length >= 2 ? bbox[1] : 0f,
                    BBoxW = bbox != null && bbox.Length >= 3 ? bbox[2] : 0f,
                    BBoxH = bbox != null && bbox.Length >= 4 ? bbox[3] : 0f,
                    CapitalX = capitalPos != null && capitalPos.Length >= 1 ? capitalPos[0] : -1f,
                    CapitalY = capitalPos != null && capitalPos.Length >= 2 ? capitalPos[1] : -1f,
                };

                result[prop.Name] = region;
            }

            return result;
        }

        /// <summary>
        /// Parse factions.json → Dictionary keyed by faction ID (e.g. "kingdom_of_rohan").
        /// </summary>
        public static Dictionary<string, FactionData> ParseFactions(string json)
        {
            var result = new Dictionary<string, FactionData>();
            if (string.IsNullOrEmpty(json)) return result;

            var root = JObject.Parse(json);
            foreach (var prop in root.Properties())
            {
                var obj = prop.Value as JObject;
                if (obj == null) continue;

                // Parse bonuses array: [{text, positive}, ...]
                var bonusesList = new System.Collections.Generic.List<FactionBonus>();
                var bonusesArr = obj["bonuses"] as JArray;
                if (bonusesArr != null)
                {
                    foreach (var b in bonusesArr)
                    {
                        if (b is JObject bObj)
                            bonusesList.Add(new FactionBonus
                            {
                                Text = bObj.Value<string>("text") ?? "",
                                Positive = bObj.Value<bool>("positive")
                            });
                    }
                }

                // Parse special_unit object: {name, description}
                FactionSpecialUnit specialUnit = null;
                var suObj = obj["special_unit"] as JObject;
                if (suObj != null)
                {
                    specialUnit = new FactionSpecialUnit
                    {
                        Name = suObj.Value<string>("name") ?? "",
                        Description = suObj.Value<string>("description") ?? ""
                    };
                }

                // Parse perks array: [{name, description}, ...]
                var perksList = new System.Collections.Generic.List<FactionPerk>();
                var perksArr = obj["perks"] as JArray;
                if (perksArr != null)
                {
                    foreach (var p in perksArr)
                    {
                        if (p is JObject pObj)
                            perksList.Add(new FactionPerk
                            {
                                Name = pObj.Value<string>("name") ?? "",
                                Description = pObj.Value<string>("description") ?? ""
                            });
                    }
                }

                var faction = new FactionData
                {
                    Name = obj.Value<string>("name") ?? "",
                    Color = obj.Value<string>("color") ?? "",
                    Playable = obj.Value<bool>("playable"),
                    GameFaction = obj.Value<string>("game_faction") ?? "",
                    Description = obj.Value<string>("description") ?? "",
                    Image = obj.Value<string>("image") ?? "",
                    Side = obj.Value<string>("side") ?? "neutral",
                    Traits = obj["traits"]?.ToObject<string[]>() ?? System.Array.Empty<string>(),
                    Bonuses = bonusesList.ToArray(),
                    SpecialUnit = specialUnit,
                    Perks = perksList.ToArray(),
                    Strengths = obj["strengths"]?.ToObject<string[]>() ?? System.Array.Empty<string>(),
                    Weaknesses = obj["weaknesses"]?.ToObject<string[]>() ?? System.Array.Empty<string>(),
                    Difficulty = obj.Value<int?>("difficulty") ?? 0,
                };

                result[prop.Name] = faction;
            }

            return result;
        }
    }
}
