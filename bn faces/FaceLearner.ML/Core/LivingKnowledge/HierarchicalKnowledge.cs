using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FaceLearner.ML.Core.HierarchicalScoring;  // v3.0.20: For MorphGroups

namespace FaceLearner.Core.LivingKnowledge
{
    // NOTE: Related types extracted to separate files (Clean Code v2.8.0):
    // - FeatureCategory enum → FeatureCategory.cs
    // - KnowledgeNode class → KnowledgeNode.cs  
    // - SharedFeatureEntry class → SharedFeatureEntry.cs
    
    /// <summary>
    /// Hierarchical knowledge tree that learns compositional face morphing rules.
    /// 
    /// ARCHITECTURE:
    /// - Layer 1: SharedFeatureKnowledge (feature-independent base morphs)
    /// - Layer 2: HierarchicalTree (contextual adjustments)
    /// </summary>
    public class HierarchicalKnowledge
    {
        #region Fields
        
        private KnowledgeNode _root;
        private string _savePath;
        private int _totalExperiments;
        private int _debugClassificationCounter = 0;
        
        #endregion
        
        #region Properties
        
        public int TotalExperiments => _totalExperiments;
        public bool IsEmpty => _totalExperiments == 0 && _root.Children.Count == 0;
        
        #endregion
        
        #region Shared Knowledge (Layer 1)
        
        private Dictionary<string, SharedFeatureEntry> _sharedKnowledge = new Dictionary<string, SharedFeatureEntry>();
        private Dictionary<string, KnowledgeNode> _featureProblemBranches = new Dictionary<string, KnowledgeNode>();
        
        #endregion
        
        #region Feature Priority Configuration
        
        private static readonly FeatureCategory[] FeaturePriority = new[]
        {
            FeatureCategory.Gender,
            FeatureCategory.SmileLevel,
            FeatureCategory.FaceWidth,
            FeatureCategory.FaceShape,
            FeatureCategory.AgeGroup,
            FeatureCategory.JawShape,
            FeatureCategory.FaceLength,
            FeatureCategory.NoseWidth,
            FeatureCategory.NoseLength,
            FeatureCategory.MouthWidth,
            FeatureCategory.LipFullness,
            FeatureCategory.EyeSize,
            FeatureCategory.EyeSpacing,
            FeatureCategory.EyeShape,
            FeatureCategory.CheekFullness,
            FeatureCategory.CheekboneProminence,
        };
        
        // v3.0.22: CRITICAL FIX - indices were completely wrong! Eyes pointed to Nose morphs,
        // Nose pointed to Eye morphs, Jaw pointed to Face morphs, etc.
        // Now aligned with MorphGroups.cs and FeatureMorphLearning.cs ranges.
        private static readonly Dictionary<string, int[]> FeatureMorphGroups = new Dictionary<string, int[]>
        {
            { "Face", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 } },  // UI_Face (GroupId 1)
            { "Brows", new[] { 14, 15, 16, 17 } },                                  // Eyebrow morphs
            { "Eyes", new[] { 18, 19, 20, 21, 22, 23, 24, 25, 26, 27 } },           // Eye morphs
            { "Nose", new[] { 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39 } },  // Nose morphs (GroupId 3)
            { "Mouth", new[] { 40, 41, 42, 43, 44, 45, 46, 47 } },                  // Mouth/Lip morphs
            { "Jaw", new[] { 48, 49, 50 } },                                         // Jaw morphs (jaw_line, neck_slope, jaw_height)
            { "Chin", new[] { 51, 52, 53 } },                                        // Chin morphs (chin_forward, chin_shape, chin_length)
        };
        
        #endregion
        
        #region Constructor
        
        public HierarchicalKnowledge(string savePath)
        {
            _savePath = savePath;
            _root = new KnowledgeNode { Path = "ROOT", Value = "ROOT" };
            
            if (File.Exists(savePath))
                Load();
        }
        
