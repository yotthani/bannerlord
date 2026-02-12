using System;
using System.Drawing;
using FaceLearner.ML.Modules.Core;
using FaceLearner.ML.Modules.FaceParsing;
using FaceLearner.ML.Modules.Demographics.Gender;
using FaceLearner.ML.Modules.Demographics.Age;
using FaceLearner.ML.Modules.Demographics.SkinTone;

// Alias to avoid namespace conflict
using GenderEnum = FaceLearner.ML.Modules.Core.Gender;

namespace FaceLearner.ML.Modules.Demographics
{
    /// <summary>
    /// Combined demographics detection result
    /// </summary>
    public class DemographicsResult : IDetectionResult
    {
        public GenderResult Gender { get; set; }
        public AgeResult Age { get; set; }
        public SkinToneResult SkinTone { get; set; }
        
        /// <summary>Detected ethnicity (if available from FairFace)</summary>
        public string Ethnicity { get; set; }
        
        /// <summary>Ethnicity confidence</summary>
        public float EthnicityConfidence { get; set; }
        
        public float Confidence { get; set; }
        public bool IsReliable => Confidence >= ConfidenceThresholds.Acceptable;
        public string Source { get; set; }
        
        /// <summary>
        /// Quick access to determined gender
        /// </summary>
        public bool IsFemale => Gender?.IsFemale ?? false;
        
        /// <summary>
        /// Quick access to estimated age
        /// </summary>
        public int EstimatedAge => Age?.EstimatedAge ?? 30;
        
        /// <summary>
        /// Quick access to normalized skin tone
        /// </summary>
        public float NormalizedSkinTone => SkinTone?.NormalizedTone ?? 0.5f;
        
        /// <summary>
        /// Check if person has facial hair (strong male indicator)
        /// </summary>
        public bool HasFacialHair => Gender?.HasFacialHair ?? false;
    }
    
    /// <summary>
    /// Combined demographics module that orchestrates gender, age, and skin tone detection.
    /// </summary>
    public class DemographicsModule
    {
        private GenderModule _genderModule;
        private AgeModule _ageModule;
        private SkinToneModule _skinToneModule;
        
        public DemographicsModule()
        {
            _genderModule = new GenderModule();
            _ageModule = new AgeModule();
            _skinToneModule = new SkinToneModule();
        }
        
        /// <summary>
        /// Analyze all demographics from image and landmarks
        /// </summary>
        public DemographicsResult Analyze(
            Bitmap image,
            float[] landmarks,
            FaceParsingResult parsing = null,
            Rectangle? faceRect = null)
        {
            var result = new DemographicsResult
            {
                Source = "DemographicsModule"
            };
            
            // Gender from landmarks and parsing
            result.Gender = _genderModule.Detect(landmarks, parsing);
            
            // Age from landmarks
            result.Age = _ageModule.EstimateFromLandmarks(landmarks);
            
            // Skin tone from image
            if (image != null)
            {
                if (parsing != null && parsing.HasRegion(FaceRegion.Skin))
                {
                    result.SkinTone = _skinToneModule.Analyze(image, parsing);
                }
                else if (faceRect.HasValue)
                {
                    result.SkinTone = _skinToneModule.AnalyzeSimple(image, faceRect.Value);
                }
            }
            
            // Combined confidence
            float confSum = 0;
            int confCount = 0;
            
            if (result.Gender != null)
            {
                confSum += result.Gender.Confidence;
                confCount++;
            }
            if (result.Age != null)
            {
                confSum += result.Age.Confidence;
                confCount++;
            }
            if (result.SkinTone != null)
            {
                confSum += result.SkinTone.Confidence;
                confCount++;
            }
            
            result.Confidence = confCount > 0 ? confSum / confCount : 0;
            
            return result;
        }
        
        /// <summary>
        /// Analyze with external model results (FairFace/InsightFace)
        /// </summary>
        public DemographicsResult AnalyzeWithModel(
            Bitmap image,
            float[] landmarks,
            FaceParsingResult parsing,
            bool modelIsFemale,
            float modelGenderConf,
            int modelAge,
            float modelAgeConf,
            string modelRace = null,
            float modelRaceConf = 0,
            string modelSource = "FairFace")
        {
            var result = new DemographicsResult
            {
                Source = $"DemographicsModule+{modelSource}"
            };
            
            // Gender with model
            result.Gender = _genderModule.DetectWithModel(
                landmarks, parsing, modelIsFemale, modelGenderConf, modelSource);
            
            // Age from model
            result.Age = _ageModule.FromModelPrediction(modelAge, modelAgeConf, modelSource);
            
            // Skin tone from image
            if (image != null && parsing != null)
            {
                result.SkinTone = _skinToneModule.Analyze(image, parsing);
            }
            
            // Ethnicity from model
            if (!string.IsNullOrEmpty(modelRace))
            {
                result.Ethnicity = modelRace;
                result.EthnicityConfidence = modelRaceConf;
            }
            
            // Combined confidence (weighted towards model)
            float modelWeight = 0.6f;
            float analysisWeight = 0.4f;
            
            float modelConf = (modelGenderConf + modelAgeConf) / 2;
            float analysisConf = result.SkinTone?.Confidence ?? 0.5f;
            
            result.Confidence = modelConf * modelWeight + analysisConf * analysisWeight;
            
            return result;
        }
    }
}
