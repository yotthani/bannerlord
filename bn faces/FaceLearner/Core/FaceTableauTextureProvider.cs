using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Tableaus;
using TaleWorlds.TwoDimension;

namespace FaceLearner.Core
{
    public class FaceTableauTextureProvider : TextureProvider
    {
        private BasicCharacterTableau _tableau;
        private TaleWorlds.Engine.Texture _texture;
        private TaleWorlds.TwoDimension.Texture _providedTexture;
        
        private float _zoomLevel = 0.82f;
        private float _heightOffset = -3.52f;
        private float _horizontalOffset = 0f;
        private float _characterScale = 1.0f;
        private float _characterYaw = 0.05f;
        
        private FieldInfo _sceneField;
        private MethodInfo _setCameraMethod;
        
        // Static camera frame - shared across all providers
        private static MatrixFrame _originalCameraFrame;
        private static bool _originalFrameSaved = false;
        
        // Instance-level spawn frame
        private MatrixFrame _originalSpawnFrame;
        private bool _originalSpawnFrameSaved = false;
        
        public static FaceTableauTextureProvider Instance { get; private set; }
        
        // Track if initial camera was applied (fixes first-load camera issue)
        private bool _cameraAppliedOnce = false;
        
        public bool IsTextureReady => _texture != null;
        
        public float ZoomLevel
        {
            get => _zoomLevel;
            set => _zoomLevel = MBMath.ClampFloat(value, 0.1f, 5f);
        }
        
        public float HeightOffset
        {
            get => _heightOffset;
            set => _heightOffset = MBMath.ClampFloat(value, -5f, 5f);
        }
        
        public float HorizontalOffset
        {
            get => _horizontalOffset;
            set => _horizontalOffset = MBMath.ClampFloat(value, -5f, 5f);
        }
        
        public float CharacterScale
        {
            get => _characterScale;
            set
            {
                _characterScale = MBMath.ClampFloat(value, 0.3f, 2.0f);
                ApplyCharacterScale(_characterScale);
            }
        }
        
        public float CharacterYaw
        {
            get => _characterYaw;
            set
            {
                _characterYaw = value;
                SetCharacterFrontal(value);
            }
        }

        public string HeroVisualCode
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _tableau?.DeserializeCharacterCode(value);
                    
