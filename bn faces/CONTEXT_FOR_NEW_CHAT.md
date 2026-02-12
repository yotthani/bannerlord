# FaceLearner - Kontext für neuen Chat

**Datum:** 2025-01-22
**Aktuelle Version:** 1.7.0
**Sprache:** Deutsch für Kommunikation, Englisch für Code/Docs

---

## Projekt-Übersicht

**FaceLearner** ist ein Bannerlord-Mod der ML nutzt um Gesichter aus Fotos nachzubauen.

### Architektur
```
FaceLearner/          # Basis-Mod (UI, Character Control)
├── Core/
│   ├── FaceController.cs      # Steuert Charakter-Morphs
│   ├── FaceLearnerVM.cs       # ViewModel für UI
│   ├── MorphDefinitions.cs    # 62 Morph-Definitionen
│   └── ...

FaceLearner.ML/       # ML-Addon (optional, lädt ONNX-Modelle)
├── Core/
│   └── LivingKnowledge/
│       ├── LearningOrchestrator.cs  # HAUPTDATEI - Optimierung
│       ├── FeatureIntelligence.cs   # NEU v1.7 - Adaptive Intelligenz
│       ├── ModuleIntegration.cs     # Feature-Scoring
│       ├── CmaEs.cs                 # CMA-ES Algorithmus
│       └── ...
├── ML/
│   └── Modules/
│       └── Scoring/
│           └── ScoringModule.cs     # Feature-Scores berechnen
```

---

## Aktueller Stand (v1.7.0)

### NEU: Feature Intelligence System

Vier neue Systeme für intelligentere Optimierung:

#### 1. Adaptive Feature Weights
- Schwierige Features bekommen höhere Gewichtung (bis 2x Boost)
- Lernt automatisch welche Features schwer zu matchen sind
- Basiert auf beobachteten Scores UND Verbesserungs-Erfolgsrate

#### 2. Feature Correlation Learning
- Lernt welche Morphs welche Features beeinflussen
- Identifiziert "sichere" Morphs (beeinflussen nur ein Feature)
- Identifiziert "konfliktreiche" Morphs (beeinflussen mehrere Features)
- Ermöglicht gezieltere Mutationen

#### 3. Progressive Feature Focus
```
Phase 1 (iter 1-20):   BROAD      - Alle Features gleich optimieren
Phase 2 (iter 21-40):  NARROWING  - Fokus auf schlechteste 3 Features
Phase 3 (iter 41+):    FOCUSED    - Starker Fokus auf schlechteste 1-2
```

#### 4. Feature History Tracking
- Verfolgt letzte 50 Iterationen pro Feature
- Erkennt Regressionen (wenn Feature plötzlich schlechter wird)
- Alerts bei signifikanten Drops (> 0.05 vom Peak)
- Regression-Fixes haben höchste Priorität

---

## Wichtige Klassen & Methoden

### FeatureIntelligence.cs (NEU)
```csharp
// Adaptive Gewichtung
Dictionary<string, float> GetAdaptiveWeights();
void UpdateFeatureDifficulty(Dictionary<string, float> featureScores);
float GetDifficulty(string feature);  // 0=easy, 1=hard

// Correlation Learning
void RecordMorphChange(int morphIndex, float delta, scoresBefore, scoresAfter);
List<int> GetSafeMorphsForFeature(string feature);
bool IsMorphConflicting(int morphIndex);

// Progressive Focus
FocusPhase CurrentPhase { get; }  // Broad, Narrowing, Focused
float GetFocusProbability();
bool ShouldFocusOnWeakFeatures(Random random);
List<string> FocusedFeatures { get; }

// History & Regression
List<RegressionInfo> GetRegressionAlerts();
float GetFeatureTrend(string feature);  // positiv=besser, negativ=schlechter
bool IsFeatureRegressing(string feature);

// Kombinierte Empfehlung
MutationRecommendation GetRecommendation(scores, random);
// Returns: Priority (FixRegression > FocusWeak > BroadOptimization)
//          TargetFeature, RecommendedMorphs, AdaptiveWeights
```

### LearningOrchestrator.cs (erweitert)
```csharp
// Neue Properties
public FocusPhase CurrentFocusPhase;
public List<string> FocusedFeatures;
public bool HasActiveRegressions;
public string FeatureIntelligenceStatus;
```

---

## Daten-Persistenz

Neue Datei: `feature_intelligence.dat`
- Feature-Schwierigkeiten
- Morph-Feature-Korrelationen
- Verbesserungs-Statistiken

---

## Log-Messages

```
[FeatureIntelligence] Saved/Loaded
⚠️ Regression detected: Jaw dropped 0.13
🎯 Feature focus: Nose (0.32) +8 morphs
[Learn] Changes:3 Step:0.15 Stuck:5
```

---

## Vorherige Versionen

### v1.6.x - Per-Feature Best Tracking
- Track beste Morphs für JEDES Feature separat
- Feature-Based Revert (jeden 5. Revert)
- CMA-ES Early Abort

### v1.5.x - Feature Prioritization
- Dynamic feature-focus probability
- Larger step size for very bad features

---

## Dateien zum Hochladen in neuen Chat

**Minimal:**
- `FaceLearner_v1.7.0_CHECKPOINT.zip`

**Oder einzeln:**
- `FaceLearner.ML/Core/LivingKnowledge/LearningOrchestrator.cs`
- `FaceLearner.ML/Core/LivingKnowledge/FeatureIntelligence.cs`
- `FaceLearner.ML/ML/Modules/Scoring/ScoringModule.cs`
- `FaceLearner/Core/MorphDefinitions.cs`

---

## Quick Start im neuen Chat

```
Ich arbeite am FaceLearner Projekt - ein Bannerlord Mod der ML nutzt 
um Gesichter aus Fotos nachzubauen.

Aktueller Stand: v1.7.0 mit Feature Intelligence System
(Adaptive Weights, Correlation Learning, Progressive Focus, History Tracking)

[Upload: FaceLearner_v1.7.0_CHECKPOINT.zip]
[Upload: CONTEXT_FOR_NEW_CHAT.md]

Lies bitte zuerst CONTEXT_FOR_NEW_CHAT.md für den vollen Kontext.
```

---

## Nächste Schritte / Ideen

1. **Testing:** Feature Intelligence in echten Optimierungsläufen testen
2. **Visualisierung:** Correlation-Matrix als Debug-Output
3. **Tuning:** Threshold-Werte für Regression-Detection anpassen
4. **Body Learning:** System auf Body-Morphs erweitern (nicht nur Face)
