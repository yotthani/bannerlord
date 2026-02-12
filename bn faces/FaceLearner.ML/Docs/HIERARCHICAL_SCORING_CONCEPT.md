# Hierarchical Scoring System - Konzept

## Motivation

Das aktuelle System hat ein fundamentales Problem:
```
41 Ratios × flache Gewichte → EIN Score
```

Probleme:
- Ping-Pong bei Gewichtungs-Tuning (35% → 50% → 65% → ?)
- Keine Struktur - Kinn-Detail gleichwertig mit Gesichtsbreite
- Wenn Grundform falsch, helfen perfekte Details nicht
- Keine klare Korrelation mit visueller Ähnlichkeit

## Lösung: Hierarchisches Scoring

### Prinzip
```
Ebene 1 (Fundament) muss stimmen, bevor Ebene 2 relevant wird.
Ebene 2 muss stimmen, bevor Ebene 3 relevant wird.
...

Ein perfektes Auge in einem komplett falschen Gesicht 
macht es NICHT ähnlicher!
```

---

## Die 4 Ebenen

### Ebene 1: GESICHTSFORM (Foundation)
Das Grundgerüst - wenn das nicht passt, ist alles andere egal.

| Feature | Beschreibung | Landmarks (Dlib 68) |
|---------|--------------|---------------------|
| `face_width` | Absolute Breite | Dist(0, 16) |
| `face_height` | Absolute Höhe | Dist(8, 27) |
| `face_ratio` | Breite/Höhe Verhältnis | width/height |
| `face_shape` | Round/Oval/Square/Heart/Oblong | Klassifikation |

**Score-Berechnung:**
```csharp
float widthMatch = 1f - Math.Abs(target.Width - current.Width) / target.Width;
float heightMatch = 1f - Math.Abs(target.Height - current.Height) / target.Height;
float ratioMatch = 1f - Math.Abs(target.Ratio - current.Ratio) * 2f;
float shapeMatch = target.Shape == current.Shape ? 1f : 0.5f;

Level1Score = (widthMatch + heightMatch + ratioMatch + shapeMatch) / 4f;
```

**Gewichtung:** 30% des Gesamtscores

---

### Ebene 2: FORMGEBENDE TEILE (Structure)
Die großen Strukturen die die Gesichtskontur definieren.

| Feature | Beschreibung | Landmarks |
|---------|--------------|-----------|
| `forehead_height` | Stirnhöhe | Y(19) - Y(27) relativ |
| `forehead_width` | Stirnbreite | Dist(17, 26) |
| `jaw_angle_left` | Kieferwinkel links | Angle(1, 4, 8) |
| `jaw_angle_right` | Kieferwinkel rechts | Angle(15, 12, 8) |
| `jaw_width` | Kieferbreite unten | Dist(5, 11) |
| `chin_width` | Kinnbreite | Dist(7, 9) |
| `chin_length` | Kinnlänge | Dist(8, 57) |
| `chin_shape` | Spitz/Rund/Eckig | Klassifikation |

**Score-Berechnung:**
```csharp
// Jedes Feature einzeln bewerten
float[] featureScores = new float[8];
featureScores[0] = MatchValue(target.ForeheadHeight, current.ForeheadHeight);
featureScores[1] = MatchValue(target.ForeheadWidth, current.ForeheadWidth);
// ... etc

Level2Score = featureScores.Average();
```

**Gewichtung:** 25% des Gesamtscores

---

### Ebene 3: GROSSE FEATURES (Major Features)
Die markanten Gesichtszüge.

| Feature | Sub-Features | Landmarks |
|---------|--------------|-----------|
| **Nase** | Breite, Länge, Brücke, Spitze, Nasenlöcher | 27-35 |
| **Augen** | Breite, Höhe, Abstand, Position, Form | 36-47 |
| **Mund** | Breite, Höhe, Lippendicke, Position | 48-67 |

