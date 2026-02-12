using System.Collections.Generic;

namespace LOTRAOM.FactionMap
{
    /// <summary>
    /// Configuration for landmark markers on the faction selection map.
    ///
    /// Landmarks are important locations (cities, fortresses, places of interest)
    /// that are displayed as small markers on the map to help with orientation.
    ///
    /// Coordinates are in the 2048x1423 texture space.
    /// </summary>
    public static class LandmarkConfig
    {
        public enum LandmarkType
        {
            Capital,      // Major faction capital (larger marker)
            City,         // Important city
            Fortress,     // Military fortification
            Landmark,     // Place of interest (Weathertop, Amon Hen, etc.)
            Ruin,         // Ancient ruins (Moria, Angmar, etc.)
            Port,         // Harbor city
        }

        public class LandmarkDef
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public LandmarkType Type { get; set; }

            /// <summary>
            /// Position in 2048x1423 texture coordinates.
            /// </summary>
            public int X { get; set; }
            public int Y { get; set; }

            /// <summary>
            /// Which faction this landmark belongs to (HitId from RegionConfig).
            /// 0 = neutral/contested.
            /// </summary>
            public int FactionId { get; set; }
        }

        private static readonly List<LandmarkDef> _landmarks = new List<LandmarkDef>();
        private static bool _initialized;

        public static IReadOnlyList<LandmarkDef> Landmarks
        {
            get
            {
                EnsureInit();
                return _landmarks;
            }
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            BuildLandmarks();
        }

