using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace HeirOfNumenor.Features.RingSystem
{
    /// <summary>
    /// Gauntlet screen for the Ring System with 3D scene background.
    /// Uses proper GameState integration via patched CreateScreen.
    /// </summary>
    public class GauntletRingScreen : ScreenBase, IHoNRingStateHandler, IGameStateListener
    {
        private GauntletLayer _gauntletLayer;
        private RingScreenVM _dataSource;
        private bool _closed;

        // 3D Scene components
        private Scene _scene;
        private SceneLayer _sceneLayer;
        private Camera _camera;
        
        // Sprite categories to load
        private SpriteCategory _mapBarCategory;
        private SpriteCategory _inventoryCategory;
        private SpriteCategory _clanCategory;
        private SpriteCategory _fullscreenCategory;
        private SpriteCategory _ringSystemCategory;
        
        // Context menu tracking
        private bool _contextMenuNeedsPositionUpdate = true;

        public HoNRingState HoNRingState { get; private set; }

        public GauntletRingScreen(HoNRingState ringState)
        {
            HoNRingState = ringState;
            if (HoNRingState != null)
            {
                HoNRingState.Handler = this;
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] GauntletRingScreen.OnInitialize starting...", Colors.Cyan));

                // Load required sprite categories BEFORE creating the UI
                LoadSpriteCategories();

                // Create ViewModel
                _dataSource = new RingScreenVM(CloseScreen);
                
                // Initialize 3D scene first (so it's behind the UI)
                OpenScene();
                
                // Create UI layer - use index 15 like inventory to stay below MapBar (202)
                _gauntletLayer = new GauntletLayer("RingScreenLayer", 15, shouldClear: false)
                {
                    IsFocusLayer = true
                };
                // Use less restrictive input - allow mouse to pass through to MapBar
                _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse | InputUsageMask.Keyboardkeys);
                
                AddLayer(_gauntletLayer);
                ScreenManager.TrySetFocus(_gauntletLayer);
                
                // Register all hotkey categories like inventory does - this is critical for MapBar panel switching
                _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
                
                // Set up input keys for Standard.TripleDialogCloseButtons
                var genericPanelCategory = HotKeyManager.GetCategory("GenericPanelGameKeyCategory");
                if (genericPanelCategory != null)
                {
                    _dataSource.SetInputKeys(
                        genericPanelCategory.GetHotKey("Exit"),       // Cancel/Leave
                        genericPanelCategory.GetHotKey("Confirm"),    // Done
                        genericPanelCategory.GetHotKey("Reset")       // Equip/Reset
                    );
                }
                
                _gauntletLayer.LoadMovie("RingScreen", _dataSource);
                
                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] GauntletRingScreen.OnInitialize completed!", Colors.Green));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] OnInitialize error: {ex.Message}", Colors.Red));
            }
        }

        private void LoadSpriteCategories()
        {
            try
            {
                SpriteData spriteData = UIResourceManager.SpriteData;
                TwoDimensionEngineResourceContext resourceContext = UIResourceManager.ResourceContext;
                ResourceDepot uiResourceDepot = UIResourceManager.ResourceDepot;

                // Load MapBar sprites (for the ring circle frames)
                _mapBarCategory = spriteData.SpriteCategories["ui_mapbar"];
                _mapBarCategory.Load((ITwoDimensionResourceContext)resourceContext, uiResourceDepot);

                // Load Inventory sprites (for panel backgrounds and buttons)
                _inventoryCategory = spriteData.SpriteCategories["ui_inventory"];
                _inventoryCategory.Load((ITwoDimensionResourceContext)resourceContext, uiResourceDepot);

                // Load Clan sprites (for additional brushes)
                _clanCategory = spriteData.SpriteCategories["ui_clan"];
                _clanCategory.Load((ITwoDimensionResourceContext)resourceContext, uiResourceDepot);

                // Load Fullscreen sprites (for bottom panel and buttons)
                _fullscreenCategory = spriteData.SpriteCategories["ui_fullscreens"];
                _fullscreenCategory.Load((ITwoDimensionResourceContext)resourceContext, uiResourceDepot);

                // Load Ring System custom sprites (for ring images and table background)
                if (spriteData.SpriteCategories.ContainsKey("ui_ring_system"))
                {
                    _ringSystemCategory = spriteData.SpriteCategories["ui_ring_system"];
                    _ringSystemCategory.Load((ITwoDimensionResourceContext)resourceContext, uiResourceDepot);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[RingSystem] Custom ring sprites loaded!", Colors.Green));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[RingSystem] ui_ring_system category not found - using fallback sprites", Colors.Yellow));
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] Sprite categories loaded!", Colors.Cyan));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] LoadSpriteCategories error: {ex.Message}", Colors.Yellow));
            }
        }

        private void OpenScene()
        {
            try
            {
                // Create a new scene
                _scene = Scene.CreateNewScene(initialize_physics: true, enable_decals: false);
                _scene.SetName("RingScreen3DScene");
                
                // Scene initialization data
                SceneInitializationData initData = new SceneInitializationData
                {
                    InitPhysicsWorld = false
                };
                
                // Load an existing scene - using crafting scene to match table image
                _scene.Read("crafting_menu_outdoor", ref initData);
                
                // Configure scene settings
                _scene.DisableStaticShadows(true);
                _scene.SetShadow(true);
                _scene.SetClothSimulationState(true);
                
                // Initialize camera
                InitializeCamera();
                
                // Create scene layer and add it first (background)
                _sceneLayer = new SceneLayer();
                _sceneLayer.IsFocusLayer = false;
                
                AddLayer(_sceneLayer);
                
                // Set up scene view
                _sceneLayer.SceneView.SetScene(_scene);
                _sceneLayer.SceneView.SetCamera(_camera);
                _sceneLayer.SceneView.SetSceneUsesShadows(true);
                _sceneLayer.SceneView.SetAcceptGlobalDebugRenderObjects(true);
                _sceneLayer.SceneView.SetRenderWithPostfx(true);
                _sceneLayer.SceneView.SetResolutionScaling(true);
                
                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] 3D Scene loaded successfully!", Colors.Green));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] OpenScene error: {ex.Message}", Colors.Red));
            }
        }

        private void InitializeCamera()
        {
            try
            {
                _camera = Camera.CreateCamera();
                
                // Try to find camera position from scene, or use default
                GameEntity cameraEntity = _scene.FindEntityWithTag("camera_point");
                
                if (cameraEntity != null)
                {
                    Vec3 dofParams = default;
                    cameraEntity.GetCameraParamsFromCameraScript(_camera, ref dofParams);
                    
                    float fov = _camera.GetFovVertical();
                    float aspectRatio = Screen.AspectRatio;
                    _camera.SetFovVertical(fov, aspectRatio, _camera.Near, _camera.Far);
                    
                    _scene.SetDepthOfFieldParameters(dofParams.x, dofParams.z, false);
                    _scene.SetDepthOfFieldFocus(dofParams.y);
                }
                else
                {
                    // Default camera setup if no camera point found
                    MatrixFrame cameraFrame = MatrixFrame.Identity;
                    cameraFrame.origin = new Vec3(0, -3, 1.6f);
                    cameraFrame.rotation.RotateAboutSide(MathF.PI / 2f);
                    
                    _camera.Frame = cameraFrame;
                    _camera.SetFovVertical(0.7f, Screen.AspectRatio, 0.1f, 12500f);
                }
                
                InformationManager.DisplayMessage(new InformationMessage(
                    "[RingSystem] Camera initialized!", Colors.Cyan));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[RingSystem] InitializeCamera error: {ex.Message}", Colors.Red));
            }
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            
            if (_closed)
                return;

            // Tick the scene for animations
            _scene?.Tick(dt);
            
            // Update camera on scene view
            if (_sceneLayer?.SceneView != null && _camera != null)
            {
                _sceneLayer.SceneView.SetCamera(_camera);
            }
            
            // Update ring animations (rotation, float effects)
            _dataSource?.UpdateAnimations(dt);
            
            // Set context menu position once when it becomes visible
            if (_dataSource != null && _dataSource.IsContextMenuVisible && _contextMenuNeedsPositionUpdate)
            {
                // Get mouse position relative to screen center for positioning
                float mouseX = Input.MousePositionPixel.X - (Screen.RealScreenResolutionWidth / 2f);
                float mouseY = Input.MousePositionPixel.Y - (Screen.RealScreenResolutionHeight / 2f);
                
                // Scale and offset position - add small offset so menu appears beside cursor
                _dataSource.ShowContextMenuAt(mouseX * 0.55f + 60f, mouseY * 0.55f);
                _contextMenuNeedsPositionUpdate = false;
            }
            else if (_dataSource != null && !_dataSource.IsContextMenuVisible)
            {
                _contextMenuNeedsPositionUpdate = true;
            }

            // Handle ESC key
            if (_gauntletLayer != null && _gauntletLayer.Input.IsHotKeyReleased("Exit"))
            {
                // If context menu is open, close it first
                if (_dataSource != null && _dataSource.IsContextMenuVisible)
                {
                    _dataSource.HideContextMenu();
                }
                else
                {
                    CloseScreen();
                }
            }
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
            _closed = true;
            if (_gauntletLayer != null)
            {
                ScreenManager.SetSuspendLayer(_gauntletLayer, true);
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (_gauntletLayer != null)
            {
                ScreenManager.SetSuspendLayer(_gauntletLayer, false);
                ScreenManager.TrySetFocus(_gauntletLayer);
            }
        }

        protected override void OnFinalize()
        {
            // NOTE: Do NOT unload sprite categories here!
            // They are shared globally and unloading them breaks other UI elements like MapBar.
            // The game manages their lifecycle.
            
            // Clean up scene resources
            if (_scene != null)
            {
                _scene.ManualInvalidate();
                _scene = null;
            }
            
            if (_sceneLayer?.SceneView != null)
            {
                _sceneLayer.SceneView.ClearAll(true, true);
            }
            _sceneLayer = null;
            _camera = null;

            if (_dataSource != null)
            {
                _dataSource.OnFinalize();
                _dataSource = null;
            }
            
            _gauntletLayer = null;

            base.OnFinalize();
        }

        private void CloseScreen()
        {
            if (_closed) return;
            _closed = true;
            
            // Pop the game state - framework will handle the screen cleanup
            Game.Current?.GameStateManager?.PopState();
        }

        // IGameStateListener implementation
        void IGameStateListener.OnActivate()
        {
        }

        void IGameStateListener.OnDeactivate()
        {
        }

        void IGameStateListener.OnInitialize()
        {
        }

        void IGameStateListener.OnFinalize()
        {
        }
    }
}