**Nase Detail:**
```csharp
NoseScore = Average(
    MatchValue(target.NoseWidth, current.NoseWidth),      // Breite
    MatchValue(target.NoseLength, current.NoseLength),    // Länge
    MatchValue(target.NoseBridge, current.NoseBridge),    // Brücke
    MatchValue(target.NoseTip, current.NoseTip),          // Spitze
    MatchValue(target.NostrilWidth, current.NostrilWidth) // Nasenlöcher
);
```

**Gewichtung:** 25% des Gesamtscores

---

### Ebene 4: FEINE DETAILS (Fine Details)
Details die nur bei gutem Grundgerüst relevant sind.

| Feature | Beschreibung |
|---------|--------------|
| `eyebrow_arch` | Augenbrauenbogen |
| `eyebrow_thickness` | Augenbrauendicke |
| `lip_cupid_bow` | Amorbogen der Oberlippe |
| `eye_corner_shape` | Augenwinkel-Form |
| `nasolabial_depth` | Nasolabialfalten |

**Gewichtung:** 20% des Gesamtscores

---

## Gesamtscore-Berechnung

### Nicht einfach addieren!

```csharp
// FALSCH (was wir jetzt machen):
totalScore = L1 * 0.30 + L2 * 0.25 + L3 * 0.25 + L4 * 0.20;

// RICHTIG (hierarchisch):
// Wenn Ebene 1 schlecht ist, werden höhere Ebenen abgewertet!

float effectiveL2 = L2 * GateFunction(L1, threshold: 0.5f);
float effectiveL3 = L3 * GateFunction(L1, 0.5f) * GateFunction(L2, 0.5f);
float effectiveL4 = L4 * GateFunction(L1, 0.5f) * GateFunction(L2, 0.5f) * GateFunction(L3, 0.5f);

totalScore = L1 * 0.30 + effectiveL2 * 0.25 + effectiveL3 * 0.25 + effectiveL4 * 0.20;
```

### Gate Function
```csharp
// Soft gate - reduziert Einfluss höherer Ebenen wenn niedrigere schlecht sind
float GateFunction(float lowerLevelScore, float threshold)
{
    if (lowerLevelScore >= threshold)
        return 1.0f;  // Voller Einfluss
    
    // Linearer Abfall unter threshold
    return lowerLevelScore / threshold;
}

// Beispiel:
// L1 = 0.80 → Gate = 1.0 (L2 hat vollen Einfluss)
// L1 = 0.40 → Gate = 0.8 (L2 hat 80% Einfluss)
// L1 = 0.20 → Gate = 0.4 (L2 hat nur 40% Einfluss)
```

---

## Race Modifiers (Externe Parameter)

### Konzept
Race-Modifikatoren sind **NICHT gelernt** - sie sind definierte Transformationen.

```
LEARNING: Race-agnostisch
  Photo → Landmarks → Tree lernt Morph↔Landmark Beziehungen
  
GENERATION: Race-aware
  Photo → Landmarks → Morphs → RaceModifier → Finale Morphs
```

### Modifier-Struktur
```csharp
public class RaceModifier
{
    public string RaceId { get; set; }  // "high_elf", "dwarf", etc.
    
    // Ebene 1: Gesichtsform-Modifikation
    public float FaceWidthScale { get; set; }   // 1.0 = keine Änderung
    public float FaceHeightScale { get; set; }
    public float FaceRatioShift { get; set; }   // +/- Verschiebung
    
    // Ebene 2: Struktur-Modifikation
    public float JawAngleShift { get; set; }    // Spitzer/Runder
    public float ChinScale { get; set; }
    public float ForeheadScale { get; set; }
    
    // Ebene 3: Feature-Modifikation
    public float NoseWidthScale { get; set; }
    public float NoseLengthScale { get; set; }
    public float EyeWidthScale { get; set; }
    public float MouthWidthScale { get; set; }
    
    // Softness (wie stark werden Kanten geglättet)
    public float OverallSoftness { get; set; }  // 0=hart, 1=weich
    
    // Morph-Biases (direkte Morph-Verschiebungen)
    public Dictionary<int, float> MorphBiases { get; set; }
}
```

### Vordefinierte Race-Modifier

