# FaceLearner v0.17.0 Checkpoint

## New Features in v0.17.0

### Preview Mode
- Text field at bottom of screen for pasting character codes
- **Preview** button - paste any BodyProperties and see the face rendered
- **Copy** button - copy current face to clipboard as BodyProperties
- Works without Init - can preview codes anytime

### UI Layout
```
[Init] [Learn] [Stop] [Generate] [Match] [Exit]

[_________Code Input Field_________] [Preview] [Copy]
```

### Code Format Supported
```xml
<BodyProperties version="4" age="35.0" weight="0.52" build="0.68" key="001CAC08..."/>
```

## All Features Summary

1. **Init** - Initialize Face API, Landmark Detector, Data Sources
2. **Learn** - Start learning loop (requires external dataset like LFW)
3. **Stop** - Stop learning, save Knowledge Tree
4. **Generate** - Create 20 varied characters → `generated_characters.xml`
5. **Match** - Match photos in `input_photos/` → `photo_matches.xml`
6. **Preview** - Paste code, see face in 3D
7. **Copy** - Copy current face code to clipboard

## File Structure
```
FaceLearner/
├── Core/
│   ├── FaceLearnerVM.cs         # Main ViewModel with all commands
│   ├── FaceController.cs        # Face manipulation + LoadFromBodyPropertiesXml
│   ├── FaceLearnerScreen.cs     # Screen setup
│   ├── FaceTableauTextureProvider.cs
│   ├── FaceTableauWidget.cs
│   └── LivingKnowledge/
│       ├── KnowledgeTree.cs     # Learning storage
│       ├── LearningOrchestrator.cs # Learning loop
│       └── FaceGenerator.cs     # Generate/Match tasks
├── ML/
│   ├── LandmarkDetector.cs      # Dlib 68-point detection
│   ├── ModelDownloader.cs       # Auto-download dlib model
│   └── DataSources/
│       ├── IFaceDataSource.cs
│       ├── DataSourceManager.cs
│       ├── GeneratedFaceSource.cs
│       ├── LFWDataSource.cs
│       └── CelebADataSource.cs
├── GUI/Prefabs/FaceLearnerScreen.xml
└── SubModule.cs / .xml / .csproj
```

## To Build
1. Set BANNERLORD_GAME_DIR environment variable
2. `dotnet restore`
3. `dotnet build`
