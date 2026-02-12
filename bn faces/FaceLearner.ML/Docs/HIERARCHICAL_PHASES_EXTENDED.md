# Hierarchisches Phasen-System - Erweitert

## Das Problem

```
AKTUELL:
  Phase 1: Coarse Search    → 70 Morphs aktiv, σ=1.0
  Phase 2: Proportions      → 40 Morphs aktiv
  Phase 3: Main Features    → 30 Morphs aktiv
  ...
  
  → System variiert ZU VIELE Morphs gleichzeitig
  → Verheddert sich, findet kein klares Optimum
  → Zufällige Verbesserungen werden durch andere Änderungen zunichte gemacht
```

## Die Lösung: 3-Ebenen Hierarchie

```
EBENE 1: HAUPTPHASEN (Was?)
    ├── 1. Gesichtsform
    ├── 2. Struktur  
    ├── 3. Große Features
    └── 4. Feine Details

EBENE 2: SUB-PHASEN (Welcher Teil?)
    Gesichtsform:
    ├── 1.1 Breite
    ├── 1.2 Höhe/Länge
    └── 1.3 Grundform (Round/Oval/Square)
    
    Struktur:
    ├── 2.1 Stirn
    ├── 2.2 Kiefer
    ├── 2.3 Kinn
    └── 2.4 Wangen

EBENE 3: SPEZIAL-PHASEN (Wie optimieren?)
    Für jede Sub-Phase:
    ├── Exploration (breite Suche)
    ├── Refinement (Feintuning)
    ├── Plateau Escape (wenn stuck)
    └── Lock-In (wenn gut genug)
```

---

## Detaillierte Phasen-Struktur

### 1. GESICHTSFORM (Foundation)

```
1.1 BREITE
    Aktive Morphs: Nur die 5-8 die Gesichtsbreite beeinflussen
    ├── Exploration: σ=0.8, 10-15 Iterationen
    ├── Refinement: σ=0.3, bis Score > 0.7 oder Plateau
    ├── Plateau Escape: Wenn stuck, größere Sprünge
    └── Lock-In: Wenn Score > 0.85, diese Morphs einfrieren
    
1.2 HÖHE/LÄNGE  
    Aktive Morphs: Nur die 5-8 die Gesichtslänge beeinflussen
    ├── Exploration: σ=0.8
    ├── Refinement: σ=0.3
    ├── Plateau Escape
    └── Lock-In
    
1.3 GRUNDFORM
    Aktive Morphs: Die restlichen Form-Morphs
    ├── Exploration
    ├── Refinement
    └── Lock-In

    → Erst wenn 1.1-1.3 alle > 0.7: Weiter zu Phase 2
```

### 2. STRUKTUR (Framework)

```
2.1 STIRN
    Aktive Morphs: Stirnhöhe, Stirnbreite, Stirnneigung
    Score: Nur Stirn-Features
    
2.2 KIEFER
    Aktive Morphs: Kieferwinkel, Kieferbreite, Kieferlinie
    Score: Nur Kiefer-Features
    
2.3 KINN
    Aktive Morphs: Kinnbreite, Kinnlänge, Kinnform
    Score: Nur Kinn-Features
    
2.4 WANGEN
    Aktive Morphs: Wangenhöhe, Wangenbreite, Wangenfülle
    Score: Nur Wangen-Features

    → Jede Sub-Phase hat eigene Exploration/Refinement/Lock-In
    → Erst wenn alle > 0.65: Weiter zu Phase 3
```

### 3. GROSSE FEATURES

```
3.1 NASE
    Sub-Sub-Phasen:
    ├── 3.1.1 Nasenbreite
    ├── 3.1.2 Nasenlänge
    ├── 3.1.3 Nasenbrücke
    └── 3.1.4 Nasenspitze
    
3.2 AUGEN
    Sub-Sub-Phasen:
    ├── 3.2.1 Augenbreite
    ├── 3.2.2 Augenabstand
    ├── 3.2.3 Augenhöhe (vertikal)
    └── 3.2.4 Augenform
    
3.3 MUND
    Sub-Sub-Phasen:
    ├── 3.3.1 Mundbreite
    ├── 3.3.2 Lippendicke
    └── 3.3.3 Mundposition
```

### 4. FEINE DETAILS

```
4.1 AUGENBRAUEN
4.2 LIPPEN-DETAILS
4.3 FALTEN/TEXTUR (falls relevant)
```

---

## Morph-Mapping

Jede Sub-Phase hat eine **definierte Liste von Morphs**:

