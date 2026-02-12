namespace FaceLearner.ML.Core.HierarchicalScoring
{
    /// <summary>
    /// Main phases of hierarchical face learning.
    /// Foundation must be good before Structure can be effective, etc.
    /// </summary>
    public enum MainPhase
    {
        Foundation,     // Face shape (width, height, proportions)
        Structure,      // Forehead, jaw, chin, cheeks
        MajorFeatures,  // Nose, eyes, mouth
        FineDetails     // Eyebrows, ears, fine details
    }
    
    /// <summary>
    /// Sub-phases within each main phase.
    /// </summary>
    public enum SubPhase
    {
        // Foundation
        FaceWidth,
        FaceHeight,
        FaceShape,
        
        // Structure
        Forehead,
        Jaw,
        Chin,
        Cheeks,
        
        // MajorFeatures
        Nose,
        Eyes,
        Mouth,
        
        // FineDetails
        Eyebrows,
        Ears,
        FineDetails
    }
    
    /// <summary>
    /// Optimization phase within each sub-phase.
    /// </summary>
    public enum OptPhase
    {
        Exploration,    // Wide search with high sigma
        Refinement,     // Narrow search around best
        LockIn,         // Very fine tuning before locking
        PlateauEscape   // Trying to escape local optimum
    }
    
    // Note: PhaseAction is defined in HierarchicalPhaseController.cs
}