                    // Force visuals dirty to trigger body mesh rebuild with new weight/build
                    ForceVisualsRefresh();
                }
            }
        }
        
        /// <summary>
        /// Directly set body properties on the tableau (bypass HeroVisualCode parsing)
        /// </summary>
        public void SetBodyProperties(float age, float weight, float build)
        {
            try
            {
                var type = typeof(BasicCharacterTableau);
                
                // Try to find and set _bodyProperties directly
                var bpField = type.GetField("_bodyProperties", BindingFlags.NonPublic | BindingFlags.Instance);
                if (bpField != null)
                {
                    var currentBp = (BodyProperties)bpField.GetValue(_tableau);
                    var newDynamic = new DynamicBodyProperties(age, weight, build);
                    var newBp = new BodyProperties(newDynamic, currentBp.StaticProperties);
                    bpField.SetValue(_tableau, newBp);
                    SubModule.Log($"SetBodyProperties direct: age={age:F1}, weight={weight:F2}, build={build:F2}");
                    
                    ForceVisualsRefresh();
                }
                else
                {
                    SubModule.Log("SetBodyProperties: _bodyProperties field not found");
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"SetBodyProperties error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Force the tableau to rebuild visuals (needed for weight/build changes)
        /// </summary>
        private void ForceVisualsRefresh()
        {
            try
            {
                var type = typeof(BasicCharacterTableau);
                
                // Method 1: Set visuals dirty
                var visualsDirtyField = type.GetField("_isVisualsDirty", BindingFlags.NonPublic | BindingFlags.Instance);
                if (visualsDirtyField != null)
                {
                    visualsDirtyField.SetValue(_tableau, true);
                    SubModule.Log("ForceVisualsRefresh: Set _isVisualsDirty=true");
                }
                
                // Method 2: Also try to call RefreshCharacterTableau if available
                var refreshMethod = type.GetMethod("RefreshCharacterTableau", BindingFlags.NonPublic | BindingFlags.Instance);
                if (refreshMethod != null)
                {
                    refreshMethod.Invoke(_tableau, null);
                    SubModule.Log("ForceVisualsRefresh: Called RefreshCharacterTableau");
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"ForceVisualsRefresh error: {ex.Message}");
            }
        }

        public bool CurrentlyRotating
        {
            set => _tableau?.RotateCharacter(value);
        }
        
        /// <summary>
        /// Set the character rotation using the internal frame (like the game does for mouse rotation)
        /// </summary>
        public bool SetCharacterFrontal(float yawOffset = 0.05f)
        {
            try
            {
                var type = typeof(BasicCharacterTableau);
                
                // Get fields
                var charsField = type.GetField("_currentCharacters", BindingFlags.NonPublic | BindingFlags.Instance);
                var spawnFrameField = type.GetField("_initialSpawnFrame", BindingFlags.NonPublic | BindingFlags.Instance);
                var rotationField = type.GetField("_mainCharacterRotation", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (charsField == null || spawnFrameField == null)
                {
                    return false;
                }
                
                var chars = charsField.GetValue(_tableau) as GameEntity[];
                
                if (chars == null) return false;
                
                // Save original spawn frame on first call
                if (!_originalSpawnFrameSaved)
                {
                    _originalSpawnFrame = (MatrixFrame)spawnFrameField.GetValue(_tableau);
                    _originalSpawnFrameSaved = true;
                }
                
                // Store yaw for ApplyCharacterScale to use
                _characterYaw = yawOffset;
                
                // Apply rotation AND scale to frame
                var frame = _originalSpawnFrame;
                frame.rotation.RotateAboutUp(yawOffset);
                
                // Apply current scale using ApplyScaleLocal (same as AgentVisuals)
                frame.rotation.ApplyScaleLocal(_characterScale);
                
                // Set on both character entities using SetGlobalFrame
                for (int i = 0; i < chars.Length; i++)
                {
                    if (chars[i] != null)
                    {
                        chars[i].SetGlobalFrame(in frame);
                    }
                }
                
                // Also update the internal rotation value so mouse dragging continues from our offset
                if (rotationField != null)
                {
                    rotationField.SetValue(_tableau, yawOffset);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"SetCharacterFrontal error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Apply scale to character by modifying _initialSpawnFrame and triggering refresh.
        /// This mimics how AgentVisuals applies scale: frame.rotation.ApplyScaleLocal(scale)
        /// The scale must be set BEFORE FillEntityWithBodyMeshes is called.
        /// </summary>
        private bool ApplyCharacterScale(float scale)
        {
            try
            {
                var type = typeof(BasicCharacterTableau);
                var spawnFrameField = type.GetField("_initialSpawnFrame", BindingFlags.NonPublic | BindingFlags.Instance);
                var visualsDirtyField = type.GetField("_isVisualsDirty", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (spawnFrameField == null) return false;
                
                // Save original spawn frame on first call (before any scaling)
                if (!_originalSpawnFrameSaved)
                {
                    _originalSpawnFrame = (MatrixFrame)spawnFrameField.GetValue(_tableau);
                    _originalSpawnFrameSaved = true;
                }
                
                // Create scaled frame from original (like AgentVisuals.Refresh does)
                var frame = _originalSpawnFrame;
                frame.rotation.ApplyScaleLocal(scale);
                
                // Set the scaled frame as the new _initialSpawnFrame
                // This way when RefreshCharacterTableau runs, it will use this scaled frame
                spawnFrameField.SetValue(_tableau, frame);
                
                // Mark visuals as dirty to trigger refresh with the new scale
                if (visualsDirtyField != null)
                {
                    visualsDirtyField.SetValue(_tableau, true);
                }
                
                SubModule.Log($"ApplyCharacterScale: {scale:F2} (frame scale applied)");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"ApplyCharacterScale error: {ex.Message}");
                return false;
            }
        }

        public FaceTableauTextureProvider()
        {
            Instance = this;
            _tableau = new BasicCharacterTableau();
            
            // Setup reflection - but don't USE it yet
            var type = typeof(BasicCharacterTableau);
            _sceneField = type.GetField("_tableauScene", BindingFlags.NonPublic | BindingFlags.Instance);
            _setCameraMethod = type.GetMethod("SetCamera", BindingFlags.NonPublic | BindingFlags.Instance);
            
            SubModule.Log("TextureProvider created");
        }
        
        /// <summary>
        /// Set a static standing animation instead of the idle animation
        /// Must be called AFTER character is loaded (may need multiple attempts)
        /// </summary>
        public bool SetStaticAnimation()
        {
            try
            {
                // Disable any rotation
                _tableau?.RotateCharacter(false);
                
                var type = typeof(BasicCharacterTableau);
                var charsField = type.GetField("_currentCharacters", BindingFlags.NonPublic | BindingFlags.Instance);
                if (charsField == null) return false;
                
                var chars = charsField.GetValue(_tableau) as GameEntity[];
                if (chars == null || chars.Length == 0) return false;
                
                // act_character_developer_idle is a static pose used in character creator
                // blendPeriodOverride: 0 = instant switch, no blending from old animation
                var standAction = ActionIndexCache.Create("act_character_developer_idle");
                
                bool success = false;
                for (int i = 0; i < chars.Length; i++)
                {
                    var entity = chars[i];
                    if (entity?.Skeleton != null)
                    {
                        // Force the static animation
                        entity.Skeleton.SetAgentActionChannel(0, standAction, forceFaceMorphRestart: true, blendPeriodOverride: 0f);
                        
                        // Freeze skeleton to prevent any animation updates
                        entity.Skeleton.Freeze(true);
                        
                        success = true;
                    }
                }
                
                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Resume the normal idle animation
        /// </summary>
        public bool ResumeIdleAnimation()
        {
            try
            {
                var type = typeof(BasicCharacterTableau);
                var charsField = type.GetField("_currentCharacters", BindingFlags.NonPublic | BindingFlags.Instance);
                if (charsField == null) return false;
                
                var chars = charsField.GetValue(_tableau) as GameEntity[];
                if (chars == null) return false;
                
                var idleAction = ActionIndexCache.Create("act_inventory_idle");
                
                bool success = false;
                for (int i = 0; i < chars.Length; i++)
                {
                    var entity = chars[i];
                    if (entity?.Skeleton != null)
                    {
                        entity.Skeleton.Freeze(false);
                        entity.Skeleton.SetAgentActionChannel(0, idleAction, forceFaceMorphRestart: true, blendPeriodOverride: 0f);
                        success = true;
                    }
                }
                
                if (success) SubModule.Log("Animation resumed");
                return success;
            }
            catch (Exception ex)
            {
                SubModule.Log($"ResumeIdleAnimation error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Call this AFTER texture is ready (from Start button)
        /// </summary>
        public bool ApplyCamera()
        {
            try
            {
                var scene = _sceneField?.GetValue(_tableau) as Scene;
                if (scene == null) { SubModule.Log("ApplyCamera: no scene"); return false; }
                
                var camEntity = scene.FindEntityWithTag("camera_instance");
                if (camEntity == null) { SubModule.Log("ApplyCamera: no camera"); return false; }
                
                if (!_originalFrameSaved)
                {
                    _originalCameraFrame = camEntity.GetGlobalFrame();
                    _originalFrameSaved = true;
                    SubModule.Log($"Original camera pos saved: {_originalCameraFrame.origin}");
                }
                
                var frame = _originalCameraFrame;
                // Lower zoomLevel = closer to face
                float fwd = (1f - _zoomLevel) * 3f;
                frame.origin = frame.origin + frame.rotation.f * fwd;
                // Positive height = camera moves up = char appears lower
                frame.origin = frame.origin + frame.rotation.u * _heightOffset;
                // Positive horizontal = camera moves right = char appears left
                frame.origin = frame.origin + frame.rotation.s * _horizontalOffset;
                
                camEntity.SetGlobalFrame(in frame);
                _setCameraMethod?.Invoke(_tableau, null);
                
                SubModule.Log($"ApplyCamera: zoom={_zoomLevel}, height={_heightOffset}, fwd={fwd:F2}");
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"ApplyCamera error: {ex.Message}");
                return false;
            }
        }

        public override void Tick(float dt)
        {
            _tableau?.OnTick(dt);
            
            // Auto-apply camera once scene is ready (fixes initial preview camera issue)
            if (!_cameraAppliedOnce && _tableau != null)
            {
                var scene = _sceneField?.GetValue(_tableau) as Scene;
                if (scene != null)
                {
                    var camEntity = scene.FindEntityWithTag("camera_instance");
                    if (camEntity != null)
                    {
                        ApplyCamera();
                        _cameraAppliedOnce = true;
                    }
                }
            }
            
            if (_tableau != null && _texture != _tableau.Texture)
            {
                // Release old texture wrapper if it exists
                var oldTexture = _providedTexture;
                
                _texture = _tableau.Texture;
                _providedTexture = _texture != null 
                    ? new TaleWorlds.TwoDimension.Texture(new EngineTexture(_texture)) 
                    : null;
                
                // Try to dispose old wrapper (may not be disposable)
                (oldTexture as IDisposable)?.Dispose();
            }
        }

        public override void SetTargetSize(int width, int height)
        {
            base.SetTargetSize(width, height);
            _tableau?.SetTargetSize(width, height);
        }

        public override void Clear(bool clearNextFrame)
        {
            if (Instance == this) Instance = null;
            _tableau?.OnFinalize();
            base.Clear(clearNextFrame);
        }

        protected override TaleWorlds.TwoDimension.Texture OnGetTextureForRender(TwoDimensionContext ctx, string name)
        {
            return _providedTexture;
        }

        public bool SaveToFile(string path)
        {
            if (_texture == null) return false;
            try 
            { 
                // Ensure directory exists
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                    
                _texture.SaveToFile(path, false); 
                return true; 
            }
            catch { return false; }
        }
    }
}