        private static void BuildLandmarks()
        {
            // ════════════════════════════════════════════════════════════════
            // FACTION CAPITALS
            // ════════════════════════════════════════════════════════════════

            // Gondor
            Add(new LandmarkDef
            {
                Id = "minas_tirith", Name = "Minas Tirith",
                Description = "Der Weiße Turm - Hauptstadt Gondors",
                Type = LandmarkType.Capital,
                X = 580, Y = 680, FactionId = 11
            });

            Add(new LandmarkDef
            {
                Id = "dol_amroth", Name = "Dol Amroth",
                Description = "Hafen der Schwanenritter",
                Type = LandmarkType.City,
                X = 420, Y = 780, FactionId = 11
            });

            Add(new LandmarkDef
            {
                Id = "osgiliath", Name = "Osgiliath",
                Description = "Die alte Hauptstadt - in Ruinen",
                Type = LandmarkType.Ruin,
                X = 620, Y = 660, FactionId = 11
            });

            // Rohan
            Add(new LandmarkDef
            {
                Id = "edoras", Name = "Edoras",
                Description = "Meduseld - Die goldene Halle",
                Type = LandmarkType.Capital,
                X = 530, Y = 530, FactionId = 9
            });

            Add(new LandmarkDef
            {
                Id = "helms_deep", Name = "Helms Klamm",
                Description = "Die Hornburg - uneinnehmbare Festung",
                Type = LandmarkType.Fortress,
                X = 480, Y = 550, FactionId = 9
            });

            // Mordor
            Add(new LandmarkDef
            {
                Id = "barad_dur", Name = "Barad-dûr",
                Description = "Der Dunkle Turm Saurons",
                Type = LandmarkType.Capital,
                X = 920, Y = 620, FactionId = 8
            });

            Add(new LandmarkDef
            {
                Id = "minas_morgul", Name = "Minas Morgul",
                Description = "Festung der Nazgûl",
                Type = LandmarkType.Fortress,
                X = 750, Y = 650, FactionId = 8
            });

            Add(new LandmarkDef
            {
                Id = "mount_doom", Name = "Schicksalsberg",
                Description = "Orodruin - wo der Eine Ring geschmiedet wurde",
                Type = LandmarkType.Landmark,
                X = 880, Y = 640, FactionId = 8
            });

            // Düsterwald
            Add(new LandmarkDef
            {
                Id = "thranduils_halls", Name = "Thranduils Hallen",
                Description = "Das Waldreich des Elbenkönigs",
                Type = LandmarkType.Capital,
                X = 920, Y = 280, FactionId = 4
            });

            // Dol Guldur
            Add(new LandmarkDef
            {
                Id = "dol_guldur_fortress", Name = "Dol Guldur",
                Description = "Die Festung des Nekromanten",
                Type = LandmarkType.Capital,
                X = 870, Y = 510, FactionId = 17
            });

            // Eisenberge / Dale
            Add(new LandmarkDef
            {
                Id = "erebor", Name = "Erebor",
                Description = "Der Einsame Berg - Königreich unter dem Berg",
                Type = LandmarkType.Capital,
                X = 1150, Y = 280, FactionId = 12
            });

            Add(new LandmarkDef
            {
                Id = "dale_city", Name = "Thal",
                Description = "Die wiederaufgebaute Handelsstadt",
                Type = LandmarkType.Capital,
                X = 1140, Y = 320, FactionId = 16
            });

            // Imladris
            Add(new LandmarkDef
            {
                Id = "rivendell", Name = "Bruchtal",
                Description = "Imladris - Das letzte heimelige Haus",
                Type = LandmarkType.Capital,
                X = 680, Y = 280, FactionId = 13
            });

            // Lindon / Mithlond
            Add(new LandmarkDef
            {
                Id = "grey_havens", Name = "Graue Anfurten",
                Description = "Mithlond - Tor zu den Unsterblichen Landen",
                Type = LandmarkType.Capital,
                X = 180, Y = 350, FactionId = 14
            });

            // Dunland / Nebelberge
            Add(new LandmarkDef
            {
                Id = "moria", Name = "Moria",
                Description = "Khazad-dûm - Das verlorene Zwergenreich",
                Type = LandmarkType.Ruin,
                X = 540, Y = 430, FactionId = 2
            });

            Add(new LandmarkDef
            {
                Id = "isengard", Name = "Isengart",
                Description = "Orthanc - Sarumans Turm",
                Type = LandmarkType.Fortress,
                X = 450, Y = 480, FactionId = 2
            });

            // Gundabad
            Add(new LandmarkDef
            {
                Id = "mount_gundabad", Name = "Gundabad",
                Description = "Die alte Ork-Festung",
                Type = LandmarkType.Capital,
                X = 620, Y = 120, FactionId = 18
            });

            // Haradwaith
            Add(new LandmarkDef
            {
                Id = "harad_capital", Name = "Harad",
                Description = "Hauptstadt des Südreichs",
                Type = LandmarkType.Capital,
                X = 780, Y = 1050, FactionId = 5
            });

            // Umbar
            Add(new LandmarkDef
            {
                Id = "umbar_port", Name = "Umbar",
                Description = "Die Korsarenstadt",
                Type = LandmarkType.Capital,
                X = 400, Y = 900, FactionId = 19
            });

            // Khand
            Add(new LandmarkDef
            {
                Id = "khand_capital", Name = "Khand",
                Description = "Land der Variags",
                Type = LandmarkType.Capital,
                X = 1300, Y = 880, FactionId = 10
            });

            // Rhovanion
            Add(new LandmarkDef
            {
                Id = "lake_town", Name = "Seestadt",
                Description = "Esgaroth - Stadt auf dem See",
                Type = LandmarkType.City,
                X = 1100, Y = 350, FactionId = 3
            });

            // ════════════════════════════════════════════════════════════════
            // ADDITIONAL LANDMARKS
            // ════════════════════════════════════════════════════════════════

            Add(new LandmarkDef
            {
                Id = "weathertop", Name = "Wetterspitze",
                Description = "Amon Sûl - Ruinen des alten Wachturms",
                Type = LandmarkType.Ruin,
                X = 380, Y = 280, FactionId = 0 // Neutral
            });

            Add(new LandmarkDef
            {
                Id = "bree", Name = "Bree",
                Description = "Kreuzung der Wege",
                Type = LandmarkType.City,
                X = 340, Y = 300, FactionId = 0
            });

            Add(new LandmarkDef
            {
                Id = "hobbiton", Name = "Hobbingen",
                Description = "Im Auenland",
                Type = LandmarkType.City,
                X = 260, Y = 290, FactionId = 0
            });

            Add(new LandmarkDef
            {
                Id = "lorien", Name = "Lothlórien",
                Description = "Der goldene Wald",
                Type = LandmarkType.Landmark,
                X = 700, Y = 400, FactionId = 0
            });

            Add(new LandmarkDef
            {
                Id = "fangorn", Name = "Fangorn",
                Description = "Der alte Wald der Ents",
                Type = LandmarkType.Landmark,
                X = 560, Y = 450, FactionId = 0
            });

            Add(new LandmarkDef
            {
                Id = "dead_marshes", Name = "Totensümpfe",
                Description = "Geisterhafte Sümpfe vor Mordor",
                Type = LandmarkType.Landmark,
                X = 720, Y = 580, FactionId = 0
            });

            Add(new LandmarkDef
            {
                Id = "amon_hen", Name = "Amon Hen",
                Description = "Hügel des Sehens",
                Type = LandmarkType.Ruin,
                X = 630, Y = 520, FactionId = 0
            });

            Add(new LandmarkDef
            {
                Id = "cirith_ungol", Name = "Cirith Ungol",
                Description = "Pass der Spinne",
                Type = LandmarkType.Fortress,
                X = 760, Y = 680, FactionId = 8
            });

            Add(new LandmarkDef
            {
                Id = "pelargir", Name = "Pelargir",
                Description = "Großer Hafen Gondors",
                Type = LandmarkType.Port,
                X = 520, Y = 780, FactionId = 11
            });

            // Östliche Regionen
            Add(new LandmarkDef
            {
                Id = "rhun_sea", Name = "Meer von Rhûn",
                Description = "Das Binnenmeer des Ostens",
                Type = LandmarkType.Landmark,
                X = 1400, Y = 450, FactionId = 3
            });

            Add(new LandmarkDef
            {
                Id = "nurnen", Name = "Núrnen-See",
                Description = "Binnenmeer in Mordor",
                Type = LandmarkType.Landmark,
                X = 1000, Y = 800, FactionId = 8
            });
        }

        private static void Add(LandmarkDef def)
        {
            _landmarks.Add(def);
        }

        /// <summary>
        /// Get all landmarks for a specific faction.
        /// </summary>
        public static IEnumerable<LandmarkDef> GetByFaction(int factionId)
        {
            EnsureInit();
            foreach (var landmark in _landmarks)
            {
                if (landmark.FactionId == factionId)
                    yield return landmark;
            }
        }

        /// <summary>
        /// Get all capitals (for showing on the main map).
        /// </summary>
        public static IEnumerable<LandmarkDef> GetCapitals()
        {
            EnsureInit();
            foreach (var landmark in _landmarks)
            {
                if (landmark.Type == LandmarkType.Capital)
                    yield return landmark;
            }
        }
    }
}