```csharp
public static class MorphGroups
{
    // Phase 1.1: Gesichtsbreite
    public static readonly int[] FaceWidth = { 0, 1, 5, 22, 23, 45 };
    
    // Phase 1.2: Gesichtslänge  
    public static readonly int[] FaceHeight = { 2, 3, 6, 24, 25, 46 };
    
    // Phase 2.1: Stirn
    public static readonly int[] Forehead = { 10, 11, 12, 50, 51 };
    
    // Phase 2.2: Kiefer
    public static readonly int[] Jaw = { 20, 21, 22, 23, 55, 56 };
    
    // Phase 2.3: Kinn
    public static readonly int[] Chin = { 25, 26, 27, 57, 58 };
    
    // Phase 3.1: Nase
    public static readonly int[] Nose = { 30, 31, 32, 33, 34 };
    
    // ... etc
}
```

---

## Spezial-Phasen (Ebene 3)

### Exploration
```csharp
class ExplorationPhase
{
    float Sigma = 0.8f;           // Große Variation
    int MaxIterations = 15;       // Kurz!
    float TargetScore = 0.5f;     // Niedriges Ziel
    
    // Ziel: Schnell eine grobe Richtung finden
    // NICHT perfektionieren!
}
```

### Refinement
```csharp
class RefinementPhase
{
    float Sigma = 0.3f;           // Kleine Variation
    int MaxIterations = 25;
    float TargetScore = 0.75f;
    
    // Ziel: Gefundene Richtung verfeinern
    // Aufhören wenn gut genug oder Plateau
}
```

### Plateau Escape
```csharp
class PlateauEscapePhase
{
    // Aktiviert wenn: 10+ Iterationen ohne Verbesserung
    
    Strategy[] Strategies = {
        // 1. Größere Sprünge bei den aktiven Morphs
        new LargeSigmaStrategy { Sigma = 1.2f, Iterations = 5 },
        
        // 2. Einen verwandten Morph dazu nehmen
        new ExpandMorphSetStrategy { AddRelated = 1 },
        
        // 3. Kurz alles auf den zweitbesten Wert zurücksetzen
        new BacktrackStrategy { ToSecondBest = true },
        
        // 4. Aufgeben und weitergehen (mit Penalty)
        new SkipWithPenalty { PenaltyFactor = 0.9f }
    };
}
```

### Lock-In
```csharp
class LockInPhase
{
    // Aktiviert wenn: Score > 0.85 für diese Sub-Phase
    
    void Execute()
    {
        // Diese Morphs werden "eingefroren"
        // Sie können in späteren Phasen nur noch ±10% variieren
        // Das verhindert dass spätere Phasen frühere Arbeit zerstören
        
        foreach (var morph in CurrentActiveMorphs)
        {
            _lockedMorphs[morph] = new MorphLock
            {
                Value = _currentMorphs[morph],
                AllowedVariation = 0.10f  // Max ±10%
            };
        }
    }
}
```

---

## Ablauf-Beispiel

```
Target: George W. Bush
Start-Score: 0.15

=== PHASE 1: GESICHTSFORM ===

[1.1 Breite]
  Exploration: Score 0.15 → 0.35 (5 Morphs aktiv, 12 iter)
  Refinement:  Score 0.35 → 0.72 (18 iter)
  Lock-In:     Morphs {0,1,5,22,23} locked at current values
  
[1.2 Höhe]
  Exploration: Score 0.40 → 0.55 (6 Morphs aktiv, 10 iter)
  Refinement:  Score 0.55 → 0.68 (15 iter)
  Plateau!     Stuck at 0.68
  Plateau Escape: LargeSigma → Score 0.74
  Lock-In:     Morphs {2,3,6,24,25} locked
  
[1.3 Form]
  Exploration: Score 0.60 → 0.71 (8 iter)
  Refinement:  Score 0.71 → 0.78 (12 iter)
  Lock-In:     Remaining form morphs locked

Phase 1 Complete: Face Shape Score = 0.73
Locked Morphs: 18

=== PHASE 2: STRUKTUR ===

[2.1 Stirn]
  (5 Morphs aktiv, 18 bereits gelockt mit ±10% Spielraum)
  Exploration: 0.50 → 0.62
  Refinement:  0.62 → 0.70
  
[2.2 Kiefer]
  Exploration: 0.45 → 0.58
  Refinement:  0.58 → 0.65
  Plateau Escape: Needed, Score → 0.71
  
[2.3 Kinn]
  Exploration: 0.52 → 0.68
  Refinement:  0.68 → 0.75
  
Phase 2 Complete: Structure Score = 0.72
Locked Morphs: 30

=== PHASE 3: FEATURES ===
...
```

---

## Score-Berechnung Pro Phase

