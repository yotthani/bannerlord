using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceLearner.ML
{
    /// <summary>
    /// Gender detection: Triple-NN (ViT + FairFace + InsightFace) with majority vote.
    /// v3.0.33: Added ViT (Vision Transformer, 86.8M params, 224x224) as primary model.
    ///
    /// Three independent neural networks with different architectures:
    /// - ViT (google/vit-base-patch16-224, 94.3% accuracy) — PRIMARY, most trusted
    /// - FairFace (ResNet34, also provides age/race) — SECONDARY, provides demographics
    /// - InsightFace genderage (96x96 lightweight) — TERTIARY, cross-validation
    ///
    /// Decision logic:
    /// - Majority vote (2 of 3 agree → that result wins)
    /// - If only 2 NNs available → weighted comparison with ViT trust bonus
    /// - Beard detection → only override (Male) with NN guard
    /// </summary>
    public class GenderEnsemble : IDisposable
    {
        private FairFaceDetector _fairFace;
        private DemographicDetector _insightFace;
        private ViTGenderDetector _vitDetector;  // v3.0.33: Primary NN (ViT, highest accuracy)
        private bool _disposed;
        
        // Vote structure
        public struct Vote
        {
            public string Source;
            public bool IsFemale;
            public float Confidence;
            public float Weight;
            
            public override string ToString() => 
                $"{Source}:{(IsFemale ? "F" : "M")}({Confidence:F2})";
        }
        
        public struct EnsembleResult
        {
            public bool IsFemale;
            public float Confidence;
            public float Age;
            public string Race;
            public float RaceConfidence;
            public float SkinTone;
            public List<Vote> Votes;
            public string Decision;  // Explains why this decision was made
        }
        
        /// <summary>
        /// Initialize with available detectors.
        /// v3.0.33: Now accepts ViTGenderDetector as primary model.
        /// </summary>
        public bool Initialize(string fairFacePath, DemographicDetector insightFace = null,
            ViTGenderDetector vitDetector = null)
        {
            try
            {
                if (File.Exists(fairFacePath))
                {
                    _fairFace = new FairFaceDetector();
                    if (_fairFace.Load(fairFacePath))
                    {
                        SubModule.Log("GenderEnsemble: FairFace loaded");
                    }
                    else
                    {
                        _fairFace = null;
                    }
                }

                if (insightFace != null && insightFace.IsInitialized)
                {
                    _insightFace = insightFace;
                    SubModule.Log("GenderEnsemble: InsightFace genderage loaded");
                }

                // v3.0.33: ViT as primary (most accurate) gender model
                if (vitDetector != null && vitDetector.IsInitialized)
                {
                    _vitDetector = vitDetector;
                    SubModule.Log("GenderEnsemble: ViT age-gender loaded (PRIMARY model)");
                }

                int nnCount = (_fairFace != null ? 1 : 0) + (_insightFace != null ? 1 : 0) + (_vitDetector != null ? 1 : 0);
                SubModule.Log($"GenderEnsemble: Initialized — {nnCount} NNs (ViT={(_vitDetector != null)}, FairFace={(_fairFace != null)}, InsightFace={(_insightFace != null)})");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"GenderEnsemble: Init error - {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Detect gender using triple-NN majority vote.
        /// v3.0.33: ViT (primary) + FairFace + InsightFace, beard override.
        /// </summary>
        public EnsembleResult Detect(string imagePath, float[] landmarks = null,
            bool? insightFaceGender = null, float insightFaceConf = 0.5f)
        {
            var votes = new List<Vote>();  // For logging/diagnostics
            float age = 30f;
            string race = "Unknown";
            float raceConf = 0f;

            // Beard detection state
            bool beardDetected = false;
            float beardConfidence = 0f;

            // === STEP 0: ViT (PRIMARY — highest accuracy, 94.3%) ===
            bool vitFemale = false;
            float vitConf = 0f;
            float vitAge = 0f;
            bool hasViT = false;

            if (_vitDetector != null && _vitDetector.IsInitialized)
            {
                try
                {
                    var vitResult = _vitDetector.Detect(imagePath);
                    if (vitResult.HasValue)
                    {
                        vitFemale = vitResult.Value.IsFemale;
                        vitConf = vitResult.Value.GenderConfidence;
                        vitAge = vitResult.Value.Age;
                        hasViT = true;

                        votes.Add(new Vote
                        {
                            Source = "ViT",
                            IsFemale = vitFemale,
                            Confidence = vitConf,
                            Weight = 2.0f  // Highest trust
                        });

                        SubModule.Log($"  Gender: ViT {(vitFemale ? "F" : "M")}(conf={vitConf:F2}, logit={vitResult.Value.GenderLogit:F3}) age={vitAge:F0}");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  Gender: ViT error - {ex.Message}");
                }
            }

            // === STEP 1: FairFace (provides age/race demographics) ===
            bool ffFemale = false;
            float ffRawConf = 0.5f;
            float ffConf = 0f;
            bool hasFairFace = false;

            if (_fairFace != null && _fairFace.IsLoaded)
            {
                try
                {
                    using (var bmp = new Bitmap(imagePath))
                    {
                        var (female, conf, ffAge, ffRace, ffRaceConf) = _fairFace.Detect(bmp);

                        ffFemale = female;
                        ffRawConf = conf;
                        ffConf = AdjustFairFaceConfidence(conf);
                        age = ffAge;
                        race = ffRace;
                        raceConf = ffRaceConf;
                        hasFairFace = true;

                        votes.Add(new Vote
                        {
                            Source = "FairFace",
                            IsFemale = female,
                            Confidence = ffConf,
                            Weight = 1.0f
                        });

                        SubModule.Log($"  Gender: FairFace {(female ? "F" : "M")}(raw={conf:F2}, adj={ffConf:F2}) age={ffAge:F0} race={ffRace}");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  Gender: FairFace error - {ex.Message}");
                }
            }

            // === STEP 2: InsightFace genderage (Second NN — cross-validation) ===
            bool ifFemale = false;
            float ifConf = 0f;
            float ifAge = 0f;
            bool hasInsightFace = false;

            if (_insightFace != null && _insightFace.IsInitialized)
            {
                try
                {
                    var ifResult = _insightFace.Detect(imagePath);
                    if (ifResult.HasValue)
                    {
                        ifFemale = ifResult.Value.IsFemale;
                        ifConf = ifResult.Value.Confidence;
                        ifAge = ifResult.Value.Age;
                        hasInsightFace = true;

                        votes.Add(new Vote
                        {
                            Source = "InsightFace",
                            IsFemale = ifFemale,
                            Confidence = ifConf,
                            Weight = 1.0f
                        });

                        // v3.0.32: Age plausibility check — if InsightFace age is wildly off from
                        // FairFace age, InsightFace likely got a bad face crop or misfired.
                        // Halve its confidence in this case.
                        if (hasFairFace && ifAge > 0 && age > 0)
                        {
                            float ageDiff = Math.Abs(ifAge - age);
                            if (ageDiff > 25f)
                            {
                                float origConf = ifConf;
                                ifConf *= 0.50f;
                                SubModule.Log($"  Gender: InsightFace age={ifAge:F0} vs FairFace age={age:F0} (diff={ageDiff:F0}) — conf halved {origConf:F2}→{ifConf:F2}");
                            }
                        }

                        SubModule.Log($"  Gender: InsightFace {(ifFemale ? "F" : "M")}(conf={ifConf:F2}) age={ifAge:F0}");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  Gender: InsightFace error - {ex.Message}");
                }
            }

            // === STEP 3: Beard Detection (override) ===
            float hairScore = 0f;
            try
            {
                var (hs, hairConf, hairDetails) = HairAppearanceDetector.Analyze(imagePath);
                hairScore = hs;

                // Parse beard from hair details
                beardDetected = hairDetails.Contains(",Beard:");
                if (beardDetected)
                {
                    int beardIdx = hairDetails.IndexOf(",Beard:");
                    if (beardIdx >= 0)
                    {
                        string afterBeard = hairDetails.Substring(beardIdx + 7);
                        string beardStr = afterBeard.Length >= 4 ? afterBeard.Substring(0, 4) : afterBeard;
                        float beardRatio = 0f;
                        float.TryParse(beardStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out beardRatio);
                        // v3.0.37: beardRatio is 0.25-0.60 for real beards, but the old code
                        // used it directly as "confidence" and required >0.65 — which NEVER fires!
                        // Remap: beardRatio 0.25 → conf 0.60, 0.40 → conf 0.80, 0.55+ → conf 0.95
                        beardConfidence = Math.Min(0.95f, 0.40f + beardRatio);
                    }
                }

                // Log hair info for diagnostics (NOT used for gender decision)
                SubModule.Log($"  Gender: Hair={hs:F2}(conf={hairConf:F2}) [{hairDetails}] beardConf={beardConfidence:F2}");
            }
            catch (Exception ex)
            {
                SubModule.Log($"  Gender: Hair analysis error - {ex.Message}");
            }

            // === STEP 4: Landmarks (logging only) ===
            if (landmarks != null && landmarks.Length >= 936)
            {
                try
                {
                    var (lmFemale, lmConf, _) = LandmarkGenderEstimator.EstimateGender(landmarks, beardDetected);
                    SubModule.Log($"  Gender: Landmarks {(lmFemale ? "F" : "M")}({lmConf:F2}) [diagnostic only]");
                }
                catch { }
            }

            // === DECISION: Triple-NN Majority Vote + Beard ===

            // v3.0.34: Count NNs before beard override (needed for guard logic)
            int nnCount = (hasViT ? 1 : 0) + (hasFairFace ? 1 : 0) + (hasInsightFace ? 1 : 0);
            int femaleVotes = (hasViT && vitFemale ? 1 : 0) + (hasFairFace && ffFemale ? 1 : 0) + (hasInsightFace && ifFemale ? 1 : 0);
            int maleVotes = nnCount - femaleVotes;

            // Priority 1: Beard override → Male
            // v3.0.40: Beard override with NN guard.
            // IsBeardPixel() is too permissive — dark shadows, dark skin, and lips
            // all trigger false positives (ratio 0.15-0.50 on women!).
            // Rules:
            // - If ALL NNs unanimously say Female → NEVER override (it's a false positive beard)
            // - If majority Male or split → beard override fires (the NNs support it)
            // - If majority Female but not unanimous (2-1) → only with very high beard confidence
            bool unanimousFemale = femaleVotes == nnCount && nnCount >= 2;
            bool majorityFemaleVote = femaleVotes > maleVotes;

            // v3.0.40: Beard override with ViT guard.
            // IsBeardPixel() has high false positive rate on shadows, dark skin, makeup, etc.
            // Example: 1473033823 (Asian woman with shadow) → beardRatio=0.57, beardConf=0.95!
            // KEY RULE: ViT is the most reliable model (94.3% accuracy).
            // If ViT says Female, beard pixel detection CANNOT override it.
            // Beard can only override when ViT says Male (or is unavailable).
            bool vitSaysFemale = hasViT && vitFemale && vitConf > 0.50f;

            if (beardDetected && beardConfidence > 0.60f)
            {
                if (unanimousFemale)
                {
                    // ALL NNs say Female — beard is certainly a false positive
                    SubModule.Log($"  Gender: BEARD BLOCKED — all {nnCount} NNs say Female, beard is false positive (conf={beardConfidence:F2})");
                }
                else if (vitSaysFemale)
                {
                    // ViT says Female — trust it over pixel-based beard detection.
                    // ViT (94.3%) is more reliable than IsBeardPixel() which false-fires on
                    // shadows, dark skin, makeup, and hair near chin.
                    SubModule.Log($"  Gender: BEARD BLOCKED — ViT says Female (conf={vitConf:F2}), beard detection unreliable (conf={beardConfidence:F2})");
                }
                else if (majorityFemaleVote && beardConfidence < 0.85f)
                {
                    // Majority Female (2-1, ViT not Female) — only override with very strong beard
                    SubModule.Log($"  Gender: BEARD BLOCKED — majority Female ({femaleVotes}/{nnCount}), beard conf {beardConfidence:F2} < 0.85 threshold");
                }
                else
                {
                    // ViT says Male (or unavailable) + beard detected → override fires
                    float overrideConf = Math.Min(0.92f, 0.50f + beardConfidence);
                    SubModule.Log($"  Gender: BEARD OVERRIDE → Male (beard={beardConfidence:F2}, femaleNNs={femaleVotes}/{nnCount})");
                    return new EnsembleResult
                    {
                        IsFemale = false, Confidence = overrideConf, Age = age,
                        Race = race, RaceConfidence = raceConf, Votes = votes,
                        Decision = $"BeardOverride({beardConfidence:F2})→M"
                    };
                }
            }

            // v3.0.38: Hair-based override for NN votes.
            // If HairAppearanceDetector gives a strong female signal (long hair, lip color)
            // and NNs say Male, flip to Female. This catches women with strong features
            // that confuse NNs (e.g. 1473033823: curly-haired woman → unanimous Male).
            // Hair score > 0.30 = strong female signal (for 2-1 flip)
            // Hair score > 0.50 = very strong female signal (for 3-0 flip)
            bool strongFemaleHair = !beardDetected && hairScore > 0.30f;
            bool veryStrongFemaleHair = !beardDetected && hairScore > 0.50f;

            // Priority 2: Triple-NN majority vote (best case — 3 independent models)
            // v3.0.34: nnCount, femaleVotes, maleVotes already computed above (before beard override)

            if (nnCount >= 3)
            {
                // 3 NNs: majority vote (2 of 3 agree)
                bool majorityFemale = femaleVotes >= 2;
                int winnerCount = majorityFemale ? femaleVotes : maleVotes;

                // v3.0.40: Hair override for 2-1 Male — ONLY with very strong hair (>0.50)
                // AND only if ViT is NOT one of the Male voters (ViT is most reliable).
                // Previous threshold (0.30) caused false flips on Asian men with soft features.
                bool vitVotedMale = hasViT && !vitFemale;
                if (!majorityFemale && winnerCount == 2 && veryStrongFemaleHair && !vitVotedMale)
                {
                    // 2-1 Male, but ViT didn't vote Male + very strong female hair → flip to Female
                    float flipConf = Math.Max(0.40f, 0.50f + hairScore * 0.3f);
                    SubModule.Log($"  Gender: HAIR OVERRIDE — 2-1 Male (ViT not Male) + hairScore={hairScore:F2} → flipping to Female (conf={flipConf:F2})");
                    return new EnsembleResult
                    {
                        IsFemale = true,
                        Confidence = flipConf,
                        Age = age,
                        Race = race,
                        RaceConfidence = raceConf,
                        Votes = votes,
                        Decision = $"HairOverride(hair={hairScore:F2},2-1M_noViT)→F"
                    };
                }

                // Confidence: unanimous (3/3) = high, majority (2/3) = moderate
                float majorityConf;
                if (winnerCount == 3)
                {
                    // Unanimous — all 3 NNs agree
                    float avgConf = ((hasViT ? vitConf : 0) + (hasFairFace ? ffConf : 0) + (hasInsightFace ? ifConf : 0)) / 3f;
                    majorityConf = Math.Min(0.95f, avgConf + 0.20f);

                    // v3.0.38: If unanimous Male but very strong female hair (>0.50), FLIP to Female.
                    // 3 NNs can all be wrong on women with strong features (e.g. 1473033823).
                    // Long hair + lip color + no beard = definitive female signal.
                    if (!majorityFemale && veryStrongFemaleHair)
                    {
                        float flipConf = Math.Max(0.40f, 0.45f + hairScore * 0.3f);
                        SubModule.Log($"  Gender: HAIR OVERRIDE — 3-0 Male but hairScore={hairScore:F2} → flipping to Female (conf={flipConf:F2})");
                        return new EnsembleResult
                        {
                            IsFemale = true,
                            Confidence = flipConf,
                            Age = age,
                            Race = race,
                            RaceConfidence = raceConf,
                            Votes = votes,
                            Decision = $"HairOverride(hair={hairScore:F2},3-0M)→F"
                        };
                    }
                    else if (!majorityFemale && strongFemaleHair)
                    {
                        majorityConf *= 0.60f;
                        SubModule.Log($"  Gender: TRIPLE-NN UNANIMOUS Male BUT hairScore={hairScore:F2} → reduced conf to {majorityConf:F2}");
                    }
                    else
                    {
                        SubModule.Log($"  Gender: TRIPLE-NN UNANIMOUS → {(majorityFemale ? "Female" : "Male")} (conf={majorityConf:F2})");
                    }
                }
                else
                {
                    // 2-1 majority
                    string majorityNNs = "";
                    string dissenterNN = "";
                    float dissenterConf = 0f;
                    if (hasViT && (vitFemale == majorityFemale)) majorityNNs += "ViT,"; else { dissenterNN = "ViT"; dissenterConf = vitConf; }
                    if (hasFairFace && (ffFemale == majorityFemale)) majorityNNs += "FF,"; else { dissenterNN = "FF"; dissenterConf = ffConf; }
                    if (hasInsightFace && (ifFemale == majorityFemale)) majorityNNs += "IF,"; else { dissenterNN = "IF"; dissenterConf = ifConf; }
                    majorityNNs = majorityNNs.TrimEnd(',');

                    // v3.0.40: ViT override — if ViT dissents with high confidence AND
                    // the other two NNs have weak combined confidence, trust ViT.
                    // ViT (94.3%) is more reliable than FF or IF individually.
                    // But if both FF and IF are confident, their combined signal is strong.
                    // Example: 1473033823 — ViT F(0.89) vs FF M(0.61)+IF M(0.96)
                    //   FF is weak (0.61), IF is strong (0.96). Average = 0.785.
                    //   ViT 0.89 > avg 0.785 → ViT wins.
                    float otherAvgConf = 0f;
                    int otherCount = 0;
                    if (hasFairFace && ffFemale != vitFemale) { otherAvgConf += ffConf; otherCount++; }
                    if (hasInsightFace && ifFemale != vitFemale) { otherAvgConf += ifConf; otherCount++; }
                    if (otherCount > 0) otherAvgConf /= otherCount;

                    if (dissenterNN == "ViT" && vitConf > 0.80f && vitConf > otherAvgConf)
                    {
                        // ViT is more confident than the average of the opposing NNs → trust ViT
                        bool vitResult = vitFemale;
                        float vitOverrideConf = Math.Min(0.80f, vitConf * 0.7f);
                        SubModule.Log($"  Gender: VIT OVERRIDE — ViT {(vitResult ? "F" : "M")}({vitConf:F2}) > avg opposition ({otherAvgConf:F2}) → trusting ViT");
                        majorityFemale = vitResult;
                        majorityConf = vitOverrideConf;
                    }
                    else
                    {
                        // Normal majority vote with penalty from dissenter
                        float penalty = dissenterConf * 0.3f;
                        majorityConf = Math.Max(0.35f, 0.70f - penalty);
                        SubModule.Log($"  Gender: TRIPLE-NN MAJORITY ({majorityNNs}) → {(majorityFemale ? "Female" : "Male")} vs {dissenterNN} (conf={majorityConf:F2})");
                    }
                }

                return new EnsembleResult
                {
                    IsFemale = majorityFemale,
                    Confidence = majorityConf,
                    Age = age,
                    Race = race,
                    RaceConfidence = raceConf,
                    Votes = votes,
                    Decision = $"TripleNN_{(femaleVotes >= 2 ? femaleVotes + "F" : maleVotes + "M")}of{nnCount}(conf={femaleVotes}F/{maleVotes}M)→{(majorityFemale ? "F" : "M")}"
                };
            }
            else if (nnCount == 2)
            {
                // 2 NNs available — weighted comparison, ViT is most trusted
                bool nn1Female, nn2Female;
                float nn1Conf, nn2Conf;
                string nn1Name, nn2Name;
                float nn1Trust;

                if (hasViT && hasFairFace) { nn1Female = vitFemale; nn1Conf = vitConf; nn1Name = "ViT"; nn1Trust = 0.20f; nn2Female = ffFemale; nn2Conf = ffConf; nn2Name = "FF"; }
                else if (hasViT && hasInsightFace) { nn1Female = vitFemale; nn1Conf = vitConf; nn1Name = "ViT"; nn1Trust = 0.25f; nn2Female = ifFemale; nn2Conf = ifConf; nn2Name = "IF"; }
                else { nn1Female = ffFemale; nn1Conf = ffConf; nn1Name = "FF"; nn1Trust = 0.15f; nn2Female = ifFemale; nn2Conf = ifConf; nn2Name = "IF"; }

                if (nn1Female == nn2Female)
                {
                    // v3.0.40: Hair override for DUAL-NN path (previously only in triple-NN path!)
                    // Both NNs agree Male, but strong female hair → flip to Female.
                    // This catches women with strong features that confuse both FairFace & InsightFace.
                    if (!nn1Female && veryStrongFemaleHair)
                    {
                        // Both say Male but very strong female hair (>0.50) → flip
                        float flipConf = Math.Max(0.40f, 0.45f + hairScore * 0.3f);
                        SubModule.Log($"  Gender: HAIR OVERRIDE — DUAL-NN 2-0 Male but hairScore={hairScore:F2} → flipping to Female (conf={flipConf:F2})");
                        return new EnsembleResult { IsFemale = true, Confidence = flipConf, Age = age, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = $"HairOverride(hair={hairScore:F2},2-0M)→F" };
                    }
                    // v3.0.40: Removed moderate hair override (0.30 threshold) for dual-NN path.
                    // Only very strong (>0.50) flips dual-NN agreement (handled above).
                    // hairScore 0.30-0.50 on men (lips+skin+eyes) caused false flips.

                    float combinedConf = Math.Min(0.95f, (nn1Conf + nn2Conf) / 2f + 0.15f);
                    SubModule.Log($"  Gender: DUAL-NN AGREE ({nn1Name}+{nn2Name}) → {(nn1Female ? "Female" : "Male")} (conf={combinedConf:F2})");
                    return new EnsembleResult { IsFemale = nn1Female, Confidence = combinedConf, Age = age, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = $"DualNN_Agree({nn1Name}+{nn2Name})→{(nn1Female ? "F" : "M")}" };
                }
                else
                {
                    // v3.0.40: When NNs disagree, hair can break the tie.
                    // If one says Male, one says Female, and we have very strong female hair → Female wins.
                    // Raised from 0.30 to 0.50 — moderate hair scores (0.30-0.50) are unreliable.
                    if (veryStrongFemaleHair)
                    {
                        float flipConf = Math.Max(0.40f, 0.50f + hairScore * 0.3f);
                        SubModule.Log($"  Gender: HAIR TIEBREAK — DUAL-NN disagree + hairScore={hairScore:F2} → Female (conf={flipConf:F2})");
                        return new EnsembleResult { IsFemale = true, Confidence = flipConf, Age = age, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = $"HairTiebreak(hair={hairScore:F2})→F" };
                    }

                    // Disagree — trust nn1 (higher trust model) with bonus
                    bool trustNN1 = (nn1Conf + nn1Trust) >= nn2Conf;
                    bool resultFemale = trustNN1 ? nn1Female : nn2Female;
                    float winnerConf = trustNN1 ? nn1Conf : nn2Conf;
                    float loserConf = trustNN1 ? nn2Conf : nn1Conf;
                    float penalty = loserConf * 0.5f;
                    float finalConf = Math.Max(0.25f, winnerConf - penalty);
                    string winner = trustNN1 ? nn1Name : nn2Name;
                    SubModule.Log($"  Gender: DUAL-NN DISAGREE — {nn1Name}:{(nn1Female?"F":"M")}({nn1Conf:F2}+{nn1Trust:F2}) vs {nn2Name}:{(nn2Female?"F":"M")}({nn2Conf:F2}) → {winner}");
                    SubModule.Log($"  Gender: DECISION → {(resultFemale ? "Female" : "Male")} (conf={finalConf:F2})");
                    return new EnsembleResult { IsFemale = resultFemale, Confidence = finalConf, Age = age, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = $"DualNN_Disagree({winner})→{(resultFemale ? "F" : "M")}" };
                }
            }

            // Priority 3: Single NN
            if (hasViT)
            {
                SubModule.Log($"  Gender: DECISION → {(vitFemale ? "Female" : "Male")} (ViT only, conf={vitConf:F2})");
                return new EnsembleResult { IsFemale = vitFemale, Confidence = Math.Max(0.45f, vitConf), Age = vitAge > 0 ? vitAge : age, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = $"ViT_Only→{(vitFemale ? "F" : "M")}" };
            }
            if (hasFairFace)
            {
                SubModule.Log($"  Gender: DECISION → {(ffFemale ? "Female" : "Male")} (FairFace only, conf={ffConf:F2})");
                return new EnsembleResult { IsFemale = ffFemale, Confidence = Math.Max(0.40f, ffConf), Age = age, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = $"FF_Only→{(ffFemale ? "F" : "M")}" };
            }
            if (hasInsightFace)
            {
                SubModule.Log($"  Gender: DECISION → {(ifFemale ? "Female" : "Male")} (InsightFace only, conf={ifConf:F2})");
                return new EnsembleResult { IsFemale = ifFemale, Confidence = Math.Max(0.35f, ifConf), Age = ifAge, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = $"IF_Only→{(ifFemale ? "F" : "M")}" };
            }

            // Fallback: No NNs available
            SubModule.Log("  Gender: No NNs available — defaulting to Male");
            return new EnsembleResult { IsFemale = false, Confidence = 0.10f, Age = age, Race = race, RaceConfidence = raceConf, Votes = votes, Decision = "NoNNs→DefaultMale" };
        }
        
        /// <summary>
        /// Adjust FairFace confidence to be more meaningful
        /// Raw softmax values near 0.5 should map to low confidence
        /// </summary>
        private float AdjustFairFaceConfidence(float rawConf)
        {
            // Raw confidence is softmax output (0.5 = uncertain, 1.0 = certain)
            // Adjust so that:
            // - 0.50 → 0.00 (totally uncertain)
            // - 0.60 → 0.20 (slight signal)
            // - 0.70 → 0.40 (moderate confidence)
            // - 0.80 → 0.60 (good confidence)
            // - 0.90 → 0.80 (high confidence)
            // - 0.95 → 0.90 (very high confidence)
            
            if (rawConf < 0.50f) rawConf = 1f - rawConf;  // Ensure we're working with the winning class
            
            float adjusted = (rawConf - 0.5f) * 2f;  // Scale 0.5-1.0 to 0.0-1.0
            adjusted = (float)Math.Pow(adjusted, 0.7);  // Compress high values slightly
            
            return Math.Max(0f, Math.Min(1f, adjusted));
        }
        
        // NOTE: CombineVotes() and AnalyzeSkinAndColors() REMOVED in v3.0.30.
        // The ensemble voting with Hair/Landmarks/SkinColor heuristics caused 57% gender swaps.
        // Now using FairFace as primary decision with beard-only override.
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _fairFace?.Dispose();
                _disposed = true;
            }
        }
    }
}
