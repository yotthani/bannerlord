namespace FaceLearner.Core.LivingKnowledge
{
    /// <summary>
    /// Feature categories used for hierarchical knowledge classification.
    /// Each category represents a facial characteristic that can be detected and learned.
    /// </summary>
    public enum FeatureCategory
    {
        #region Face Shape & Structure
        
        /// <summary>Horizontal dimension: Narrow, Medium, Wide</summary>
        FaceWidth,
        
        /// <summary>Vertical dimension: Short, Medium, Long</summary>
        FaceLength,
        
        /// <summary>Overall form: Round, Oval, Angular</summary>
        FaceShape,
        
        /// <summary>Jaw/chin specific: Round, Square, Pointed</summary>
        JawShape,
        
        #endregion
        
        #region Demographics
        
        /// <summary>Gender: Male, Female</summary>
        Gender,
        
        /// <summary>Age groups: Young (18-30), Middle (30-50), Mature (50+)</summary>
        AgeGroup,
        
        #endregion
        
        #region Expression
        
        /// <summary>Smile level affects landmark reliability: None, Smile, BigSmile</summary>
        SmileLevel,
        
        #endregion
        
        #region Nose
        
        /// <summary>Nose width: Narrow, Medium, Wide</summary>
        NoseWidth,
        
        /// <summary>Nose length: Short, Medium, Long</summary>
        NoseLength,
        
        #endregion
        
        #region Mouth
        
        /// <summary>Mouth width: Narrow, Medium, Wide</summary>
        MouthWidth,
        
        /// <summary>Lip fullness: Thin, Medium, Full</summary>
        LipFullness,
        
        #endregion
        
        #region Eyes
        
        /// <summary>Eye size: Small, Medium, Large</summary>
        EyeSize,
        
        /// <summary>Eye spacing: Close, Medium, Wide</summary>
        EyeSpacing,
        
        /// <summary>Eye shape: Round, Almond, Hooded</summary>
        EyeShape,
        
        #endregion
        
        #region Cheeks
        
        /// <summary>Cheek fullness: Hollow, Medium, Full</summary>
        CheekFullness,
        
        /// <summary>Cheekbone prominence: Flat, Medium, High</summary>
        CheekboneProminence,
        
        #endregion
    }
}