```csharp
public class PhaseScorer
{
    /// <summary>
    /// Berechnet Score NUR für die aktuelle Sub-Phase
    /// </summary>
    public float CalculateSubPhaseScore(SubPhase phase, FeatureSet target, FeatureSet current)
    {
        switch (phase)
        {
            case SubPhase.FaceWidth:
                return MatchValue(target.FaceWidth, current.FaceWidth);
                
            case SubPhase.FaceHeight:
                return MatchValue(target.FaceHeight, current.FaceHeight);
                
            case SubPhase.Jaw:
                return Average(
                    MatchValue(target.JawAngleLeft, current.JawAngleLeft),
                    MatchValue(target.JawAngleRight, current.JawAngleRight),
                    MatchValue(target.JawWidth, current.JawWidth)
                );
                
            case SubPhase.Nose:
                return Average(
                    MatchValue(target.NoseWidth, current.NoseWidth),
                    MatchValue(target.NoseLength, current.NoseLength),
                    MatchValue(target.NoseBridge, current.NoseBridge),
                    MatchValue(target.NoseTip, current.NoseTip)
                );
                
            // ... etc
        }
    }
    
    /// <summary>
    /// Gesamt-Score mit hierarchischer Gewichtung
    /// </summary>
    public float CalculateTotalScore(Dictionary<SubPhase, float> subScores)
    {
        // Ebene 1 Scores
        float faceShape = Average(
            subScores[SubPhase.FaceWidth],
            subScores[SubPhase.FaceHeight],
            subScores[SubPhase.FaceForm]
        );
        
        // Ebene 2 Scores (gated by Ebene 1)
        float structure = Average(
            subScores[SubPhase.Forehead],
            subScores[SubPhase.Jaw],
            subScores[SubPhase.Chin],
            subScores[SubPhase.Cheeks]
        ) * Gate(faceShape);
        
        // Ebene 3 Scores (gated by Ebene 1+2)
        float features = Average(
            subScores[SubPhase.Nose],
            subScores[SubPhase.Eyes],
            subScores[SubPhase.Mouth]
        ) * Gate(faceShape) * Gate(structure);
        
        return faceShape * 0.35f + structure * 0.30f + features * 0.35f;
    }
}
```

---

## Vorteile des 3-Ebenen Systems

### 1. Fokussiert
```
Statt 70 Morphs gleichzeitig: Nur 5-8 pro Sub-Phase
→ Klare Richtung, weniger Verheddung
```

### 2. Schnellere Konvergenz
```
Kleine Optimierungs-Probleme lösen sich schneller
5 Variablen optimieren ist 10x einfacher als 50
```

### 3. Schutz vor Rückschritten
```
Lock-In verhindert dass Phase 3 die Arbeit von Phase 1 zerstört
Gelockte Morphs haben nur ±10% Spielraum
```

### 4. Besseres Debugging
```
Log zeigt genau:
"Phase 2.2 Kiefer stuck at 0.58 after 25 iter"
→ Wir wissen EXAKT was nicht funktioniert
```

### 5. Adaptive Tiefe
```
Wenn Face Shape schnell 0.9 erreicht → Lock-In, weiter
Wenn Face Shape stuck bei 0.5 → Mehr Zeit, Plateau Escape
→ Zeit wird dort investiert wo sie gebraucht wird
```

---

## Vergleich Alt vs Neu

| Aspekt | Alt (Flach) | Neu (3-Ebenen) |
|--------|-------------|----------------|
| Morphs aktiv | 30-70 | 5-8 |
| Phase-Dauer | 40 iter fix | 10-30 iter adaptiv |
| Score-Fokus | Gesamt | Sub-Phase spezifisch |
| Rückschritt-Schutz | Keiner | Lock-In mit ±10% |
| Plateau-Handling | Global | Sub-Phase spezifisch |
| Debugging | Schwer | Präzise |

---

## Integration mit Race-System

```csharp
// Race-Modifier beeinflusst die TARGET-Werte pro Sub-Phase

[1.1 Breite] 
  Human Target: 0.52
  Elf Modifier: × 0.95
  Elf Target:   0.494
  
  → System optimiert auf schmaleres Gesicht!

[2.2 Kiefer]
  Human Target: JawAngle = 45°
  Elf Modifier: -5°
  Elf Target:   JawAngle = 40°
  
  → System optimiert auf spitzeren Kiefer!
```

---

## Nächste Schritte

1. **Morph-Mapping erstellen**
   - Welche Morphs beeinflussen welches Feature?
   - Das brauchen wir sowieso für besseres Learning

2. **Sub-Phase Scorer implementieren**
   - Pro Sub-Phase eigene Score-Berechnung
   - Landmark-basiert, nicht Ratio-basiert

3. **Lock-In Mechanismus**
   - Morphs einfrieren mit Varianz-Limit
   - Verhindert Rückschritte

4. **Logging erweitern**
   - Score pro Sub-Phase
   - Aktive Morphs pro Phase
   - Lock-In Status