```csharp
public static class RaceModifiers
{
    public static RaceModifier HighElf = new RaceModifier
    {
        RaceId = "high_elf",
        
        // Ebene 1: Schmaleres, längeres Gesicht
        FaceWidthScale = 0.95f,
        FaceHeightScale = 1.05f,
        
        // Ebene 2: Feinerer Kiefer, spitzeres Kinn
        JawAngleShift = -5f,  // Spitzer
        ChinScale = 0.90f,
        
        // Ebene 3: Feinere Features
        NoseWidthScale = 0.90f,
        NoseLengthScale = 1.05f,
        EyeWidthScale = 1.05f,
        
        // Generell weicher
        OverallSoftness = 0.7f,
        
        // Spezifische Morph-Biases
        MorphBiases = new Dictionary<int, float>
        {
            { 22, -0.15f },  // Jaw width
            { 25, -0.10f },  // Chin width
            { 30, 0.10f },   // Cheekbone height
        }
    };
    
    public static RaceModifier Dwarf = new RaceModifier
    {
        RaceId = "dwarf",
        
        // Ebene 1: Breiteres, kürzeres Gesicht
        FaceWidthScale = 1.10f,
        FaceHeightScale = 0.95f,
        
        // Ebene 2: Breiter Kiefer, kräftiges Kinn
        JawAngleShift = +5f,  // Breiter
        ChinScale = 1.10f,
        ForeheadScale = 0.95f,
        
        // Ebene 3: Breitere Features
        NoseWidthScale = 1.15f,
        NoseLengthScale = 0.95f,
        
        // Kantiger
        OverallSoftness = 0.3f,
    };
    
    public static RaceModifier Orc = new RaceModifier
    {
        RaceId = "orc",
        
        // Stark deformiert
        FaceWidthScale = 1.15f,
        JawAngleShift = +10f,
        NoseWidthScale = 1.30f,
        NoseLengthScale = 0.85f,  // Flacher
        
        OverallSoftness = 0.1f,  // Sehr kantig
        
        MorphBiases = new Dictionary<int, float>
        {
            { 15, 0.25f },   // Brow prominence
            { 22, 0.20f },   // Jaw width
            { 28, -0.20f },  // Nose bridge (flacher)
        }
    };
}
```

### Anwendung des Modifiers

```csharp
public class HierarchicalScorer
{
    private RaceModifier _activeModifier;
    
    public void SetRace(string raceId)
    {
        _activeModifier = RaceModifiers.Get(raceId);
    }
    
    /// <summary>
    /// Score berechnen MIT Race-Modifier
    /// Der Modifier verschiebt die TARGET-Werte, nicht die Current-Werte
    /// </summary>
    public HierarchicalScore CalculateScore(
        FeatureSet target, 
        FeatureSet current)
    {
        // Wenn Race aktiv: Target-Werte modifizieren
        if (_activeModifier != null)
        {
            target = ApplyModifierToTarget(target, _activeModifier);
        }
        
        // Dann normal scoren
        return CalculateHierarchicalScore(target, current);
    }
    
    private FeatureSet ApplyModifierToTarget(FeatureSet target, RaceModifier mod)
    {
        var modified = target.Clone();
        
        // Ebene 1
        modified.FaceWidth *= mod.FaceWidthScale;
        modified.FaceHeight *= mod.FaceHeightScale;
        
        // Ebene 2
        modified.JawAngle += mod.JawAngleShift;
        modified.ChinWidth *= mod.ChinScale;
        
        // Ebene 3
        modified.NoseWidth *= mod.NoseWidthScale;
        modified.NoseLength *= mod.NoseLengthScale;
        
        return modified;
    }
}
```

---

## Verwendung in Learning vs Generation

### Learning (Race-agnostisch)
```csharp
// Beim Lernen: Keine Race-Modifier
// Wir lernen die ROHEN Morph↔Landmark Beziehungen

hierarchicalScorer.SetRace(null);  // Kein Modifier

foreach (var target in dataset)
{
    var score = hierarchicalScorer.CalculateScore(target.Features, current.Features);
    // Score wird verwendet um Morphs zu optimieren
    // Tree lernt: "Morph 22 erhöhen → Landmark-Abstand X verringern"
}
```

