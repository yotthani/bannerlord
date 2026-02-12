using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.MemorySystem
{
    /// <summary>
    /// Represents a virtual captain that emerges from a bonded troop group.
    /// Provides bonuses and adds personality to your army.
    /// Now includes hero-like skills and traits for enhanced depth.
    /// </summary>
    public class HoNVirtualCaptain
    {
        /// <summary>
        /// Minimum bonding level required for a captain to emerge.
        /// </summary>
        public const float CaptainBondingThreshold = 75f;

        /// <summary>
        /// Minimum troop count required for a captain.
        /// </summary>
        public const int CaptainMinTroopCount = 10;

        #region Core Identity

        /// <summary>
        /// The troop type this captain leads.
        /// </summary>
        [SaveableField(1)]
        public string TroopId;

        /// <summary>
        /// Generated name for the captain.
        /// </summary>
        [SaveableField(2)]
        public string Name;

        /// <summary>
        /// Title/epithet (e.g., "the Brave", "Shieldbearer").
        /// </summary>
        [SaveableField(3)]
        public string Title;

        /// <summary>
        /// Day the captain emerged.
        /// </summary>
        [SaveableField(4)]
        public float DayPromoted;

        /// <summary>
        /// Number of battles survived as captain.
        /// </summary>
        [SaveableField(5)]
        public int BattlesSurvived;

        /// <summary>
        /// Captain's personal experience/rank (0-100).
        /// </summary>
        [SaveableField(6)]
        public float Experience;

        /// <summary>
        /// Is the captain still alive?
        /// </summary>
        [SaveableField(7)]
        public bool IsAlive;

        /// <summary>
        /// Day the captain died (if applicable).
        /// </summary>
        [SaveableField(8)]
        public float DayDied;

        /// <summary>
        /// Cause of death description.
        /// </summary>
        [SaveableField(9)]
        public string DeathDescription;

        #endregion

        #region Hero-Like Skills (0-100 scale)

        /// <summary>
        /// Leadership skill - affects morale bonuses.
        /// Grows with battles won.
        /// </summary>
        [SaveableField(10)]
        public int LeadershipLevel;

        /// <summary>
        /// Tactics skill - affects formation effectiveness.
        /// Grows with victories, especially decisive ones.
        /// </summary>
        [SaveableField(11)]
        public int TacticsLevel;

        /// <summary>
        /// Combat skill - affects damage bonuses for led troops.
        /// Grows in all battles.
        /// </summary>
        [SaveableField(12)]
        public int CombatLevel;

        /// <summary>
        /// Scouting skill - affects detection and stealth bonuses.
        /// Grows when party evades or detects enemies.
        /// </summary>
        [SaveableField(13)]
        public int ScoutingLevel;

        #endregion

        #region Hero-Like Traits (0-100 scale, can be negative)

        /// <summary>
        /// Valor trait - from surviving tough battles.
        /// High valor = better morale in tough situations.
        /// </summary>
        [SaveableField(14)]
        public int Valor;

        /// <summary>
        /// Mercy trait - from protecting wounded, taking prisoners.
        /// Affects how enemy troops react to surrender.
        /// </summary>
        [SaveableField(15)]
        public int Mercy;

        /// <summary>
        /// Honor trait - from keeping formations, following orders.
        /// Affects discipline of led troops.
        /// </summary>
        [SaveableField(16)]
        public int Honor;

        /// <summary>
        /// Cunning trait - from ambushes, tactical victories.
        /// Affects stealth and ambush effectiveness.
        /// </summary>
        [SaveableField(17)]
        public int Cunning;

        #endregion

        #region Tracking for Promotion

        /// <summary>
        /// Number of decisive victories (won while outnumbered or against elite).
        /// </summary>
        [SaveableField(18)]
        public int DecisiveVictories;

        /// <summary>
        /// Number of troops saved from death under this captain's leadership.
        /// </summary>
        [SaveableField(19)]
        public int TroopsSaved;

        /// <summary>
        /// Whether this captain has been considered for promotion.
        /// </summary>
        [SaveableField(20)]
        public bool PromotionConsidered;

        #endregion

        public HoNVirtualCaptain()
        {
            IsAlive = true;
            Experience = 0f;
            BattlesSurvived = 0;
            LeadershipLevel = 10;
            TacticsLevel = 5;
            CombatLevel = 10;
            ScoutingLevel = 5;
            Valor = 0;
            Mercy = 0;
            Honor = 0;
            Cunning = 0;
        }

        public HoNVirtualCaptain(string troopId, float currentDay) : this()
        {
            TroopId = troopId;
            DayPromoted = currentDay;
            GenerateIdentity(troopId);
            
            // Initial skill variance based on troop type
            InitializeSkillsFromTroopType(troopId);
        }

        #region Skill Initialization

        private void InitializeSkillsFromTroopType(string troopId)
        {
            string id = troopId.ToLowerInvariant();

            // Cavalry captains are better scouts and more cunning
            if (id.Contains("cavalry") || id.Contains("rider") || id.Contains("knight"))
            {
                ScoutingLevel += MBRandom.RandomInt(5, 15);
                Cunning += MBRandom.RandomInt(0, 10);
            }
            // Archers are more patient and have higher honor
            else if (id.Contains("archer") || id.Contains("bowman"))
            {
                Honor += MBRandom.RandomInt(5, 15);
                ScoutingLevel += MBRandom.RandomInt(0, 10);
            }
            // Heavy infantry have more valor and leadership
            else if (id.Contains("guard") || id.Contains("heavy") || id.Contains("elite"))
            {
                Valor += MBRandom.RandomInt(5, 15);
                LeadershipLevel += MBRandom.RandomInt(5, 10);
            }
            // Orcs start with cunning but low mercy
            else if (id.Contains("orc") || id.Contains("uruk"))
            {
                Cunning += MBRandom.RandomInt(10, 20);
                Mercy -= MBRandom.RandomInt(10, 20);
            }
        }

        #endregion

        #region Identity Generation

        private void GenerateIdentity(string troopId)
        {
            // Generate name based on culture hints in troop ID
            Name = GenerateName(troopId);
            Title = GenerateTitle(troopId);
        }

        private static string GenerateName(string troopId)
        {
            string id = troopId.ToLowerInvariant();

            // Culture-specific name pools
            if (id.Contains("gondor") || id.Contains("dunedain"))
                return GetRandomName(GondorNames);
            if (id.Contains("rohan") || id.Contains("rider"))
                return GetRandomName(RohanNames);
            if (id.Contains("elf") || id.Contains("lorien") || id.Contains("rivendell") || id.Contains("mirkwood"))
                return GetRandomName(ElvenNames);
            if (id.Contains("dwarf") || id.Contains("erebor"))
                return GetRandomName(DwarfNames);
            if (id.Contains("orc") || id.Contains("uruk") || id.Contains("mordor") || id.Contains("isengard"))
                return GetRandomName(OrcNames);
            if (id.Contains("harad") || id.Contains("umbar"))
                return GetRandomName(HaradNames);
            if (id.Contains("rhun") || id.Contains("easterling"))
                return GetRandomName(EasterlingNames);

            // Default to generic names
            return GetRandomName(GenericNames);
        }

        private static string GenerateTitle(string troopId)
        {
            string id = troopId.ToLowerInvariant();

            // Unit-type specific titles
            if (id.Contains("archer") || id.Contains("bowman"))
                return GetRandomName(ArcherTitles);
            if (id.Contains("cavalry") || id.Contains("rider") || id.Contains("knight"))
                return GetRandomName(CavalryTitles);
            if (id.Contains("spear") || id.Contains("pike"))
                return GetRandomName(SpearTitles);
            if (id.Contains("guard") || id.Contains("heavy"))
                return GetRandomName(GuardTitles);

            return GetRandomName(GenericTitles);
        }

        private static string GetRandomName(string[] pool)
        {
            return pool[MBRandom.RandomInt(pool.Length)];
        }

        // Name pools by culture
        private static readonly string[] GondorNames = { "Beregond", "Húrin", "Forlong", "Ingold", "Hirluin", "Dervorin", "Golasgil", "Mablung", "Damrod", "Anborn" };
        private static readonly string[] RohanNames = { "Éomund", "Háma", "Gamling", "Erkenbrand", "Elfhelm", "Grimbold", "Déorwine", "Herubrand", "Herefara", "Guthlaf" };
        private static readonly string[] ElvenNames = { "Haldir", "Rúmil", "Orophin", "Gildor", "Lindir", "Erestor", "Glorfindel", "Beleg", "Mablung", "Celeborn" };
        private static readonly string[] DwarfNames = { "Dwalin", "Balin", "Bifur", "Bofur", "Bombur", "Óin", "Glóin", "Nori", "Dori", "Thorin" };
        private static readonly string[] OrcNames = { "Grishnákh", "Uglúk", "Shagrat", "Gorbag", "Lugdush", "Muzgash", "Radbug", "Lagduf", "Snaga", "Mauhúr" };
        private static readonly string[] HaradNames = { "Suladan", "Harad", "Incánus", "Fuinur", "Herumor", "Azgara", "Khaldun", "Rashid", "Salim", "Tariq" };
        private static readonly string[] EasterlingNames = { "Khamûl", "Uldor", "Ulfang", "Ulfast", "Ulwarth", "Borthand", "Brodda", "Lorgan", "Amlach", "Borlach" };
        private static readonly string[] GenericNames = { "Marcus", "Aldric", "Gareth", "Roland", "Edmund", "Conrad", "Werner", "Lothar", "Siegfried", "Baldwin" };

        // Title pools by unit type
        private static readonly string[] ArcherTitles = { "the Keen-eyed", "Trueshot", "the Swift", "Deadeye", "of the Long Bow" };
        private static readonly string[] CavalryTitles = { "the Swift", "Horsebane", "Lancebreaker", "of the Charge", "Thunderhoof" };
        private static readonly string[] SpearTitles = { "the Steadfast", "Shieldwall", "the Unyielding", "Spearpoint", "Ironwall" };
        private static readonly string[] GuardTitles = { "the Bold", "Ironside", "the Valiant", "Stoneshield", "the Defender" };
        private static readonly string[] GenericTitles = { "the Brave", "the Loyal", "the Fierce", "Battleborn", "the Veteran" };

        #endregion

        #region Full Name

        /// <summary>
        /// Gets the full display name with title.
        /// </summary>
        public string GetFullName()
        {
            if (string.IsNullOrEmpty(Title))
                return Name;
            return $"{Name} {Title}";
        }

        /// <summary>
        /// Gets the captain's unit name (e.g., "Beregond's Spearmen").
        /// </summary>
        public string GetUnitName(string baseTroopName)
        {
            return $"{Name}'s {baseTroopName}";
        }

        #endregion

        #region Bonuses (Now skill-based)

        /// <summary>
        /// Gets the morale bonus provided by this captain.
        /// </summary>
        public float GetMoraleBonus()
        {
            // Based on Leadership skill + Valor trait
            float leadershipBonus = LeadershipLevel * 0.10f; // Up to +10
            float valorBonus = Math.Max(0, Valor) * 0.05f;   // Up to +5
            return leadershipBonus + valorBonus;
        }

        /// <summary>
        /// Gets the damage bonus provided by this captain.
        /// </summary>
        public float GetDamageBonus()
        {
            // Based on Combat skill
            return CombatLevel * 0.001f; // Up to +10%
        }

        /// <summary>
        /// Gets the defense bonus provided by this captain.
        /// </summary>
        public float GetDefenseBonus()
        {
            // Based on Tactics + Honor
            float tacticsBonus = TacticsLevel * 0.0005f;
            float honorBonus = Math.Max(0, Honor) * 0.0003f;
            return tacticsBonus + honorBonus;
        }

        /// <summary>
        /// Gets the fear resistance bonus provided by this captain.
        /// </summary>
        public float GetFearResistanceBonus()
        {
            // Based on Valor + Leadership
            float valorBonus = Math.Max(0, Valor) * 0.002f;
            float leadershipBonus = LeadershipLevel * 0.001f;
            return 0.05f + valorBonus + leadershipBonus;
        }

        /// <summary>
        /// Gets the scouting bonus provided by this captain.
        /// </summary>
        public float GetScoutingBonus()
        {
            // Based on Scouting + Cunning
            float scoutBonus = ScoutingLevel * 0.001f;
            float cunningBonus = Math.Max(0, Cunning) * 0.0005f;
            return scoutBonus + cunningBonus;
        }

        #endregion

        #region Events

        /// <summary>
        /// Called when a battle is survived.
        /// </summary>
        public void OnBattleSurvived(bool wasVictory, bool wasDecisive = false, int troopsSavedThisBattle = 0)
        {
            BattlesSurvived++;
            
            // Gain experience
            float expGain = wasVictory ? 5f : 2f;
            if (wasDecisive) expGain += 3f;
            Experience = Math.Min(100f, Experience + expGain);

            // Skill improvements
            CombatLevel = Math.Min(100, CombatLevel + (wasVictory ? 2 : 1));
            
            if (wasVictory)
            {
                LeadershipLevel = Math.Min(100, LeadershipLevel + 1);
                TacticsLevel = Math.Min(100, TacticsLevel + (wasDecisive ? 2 : 1));
            }

            // Trait changes
            Valor = Math.Min(100, Valor + 1); // All battles increase valor
            
            if (wasDecisive)
            {
                DecisiveVictories++;
                Honor = Math.Min(100, Honor + 2);
            }

            if (troopsSavedThisBattle > 0)
            {
                TroopsSaved += troopsSavedThisBattle;
                Mercy = Math.Min(100, Mercy + 1);
            }
        }

        /// <summary>
        /// Called when captain performs a successful ambush.
        /// </summary>
        public void OnAmbushSuccess()
        {
            Cunning = Math.Min(100, Cunning + 3);
            ScoutingLevel = Math.Min(100, ScoutingLevel + 2);
        }

        /// <summary>
        /// Marks the captain as dead.
        /// </summary>
        public void OnDeath(float currentDay, string cause = "Fell in battle")
        {
            IsAlive = false;
            DayDied = currentDay;
            DeathDescription = cause;
        }

        #endregion

        #region Promotion Eligibility

        /// <summary>
        /// Gets a promotion eligibility score (0-100).
        /// Higher score = more likely to be promoted to companion.
        /// </summary>
        public float GetPromotionScore()
        {
            if (!IsAlive) return 0f;

            float score = 0f;

            // Experience contributes heavily
            score += Experience * 0.3f; // Up to 30

            // Skills contribute
            score += LeadershipLevel * 0.15f; // Up to 15
            score += TacticsLevel * 0.1f;     // Up to 10
            score += CombatLevel * 0.1f;      // Up to 10

            // Battles survived
            score += Math.Min(BattlesSurvived, 50) * 0.2f; // Up to 10

            // Decisive victories are important
            score += Math.Min(DecisiveVictories, 10) * 1.5f; // Up to 15

            // Positive traits
            score += Math.Max(0, Valor) * 0.05f;  // Up to 5
            score += Math.Max(0, Honor) * 0.05f;  // Up to 5

            return Math.Min(100f, score);
        }

        /// <summary>
        /// Checks if captain meets minimum requirements for promotion consideration.
        /// </summary>
        public bool MeetsPromotionRequirements(int minBattles = 50)
        {
            return IsAlive && 
                   BattlesSurvived >= minBattles && 
                   Experience >= 80f &&
                   LeadershipLevel >= 30;
        }

        #endregion

        /// <summary>
        /// Gets status summary for UI.
        /// </summary>
        public string GetStatusSummary()
        {
            if (!IsAlive)
                return $"† {GetFullName()} - {DeathDescription}";

            string rank = Experience switch
            {
                >= 90 => "Legendary",
                >= 70 => "Veteran",
                >= 50 => "Experienced",
                >= 30 => "Seasoned",
                _ => "Rising"
            };

            return $"★ {GetFullName()} [{rank}] - {BattlesSurvived} battles";
        }

        /// <summary>
        /// Gets detailed info for tooltips.
        /// </summary>
        public string GetDetailedInfo()
        {
            if (!IsAlive)
            {
                return $"{GetFullName()}\n" +
                       $"Fell after {BattlesSurvived} battles\n" +
                       $"Cause: {DeathDescription}";
            }

            return $"{GetFullName()}\n" +
                   $"Battles: {BattlesSurvived} | Exp: {Experience:F0}%\n" +
                   $"Leadership: {LeadershipLevel} | Tactics: {TacticsLevel}\n" +
                   $"Combat: {CombatLevel} | Scouting: {ScoutingLevel}\n" +
                   $"Valor: {Valor:+#;-#;0} | Honor: {Honor:+#;-#;0}";
        }
    }
}