        #endregion
        public Dictionary<FeatureCategory, string> ClassifyFace(float[] landmarks, Dictionary<string, float> metadata = null)
        {
            var result = new Dictionary<FeatureCategory, string>();
            
            // Use FaceTypeAnalyzer ratios if available
            var ratios = LandmarkUtils.CalculateFaceShapeRatios(landmarks);
            // ratios is a 41-element array with various facial proportions
            if (ratios == null || ratios.Length < 41) return result;
            
            // === SMILE DETECTION ===
            var smileInfo = LandmarkUtils.DetectSmile(landmarks);
            if (smileInfo.IsBigSmile)
                result[FeatureCategory.SmileLevel] = "BigSmile";
            else if (smileInfo.IsSmiling)
                result[FeatureCategory.SmileLevel] = "Smile";
            else
                result[FeatureCategory.SmileLevel] = "None";
            
            // === GENDER (from metadata) ===
            if (metadata != null && metadata.TryGetValue("gender", out float genderVal))
            {
                result[FeatureCategory.Gender] = genderVal > 0.5f ? "Female" : "Male";
            }
            
            // === AGE GROUP (from metadata) ===
            if (metadata != null && metadata.TryGetValue("age", out float age))
            {
                if (age < 30) result[FeatureCategory.AgeGroup] = "Young";
                else if (age < 50) result[FeatureCategory.AgeGroup] = "Middle";
                else result[FeatureCategory.AgeGroup] = "Mature";
            }
            
            // ========================================
            // FACE DIMENSIONS
            // v2.7.3: Thresholds recalibrated based on actual data analysis
            // Data shows: hRatio range 0.90-1.42, avg 1.16
            // ========================================
            
            // === FACE WIDTH (ratios[1]) ===
            // ratios[1] = faceHeight / faceWidth
            // Higher = taller/narrower face, lower = shorter/wider face
            // OLD: >1.08 Narrow, <0.92 Wide (too tight!)
            // NEW: Based on p33=1.12, p66=1.22
            float heightRatio = ratios[1];
            if (heightRatio > 1.25f) result[FeatureCategory.FaceWidth] = "Narrow";
            else if (heightRatio < 1.05f) result[FeatureCategory.FaceWidth] = "Wide";
            else result[FeatureCategory.FaceWidth] = "Medium";
            
            // === FACE LENGTH (ratios[2]) === 
            float fullHeightRatio = ratios[2];
            if (fullHeightRatio > 1.35f) result[FeatureCategory.FaceLength] = "Long";
            else if (fullHeightRatio < 1.10f) result[FeatureCategory.FaceLength] = "Short";
            else result[FeatureCategory.FaceLength] = "Medium";
            
            // === FACE SHAPE (Round, Oval, Angular) ===
            // v2.7.3: Improved thresholds based on actual data
            // segVar range: 0.033-0.056 → p33=0.040, p66=0.050
            // heightRatio range: 0.96-1.28 → p33=1.12, p66=1.22
            float[] jawSegments = { ratios[5], ratios[6], ratios[7], ratios[8], ratios[9], ratios[10] };
            float avgSegment = jawSegments.Average();
            float segmentVariance = (float)jawSegments.Select(s => (s - avgSegment) * (s - avgSegment)).Average();
            
            float lowerThird = ratios[40];
            
            // Round: compact face, uniform jaw (LOW variance), large lower third
            // Angular: HIGH variance in jaw segments, sharp transitions
            // Oval: balanced
            // v2.7.2: isRound segVar < 0.035, isAngular > 0.055 → 100% Angular!
            // v2.7.3: Adjusted for actual distribution
            bool isRound = heightRatio < 1.12f && segmentVariance < 0.042f && lowerThird > 0.35f;
            bool isAngular = segmentVariance > 0.050f || heightRatio > 1.25f;
            
            if (isRound) 
                result[FeatureCategory.FaceShape] = "Round";
            else if (isAngular) 
                result[FeatureCategory.FaceShape] = "Angular";
            else 
                result[FeatureCategory.FaceShape] = "Oval";
            
            // === JAW SHAPE (ratios[11-16]) ===
            // v3.0.18: COMPLETELY REWRITTEN based on actual data analysis
            // The old formula was WRONG - it always produced "Pointed"!
            // 
            // OLD (broken): pointedness = jawTaper / chinWidth → always > 1.2
            // NEW: Use chin angle directly from jaw contour landmarks
            //
            // Key insight: Round jaw has SMALL angle at chin (landmarks taper gently)
            //              Pointed jaw has SHARP angle at chin
            //              Square jaw has WIDE angle at chin (almost flat)
            
            // Method 1: Use jawDrop ratio - how much jaw "drops" from widest point to chin
            // Round face: gentle drop, Square face: minimal drop, Pointed: steep drop
            float jawDropL = Math.Abs(ratios[13]);  // Left side drop
            float jawDropR = Math.Abs(ratios[14]);  // Right side drop
            float avgJawDrop = (jawDropL + jawDropR) / 2f;
            
            // Method 2: Compare chin width to mid-jaw width
            float chinW = ratios[15] + ratios[16];  // Combined chin width
            float midJawW = ratios[7] + ratios[8];  // Mid-jaw segments
            float taperRatio = chinW / (midJawW + 0.01f);  // <1 = tapered, >1 = square
            
            // Classification:
            // Pointed: Steep drop (>0.4) AND narrow chin relative to jaw
            // Square: Minimal drop (<0.2) OR wide chin relative to jaw
            // Round: Everything else (moderate drop, moderate taper)
            if (avgJawDrop > 0.40f && taperRatio < 0.9f) 
                result[FeatureCategory.JawShape] = "Pointed";
            else if (avgJawDrop < 0.20f || taperRatio > 1.2f) 
                result[FeatureCategory.JawShape] = "Square";
            else 
                result[FeatureCategory.JawShape] = "Round";
            
            // ========================================
            // NOSE
            // v2.7.3: Fixed landmark indices - values will be different now!
            // ========================================
            
            // === NOSE WIDTH (ratios[30]) ===
            // v2.7.3: Now using correct landmarks (48, 278) for nostril width
            // Expected range after fix: ~0.15-0.35 relative to face width
            float noseWidthRatio = ratios[30];
            if (noseWidthRatio < 0.20f) result[FeatureCategory.NoseWidth] = "Narrow";
            else if (noseWidthRatio > 0.30f) result[FeatureCategory.NoseWidth] = "Wide";
            else result[FeatureCategory.NoseWidth] = "Medium";
            
            // === NOSE LENGTH (ratios[33]) ===
            // Data shows: range 0.22-0.34
            float noseLengthRatio = ratios[33];
            if (noseLengthRatio < 0.24f) result[FeatureCategory.NoseLength] = "Short";
            else if (noseLengthRatio > 0.30f) result[FeatureCategory.NoseLength] = "Long";
            else result[FeatureCategory.NoseLength] = "Medium";
            
            // ========================================
            // MOUTH & LIPS
            // Data: mouthW 0.29-0.53, lip 0.23-0.49
            // ========================================
            
            // === MOUTH WIDTH (ratios[34]) ===
            float mouthWidthRatio = ratios[34];
            if (mouthWidthRatio < 0.36f) result[FeatureCategory.MouthWidth] = "Narrow";
            else if (mouthWidthRatio > 0.45f) result[FeatureCategory.MouthWidth] = "Wide";
            else result[FeatureCategory.MouthWidth] = "Medium";
            
            // === LIP FULLNESS (ratios[35] + ratios[38]) ===
            // v3.0.18: FIXED - old thresholds were wrong!
            // Data shows: lipScore typically 0.05-0.20 range, NOT 0.28-0.40!
            // 
            // ratios[35] = mouthH / mouthW (mouth aspect ratio)
            // ratios[38] = upperLipThickness / mouthH
            float mouthAspect = ratios[35];
            float lipThickness = ratios[38];
            float lipScore = mouthAspect * 0.5f + lipThickness * 0.5f;
            
            // NEW thresholds based on actual data (0.05-0.20 range):
            if (lipScore < 0.08f) result[FeatureCategory.LipFullness] = "Thin";
            else if (lipScore > 0.15f) result[FeatureCategory.LipFullness] = "Full";
            else result[FeatureCategory.LipFullness] = "Medium";
            
            // ========================================
            // EYES
            // Data: eyeW 0.19-0.23 (very narrow range!), eyeSep 0.22-0.31, eyeAsp 0.21-0.41
            // ========================================
            
            // === EYE SIZE (ratios[18]+[19]) ===
            // v3.0.18: FIXED - thresholds were WAY too tight!
            // Data shows eyeW range: 0.19-0.23
            // OLD: <0.200 Small, >0.210 Large → Only 0.01 difference = always Medium!
            // NEW: Wider bands for actual variation
            float avgEyeWidth = (ratios[18] + ratios[19]) / 2f;
            if (avgEyeWidth < 0.195f) result[FeatureCategory.EyeSize] = "Small";
            else if (avgEyeWidth > 0.220f) result[FeatureCategory.EyeSize] = "Large";
            else result[FeatureCategory.EyeSize] = "Medium";
            
            // === EYE SPACING (ratios[22]) ===
            // v3.0.18: FIXED - thresholds were too tight!
            // Data shows: range 0.22-0.31
            // OLD: <0.25 Close, >0.28 Wide → Only 0.03 difference!
            // NEW: Wider bands based on actual distribution
            float eyeSeparation = ratios[22];
            if (eyeSeparation < 0.24f) result[FeatureCategory.EyeSpacing] = "Close";
            else if (eyeSeparation > 0.29f) result[FeatureCategory.EyeSpacing] = "Wide";
            else result[FeatureCategory.EyeSpacing] = "Medium";
            
            // === EYE SHAPE (ratios[20]+[21]) ===
            // v3.0.18: FIXED - thresholds were too tight!
            // Data shows: avgEyeAspect typically 0.21-0.41 range
            // 0.26 threshold was producing too many "Hooded"
            float avgEyeAspect = (ratios[20] + ratios[21]) / 2f;
            
            // NEW thresholds for better distribution:
            // Round eyes: very tall aspect (>0.38)
            // Hooded eyes: very flat aspect (<0.23)
            // Almond: everything in between
            if (avgEyeAspect > 0.38f) result[FeatureCategory.EyeShape] = "Round";
            else if (avgEyeAspect < 0.23f) result[FeatureCategory.EyeShape] = "Hooded";
            else result[FeatureCategory.EyeShape] = "Almond";
            
            // ========================================
            // CHEEKS
            // Data: cheek ratio 0.19-0.48
            // ========================================
            
            // === CHEEK FULLNESS ===
            // v2.7.3: Fixed thresholds based on actual data
            // Data shows cheekRatio range: 0.19-0.30
            // v2.7.2 WRONG: >1.4 Full, <0.9 Hollow → 100% Hollow!
            // v2.7.3 CORRECT: Based on actual range
            float upperJawWidth = ratios[5] + ratios[6];
            float lowerJawWidth = ratios[9] + ratios[10];
            float cheekRatio = upperJawWidth / (lowerJawWidth + 0.01f);
            
            if (cheekRatio > 0.27f) result[FeatureCategory.CheekFullness] = "Full";
            else if (cheekRatio < 0.21f) result[FeatureCategory.CheekFullness] = "Hollow";
            else result[FeatureCategory.CheekFullness] = "Medium";
            
            // === CHEEKBONE PROMINENCE ===
            float middleThird = ratios[39];
            if (middleThird > 0.38f) result[FeatureCategory.CheekboneProminence] = "High";
            else if (middleThird < 0.30f) result[FeatureCategory.CheekboneProminence] = "Flat";
            else result[FeatureCategory.CheekboneProminence] = "Medium";
            
            // ========================================
            // DEBUG LOGGING (every 50 faces)
            // ========================================
            if (_debugClassificationCounter++ % 50 == 0)
            {
                SubModule.Log($"  [DEBUG] hRatio={heightRatio:F2} fullH={fullHeightRatio:F2} segVar={segmentVariance:F5} lowerThird={lowerThird:F2}");
                SubModule.Log($"  [DEBUG] noseW={noseWidthRatio:F2} noseL={noseLengthRatio:F2} mouthW={mouthWidthRatio:F2} lip={lipScore:F2}");
                SubModule.Log($"  [DEBUG] eyeW={avgEyeWidth:F2} eyeSep={eyeSeparation:F2} eyeAsp={avgEyeAspect:F2} cheek={cheekRatio:F2}");
                SubModule.Log($"  [DEBUG] jawDrop={avgJawDrop:F2} taperRatio={taperRatio:F2} chinW={chinW:F2} midThird={middleThird:F2}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Get the morph starting point using THREE-LAYER system:
        /// 1. Get BASE morphs from shared feature knowledge
        /// 2. Add CONTEXTUAL adjustments from hierarchical tree
        /// 3. FALLBACK: Use feature-level knowledge if tree is empty
        /// </summary>
        public float[] GetStartingMorphs(float[] targetLandmarks, Dictionary<string, float> metadata, int numMorphs)
        {
            var features = ClassifyFace(targetLandmarks, metadata);
            var morphs = new float[numMorphs];
            
            // Start from neutral
            for (int i = 0; i < numMorphs; i++)
                morphs[i] = 0f;
            
            // === LAYER 1: Apply Shared Feature Knowledge ===
            int sharedCount = 0;
            foreach (var feature in features)
            {
                string featureKey = $"{feature.Key}:{feature.Value}";
                if (_sharedKnowledge.TryGetValue(featureKey, out var shared) && shared.Confidence > 0.3f)
                {
                    foreach (var kv in shared.BaseMorphs)
                    {
                        if (kv.Key >= 0 && kv.Key < numMorphs)
                        {
                            morphs[kv.Key] += kv.Value;
                        }
                    }
                    sharedCount++;
                }
            }
            
            // === LAYER 2: Add Contextual Adjustments from Tree ===
            var contextPath = new List<string>();
            TraverseAndAccumulate(_root, features, morphs, contextPath, 0);
            
            // === LAYER 3: FALLBACK - Use Feature-Level Knowledge ===
            // v3.0.20: If tree/shared is empty, try feature-level knowledge
            if (sharedCount == 0 && contextPath.Count == 0 && _featureLevelKnowledge.Count > 0)
            {
                SubModule.Log($"  HierarchicalKnowledge: Tree empty, trying feature-level fallback");
                
                // Try to get morphs for each phase
                var phases = new[] { 
                    SubPhase.FaceWidth, SubPhase.FaceShape, SubPhase.Jaw, SubPhase.Chin,
                    SubPhase.Nose, SubPhase.Eyes, SubPhase.Mouth, SubPhase.Eyebrows 
                };
                
                int featureLevelCount = 0;
                foreach (var phase in phases)
                {
                    var phaseMorphs = GetFeatureLevelMorphs(phase, features, numMorphs);
                    if (phaseMorphs != null)
                    {
                        // Get relevant morph indices for this phase
                        int[] indices = phase switch
                        {
                            SubPhase.FaceWidth => MorphGroups.FaceWidth,
                            SubPhase.FaceShape => MorphGroups.FaceShape,
                            SubPhase.Jaw => MorphGroups.Jaw,
                            SubPhase.Chin => MorphGroups.Chin,
                            SubPhase.Nose => MorphGroups.Nose,
                            SubPhase.Eyes => MorphGroups.Eyes,
                            SubPhase.Mouth => MorphGroups.Mouth,
                            SubPhase.Eyebrows => MorphGroups.Eyebrows,
                            _ => null
                        };
                        
                        if (indices != null)
                        {
                            foreach (int idx in indices)
                            {
                                if (idx >= 0 && idx < numMorphs)
                                {
                                    // Blend feature-level knowledge
                                    float featureVal = phaseMorphs[idx] - 0.5f;
                                    morphs[idx] += featureVal;
                                }
                            }
                            featureLevelCount++;
                        }
                    }
                }
                
                if (featureLevelCount > 0)
                {
                    SubModule.Log($"    Feature-level: {featureLevelCount} phases had knowledge");
                }
            }
            
            // Log what we used
            if (sharedCount > 0 || contextPath.Count > 0)
            {
                SubModule.Log($"  HierarchicalKnowledge: {sharedCount} shared features + {contextPath.Count} contextual adjustments");
                if (contextPath.Count > 0)
                {
                    SubModule.Log($"    Context path: {string.Join(" → ", contextPath)}");
                }
            }
            
            return morphs;
        }
        
        private void TraverseAndAccumulate(
            KnowledgeNode node, 
            Dictionary<FeatureCategory, string> features,
            float[] morphs,
            List<string> path,
            int depth)
        {
            if (node == null || depth >= FeaturePriority.Length) return;
            
            // Apply this node's CONTEXTUAL deltas (if confident enough)
            // NOTE: These are now contextual adjustments, not full deltas!
            if (node.ConfidenceScore > 0.3f && node.UseCount > 5)
            {
                foreach (var delta in node.MorphDeltas)
                {
                    if (delta.Key >= 0 && delta.Key < morphs.Length)
                    {
                        morphs[delta.Key] += delta.Value;
                    }
                }
                if (node.Path != "ROOT" && node.MorphDeltas.Count > 0)
                    path.Add(node.Value);
            }
            
            // Find matching child for next feature level
            var nextFeature = FeaturePriority[depth];
            if (features.TryGetValue(nextFeature, out string featureValue))
            {
                var matchingChild = node.Children.FirstOrDefault(c => 
                    c.Feature == nextFeature && c.Value == featureValue);
                
                if (matchingChild != null)
                {
                    TraverseAndAccumulate(matchingChild, features, morphs, path, depth + 1);
                }
            }
        }
        
        /// <summary>
        /// Learn from a successful morph configuration.
        /// Uses TWO-LAYER system:
        /// 1. Update shared feature knowledge (base morphs for each feature value)
        /// 2. Store only CONTEXTUAL DELTAS in tree (what's different from shared base)
        /// </summary>
        public void LearnFrom(
            float[] targetLandmarks, 
            Dictionary<string, float> metadata,
            float[] startingMorphs,
            float[] finalMorphs,
            float improvement)
        {
            if (improvement <= 0) return;  // Only learn from improvements
            
            var features = ClassifyFace(targetLandmarks, metadata);
            
            // Calculate what changed
            var deltas = new Dictionary<int, float>();
            for (int i = 0; i < Math.Min(startingMorphs.Length, finalMorphs.Length); i++)
            {
                float delta = finalMorphs[i] - startingMorphs[i];
                if (Math.Abs(delta) > 0.01f)  // Significant change
                {
                    deltas[i] = delta;
                }
            }
            
            // ALWAYS increment experiment count - we learned SOMETHING
            // (either that changes improved, or that starting was already good)
            _totalExperiments++;
            
            if (deltas.Count == 0)
            {
                // No morph changes, but this is still useful information!
                // The starting morphs worked well for this face type.
                // Just update usage stats without adding deltas.
                UpdatePathStatsOnly(_root, features, improvement, 0);
                PerformMaintenance();
                return;
            }
            
            // Build context path for deduplication tracking
            string contextPath = string.Join("/", features.OrderBy(f => f.Key).Select(f => $"{f.Key}:{f.Value}"));
            
            // === LAYER 1: Update Shared Feature Knowledge ===
            // Each feature:value pair gets updated with this observation
            var remainingDeltas = new Dictionary<int, float>(deltas);
            
            foreach (var feature in features)
            {
                string featureKey = $"{feature.Key}:{feature.Value}";
                
                if (!_sharedKnowledge.ContainsKey(featureKey))
                {
                    _sharedKnowledge[featureKey] = new SharedFeatureEntry { FeatureKey = featureKey };
                }
                
                // Update shared knowledge with this observation
                _sharedKnowledge[featureKey].UpdateFromObservation(deltas, contextPath);
            }
            
            // === LAYER 2: Calculate Contextual Deltas ===
            // What's different from just using shared knowledge?
            var contextualDeltas = CalculateContextualDeltas(features, deltas);
            
            // Only pass contextual differences to tree (not full deltas!)
            if (contextualDeltas.Count > 0)
            {
                UpdatePath(_root, features, contextualDeltas, improvement, 0);
            }
            else
            {
                // Still update usage stats even if no contextual deltas
                UpdatePathStatsOnly(_root, features, improvement, 0);
            }
            
            // Periodic self-organization
            PerformMaintenance();
        }
        
        /// <summary>
        /// v3.0.20: FEATURE-LEVEL LEARNING
        /// Learn from a specific feature even if total score is bad.
        /// E.g., if nose matched well (score 0.9) but total was bad (0.3),
        /// we still want to learn the nose morphs!
        /// </summary>
        public void LearnFeature(
            SubPhase phase,
            float[] targetLandmarks,
            Dictionary<string, float> metadata,
            float[] morphs,
            int[] relevantMorphIndices,
            float featureScore)
        {
            if (featureScore < 0.5f) return;  // Still need minimum threshold
            if (morphs == null || relevantMorphIndices == null) return;
            
            var features = ClassifyFace(targetLandmarks, metadata);
            if (features == null || features.Count == 0) return;
            
            // Build feature key for this phase
            string phaseKey = $"Phase:{phase}";
            
            // Extract just the relevant morphs
            var morphDeltas = new Dictionary<int, float>();
            foreach (int idx in relevantMorphIndices)
            {
                if (idx >= 0 && idx < morphs.Length)
                {
                    float val = morphs[idx] - 0.5f;  // Delta from neutral
                    if (Math.Abs(val) > 0.01f)
                    {
                        morphDeltas[idx] = val;
                    }
                }
            }
            
            if (morphDeltas.Count == 0) return;
            
            // Store in feature-specific knowledge
            // Key format: "Phase:Nose|JawShape:Round|FaceShape:Oval"
            var relevantFeatures = new List<string> { phaseKey };
            
            // Add related features based on phase
            switch (phase)
            {
                case SubPhase.Jaw:
                case SubPhase.Chin:
                    if (features.TryGetValue(FeatureCategory.JawShape, out var jaw))
                        relevantFeatures.Add($"JawShape:{jaw}");
                    if (features.TryGetValue(FeatureCategory.FaceShape, out var face))
                        relevantFeatures.Add($"FaceShape:{face}");
                    break;
                    
                case SubPhase.Nose:
                    if (features.TryGetValue(FeatureCategory.NoseWidth, out var nw))
                        relevantFeatures.Add($"NoseWidth:{nw}");
                    if (features.TryGetValue(FeatureCategory.NoseLength, out var nl))
                        relevantFeatures.Add($"NoseLength:{nl}");
                    break;
                    
                case SubPhase.Eyes:
                    if (features.TryGetValue(FeatureCategory.EyeShape, out var es))
                        relevantFeatures.Add($"EyeShape:{es}");
                    if (features.TryGetValue(FeatureCategory.EyeSize, out var ez))
                        relevantFeatures.Add($"EyeSize:{ez}");
                    break;
                    
                case SubPhase.Mouth:
                    if (features.TryGetValue(FeatureCategory.MouthWidth, out var mw))
                        relevantFeatures.Add($"MouthWidth:{mw}");
                    if (features.TryGetValue(FeatureCategory.LipFullness, out var lf))
                        relevantFeatures.Add($"LipFullness:{lf}");
                    break;
            }
            
            // Add gender if available
            if (features.TryGetValue(FeatureCategory.Gender, out var gender))
                relevantFeatures.Add($"Gender:{gender}");
            
            // Build combined key
            string featureKey = string.Join("|", relevantFeatures);
            
            // Update shared knowledge for this feature combination
            if (!_featureLevelKnowledge.ContainsKey(featureKey))
            {
                _featureLevelKnowledge[featureKey] = new FeatureLevelEntry { Key = featureKey };
            }
            
            _featureLevelKnowledge[featureKey].AddObservation(morphDeltas, featureScore);
            _totalExperiments++;
        }
        
        // Storage for feature-level knowledge
        private Dictionary<string, FeatureLevelEntry> _featureLevelKnowledge = new Dictionary<string, FeatureLevelEntry>();
        
        /// <summary>
        /// Get feature-level starting morphs for a specific phase.
        /// Used as fallback when tree is empty.
        /// </summary>
        public float[] GetFeatureLevelMorphs(SubPhase phase, Dictionary<FeatureCategory, string> features, int numMorphs)
        {
            if (_featureLevelKnowledge.Count == 0) return null;
            
            var morphs = new float[numMorphs];
            for (int i = 0; i < numMorphs; i++) morphs[i] = 0.5f;  // Start neutral
            
            // Build possible keys to look up
            string phaseKey = $"Phase:{phase}";
            
            // Find matching entries
            float totalWeight = 0f;
            foreach (var kvp in _featureLevelKnowledge)
            {
                if (!kvp.Key.StartsWith(phaseKey)) continue;
                
                // Check if features match
                bool matches = true;
                foreach (var feature in features)
                {
                    string check = $"{feature.Key}:{feature.Value}";
                    if (kvp.Key.Contains(check) || !kvp.Key.Contains(feature.Key.ToString()))
                    {
                        // Either matches or feature not in key (acceptable)
                    }
                    else
                    {
                        matches = false;
                        break;
                    }
                }
                
                if (matches)
                {
                    var entry = kvp.Value;
                    float weight = entry.TotalScore / entry.Count;
                    
                    foreach (var morph in entry.AverageMorphs)
                    {
                        if (morph.Key >= 0 && morph.Key < numMorphs)
                        {
                            morphs[morph.Key] += morph.Value * weight;
                        }
                    }
                    totalWeight += weight;
                }
            }
            
            // Normalize
            if (totalWeight > 0)
            {
                for (int i = 0; i < numMorphs; i++)
                {
                    morphs[i] = 0.5f + (morphs[i] - 0.5f) / totalWeight;
                    morphs[i] = Math.Max(0f, Math.Min(1f, morphs[i]));
                }
                return morphs;
            }
            
            return null;
        }
        
        /// <summary>
        /// Entry for feature-level knowledge storage.
        /// </summary>
        private class FeatureLevelEntry
        {
            public string Key { get; set; }
            public int Count { get; set; }
            public float TotalScore { get; set; }
            public Dictionary<int, float> SumMorphs { get; set; } = new Dictionary<int, float>();
            
            public Dictionary<int, float> AverageMorphs
            {
                get
                {
                    if (Count == 0) return new Dictionary<int, float>();
                    return SumMorphs.ToDictionary(kv => kv.Key, kv => kv.Value / Count);
                }
            }
            
            public void AddObservation(Dictionary<int, float> morphDeltas, float score)
            {
                Count++;
                TotalScore += score;
                
                foreach (var kv in morphDeltas)
                {
                    if (!SumMorphs.ContainsKey(kv.Key))
                        SumMorphs[kv.Key] = 0f;
                    SumMorphs[kv.Key] += kv.Value;
                }
            }
        }
        private Dictionary<int, float> CalculateContextualDeltas(
            Dictionary<FeatureCategory, string> features,
            Dictionary<int, float> observedDeltas)
        {
            // Get expected deltas from shared knowledge
            var expectedDeltas = new Dictionary<int, float>();
            
            foreach (var feature in features)
            {
                string featureKey = $"{feature.Key}:{feature.Value}";
                if (_sharedKnowledge.TryGetValue(featureKey, out var shared))
                {
                    foreach (var kv in shared.BaseMorphs)
                    {
                        if (!expectedDeltas.ContainsKey(kv.Key))
                            expectedDeltas[kv.Key] = 0f;
                        expectedDeltas[kv.Key] += kv.Value;
                    }
                }
            }
            
            // Contextual delta = observed - expected
            var contextualDeltas = new Dictionary<int, float>();
            
            foreach (var kv in observedDeltas)
            {
                float expected = expectedDeltas.ContainsKey(kv.Key) ? expectedDeltas[kv.Key] : 0f;
                float diff = kv.Value - expected;
                
                if (Math.Abs(diff) > 0.02f)  // Only significant contextual differences
                {
                    contextualDeltas[kv.Key] = diff;
                }
            }
            
            return contextualDeltas;
        }
        
        private void UpdatePath(
            KnowledgeNode node,
            Dictionary<FeatureCategory, string> features,
            Dictionary<int, float> contextualDeltas,  // Now receives CONTEXTUAL deltas, not full
            float improvement,
            int depth)
        {
            if (depth >= FeaturePriority.Length) return;
            
            var feature = FeaturePriority[depth];
            if (!features.TryGetValue(feature, out string featureValue)) return;
            
            // Find or create child node
            var child = node.Children.FirstOrDefault(c => 
                c.Feature == feature && c.Value == featureValue);
            
            if (child == null)
            {
                child = new KnowledgeNode
                {
                    Feature = feature,
                    Value = featureValue,
                    Path = node.Path == "ROOT" ? featureValue : $"{node.Path}/{featureValue}"
                };
                node.Children.Add(child);
            }
            
            // Update this node's knowledge
            child.UseCount++;
            if (improvement > 0.01f) child.SuccessCount++;
            
            // Track outcome for variance analysis and sub-pattern detection
            child.RecordOutcome(improvement, features);
            
            // Learning rate based on depth (deeper = more specific = learn faster)
            float learningRate = 0.1f + depth * 0.05f;
            learningRate *= Math.Min(1f, improvement * 2f);  // Scale by improvement
            
            // Update morph deltas with exponential moving average
            // NOTE: Now storing CONTEXTUAL deltas, not full deltas!
            foreach (var delta in contextualDeltas)
            {
                if (!child.MorphDeltas.ContainsKey(delta.Key))
                    child.MorphDeltas[delta.Key] = 0f;
                
                // Track variance per morph
                if (!child.MorphVariance.ContainsKey(delta.Key))
                    child.MorphVariance[delta.Key] = 0f;
                
                float oldDelta = child.MorphDeltas[delta.Key];
                
                // Blend new observation with existing knowledge
                child.MorphDeltas[delta.Key] = 
                    child.MorphDeltas[delta.Key] * (1 - learningRate) + 
                    delta.Value * learningRate;
                
                // Update variance (exponential moving average of squared difference)
                float diff = delta.Value - oldDelta;
                child.MorphVariance[delta.Key] = 
                    child.MorphVariance[delta.Key] * 0.9f + diff * diff * 0.1f;
            }
            
            // Update confidence based on success rate and use count
            child.ConfidenceScore = child.SuccessRate * Math.Min(1f, child.UseCount / 20f);
            child.UpdateHealth();
            
            // Recurse to more specific levels
            UpdatePath(child, features, contextualDeltas, improvement, depth + 1);
        }
        
        /// <summary>
        /// Update path statistics only (when no contextual deltas to store)
        /// </summary>
        private void UpdatePathStatsOnly(
            KnowledgeNode node,
            Dictionary<FeatureCategory, string> features,
            float improvement,
            int depth)
        {
            if (depth >= FeaturePriority.Length) return;
            
            var feature = FeaturePriority[depth];
            if (!features.TryGetValue(feature, out string featureValue)) return;
            
            var child = node.Children.FirstOrDefault(c => 
                c.Feature == feature && c.Value == featureValue);
            
            if (child != null)
            {
                child.UseCount++;
                if (improvement > 0.01f) child.SuccessCount++;
                child.RecordOutcome(improvement, features);
                child.UpdateHealth();
                
                UpdatePathStatsOnly(child, features, improvement, depth + 1);
            }
        }
        
        /// <summary>
        /// Learn from feature score improvements.
        /// When a specific feature (Nose, Jaw, etc.) improves, learn what morphs helped.
        /// This creates specialized branches for each feature problem.
        /// </summary>
        public void LearnFeatureImprovement(
            string featureName,           // "Face", "Eyes", "Nose", "Mouth", "Jaw", "Brows"
            float scoreBefore,
            float scoreAfter,
            float[] startingMorphs,
            float[] finalMorphs,
            Dictionary<string, float> metadata = null)
        {
            float improvement = scoreAfter - scoreBefore;
            if (improvement < 0.05f) return;  // Only learn from significant improvements
            
            // Classify the problem severity
            string problemLevel;
            if (scoreBefore < 0.40f) problemLevel = "Severe";
            else if (scoreBefore < 0.60f) problemLevel = "Moderate"; 
            else if (scoreBefore < 0.75f) problemLevel = "Minor";
            else return;  // Score was already good, not much to learn
            
            // Calculate which morphs changed
            var deltas = new Dictionary<int, float>();
            for (int i = 0; i < Math.Min(startingMorphs.Length, finalMorphs.Length); i++)
            {
                float delta = finalMorphs[i] - startingMorphs[i];
                if (Math.Abs(delta) > 0.02f)  // Significant change
                {
                    deltas[i] = delta;
                }
            }
            
            if (deltas.Count == 0) return;
            
            // Get or create feature problem branch
            string branchKey = $"{featureName}_{problemLevel}";
            if (!_featureProblemBranches.TryGetValue(branchKey, out var branch))
            {
                branch = new KnowledgeNode
                {
                    Path = $"FEATURE/{featureName}/{problemLevel}",
                    Value = branchKey,
                    Feature = FeatureCategory.FaceWidth  // Placeholder
                };
                _featureProblemBranches[branchKey] = branch;
            }
            
            // Update branch with new knowledge
            branch.UseCount++;
            branch.SuccessCount++;
            
            // Learning rate based on improvement magnitude
            float learningRate = 0.15f * Math.Min(2f, improvement * 3f);
            
            // Focus on morphs that are likely relevant to this feature
            int[] relevantMorphs = null;
            if (FeatureMorphGroups.TryGetValue(featureName, out relevantMorphs))
            {
                // Boost learning for relevant morphs
                foreach (var delta in deltas)
                {
                    bool isRelevant = Array.IndexOf(relevantMorphs, delta.Key) >= 0;
                    float effectiveLR = isRelevant ? learningRate * 1.5f : learningRate * 0.5f;
                    
                    if (!branch.MorphDeltas.ContainsKey(delta.Key))
                        branch.MorphDeltas[delta.Key] = 0f;
                    
                    branch.MorphDeltas[delta.Key] = 
                        branch.MorphDeltas[delta.Key] * (1 - effectiveLR) + 
                        delta.Value * effectiveLR;
                }
            }
            else
            {
                // No morph group defined, learn all deltas equally
                foreach (var delta in deltas)
                {
                    if (!branch.MorphDeltas.ContainsKey(delta.Key))
                        branch.MorphDeltas[delta.Key] = 0f;
                    
                    branch.MorphDeltas[delta.Key] = 
                        branch.MorphDeltas[delta.Key] * (1 - learningRate) + 
                        delta.Value * learningRate;
                }
            }
            
            branch.ConfidenceScore = branch.SuccessRate * Math.Min(1f, branch.UseCount / 15f);
            _totalExperiments++;
        }
        
        /// <summary>
        /// Get suggested morphs to fix a specific feature problem.
        /// </summary>
        public Dictionary<int, float> GetFeatureFix(string featureName, float currentScore)
        {
            string problemLevel;
            if (currentScore < 0.40f) problemLevel = "Severe";
            else if (currentScore < 0.60f) problemLevel = "Moderate";
            else if (currentScore < 0.75f) problemLevel = "Minor";
            else return new Dictionary<int, float>();  // No fix needed
            
            string branchKey = $"{featureName}_{problemLevel}";
            if (_featureProblemBranches.TryGetValue(branchKey, out var branch))
            {
                if (branch.ConfidenceScore > 0.3f && branch.MorphDeltas.Count > 0)
                {
                    // Return scaled deltas based on confidence
                    var result = new Dictionary<int, float>();
                    foreach (var kv in branch.MorphDeltas)
                    {
                        result[kv.Key] = kv.Value * branch.ConfidenceScore;
                    }
                    return result;
                }
            }
            
            return new Dictionary<int, float>();
        }
        
        /// <summary>
        /// Get count of feature problem branches
        /// </summary>
        public int FeatureBranchCount => _featureProblemBranches.Count;
        
        #region Self-Organization
        
        private int _maintenanceCounter = 0;
        private const int MAINTENANCE_INTERVAL = 50;  // Every 50 experiments
        
        /// <summary>
        /// Perform periodic tree maintenance: split, merge, prune, refine.
        /// Call this periodically (e.g., after each learning session).
        /// </summary>
        public void PerformMaintenance()
        {
            _maintenanceCounter++;
            if (_maintenanceCounter < MAINTENANCE_INTERVAL) return;
            _maintenanceCounter = 0;
            
            int splits = 0, merges = 0, prunes = 0;
            
            // 1. Split high-variance nodes
            splits = SplitHighVarianceNodes(_root, 0);
            
            // 2. Merge similar siblings
            merges = MergeSimilarSiblings(_root);
            
            // 3. Prune stale/unused nodes
            prunes = PruneStaleNodes(_root);
            
            if (splits > 0 || merges > 0 || prunes > 0)
            {
                SubModule.Log($"  Tree maintenance: {splits} splits, {merges} merges, {prunes} prunes");
            }
        }
        
        /// <summary>
        /// Split nodes that have high outcome variance.
        /// High variance means the node is too general - it needs sub-categories.
        /// </summary>
        private int SplitHighVarianceNodes(KnowledgeNode node, int depth)
        {
            int splitCount = 0;
            
            foreach (var child in node.Children.ToList())
            {
                // First recurse to children
                splitCount += SplitHighVarianceNodes(child, depth + 1);
                
                // Check if this node needs splitting
                if (child.NeedsSplit && child.Children.Count == 0)
                {
                    var splitCandidate = child.GetBestSplitCandidate();
                    if (splitCandidate.HasValue)
                    {
                        // Create two new child nodes based on the best split feature
                        var (featureStr, value, scoreDiff) = splitCandidate.Value;
                        
                        if (Enum.TryParse<FeatureCategory>(featureStr, out var feature))
                        {
                            // Create positive branch (has the feature)
                            var posNode = new KnowledgeNode
                            {
                                Feature = feature,
                                Value = value,
                                Path = $"{child.Path}/{value}",
                                MorphDeltas = new Dictionary<int, float>(child.MorphDeltas),
                                UseCount = child.UseCount / 3,
                                SuccessCount = child.SuccessCount / 3,
                                ConfidenceScore = child.ConfidenceScore * 0.7f
                            };
                            
                            // Create "other" branch
                            var negNode = new KnowledgeNode
                            {
                                Feature = feature,
                                Value = $"Other{value}",
                                Path = $"{child.Path}/Other{value}",
                                MorphDeltas = new Dictionary<int, float>(child.MorphDeltas),
                                UseCount = child.UseCount / 3,
                                SuccessCount = child.SuccessCount / 3,
                                ConfidenceScore = child.ConfidenceScore * 0.7f
                            };
                            
                            child.Children.Add(posNode);
                            child.Children.Add(negNode);
                            child.SplitCount++;
                            child.NeedsSplit = false;
                            child.LastRefinement = DateTime.Now;
                            
                            splitCount++;
                            SubModule.Log($"    Split: {child.Path} → +{value} (variance was {child.OutcomeVariance:F2})");
                        }
                    }
                }
            }
            
            return splitCount;
        }
        
        /// <summary>
        /// Merge sibling nodes that have very similar morph deltas.
        /// This reduces redundancy and consolidates knowledge.
        /// </summary>
        private int MergeSimilarSiblings(KnowledgeNode node)
        {
            int mergeCount = 0;
            
            // First recurse to all children
            foreach (var child in node.Children.ToList())
            {
                mergeCount += MergeSimilarSiblings(child);
            }
            
            // Check siblings for similarity
            if (node.Children.Count >= 2)
            {
                for (int i = 0; i < node.Children.Count - 1; i++)
                {
                    for (int j = i + 1; j < node.Children.Count; j++)
                    {
                        var a = node.Children[i];
                        var b = node.Children[j];
                        
                        // Skip if different features
                        if (a.Feature != b.Feature) continue;
                        
                        // Check morph delta similarity
                        float similarity = CalculateDeltaSimilarity(a.MorphDeltas, b.MorphDeltas);
                        
                        // If very similar (>90%) and both have low variance, merge
                        if (similarity > 0.90f && a.IsStable && b.IsStable)
                        {
                            // Merge B into A (weighted by use count)
                            int totalUse = a.UseCount + b.UseCount;
                            float weightA = (float)a.UseCount / totalUse;
                            float weightB = (float)b.UseCount / totalUse;
                            
                            foreach (var kv in b.MorphDeltas)
                            {
                                if (a.MorphDeltas.ContainsKey(kv.Key))
                                    a.MorphDeltas[kv.Key] = a.MorphDeltas[kv.Key] * weightA + kv.Value * weightB;
                                else
                                    a.MorphDeltas[kv.Key] = kv.Value * weightB;
                            }
                            
                            a.UseCount = totalUse;
                            a.SuccessCount += b.SuccessCount;
                            a.MergeCount++;
                            a.Value = $"{a.Value}+{b.Value}";  // Mark as merged
                            a.LastRefinement = DateTime.Now;
                            
                            // Move B's children to A
                            a.Children.AddRange(b.Children);
                            
                            // Remove B
                            node.Children.RemoveAt(j);
                            j--;
                            mergeCount++;
                            
                            SubModule.Log($"    Merge: {a.Path} absorbed {b.Value} (similarity {similarity:P0})");
                        }
                    }
                }
            }
            
            return mergeCount;
        }
        
        /// <summary>
        /// Remove stale nodes that haven't been used and have low confidence.
        /// </summary>
        private int PruneStaleNodes(KnowledgeNode node)
        {
            int pruneCount = 0;
            
            // First recurse to children (depth-first so we prune leaves first)
            foreach (var child in node.Children.ToList())
            {
                pruneCount += PruneStaleNodes(child);
            }
            
            // Prune children that are stale
            var toRemove = node.Children
                .Where(c => c.IsStale && c.ConfidenceScore < 0.2f && c.Children.Count == 0)
                .ToList();
            
            foreach (var stale in toRemove)
            {
                node.Children.Remove(stale);
                pruneCount++;
            }
            
            return pruneCount;
        }
        
        /// <summary>
        /// Calculate similarity between two morph delta dictionaries.
        /// </summary>
        private float CalculateDeltaSimilarity(Dictionary<int, float> a, Dictionary<int, float> b)
        {
            if (a.Count == 0 && b.Count == 0) return 1f;
            if (a.Count == 0 || b.Count == 0) return 0f;
            
            var allKeys = a.Keys.Union(b.Keys).ToList();
            float sumDiffSq = 0;
            float sumMag = 0;
            
            foreach (var key in allKeys)
            {
                float valA = a.ContainsKey(key) ? a[key] : 0;
                float valB = b.ContainsKey(key) ? b[key] : 0;
                sumDiffSq += (valA - valB) * (valA - valB);
                sumMag += Math.Abs(valA) + Math.Abs(valB);
            }
            
            if (sumMag < 0.01f) return 1f;
            return 1f - (float)Math.Sqrt(sumDiffSq) / sumMag;
        }
        
        #endregion
        
        /// <summary>
        /// Get a summary of the knowledge tree
        /// </summary>
        public string GetSummary()
        {
            int nodeCount = CountNodes(_root);
            int learnedNodes = CountLearnedNodes(_root);
            var (stable, highVar, stale) = GetTreeHealth(_root);
            int confidentShared = _sharedKnowledge.Count(kv => kv.Value.Confidence > 0.5f);
            return $"HierarchicalKnowledge: {_sharedKnowledge.Count} shared ({confidentShared} confident), {nodeCount} tree nodes ({stable} stable), {_totalExperiments} exp";
        }
        
        /// <summary>
        /// Count stable, high-variance, and stale nodes
        /// </summary>
        private (int stable, int highVar, int stale) GetTreeHealth(KnowledgeNode node)
        {
            int stable = node.IsStable ? 1 : 0;
            int highVar = node.IsHighVariance ? 1 : 0;
            int stale = node.IsStale ? 1 : 0;
            
            foreach (var child in node.Children)
            {
                var (s, h, st) = GetTreeHealth(child);
                stable += s;
                highVar += h;
                stale += st;
            }
            
            return (stable, highVar, stale);
        }
        
        private int CountNodes(KnowledgeNode node)
        {
            if (node == null) return 0;
            return 1 + node.Children.Sum(c => CountNodes(c));
        }
        
        private int CountLearnedNodes(KnowledgeNode node)
        {
            if (node == null) return 0;
            int count = (node.UseCount > 5 && node.ConfidenceScore > 0.3f) ? 1 : 0;
            return count + node.Children.Sum(c => CountLearnedNodes(c));
        }
        
        /// <summary>
        /// Print the tree structure for debugging
        /// </summary>
        public void PrintTree()
        {
            SubModule.Log("=== Hierarchical Knowledge Tree ===");
            PrintNode(_root, 0);
        }
        
        private void PrintNode(KnowledgeNode node, int indent)
        {
            if (node == null) return;
            
            string prefix = new string(' ', indent * 2);
            string status = node.ConfidenceScore > 0.3f ? "✓" : "○";
            SubModule.Log($"{prefix}{status} {node.Value} [n={node.UseCount}, conf={node.ConfidenceScore:F2}, deltas={node.MorphDeltas.Count}]");
            
            foreach (var child in node.Children.OrderByDescending(c => c.UseCount))
            {
                PrintNode(child, indent + 1);
            }
        }
        
        public void Save()
        {
            try
            {
                using (var writer = new BinaryWriter(File.Create(_savePath)))
                {
                    writer.Write("HKNOW04");  // Version 4 with two-layer knowledge
                    writer.Write(_totalExperiments);
                    WriteNode(writer, _root);
                    
                    // Save feature problem branches
                    writer.Write(_featureProblemBranches.Count);
                    foreach (var kvp in _featureProblemBranches)
                    {
                        writer.Write(kvp.Key);
                        WriteNode(writer, kvp.Value);
                    }
                    
                    // V4: Save shared feature knowledge
                    writer.Write(_sharedKnowledge.Count);
                    foreach (var kvp in _sharedKnowledge)
                    {
                        writer.Write(kvp.Key);  // Feature key
                        var entry = kvp.Value;
                        writer.Write(entry.LearnCount);
                        writer.Write(entry.Confidence);
                        
                        // Base morphs
                        writer.Write(entry.BaseMorphs.Count);
                        foreach (var morph in entry.BaseMorphs)
                        {
                            writer.Write(morph.Key);
                            writer.Write(morph.Value);
                        }
                        
                        // Learned contexts (limited)
                        int contextCount = Math.Min(entry.LearnedContexts.Count, 20);
                        writer.Write(contextCount);
                        foreach (var context in entry.LearnedContexts.Take(20))
                        {
                            writer.Write(context);
                        }
                    }
                }
                var (stable, highVar, stale) = GetTreeHealth(_root);
                SubModule.Log($"HierarchicalKnowledge: Saved ({CountNodes(_root)} nodes, {_sharedKnowledge.Count} shared, {stable} stable)");
            }
            catch (Exception ex)
            {
                SubModule.Log($"HierarchicalKnowledge save error: {ex.Message}");
            }
        }
        
        private void WriteNode(BinaryWriter writer, KnowledgeNode node)
        {
            writer.Write(node.Path ?? "");
            writer.Write((int)node.Feature);
            writer.Write(node.Value ?? "");
            writer.Write(node.UseCount);
            writer.Write(node.SuccessCount);
            writer.Write(node.ConfidenceScore);
            
            // MorphDeltas
            writer.Write(node.MorphDeltas.Count);
            foreach (var kvp in node.MorphDeltas)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
            
            // V3: MorphVariance
            writer.Write(node.MorphVariance?.Count ?? 0);
            if (node.MorphVariance != null)
            {
                foreach (var kvp in node.MorphVariance)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
            }
            
            // V3: Self-organization metrics
            writer.Write(node.OutcomeVariance);
            writer.Write(node.Health);
            writer.Write(node.SplitCount);
            writer.Write(node.MergeCount);
            writer.Write(node.LastUsed.Ticks);
            writer.Write(node.LastRefinement.Ticks);
            
            // V3: Recent outcomes (for variance calculation on load)
            writer.Write(node.RecentOutcomes?.Count ?? 0);
            if (node.RecentOutcomes != null)
            {
                foreach (var outcome in node.RecentOutcomes)
                    writer.Write(outcome);
            }
            
            // Children
            writer.Write(node.Children.Count);
            foreach (var child in node.Children)
            {
                WriteNode(writer, child);
            }
        }
        
        public void Load()
        {
            try
            {
                using (var reader = new BinaryReader(File.OpenRead(_savePath)))
                {
                    string header = reader.ReadString();
                    bool isV2 = header == "HKNOW02";
                    bool isV3 = header == "HKNOW03";
                    bool isV4 = header == "HKNOW04";
                    if (header != "HKNOW01" && !isV2 && !isV3 && !isV4)
                    {
                        SubModule.Log("HierarchicalKnowledge: Invalid file format");
                        return;
                    }
                    
                    _loadedVersion = isV4 ? 4 : (isV3 ? 3 : (isV2 ? 2 : 1));
                    _totalExperiments = reader.ReadInt32();
                    _root = ReadNode(reader);
                    
                    // Load feature problem branches (V2+)
                    if (isV2 || isV3 || isV4)
                    {
                        int branchCount = reader.ReadInt32();
                        _featureProblemBranches.Clear();
                        for (int i = 0; i < branchCount; i++)
                        {
                            string key = reader.ReadString();
                            var node = ReadNode(reader);
                            _featureProblemBranches[key] = node;
                        }
                    }
                    
                    // V4: Load shared feature knowledge
                    if (isV4)
                    {
                        int sharedCount = reader.ReadInt32();
                        _sharedKnowledge.Clear();
                        for (int i = 0; i < sharedCount; i++)
                        {
                            string featureKey = reader.ReadString();
                            var entry = new SharedFeatureEntry { FeatureKey = featureKey };
                            entry.LearnCount = reader.ReadInt32();
                            entry.Confidence = reader.ReadSingle();
                            
                            // Base morphs
                            int morphCount = reader.ReadInt32();
                            for (int j = 0; j < morphCount; j++)
                            {
                                int key = reader.ReadInt32();
                                float value = reader.ReadSingle();
                                entry.BaseMorphs[key] = value;
                            }
                            
                            // Learned contexts
                            int contextCount = reader.ReadInt32();
                            for (int j = 0; j < contextCount; j++)
                            {
                                entry.LearnedContexts.Add(reader.ReadString());
                            }
                            
                            _sharedKnowledge[featureKey] = entry;
                        }
                    }
                }
                var (stable, highVar, stale) = GetTreeHealth(_root);
                SubModule.Log($"HierarchicalKnowledge: Loaded ({CountNodes(_root)} nodes, {_sharedKnowledge.Count} shared, {stable} stable, {_totalExperiments} exp)");
            }
            catch (Exception ex)
            {
                SubModule.Log($"HierarchicalKnowledge load error: {ex.Message}");
                _root = new KnowledgeNode { Path = "ROOT", Value = "ROOT" };
            }
        }
        
        private int _loadedVersion = 1;
        
        private KnowledgeNode ReadNode(BinaryReader reader)
        {
            var node = new KnowledgeNode();
            node.Path = reader.ReadString();
            node.Feature = (FeatureCategory)reader.ReadInt32();
            node.Value = reader.ReadString();
            node.UseCount = reader.ReadInt32();
            node.SuccessCount = reader.ReadInt32();
            node.ConfidenceScore = reader.ReadSingle();
            
            // MorphDeltas
            int deltaCount = reader.ReadInt32();
            for (int i = 0; i < deltaCount; i++)
            {
                int key = reader.ReadInt32();
                float value = reader.ReadSingle();
                node.MorphDeltas[key] = value;
            }
            
            // V3: MorphVariance
            if (_loadedVersion >= 3)
            {
                int varCount = reader.ReadInt32();
                for (int i = 0; i < varCount; i++)
                {
                    int key = reader.ReadInt32();
                    float value = reader.ReadSingle();
                    node.MorphVariance[key] = value;
                }
                
                // Self-organization metrics
                node.OutcomeVariance = reader.ReadSingle();
                node.Health = reader.ReadSingle();
                node.SplitCount = reader.ReadInt32();
                node.MergeCount = reader.ReadInt32();
                node.LastUsed = new DateTime(reader.ReadInt64());
                node.LastRefinement = new DateTime(reader.ReadInt64());
                
                // Recent outcomes
                int outcomeCount = reader.ReadInt32();
                for (int i = 0; i < outcomeCount; i++)
                {
                    node.RecentOutcomes.Add(reader.ReadSingle());
                }
            }
            
            // Children
            int childCount = reader.ReadInt32();
            for (int i = 0; i < childCount; i++)
            {
                node.Children.Add(ReadNode(reader));
            }
            
            return node;
        }
        
        #region Knowledge Sharing Support
        
        /// <summary>
        /// Get root node for export
        /// </summary>
        public KnowledgeNode GetRootForExport()
        {
            return _root;
        }
        
        /// <summary>
        /// Get feature branches for export
        /// </summary>
        public Dictionary<string, KnowledgeNode> GetFeatureBranchesForExport()
        {
            return new Dictionary<string, KnowledgeNode>(_featureProblemBranches);
        }
        
        /// <summary>
        /// Get shared knowledge entries for export
        /// </summary>
        public Dictionary<string, SharedFeatureEntry> GetSharedKnowledgeForExport()
        {
            return new Dictionary<string, SharedFeatureEntry>(_sharedKnowledge);
        }
        
        /// <summary>
        /// Find a node by its path
        /// </summary>
        public KnowledgeNode FindNodeByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return _root;
            return FindNodeByPathRecursive(_root, path);
        }
        
        private KnowledgeNode FindNodeByPathRecursive(KnowledgeNode node, string targetPath)
        {
            if (node == null) return null;
            if (node.Path == targetPath) return node;
            
            foreach (var child in node.Children)
            {
                var found = FindNodeByPathRecursive(child, targetPath);
                if (found != null) return found;
            }
            
            return null;
        }
        
        /// <summary>
        /// Add a new node at the specified path
        /// </summary>
        public void AddNodeAtPath(string path, KnowledgeNode newNode)
        {
            if (string.IsNullOrEmpty(path) || newNode == null) return;
            
            // Find parent path
            int lastSlash = path.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                // Add to root
                _root.Children.Add(newNode);
            }
            else
            {
                string parentPath = path.Substring(0, lastSlash);
                var parent = FindNodeByPath(parentPath);
                if (parent != null)
                {
                    parent.Children.Add(newNode);
                }
            }
        }
        
        /// <summary>
        /// Merge a feature branch from imported data
        /// </summary>
        public void MergeFeatureBranch(string key, KnowledgeNode importedBranch, float trustLevel)
        {
            if (string.IsNullOrEmpty(key) || importedBranch == null) return;
            
            if (_featureProblemBranches.ContainsKey(key))
            {
                // Merge with existing
                MergeNodeRecursive(_featureProblemBranches[key], importedBranch, trustLevel);
            }
            else
            {
                // Add new (with reduced confidence)
                ScaleNodeConfidence(importedBranch, trustLevel);
                _featureProblemBranches[key] = importedBranch;
            }
        }
        
        private void MergeNodeRecursive(KnowledgeNode target, KnowledgeNode source, float trustLevel)
        {
            if (target == null || source == null) return;
            
            float targetWeight = 1f - trustLevel;
            float sourceWeight = trustLevel * (source.SuccessCount / (float)Math.Max(1, source.UseCount));
            float totalWeight = targetWeight + sourceWeight;
            
            // Merge deltas
            foreach (var kv in source.MorphDeltas)
            {
                if (target.MorphDeltas.ContainsKey(kv.Key))
                {
                    target.MorphDeltas[kv.Key] = 
                        (target.MorphDeltas[kv.Key] * targetWeight + kv.Value * sourceWeight) / totalWeight;
                }
                else
                {
                    target.MorphDeltas[kv.Key] = kv.Value * (sourceWeight / totalWeight);
                }
            }
            
            // Update stats
            target.UseCount += (int)(source.UseCount * trustLevel);
            target.SuccessCount += (int)(source.SuccessCount * trustLevel);
            
            // Merge children
            foreach (var sourceChild in source.Children)
            {
                var matchingChild = target.Children.FirstOrDefault(c => c.Path == sourceChild.Path);
                if (matchingChild != null)
                {
                    MergeNodeRecursive(matchingChild, sourceChild, trustLevel);
                }
                else
                {
                    var newChild = CloneNode(sourceChild);
                    ScaleNodeConfidence(newChild, trustLevel);
                    target.Children.Add(newChild);
                }
            }
        }
        
        private void ScaleNodeConfidence(KnowledgeNode node, float scale)
        {
            if (node == null) return;
            
            node.ConfidenceScore *= scale;
            node.UseCount = (int)(node.UseCount * scale);
            node.SuccessCount = (int)(node.SuccessCount * scale);
            
            foreach (var child in node.Children)
            {
                ScaleNodeConfidence(child, scale);
            }
        }
        
        private KnowledgeNode CloneNode(KnowledgeNode source)
        {
            if (source == null) return null;
            
            var clone = new KnowledgeNode
            {
                Path = source.Path,
                Feature = source.Feature,
                Value = source.Value,
                MorphDeltas = new Dictionary<int, float>(source.MorphDeltas),
                MorphVariance = source.MorphVariance != null 
                    ? new Dictionary<int, float>(source.MorphVariance) 
                    : new Dictionary<int, float>(),
                UseCount = source.UseCount,
                SuccessCount = source.SuccessCount,
                ConfidenceScore = source.ConfidenceScore,
                OutcomeVariance = source.OutcomeVariance,
                Health = source.Health,
                Children = new List<KnowledgeNode>()
            };
            
            foreach (var child in source.Children)
            {
                clone.Children.Add(CloneNode(child));
            }
            
            return clone;
        }
        
        /// <summary>
        /// Merge a shared knowledge entry from imported data
        /// </summary>
        public void MergeSharedEntry(string key, Dictionary<int, float> morphs, int learnCount, float confidence, float trustLevel)
        {
            if (string.IsNullOrEmpty(key) || morphs == null) return;
            
            if (_sharedKnowledge.ContainsKey(key))
            {
                var existing = _sharedKnowledge[key];
                float existingWeight = 1f - trustLevel;
                float importWeight = trustLevel * confidence;
                float totalWeight = existingWeight + importWeight;
                
                // Merge morphs
                foreach (var kv in morphs)
                {
                    if (existing.BaseMorphs.ContainsKey(kv.Key))
                    {
                        existing.BaseMorphs[kv.Key] = 
                            (existing.BaseMorphs[kv.Key] * existingWeight + kv.Value * importWeight) / totalWeight;
                    }
                    else
                    {
                        existing.BaseMorphs[kv.Key] = kv.Value * (importWeight / totalWeight);
                    }
                }
                
                existing.LearnCount += (int)(learnCount * trustLevel);
                existing.Confidence = Math.Min(1f, 
                    (existing.Confidence * existingWeight + confidence * importWeight) / totalWeight);
            }
            else
            {
                // New entry
                _sharedKnowledge[key] = new SharedFeatureEntry
                {
                    FeatureKey = key,
                    BaseMorphs = new Dictionary<int, float>(morphs),
                    LearnCount = (int)(learnCount * trustLevel),
                    Confidence = confidence * trustLevel
                };
            }
        }
        
        #endregion
    }
}