### Generation (Race-aware)
```csharp
// Bei Generation: Race-Modifier aktiv
// User hat "High Elf" gewählt und lädt ein Foto

hierarchicalScorer.SetRace("high_elf");

// Das Foto zeigt Cate Blanchett als Galadriel
var photoFeatures = ExtractFeatures(photoLandmarks);

// Der Modifier verschiebt das Target:
// - Gesicht soll schmaler sein als im Foto
// - Kiefer soll spitzer sein
// - Nase soll feiner sein

var score = hierarchicalScorer.CalculateScore(photoFeatures, currentFeatures);
// Score ist gut wenn Current elfischer ist als das Foto!
```

---

## Integration mit HierarchicalPhaseSystem

Das Scoring-System korreliert mit den Learning-Phasen:

| Phase | Fokus-Ebene | Beschreibung |
|-------|-------------|--------------|
| 1. Coarse Search | Ebene 1 | Gesichtsform grob treffen |
| 2. Proportions | Ebene 1+2 | Form + Struktur |
| 3. Main Features | Ebene 2+3 | Struktur + Features |
| 4. Details | Ebene 3+4 | Features + Details |
| 5. Polish | Ebene 4 | Feinschliff |

```csharp
public float GetPhaseRelevantScore(LearningPhase phase, HierarchicalScore score)
{
    switch (phase)
    {
        case LearningPhase.CoarseSearch:
            // Nur Ebene 1 zählt
            return score.Level1;
            
        case LearningPhase.Proportions:
            // Ebene 1+2, aber 1 wichtiger
            return score.Level1 * 0.6f + score.Level2 * 0.4f;
            
        case LearningPhase.MainFeatures:
            // Alle drei, aber 2+3 wichtiger
            return score.Level1 * 0.2f + score.Level2 * 0.4f + score.Level3 * 0.4f;
            
        case LearningPhase.Details:
            // Fokus auf 3+4
            return score.Level2 * 0.2f + score.Level3 * 0.4f + score.Level4 * 0.4f;
            
        case LearningPhase.Polish:
            // Vollständiger hierarchischer Score
            return score.Total;
    }
}
```

---

## Implementierungsplan

### Phase 1: Feature Extraction
- [ ] `FeatureSet` Klasse mit allen Ebenen-Features
- [ ] `FeatureExtractor` der aus Landmarks die Features extrahiert
- [ ] Unit Tests für Feature-Extraktion

### Phase 2: Hierarchical Scoring
- [ ] `HierarchicalScorer` Klasse
- [ ] Gate-Funktion implementieren
- [ ] Einzelne Feature-Match-Funktionen
- [ ] Unit Tests für Scoring

### Phase 3: Race Modifiers
- [ ] `RaceModifier` Klasse
- [ ] Vordefinierte Modifier (Elf, Dwarf, Orc, etc.)
- [ ] XML-Laden für custom Modifier
- [ ] Integration in Scorer

### Phase 4: Integration
- [ ] `LearningOrchestrator` umstellen auf HierarchicalScorer
- [ ] Phase-relevante Scores nutzen
- [ ] Logging der Einzelscores pro Ebene

### Phase 5: Validation
- [ ] Testset mit Ground-Truth erstellen
- [ ] Korrelation Score↔Visuelle Ähnlichkeit messen
- [ ] Feintuning der Ebenen-Gewichte basierend auf Daten

---

## Vorteile

1. **Strukturiert statt flach** - Klare Hierarchie
2. **Debuggbar** - Man sieht welche Ebene das Problem ist
3. **Keine Ping-Pong Gewichtung** - Ebenen haben feste Bedeutung
4. **Race-System integriert** - Saubere Trennung Learning/Generation
5. **Korreliert mit Phasen** - Jede Phase optimiert relevante Ebenen
6. **Erweiterbar** - Neue Features einfach in passende Ebene einfügen
