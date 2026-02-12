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
    /// Robust gender detection using ensemble of multiple methods.
    /// Combines: FairFace, Hair/Appearance analysis, Landmark analysis, and voting.
    /// 
    /// The key insight: Each method has different failure modes:
    /// - FairFace: Fails on certain facial bone structures
    /// - Landmarks: Fails on unusual face angles
    /// - Hair/Appearance: Only works when hair/makeup visible
    /// 
    /// By combining them with proper voting, we get much better accuracy.
    /// </summary>
    public class GenderEnsemble : IDisposable
    {
        private FairFaceDetector _fairFace;
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
        /// Initialize the ensemble with available detectors
        /// </summary>
        public bool Initialize(string fairFacePath)
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
                
                SubModule.Log("GenderEnsemble: Initialized");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"GenderEnsemble: Init error - {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Detect gender using all available methods and vote
        /// </summary>
        /// <param name="imagePath">Path to face image</param>
        /// <param name="landmarks">Optional facial landmarks (68-point * 2 = 136 floats)</param>
        /// <param name="insightFaceGender">Optional gender from InsightFace DemographicDetector (true=female)</param>
        /// <param name="insightFaceConf">Confidence of InsightFace gender (0-1)</param>
        public EnsembleResult Detect(string imagePath, float[] landmarks = null, 
            bool? insightFaceGender = null, float insightFaceConf = 0.5f)
        {
            var votes = new List<Vote>();
            float age = 30f;
            string race = "Unknown";
            float raceConf = 0f;
            
            // Store extra info for long-hair-male detection
            Dictionary<string, float> landmarkScores = null;
            bool hasLongHair = false;
            float hairSideRatio = 0f;

            // v3.0.24: Beard detection state (set by Hair analysis below)
            bool beardDetected = false;
            float beardConfidence = 0f;
            
            // === METHOD 0: InsightFace (if provided) ===
            // InsightFace genderage model - different architecture than FairFace
            // Having two deep learning models vote helps catch edge cases!
            if (insightFaceGender.HasValue)
            {
                votes.Add(new Vote
                {
                    Source = "InsightFace",
                    IsFemale = insightFaceGender.Value,
                    Confidence = insightFaceConf,
                    Weight = 2.0f  // High weight - another neural network
                });
                SubModule.Log($"  Ensemble: InsightFace {(insightFaceGender.Value ? "F" : "M")}({insightFaceConf:F2})");
            }
            
            // === METHOD 1: FairFace (Deep Learning) ===
            if (_fairFace != null && _fairFace.IsLoaded)
            {
                try
                {
                    using (var bmp = new Bitmap(imagePath))
                    {
                        var (ffFemale, ffConf, ffAge, ffRace, ffRaceConf) = _fairFace.Detect(bmp);
                        
                        age = ffAge;
                        race = ffRace;
                        raceConf = ffRaceConf;
                        
                        // FairFace confidence needs adjustment
                        // Raw softmax of 0.55 means 55% vs 45% - that's uncertain!
                        float adjustedConf = AdjustFairFaceConfidence(ffConf);
                        
                        votes.Add(new Vote
                        {
                            Source = "FairFace",
                            IsFemale = ffFemale,
                            Confidence = adjustedConf,
                            Weight = 2.5f  // High weight - deep learning model
                        });
                        
                        SubModule.Log($"  Ensemble: FairFace {(ffFemale ? "F" : "M")}({ffConf:F2}→{adjustedConf:F2}) age={ffAge:F0}");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  Ensemble: FairFace error - {ex.Message}");
                }
            }
            
            // === METHOD 2: Hair/Appearance Analysis ===
            try
            {
                var (hairScore, hairConf, hairDetails) = HairAppearanceDetector.Analyze(imagePath);
                
                // Check for long hair - look for Hair:0.5 in details which means side hair detected
                // OR if the hairScore itself is high (>0.35) indicating long/feminine hair
                hasLongHair = hairDetails.Contains("Hair:0.5") || hairScore > 0.35f;
                hairSideRatio = hairScore;
                
                // v3.0.24: Parse beard detection from hair details
                beardDetected = hairDetails.Contains(",Beard:");
                if (beardDetected)
                {
                    int beardIdx = hairDetails.IndexOf(",Beard:");
                    if (beardIdx >= 0)
                    {
                        string afterBeard = hairDetails.Substring(beardIdx + 7);
                        // Take up to 4 chars for the float value
                        string beardStr = afterBeard.Length >= 4 ? afterBeard.Substring(0, 4) : afterBeard;
                        float.TryParse(beardStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out beardConfidence);
                    }
                    SubModule.Log($"  Ensemble: BEARD DETECTED (ratio={beardConfidence:F2})");
                }

                if (hairConf > 0.25f)
                {
                    bool hairFemale = hairScore > 0;

                    // v3.0.24: Reduced hair weight from 2.0/3.0 to 1.0/2.0
                    // Hair analysis is the most unreliable method (beards → "long hair", backgrounds)
                    float hairWeight = 1.0f;

                    // Boost weight if we have strong signals (makeup, long hair) but NOT beard
                    if (Math.Abs(hairScore) > 0.4f && !beardDetected)
                    {
                        hairWeight = 2.0f;  // Strong signal (reduced from 3.0)
                    }
                    
                    votes.Add(new Vote
                    {
                        Source = "Hair",
                        IsFemale = hairFemale,
                        Confidence = hairConf * Math.Abs(hairScore),
                        Weight = hairWeight
                    });
                    
                    SubModule.Log($"  Ensemble: Hair {(hairFemale ? "F" : "M")}({hairScore:F2}) [{hairDetails}]");
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"  Ensemble: Hair error - {ex.Message}");
            }
            
            // === METHOD 3: Landmark Geometry ===
            if (landmarks != null && landmarks.Length >= 136)
            {
                try
                {
                    var (lmFemale, lmConf, lmScores) = LandmarkGenderEstimator.EstimateGender(landmarks, beardDetected);
                    landmarkScores = lmScores;  // Store for long-hair-male detection
                    
                    if (lmConf > 0.15f)
                    {
                        votes.Add(new Vote
                        {
                            Source = "Landmarks",
                            IsFemale = lmFemale,
                            Confidence = lmConf,
                            Weight = 1.5f  // Medium weight
                        });
                        
                        SubModule.Log($"  Ensemble: Landmarks {(lmFemale ? "F" : "M")}({lmConf:F2})");
                    }
                }
                catch (Exception ex)
                {
                    SubModule.Log($"  Ensemble: Landmarks error - {ex.Message}");
                }
            }
            
            // === METHOD 4: Skin/Color Analysis for soft signals ===
            try
            {
                var (skinScore, skinFemaleHint) = AnalyzeSkinAndColors(imagePath);
                if (Math.Abs(skinScore) > 0.2f)
                {
                    votes.Add(new Vote
                    {
                        Source = "SkinColor",
                        IsFemale = skinFemaleHint,
                        Confidence = Math.Abs(skinScore),
                        Weight = 0.5f  // Low weight - weak signal
                    });
                }
            }
            catch { }
            
            // === SPECIAL CASE: Long Hair Male Detection ===
            // FairFace is often tricked by long hair on men (metal musicians, etc.)
            // BUT we must be VERY careful not to override real women!
            // Check if we have: long hair + FairFace Female (not too confident) + STRONG male facial features
            var ffVote = votes.FirstOrDefault(v => v.Source == "FairFace");
            var lmVote = votes.FirstOrDefault(v => v.Source == "Landmarks");

            // === v3.0.24: SPECIAL CASE: BEARD DETECTED ===
            // A beard is an extremely strong male indicator. All other methods fail on bearded men:
            // - FairFace: confused by beard bulk on lower face
            // - Landmarks: jaw/chin occluded by beard → false female indicators
            // - Hair: beard misidentified as "long hair"
            // Override to Male if beard is detected with reasonable confidence.
            if (beardDetected && beardConfidence > 0.25f)
            {
                // Check if FairFace is VERY confidently Female — if so, be cautious
                bool ffVeryConfidentFemale = ffVote.Source == "FairFace" && ffVote.IsFemale && ffVote.Confidence >= 0.80f;

                if (!ffVeryConfidentFemale)
                {
                    float beardOverrideConf = Math.Min(0.85f, 0.50f + beardConfidence);
                    SubModule.Log($"  Ensemble: BEARD OVERRIDE → Male (beard={beardConfidence:F2}, conf={beardOverrideConf:F2})");
                    var beardResult = new EnsembleResult
                    {
                        IsFemale = false,
                        Confidence = beardOverrideConf,
                        Age = age,
                        Race = race,
                        RaceConfidence = raceConf,
                        Votes = votes,
                        Decision = $"BeardOverride({beardConfidence:F2})→M"
                    };
                    return beardResult;
                }
                else
                {
                    SubModule.Log($"  Ensemble: Beard detected but FairFace very confident Female ({ffVote.Confidence:F2}) — not overriding");
                }
            }
            
            // Only consider override if:
            // 1. FairFace says Female but confidence is not extremely high (<0.97)
            // 2. Long hair is detected
            // 3. Landmarks exist and lean MALE (or are uncertain)
            bool landmarksLeanMale = lmVote.Source == "Landmarks" && !lmVote.IsFemale;
            bool landmarksUncertain = lmVote.Source != "Landmarks" || lmVote.Confidence < 0.25f;
            
            if (ffVote.Source == "FairFace" && ffVote.IsFemale && ffVote.Confidence < 0.97f && 
                hasLongHair && landmarkScores != null && (landmarksLeanMale || landmarksUncertain))
            {
                int maleFeatureCount = 0;
                int strongMaleCount = 0;  // Very strong indicators
                
                // Check for male facial structure features
                // Use STRICTER thresholds to avoid false positives on women
                // Negative scores = male indicators, but need to be CLEARLY negative
                if (landmarkScores.TryGetValue("jawRatio", out float jawScore) && jawScore < -0.15f)
                {
                    maleFeatureCount++;
                    if (jawScore < -0.3f) strongMaleCount++;
                }
                if (landmarkScores.TryGetValue("chinShape", out float chinScore) && chinScore < -0.15f)
                {
                    maleFeatureCount++;
                    if (chinScore < -0.3f) strongMaleCount++;
                }
                if (landmarkScores.TryGetValue("browProminence", out float browScore) && browScore < -0.2f)
                {
                    maleFeatureCount++;
                    if (browScore < -0.4f) strongMaleCount++;
                }
                if (landmarkScores.TryGetValue("noseSize", out float noseScore) && noseScore < -0.2f)
                {
                    maleFeatureCount++;
                    if (noseScore < -0.4f) strongMaleCount++;
                }
                if (landmarkScores.TryGetValue("faceAspect", out float aspectScore) && aspectScore < -0.2f)
                {
                    maleFeatureCount++;
                    if (aspectScore < -0.4f) strongMaleCount++;
                }
                
                // Require STRONG evidence: either 3+ male features OR 1 very strong + landmarks lean male
                bool shouldOverride = (maleFeatureCount >= 3) || 
                                     (strongMaleCount >= 1 && landmarksLeanMale && maleFeatureCount >= 2);
                
                if (shouldOverride)
                {
                    SubModule.Log($"  Ensemble: LONG-HAIR-MALE OVERRIDE - {maleFeatureCount} male features ({strongMaleCount} strong) despite FairFace F({ffVote.Confidence:F2})");
                    var result = new EnsembleResult
                    {
                        IsFemale = false,  // Override to Male
                        Confidence = 0.60f,
                        Age = age,
                        Race = race,
                        RaceConfidence = raceConf,
                        Votes = votes,
                        Decision = $"LongHairMaleOverride({maleFeatureCount}f/{strongMaleCount}s)→M"
                    };
                    return result;
                }
            }
            
            // === VOTING ===
            return CombineVotes(votes, age, race, raceConf);
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
        
        /// <summary>
        /// Combine all votes into final decision
        /// </summary>
        private EnsembleResult CombineVotes(List<Vote> votes, float age, string race, float raceConf)
        {
            var result = new EnsembleResult
            {
                Votes = votes,
                Age = age,
                Race = race,
                RaceConfidence = raceConf
            };
            
            if (votes.Count == 0)
            {
                // No votes - default to male with low confidence
                result.IsFemale = false;
                result.Confidence = 0.1f;
                result.Decision = "NoVotes→DefaultMale";
                return result;
            }
            
            var hairVote = votes.FirstOrDefault(v => v.Source == "Hair");
            var ffVote = votes.FirstOrDefault(v => v.Source == "FairFace");
            var ifVote = votes.FirstOrDefault(v => v.Source == "InsightFace");
            
            // === PRIORITY 0: Handle VERY UNCERTAIN FairFace ===
            // When FairFace confidence < 0.20, it's basically a coin flip!
            // Don't let such uncertain votes influence the decision
            // FIX for celeba_091950: FairFace:F(0.09) was incorrectly treated as a Female vote
            if (ffVote.Source == "FairFace" && ffVote.Confidence < 0.20f)
            {
                SubModule.Log($"  ⚠️ FairFace is VERY UNCERTAIN ({ffVote.Confidence:F2}) - treating as coin flip");
                
                // Check if other signals have STRONG evidence
                var otherVotes = votes.Where(v => v.Source != "FairFace").ToList();
                float strongThreshold = 0.50f;  // Need strong signal to override
                
                var strongVotes = otherVotes.Where(v => v.Confidence >= strongThreshold).ToList();
                if (strongVotes.Count >= 2)
                {
                    // Multiple strong signals - use them
                    bool consensusFemale = strongVotes.Count(v => v.IsFemale) > strongVotes.Count / 2;
                    result.IsFemale = consensusFemale;
                    result.Confidence = (float)strongVotes.Average(v => v.Confidence);
                    result.Decision = $"UncertainFF→StrongConsensus({strongVotes.Count})→{(result.IsFemale ? "F" : "M")}";
                    return result;
                }
                else
                {
                    // No strong signals either - everything is uncertain
                    // In this case, default to what FairFace chose but with LOW confidence
                    // OR if Hair signal exists and has any confidence, use that as tie-breaker
                    if (hairVote.Source == "Hair" && hairVote.Confidence > 0.25f)
                    {
                        result.IsFemale = hairVote.IsFemale;
                        result.Confidence = 0.40f;  // Low confidence - uncertain
                        result.Decision = $"UncertainFF→HairTieBreak→{(result.IsFemale ? "F" : "M")}";
                        return result;
                    }
                    else
                    {
                        // Everything uncertain - trust FairFace's raw output but note uncertainty
                        result.IsFemale = ffVote.IsFemale;
                        result.Confidence = 0.35f;  // Very low confidence
                        result.Decision = $"AllUncertain→FFDefault({ffVote.Confidence:F2})→{(result.IsFemale ? "F" : "M")}";
                        return result;
                    }
                }
            }
            
            // === PRIORITY 1: FairFace vs InsightFace Disagreement ===
            // If two neural networks disagree, at least one is wrong!
            // Use other signals (Landmarks, Hair) as tie-breakers
            if (ffVote.Source == "FairFace" && ifVote.Source == "InsightFace" &&
                ffVote.IsFemale != ifVote.IsFemale)
            {
                SubModule.Log($"  ⚠️ NN DISAGREEMENT: FairFace={(!ffVote.IsFemale ? "M" : "F")}({ffVote.Confidence:F2}) vs InsightFace={(!ifVote.IsFemale ? "M" : "F")}({ifVote.Confidence:F2})");
                
                // Count other votes as tie-breakers
                var tieBreakers = votes.Where(v => v.Source != "FairFace" && v.Source != "InsightFace").ToList();
                int maleCount = tieBreakers.Count(v => !v.IsFemale);
                int femaleCount = tieBreakers.Count(v => v.IsFemale);
                
                // Also weight by which NN has higher confidence
                float ffWeight = ffVote.Confidence;
                float ifWeight = ifVote.Confidence;
                
                // Trust the NN with higher confidence, UNLESS tie-breakers strongly disagree
                bool preferFairFace = ffWeight > ifWeight;
                bool preferredGender = preferFairFace ? ffVote.IsFemale : ifVote.IsFemale;
                
                // Count how many tie-breakers agree with the preferred gender
                int agreesWithPreferred = tieBreakers.Count(v => v.IsFemale == preferredGender);
                
                if (agreesWithPreferred < tieBreakers.Count / 2 && tieBreakers.Count >= 2)
                {
                    // Tie-breakers disagree with the higher-confidence NN!
                    // Trust the OTHER neural network instead
                    result.IsFemale = !preferredGender;
                    result.Confidence = 0.55f;  // Lower confidence due to disagreement
                    string winner = result.IsFemale ? "InsightFace" : "FairFace";
                    if (preferFairFace) winner = result.IsFemale ? "InsightFace" : "FairFace";
                    else winner = result.IsFemale ? "FairFace" : "InsightFace";
                    result.Decision = $"NNDisagreement→{(result.IsFemale ? "F" : "M")} (tie-breakers chose)";
                    SubModule.Log($"  Ensemble: Tie-breakers ({femaleCount}F/{maleCount}M) chose against higher-conf NN");
                    return result;
                }
                else
                {
                    // Tie-breakers agree with higher-confidence NN
                    result.IsFemale = preferredGender;
                    result.Confidence = preferFairFace ? ffWeight : ifWeight;
                    result.Decision = $"NNDisagreement→{(result.IsFemale ? "F" : "M")} ({(preferFairFace ? "FairFace" : "InsightFace")} won)";
                    return result;
                }
            }
            
            // === PRIORITY 1: Very high confidence FairFace should ALWAYS be trusted ===
            // This prevents hair from overriding obviously correct gender detection
            // FIX: Paul Tagliabue was being detected as Female due to light curly hair
            // v3.0.23: Raised from 0.80 to 0.90 — FairFace gives 0.80-0.89 on many wrong cases
            // (e.g. celeba_197864: woman detected as M with 0.85+ confidence)
            if (ffVote.Source == "FairFace" && ffVote.Confidence >= 0.90f)
            {
                // NEW: Check for possible background face issue
                // If FairFace says one thing with HIGH confidence, but BOTH Hair AND Landmarks disagree,
                // FairFace might be detecting a background face instead of the main subject
                // Megawati case: FairFace 0.99 Male, but Hair/Landmarks both say Female
                var nonFfVotes = votes.Where(v => v.Source != "FairFace").ToList();
                int disagreeCount = nonFfVotes.Count(v => v.IsFemale != ffVote.IsFemale);
                
                if (disagreeCount >= 2 && nonFfVotes.Count >= 2)
                {
                    // All other signals disagree with FairFace - possible background face!
                    SubModule.Log($"  ⚠️ BACKGROUND FACE WARNING: FairFace says {(ffVote.IsFemale ? "F" : "M")} ({ffVote.Confidence:F2}) but {disagreeCount} other signals disagree!");
                    SubModule.Log($"     This might indicate FairFace is detecting a background face, not the main subject.");
                    
                    // If other signals have reasonable combined confidence, consider them
                    float otherAvgConf = (float)nonFfVotes.Average(v => v.Confidence);
                    if (otherAvgConf >= 0.30f)
                    {
                        // Reduce FairFace effective confidence when there's strong disagreement
                        // This helps catch cases like Megawati where background faces confuse FairFace
                        float reducedConf = ffVote.Confidence * 0.7f;
                        if (reducedConf < 0.80f)
                        {
                            // Fall through to other detection methods instead of trusting FairFace blindly
                            SubModule.Log($"     Reducing FairFace trust: {ffVote.Confidence:F2} → {reducedConf:F2}, checking other signals...");
                            // Don't return here - let it fall through to consensus check
                        }
                        else
                        {
                            result.IsFemale = ffVote.IsFemale;
                            result.Confidence = reducedConf;
                            result.Decision = $"FairFaceHighConf({ffVote.Confidence:F2})→{(result.IsFemale ? "F" : "M")} [with disagreement]";
                            return result;
                        }
                    }
                    else
                    {
                        // Other signals too weak, trust FairFace
                        result.IsFemale = ffVote.IsFemale;
                        result.Confidence = ffVote.Confidence;
                        result.Decision = $"FairFaceHighConf({ffVote.Confidence:F2})→{(result.IsFemale ? "F" : "M")}";
                        return result;
                    }
                }
                else
                {
                    // Normal case - FairFace agrees with others or only weak disagreement
                    result.IsFemale = ffVote.IsFemale;
                    result.Confidence = ffVote.Confidence;
                    result.Decision = $"FairFaceHighConf({ffVote.Confidence:F2})→{(result.IsFemale ? "F" : "M")}";
                    return result;
                }
            }
            
            // === PRIORITY 2: Strong hair signal overrides UNCERTAIN AI ===
            // This fixes the "blonde woman detected as male" problem
            // BUT only if FairFace is NOT confident about Male!
            if (hairVote.Source == "Hair" && hairVote.IsFemale && hairVote.Confidence > 0.40f)
            {
                // Only override if FairFace is uncertain (conf < 0.70) OR says Female
                bool ffUncertain = ffVote.Source != "FairFace" || ffVote.Confidence < 0.70f;
                bool ffAgreesFemale = ffVote.Source == "FairFace" && ffVote.IsFemale;
                
                if (ffUncertain || ffAgreesFemale)
                {
                    result.IsFemale = true;
                    result.Confidence = Math.Max(0.6f, hairVote.Confidence);
                    result.Decision = "HairOverride→Female";
                    SubModule.Log($"  Ensemble: HAIR OVERRIDE - strong female hair signal overrides uncertain AI");
                    return result;
                }
            }
            
            // === PRIORITY 3: Consensus Override ===
            // When 3+ non-FairFace methods agree and FairFace disagrees, trust the consensus
            // BUT ONLY if:
            // 1. FairFace confidence is not very high
            // 2. The consensus signals are STRONG (not just numerous)
            // FIX for celeba_032676: Hair:F(0.33)+SkinColor:F(0.40) should NOT override FairFace:M(0.53)
            if (ffVote.Source == "FairFace" && votes.Count >= 3)
            {
                // CRITICAL FIX: Don't override high-confidence FairFace!
                // v3.0.23: Raised from 0.85 to 0.92 — FairFace is often wrong at 0.85-0.91
                if (ffVote.Confidence >= 0.92f)
                {
                    result.IsFemale = ffVote.IsFemale;
                    result.Confidence = ffVote.Confidence;
                    result.Decision = $"FairFaceHighConf({ffVote.Confidence:F2})→{(result.IsFemale ? "F" : "M")}";
                    SubModule.Log($"  Ensemble: TRUSTING HIGH-CONFIDENCE FairFace ({ffVote.Confidence:F2})");
                    return result;
                }
                
                var nonFfVotes = votes.Where(v => v.Source != "FairFace").ToList();
                if (nonFfVotes.Count >= 2)
                {
                    bool consensusFemale = nonFfVotes[0].IsFemale;
                    int agreeing = nonFfVotes.Count(v => v.IsFemale == consensusFemale);
                    
                    // If all non-FairFace methods agree AND FairFace disagrees
                    if (agreeing == nonFfVotes.Count && ffVote.IsFemale != consensusFemale)
                    {
                        // Calculate combined confidence of agreeing methods
                        float consensusConf = (float)nonFfVotes.Average(v => v.Confidence);
                        float maxConsensusConf = nonFfVotes.Max(v => v.Confidence);
                        
                        // FIX: Require STRONGER evidence to override FairFace
                        // - At least one signal must be >= 0.50 confidence
                        // - OR 3+ signals with average >= 0.40
                        // - AND FairFace must be genuinely uncertain (< 0.60)
                        bool hasStrongSignal = maxConsensusConf >= 0.50f;
                        bool hasGoodConsensus = agreeing >= 3 && consensusConf >= 0.40f;
                        bool ffGenuinelyUncertain = ffVote.Confidence < 0.60f;
                        
                        if ((hasStrongSignal || hasGoodConsensus) && ffGenuinelyUncertain)
                        {
                            result.IsFemale = consensusFemale;
                            result.Confidence = Math.Min(0.7f, consensusConf + 0.10f);
                            result.Decision = $"ConsensusOverride({agreeing}vs1)→{(result.IsFemale ? "F" : "M")}";
                            SubModule.Log($"  Ensemble: CONSENSUS OVERRIDE - {agreeing} methods (maxConf={maxConsensusConf:F2}) against uncertain FairFace ({ffVote.Confidence:F2})");
                            return result;
                        }
                        else
                        {
                            // Consensus exists but isn't strong enough - trust FairFace
                            SubModule.Log($"  Ensemble: Consensus too weak (avg={consensusConf:F2}, max={maxConsensusConf:F2}) to override FairFace ({ffVote.Confidence:F2})");
                        }
                    }
                }
            }
            
            // === SPECIAL CASE: All votes agree ===
            bool allAgree = votes.All(v => v.IsFemale == votes[0].IsFemale);
            if (allAgree && votes.Count >= 2)
            {
                result.IsFemale = votes[0].IsFemale;
                float avgConf = votes.Average(v => v.Confidence);
                result.Confidence = Math.Min(0.95f, avgConf + 0.15f);  // Boost for agreement
                result.Decision = $"AllAgree({votes.Count})→{(result.IsFemale ? "F" : "M")}";
                return result;
            }
            
            // === WEIGHTED VOTING ===
            float femaleScore = 0;
            float maleScore = 0;
            float totalWeight = 0;
            
            foreach (var vote in votes)
            {
                float voteWeight = vote.Weight * vote.Confidence;
                if (vote.IsFemale)
                    femaleScore += voteWeight;
                else
                    maleScore += voteWeight;
                totalWeight += voteWeight;
            }
            
            if (totalWeight < 0.01f)
            {
                result.IsFemale = false;
                result.Confidence = 0.1f;
                result.Decision = "LowWeight→DefaultMale";
                return result;
            }
            
            float femaleProbability = femaleScore / totalWeight;
            result.IsFemale = femaleProbability > 0.5f;
            
            // Confidence is how far from 50/50 we are
            result.Confidence = Math.Abs(femaleProbability - 0.5f) * 2f;
            
            // Describe the decision
            string voteStr = string.Join(", ", votes.Select(v => v.ToString()));
            result.Decision = $"Voted({voteStr})→{(result.IsFemale ? "F" : "M")}({femaleProbability:F2})";
            
            return result;
        }
        
        /// <summary>
        /// Analyze skin smoothness and lip color for gender hints
        /// </summary>
        private (float score, bool femaleHint) AnalyzeSkinAndColors(string imagePath)
        {
            float score = 0;
            
            using (var bmp = new Bitmap(imagePath))
            {
                int w = bmp.Width;
                int h = bmp.Height;
                
                // Sample lip region for red/pink tones (makeup indicator)
                int lipY = h * 65 / 100;
                int lipH = h * 15 / 100;
                int lipX = w * 30 / 100;
                int lipW = w * 40 / 100;
                
                float redCount = 0;
                float total = 0;
                
                for (int y = lipY; y < lipY + lipH && y < h; y += 2)
                {
                    for (int x = lipX; x < lipX + lipW && x < w; x += 2)
                    {
                        var p = bmp.GetPixel(x, y);
                        total++;
                        
                        // Red/pink lips indicate makeup
                        float redRatio = p.R / (float)(p.G + p.B + 1);
                        if (redRatio > 0.7f && p.R > 100)
                        {
                            redCount++;
                        }
                    }
                }
                
                float redLipRatio = total > 0 ? redCount / total : 0;
                if (redLipRatio > 0.05f)
                {
                    score += 0.4f;  // Red lips = likely female (makeup)
                }
            }
            
            return (score, score > 0);
        }
        
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
