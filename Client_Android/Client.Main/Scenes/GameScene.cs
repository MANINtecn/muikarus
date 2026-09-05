// GameScene.cs
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Objects;
using Client.Main.Core.Utilities;
using Client.Main.Networking.PacketHandling.Handlers; // For CharacterClassNumber
using Client.Main.Controllers;
using Microsoft.Extensions.Logging;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly PlayerObject _hero = new();
        private readonly MainControl _main;
        private WorldControl _nextWorld; // Used for map changes
        private LoadingScreenControl _loadingScreen; // For initial load and map changes
        private MapListControl _mapListControl;
        private ChatLogWindow _chatLog;
        private MoveCommandWindow _moveCommandWindow;
        private ChatInputBoxControl _chatInput;
        private InventoryControl _inventoryControl; // Dodaj to pole
        private NotificationManager _notificationManager;
        private readonly (string Name, CharacterClassNumber Class, ushort Level) _characterInfo;
        private KeyboardState _previousKeyboardState;
        private bool _isChangingWorld = false;
        private readonly List<(ServerMessage.MessageType Type, string Message)> _pendingNotifications = new();
        private CharacterInfoWindowControl _characterInfoWindow;
        private MobileControlsOverlay _mobileControls;
        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<GameScene>();
        private MapNameControl _currentMapNameControl; // Track active map name display

        // ───────────────────────── Properties ─────────────────────────
        public PlayerObject Hero => _hero;

        public static readonly IReadOnlyDictionary<byte, Type> MapWorldRegistry = new Dictionary<byte, Type>
        {
            { 0, typeof(LorenciaWorld) },
            { 1, typeof(DungeonWorld) },
            { 2, typeof(DeviasWorld) },
            { 3, typeof(NoriaWorld) },
            { 51, typeof(ElvelandWorld) },
            { 4, typeof(LostTowerWorld) },
            // { 5, typeof(Exile) },
            { 6, typeof(StadiumWorld) },
            { 7, typeof(AtlansWorld) },
            { 8, typeof(TarkanWorld) },
            { 9, typeof(DevilSquareWorld) },
            { 10, typeof(IcarusWorld) },
            // { 11, typeof(BloodCastleWorld) },
            // { 12, "Blood Castle 2" },
            // { 13, "Blood Castle 3" },
            // { 14, "Blood Castle 4" },
            // { 15, "Blood Castle 5" },
            // { 16, "Blood Castle 6" },
            // { 17, "Blood Castle 7" },
            // { 18, "Chaos Castle 1" },
            // { 19, "Chaos Castle 2" },
            // { 20, "Chaos Castle 3" },
            // { 21, "Chaos Castle 4" },
            // { 22, "Chaos Castle 5" },
            // { 23, "Chaos Castle 6" },
            // { 24, "Kalima 1" },
            // { 25, "Kalima 2" },
            // { 26, "Kalima 3" },
            // { 27, "Kalima 4" },
            // { 28, "Kalima 5" },
            // { 29, "Kalima 6" },
            { 30, typeof(World031World) },
            { 31, typeof(World032World) },
            // { 32, typeof(World033World)},
            { 33, typeof(World034World) },
            { 34, typeof(World035World) },
            // { 36, typeof(World036World) },
            // { 37, "Kantru1" },
            // { 38, "Kantru2" },
            // { 39, "Kantru3" },
            // Add additional maps according to mapId
        };

        private static readonly Dictionary<ServerMessage.MessageType, Color> NotificationColors = new Dictionary<ServerMessage.MessageType, Color>
        {
            { ServerMessage.MessageType.GoldenCenter, Color.Goldenrod },
            { ServerMessage.MessageType.BlueNormal,   new Color(100, 150, 255) },
            { ServerMessage.MessageType.GuildNotice,  new Color(144, 238, 144) },
        };

        // ──────────────────────── Constructors ────────────────────────
        public GameScene((string Name, CharacterClassNumber Class, ushort Level) characterInfo)
        {
            _characterInfo = characterInfo;
            _logger?.LogDebug($"GameScene constructor called for Character: {_characterInfo.Name} ({_characterInfo.Class})");

            _main = new MainControl(MuGame.Network.GetCharacterState());
            Controls.Add(_main);
            Controls.Add(NpcShopControl.Instance);

            _mapListControl = new MapListControl { Visible = false };

            _chatLog = new ChatLogWindow
            {
                X = 5,
                Y = MuGame.Instance.Height - 160 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            Controls.Add(_chatLog);

            _chatInput = new ChatInputBoxControl(_chatLog, MuGame.AppLoggerFactory)
            {
                X = 5,
                Y = MuGame.Instance.Height - 65 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            Controls.Add(_chatInput);

            _pendingNotifications.AddRange(ChatMessageHandler.TakePendingServerMessages());
            _notificationManager = new NotificationManager();
            Controls.Add(_notificationManager);
            _notificationManager.BringToFront();

            _inventoryControl = new InventoryControl(MuGame.Network);
            Controls.Add(_inventoryControl);

            _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Game...", AutoDismissTimeout = 5.0f };
            Controls.Add(_loadingScreen);
            _loadingScreen.BringToFront();

            _moveCommandWindow = new MoveCommandWindow(MuGame.AppLoggerFactory, MuGame.Network);
            Controls.Add(_moveCommandWindow);
            _moveCommandWindow.MapWarpRequested += OnMapWarpRequested;

            _characterInfoWindow = new CharacterInfoWindowControl { X = 20, Y = 50, Visible = false };
            Controls.Add(_characterInfoWindow);

            _mobileControls = new MobileControlsOverlay(this, _hero, _inventoryControl, _characterInfoWindow, _moveCommandWindow);
            Controls.Add(_mobileControls);

            _chatInput.BringToFront();
            _mobileControls.BringToFront();
            DebugPanel.BringToFront();
            Cursor.BringToFront();

            OnScreenLogger.OnLogged += OnOnScreenLogged;
        }

        private void OnOnScreenLogged(string message, LogLevel level, string category)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                try
                {
                    if (_chatLog != null && _chatLog.Status != GameControlStatus.Disposed)
                    {
                        _chatLog.AddMessage("SYS", message, MessageType.System);
                    }
                }
                catch { }
            });
        }

        public override void Dispose()
        {
            OnScreenLogger.OnLogged -= OnOnScreenLogged;
            base.Dispose();
        }

        public GameScene() : this(GetCharacterInfoFromState()) { }

        private static (string Name, CharacterClassNumber Class, ushort Level) GetCharacterInfoFromState()
        {
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                return (state.Name ?? "Unknown", state.Class, state.Level);
            }
            return ("Unknown", CharacterClassNumber.DarkKnight, 1);
        }

        // ───────────────────── Content Loading (Progressive) ─────────────────────
        private void UpdateLoadProgress(string message, float progress)
        {
            OnScreenLogger.Log(message);
            MuGame.ScheduleOnMainThread(() => // Ensure UI updates are on the main thread
            {
                if (_loadingScreen != null)
                {
                    _loadingScreen.Message = message;
                    _loadingScreen.Progress = progress;
                }
            });
        }

        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            try
            {
                UpdateLoadProgress("Iniciando carregamento do jogo...", 0.0f);

                // 1. Hero Setup
                UpdateLoadProgress("Configurando dados do personagem...", 0.05f);

                var charState = MuGame.Network?.GetCharacterState();
                if (charState == null)
                {
                    UpdateLoadProgress("Erro: CharacterState nulo.", 1.0f);
                    OnScreenLogger.Log("ERRO: CharacterState nulo ao entrar no mundo.", LogLevel.Error);
                    _logger?.LogDebug("CharacterState is null in GameScene.Load, cannot proceed.");
                    return;
                }

                _hero.CharacterClass = _characterInfo.Class;
                _hero.Name = _characterInfo.Name;

                charState.UpdateCoreCharacterInfo(
                    charState.Id,
                    _characterInfo.Name,
                    _characterInfo.Class,
                    _characterInfo.Level,
                    charState.PositionX, 
                    charState.PositionY,
                    charState.MapId
                );

                _hero.NetworkId = charState.Id;
                _hero.Location = new Vector2(charState.PositionX, charState.PositionY);
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: _hero.NetworkId set to {charState.Id:X4}, Location set to ({charState.PositionX}, {charState.PositionY}).");

                UpdateLoadProgress("Dados do heroi aplicados.", 0.1f);

                // 2. Determine Initial World (Quick)
                UpdateLoadProgress("Determinando mapa inicial...", 0.15f);
                Type initialWorldType = typeof(LorenciaWorld);
                if (charState != null && MapWorldRegistry.TryGetValue((byte)charState.MapId, out var mappedType))
                {
                    initialWorldType = mappedType;
                }
                else
                {
                    _logger?.LogDebug($"GameScene.Load: Unknown MapId: {charState?.MapId}. Defaulting to Lorencia.");
                }
                UpdateLoadProgress($"Mapa definido: {initialWorldType.Name}.", 0.2f);

                // 3. Instantiate and Initialize World
                UpdateLoadProgress($"Instanciando mapa: {initialWorldType.Name}...", 0.25f);

                if (World != null)
                {
                    Controls.Remove(World);
                    World.Dispose();
                    World = null;
                }

                var worldInstance = (WorldControl)Activator.CreateInstance(initialWorldType);
                if (worldInstance is WalkableWorldControl walkable)
                {
                    walkable.Walker = _hero;
                    if (walkable.Walker.NetworkId != charState.Id)
                    {
                        walkable.Walker.NetworkId = charState.Id;
                    }
                }

                Controls.Add(worldInstance);
                World = worldInstance;
                World.Objects.Add(_hero);

                UpdateLoadProgress($"Carregando terreno e modelos ({initialWorldType.Name})...", 0.35f);
                try
                {
                    var worldInitTask = worldInstance.Initialize();
                    var finishedTask = await Task.WhenAny(worldInitTask, Task.Delay(8000));
                    if (finishedTask != worldInitTask)
                    {
                        OnScreenLogger.Log($"[MAP] Carregamento de {initialWorldType.Name} demorou >8s. Forcando Status=Ready.", LogLevel.Warning);
                    }
                    else
                    {
                        await worldInitTask;
                    }
                }
                catch (Exception ex)
                {
                    OnScreenLogger.Log($"[MAP] Erro inicializando {initialWorldType.Name}: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    // Enforce Ready status and visibility so the 3D scene & terrain are NEVER skipped
                    worldInstance.Status = GameControlStatus.Ready;
                    worldInstance.Visible = true;
                    if (worldInstance.Terrain != null)
                    {
                        worldInstance.Terrain.Status = GameControlStatus.Ready;
                        worldInstance.Terrain.Visible = true;
                    }
                }

                UpdateLoadProgress($"Mapa {initialWorldType.Name} pronto.", 0.6f);

                if (worldInstance is WalkableWorldControl walkableAfterInit)
                {
                    walkableAfterInit.Walker = _hero;
                    if (walkableAfterInit.Walker.NetworkId != charState.Id)
                    {
                        walkableAfterInit.Walker.NetworkId = charState.Id;
                    }
                }

                // Force Hero position & immediate camera synchronization
                _hero.World = worldInstance;
                _hero.Location = new Vector2(charState.PositionX, charState.PositionY);
                _hero.Position = _hero.TargetPosition;
                _hero.MoveTargetPosition = _hero.TargetPosition;
                _hero.Update(new GameTime());

                OnScreenLogger.Log($"[MAP] Entrou em {initialWorldType.Name} (MapId: {charState.MapId}, Hero: {charState.PositionX},{charState.PositionY})", LogLevel.Information);

                // 4. Load Hero Assets
                UpdateLoadProgress("Carregando aparencia do heroi...", 0.65f);
                if (_hero.Status == GameControlStatus.NonInitialized || _hero.Status == GameControlStatus.Initializing)
                {
                    ushort expectedNetworkId = charState.Id;
                    var heroLoadTask = _hero.Load();
                    await Task.WhenAny(heroLoadTask, Task.Delay(3000));
                    if (_hero.NetworkId != expectedNetworkId)
                    {
                        _hero.NetworkId = expectedNetworkId;
                    }
                }
                UpdateLoadProgress("Heroi pronto.", 0.80f);

                // 5. Import Pending Objects
                UpdateLoadProgress("Importando monstros e NPCs...", 0.85f);
                if (World.Status == GameControlStatus.Ready)
                {
                    try
                    {
                        await Task.WhenAny(Task.WhenAll(ImportPendingRemotePlayers(), ImportPendingNpcsMonsters()), Task.Delay(2000));
                    }
                    catch { }
                }
                UpdateLoadProgress("Entidades importadas.", 0.95f);

                // Preload sounds for dropped items
                UpdateLoadProgress("Inicializando sons...", 0.96f);
                try { PreloadSounds(); } catch { }
                UpdateLoadProgress("Sons prontos.", 0.97f);

                if (World is WalkableWorldControl finalWalkable)
                {
                    if (finalWalkable.Walker?.NetworkId != charState.Id)
                    {
                        finalWalkable.Walker.NetworkId = charState.Id;
                    }
                }

                // Non-blocking notification to server in background if supported
                if (MuGame.Network != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await MuGame.Network.SendClientReadyAfterMapChangeAsync();
                        }
                        catch { /* Ignore */ }
                    });
                }
            }
            catch (Exception ex)
            {
                OnScreenLogger.Log($"ERRO no carregamento: {ex.Message}", LogLevel.Error);
                _logger?.LogError(ex, "Error during GameScene.LoadSceneContentWithProgress.");
            }
            finally
            {
                OnScreenLogger.Log(">>> Carregamento concluido com sucesso! Ativando Lorencia 3D.");
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (_loadingScreen != null)
                    {
                        _loadingScreen.Visible = false;
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                    }
                    if (_main != null)
                    {
                        _main.Visible = true;
                    }
                    if (World != null)
                    {
                        World.Visible = true;
                    }
                    UpdateLoadProgress("Jogo pronto!", 1.0f);
                });
            }
        }

        public override async Task Initialize()
        {
            if (Status != GameControlStatus.NonInitialized)
                return;

            Status = GameControlStatus.Initializing;
            OnScreenLogger.Log("GameScene: Inicializando mundo e heroi...");

            try
            {
                // Ensure loading screen is initialized so it can draw progress and diagnostic logs
                if (_loadingScreen != null && _loadingScreen.Status == GameControlStatus.NonInitialized)
                {
                    try { await _loadingScreen.Initialize(); } catch { }
                }

                // Execute the progressive load directly (World, Hero, NPCs, Sounds)
                await LoadSceneContentWithProgress(UpdateLoadProgress);

                Status = GameControlStatus.Ready;
                OnScreenLogger.Log("GameScene: Pronto! Lorencia 3D ativa!");

                // Initialize secondary UI controls sequentially in background without blocking world entry
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var uiControls = new GameControl[] { _main, _chatLog, _chatInput, _notificationManager, _inventoryControl, _moveCommandWindow, _characterInfoWindow };
                        foreach (var ctrl in uiControls)
                        {
                            if (ctrl != null && ctrl.Status == GameControlStatus.NonInitialized)
                            {
                                try
                                {
                                    await ctrl.Initialize();
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning(ex, "Failed to initialize UI control {CtrlType}", ctrl.GetType().Name);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to initialize secondary UI controls");
                    }
                });
            }
            catch (Exception ex)
            {
                OnScreenLogger.Log($"ERRO fatal ao inicializar GameScene: {ex.Message}", LogLevel.Error);
                _logger?.LogError(ex, "Fatal error initializing GameScene.");
                Status = GameControlStatus.Error;
            }
        }

        public override async Task Load()
        {
            // This method is called by BaseScene.Initialize() if LoadSceneContentWithProgress is not overridden,
            // OR if the overridden method calls base.Load().
            // For GameScene, we want the progressive loading, so we'll call it from here if this Load is hit.
            // However, with the new structure, InitializeWithProgressReporting should call LoadSceneContentWithProgress directly.
            // This is a fallback / ensures old paths might still work or for clarity.
            if (Status == GameControlStatus.Initializing) // Check if we are already in the new init flow
            {
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
            else
            {
                // Fallback to old behavior or log a warning
                _logger?.LogDebug("GameScene.Load() called outside of InitializeWithProgressReporting flow. Consider refactoring.");
                await base.Load(); // Which is empty in BaseScene, then calls derived GameScene's old Load logic
            }
        }

        private async void OnMapWarpRequested(int mapIndex)
        {
            _logger?.LogDebug($"Player requested warp to map index: {mapIndex}");
            _chatLog.AddMessage("System", $"Warping to map index {mapIndex} requested.", MessageType.System);

            try
            {
                await MuGame.Network.SendWarpRequestAsync((ushort)mapIndex);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, $"Error sending warp request for map index {mapIndex}.");
                _chatLog.AddMessage("System", $"Error warping: {ex.Message}", MessageType.Error);
            }
        }


        // ─────────────────── Map Change Logic (Remains largely the same) ───────────────────
        public async Task ChangeMap(Type worldType)
        {
            _isChangingWorld = true;

            if (_loadingScreen == null) // Recreate if disposed, or handle if just hidden
            {
                _loadingScreen = new LoadingScreenControl { Visible = true };
                Controls.Add(_loadingScreen);
                _loadingScreen.BringToFront();
            }
            else
            {
                _loadingScreen.Visible = true;
            }
            _loadingScreen.Message = $"Loading {worldType.Name}…";
            _loadingScreen.Progress = 0f; // Reset progress for map change
            _main.Visible = false;

            var previousWorld = World;
            _nextWorld = (WorldControl)Activator.CreateInstance(worldType)
                ?? throw new InvalidOperationException($"Cannot create world: {worldType}");

            if (_nextWorld is WalkableWorldControl walkable)
                walkable.Walker = _hero;

            // Add to controls and set World property BEFORE Initialize.
            Controls.Add(_nextWorld);
            World = _nextWorld; // Temporarily set to new world for its Initialize
            World.Objects.Add(_hero); // Add hero to new world

            _loadingScreen.Progress = 0.1f;
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: Initializing new world...");
            try
            {
                await _nextWorld.Initialize();
            }
            catch (Exception ex)
            {
                OnScreenLogger.Log($"[WARP] Erro ao carregar {worldType.Name}: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _nextWorld.Status = GameControlStatus.Ready;
                _nextWorld.Visible = true;
                if (_nextWorld.Terrain != null)
                {
                    _nextWorld.Terrain.Status = GameControlStatus.Ready;
                    _nextWorld.Terrain.Visible = true;
                }
            }
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: New world initialized. Status: {_nextWorld.Status}");
            _loadingScreen.Progress = 0.7f;

            if (previousWorld != null)
            {
                Controls.Remove(previousWorld);
                previousWorld.Dispose();
                _logger?.LogDebug("GameScene.ChangeMap: Disposed previous world.");
            }

            _nextWorld = null;

            if (World.Status == GameControlStatus.Ready)
            {
                _loadingScreen.Progress = 0.8f;
                _logger?.LogDebug("GameScene.ChangeMap: World is Ready. Importing pending objects...");
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
            }
            else
            {
                _logger?.LogDebug($"GameScene.ChangeMap: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }
            _loadingScreen.Progress = 0.95f;

            try
            {
                if (MuGame.Network != null)
                {
                    await MuGame.Network.SendClientReadyAfterMapChangeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending ClientReadyAfterMapChange in ChangeMap.");
            }
            finally
            {
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (_loadingScreen != null)
                    {
                        _loadingScreen.Visible = false;
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                    }
                    if (_main != null)
                    {
                        _main.Visible = true;
                    }
                    _isChangingWorld = false;
                });
            }

            if (!string.IsNullOrEmpty(World.Name))
            {
                // Hide previous map name control if exists
                if (_currentMapNameControl != null)
                {
                    Controls.Remove(_currentMapNameControl);
                    _currentMapNameControl = null;
                }

                // Create and show new map name control
                _currentMapNameControl = new MapNameControl { LabelText = World.Name };
                Controls.Add(_currentMapNameControl);
                _currentMapNameControl.BringToFront();
                _chatLog.BringToFront();
                _chatInput.BringToFront();
                _mapListControl?.BringToFront();
                DebugPanel.BringToFront();
                Cursor.BringToFront();
            }
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: ChangeMap completed.");
        }

        public async Task ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            await ChangeMap(typeof(T));
        }

        // ──────────────────── Debug & Test Methods ────────────────────
        private void AddChatTestData()
        {
            if (_chatLog == null) return;
            _chatLog.AddMessage(string.Empty, "Welcome to the world of Mu!", MessageType.System);
            _chatLog.AddMessage("Player1", "Hello everyone!", MessageType.Chat);
            _chatLog.AddMessage("System", "Server will restart in 5 minutes.", MessageType.Info);
            _chatLog.AddMessage("GM_Tester", "Please report for an interview.", MessageType.GM);
            _chatLog.AddMessage("AllyMember", "Shall we go for the boss?", MessageType.Guild);
            _chatLog.AddMessage("PartyDude", "I've got a spot!", MessageType.Party);
            _chatLog.AddMessage("Whisperer", "Shall we meet in Lorencia?", MessageType.Whisper);
            for (int i = 0; i < 10; i++) _chatLog.AddMessage("Spammer", $"Test message number {i + 1}.", MessageType.Chat);
            _chatLog.AddMessage(string.Empty, "An unexpected error has occurred.", MessageType.Error);
        }

        // ─────────────────── Import NPCs & Monsters ───────────────────
        private async Task ImportPendingNpcsMonsters()
        {
            if (World is not WalkableWorldControl w) return;
            var list = ScopeHandler.TakePendingNpcsMonsters();
            if (!list.Any()) return;
            foreach (var s in list)
            {
                if (w.Objects.OfType<WalkerObject>().Any(p => p.NetworkId == s.Id)) continue;
                if (!NpcDatabase.TryGetNpcType(s.TypeNumber, out Type objectType)) continue;
                if (Activator.CreateInstance(objectType) is WalkerObject npcMonster)
                {
                    npcMonster.NetworkId = s.Id;
                    npcMonster.Location = new Vector2(s.PositionX, s.PositionY);
                    w.Objects.Add(npcMonster);
                    await npcMonster.Load();
                }
            }
        }

        // ─────────────────── Import Remote Players ───────────────────
        private async Task ImportPendingRemotePlayers()
        {
            if (World is not WalkableWorldControl w) return;
            var list = ScopeHandler.TakePendingPlayers();
            foreach (var s in list)
            {
                if (s.Id == MuGame.Network.GetCharacterState().Id) continue;
                if (w.Objects.OfType<PlayerObject>().Any(p => p.NetworkId == s.Id)) continue;
                var remote = new PlayerObject
                {
                    NetworkId = s.Id,
                    Name = s.Name,
                    CharacterClass = s.Class,
                    Location = new Vector2(s.PositionX, s.PositionY)
                };
                w.Objects.Add(remote);
                await remote.Load();
            }
        }

        // ─────────────────── Notification Handling ───────────────────
        public void ShowNotificationMessage(ServerMessage.MessageType messageType, string message)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                lock (_pendingNotifications) { _pendingNotifications.Add((messageType, message)); }
            });
        }

        private void ProcessPendingNotifications()
        {
            if (_notificationManager == null) return;
            List<(ServerMessage.MessageType Type, string Message)> currentBatch;
            lock (_pendingNotifications)
            {
                if (!_pendingNotifications.Any()) return;
                currentBatch = _pendingNotifications.ToList();
                _pendingNotifications.Clear();
            }
            foreach (var pending in currentBatch)
            {
                if (NotificationColors.TryGetValue(pending.Type, out Color notificationColor))
                {
                    if (pending.Type != ServerMessage.MessageType.BlueNormal)
                    {
                        _notificationManager.AddNotification(pending.Message, notificationColor);
                    }
                }
                else
                {
                    Controls.OfType<ChatLogWindow>().FirstOrDefault()?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
                }
                if (pending.Type == ServerMessage.MessageType.BlueNormal)
                {
                    Controls.OfType<ChatLogWindow>().FirstOrDefault()?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
                }
            }
        }

        // ─────────────────────────── Update Loop ───────────────────────────
        public override void Update(GameTime gameTime)
        {
            if (_isChangingWorld)
            {
                _loadingScreen?.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();

            base.Update(gameTime);

            if (FocusControl == _moveCommandWindow && _moveCommandWindow.Visible)
            {
                foreach (Keys key in System.Enum.GetValues(typeof(Keys)))
                {
                    if (currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key))
                    {
                        _moveCommandWindow.ProcessKeyInput(key, false);
                    }
                }
            }

            bool isMoveCommandWindowFocused = FocusControl == _moveCommandWindow && _moveCommandWindow.Visible;
            bool isChatInputFocused = FocusControl == _chatInput && _chatInput.Visible;

            if (!isChatInputFocused && !isMoveCommandWindowFocused)
            {
                if (currentKeyboardState.IsKeyDown(Keys.I) && !_previousKeyboardState.IsKeyDown(Keys.I))
                {
                    if (_inventoryControl.Visible)
                        _inventoryControl.Hide();
                    else
                        _inventoryControl.Show();
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
                if (currentKeyboardState.IsKeyDown(Keys.V) && !_previousKeyboardState.IsKeyDown(Keys.V))
                {
                    if (NpcShopControl.Instance.Visible)
                        NpcShopControl.Instance.Visible = false;
                    else
                        NpcShopControl.Instance.Visible = true;

                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            }

            if (!isMoveCommandWindowFocused && !isChatInputFocused)
            {
                if (currentKeyboardState.IsKeyDown(Keys.M) && !_previousKeyboardState.IsKeyDown(Keys.M))
                {
                    if (!NpcShopControl.Instance.Visible)
                    {
                        _moveCommandWindow.ToggleVisibility();
                    }
                }
                else if (!IsKeyboardEnterConsumedThisFrame && !_chatInput.Visible && currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    _chatInput.Show();
                }
            }

            if (currentKeyboardState.IsKeyDown(Keys.C) && !_previousKeyboardState.IsKeyDown(Keys.C))
            {
                if (_characterInfoWindow != null)
                {
                    if (_characterInfoWindow.Visible)
                        _characterInfoWindow.HideWindow();
                    else
                        _characterInfoWindow.ShowWindow();

                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            }

            _notificationManager?.Update(gameTime);
            ProcessPendingNotifications();

            if (World == null || World.Status != GameControlStatus.Ready)
            {
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            // Handle attack clicks on monsters
            else if (!IsMouseInputConsumedThisFrame && MouseHoverObject is Client.Main.Objects.Monsters.MonsterObject targetMonster &&
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released) // Fresh press
            {
                if (Hero != null)
                {
                    Hero.Attack(targetMonster);
                    SetMouseInputConsumed(); // Consume the click
                }
            }

            if (currentKeyboardState.IsKeyDown(Keys.F5) && _previousKeyboardState.IsKeyUp(Keys.F5)) _chatLog?.ToggleFrame();
            if (currentKeyboardState.IsKeyDown(Keys.F4) && _previousKeyboardState.IsKeyUp(Keys.F4)) _chatLog?.CycleSize();
            if (currentKeyboardState.IsKeyDown(Keys.F6) && _previousKeyboardState.IsKeyUp(Keys.F6)) _chatLog?.CycleBackgroundAlpha();
            if (currentKeyboardState.IsKeyDown(Keys.F2) && _previousKeyboardState.IsKeyUp(Keys.F2))
            {
                if (_chatLog != null)
                {
                    var nextType = _chatLog.CurrentViewType + 1;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown) nextType = Models.MessageType.All;
                    if (nextType == Models.MessageType.Info || nextType == Models.MessageType.Error) nextType++;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown) nextType = Models.MessageType.All;
                    _chatLog.ChangeViewType(nextType);
                    Console.WriteLine($"[ChatLog] Changed view to: {nextType}");
                }
            }
            if (_chatLog != null && _chatLog.IsFrameVisible)
            {
                int scrollDelta = 0;
                if (currentKeyboardState.IsKeyDown(Keys.PageUp) && _previousKeyboardState.IsKeyUp(Keys.PageUp)) scrollDelta = _chatLog.NumberOfShowingLines;
                if (currentKeyboardState.IsKeyDown(Keys.PageDown) && _previousKeyboardState.IsKeyUp(Keys.PageDown)) scrollDelta = -_chatLog.NumberOfShowingLines;
                if (scrollDelta != 0) _chatLog.ScrollLines(scrollDelta);
            }
            _previousKeyboardState = currentKeyboardState;
        }

        // ─────────────────────────── Draw Loop ───────────────────────────
        public override void Draw(GameTime gameTime)
        {
            if (_isChangingWorld || World == null || World.Status != GameControlStatus.Ready)
            {
                _loadingScreen?.Draw(gameTime);
                return;
            }

            using (new SpriteBatchScope(
                       GraphicsManager.Instance.Sprite,
                       SpriteSortMode.Deferred,
                       BlendState.AlphaBlend,
                       SamplerState.PointClamp,
                       DepthStencilState.None))
            {
                for (int i = 0; i < Controls.Count; i++)
                {
                    var ctrl = Controls[i];
                    if (ctrl != World && ctrl != _inventoryControl?._pickedItemRenderer && ctrl.Visible)
                        ctrl.Draw(gameTime);
                }

                _inventoryControl?._pickedItemRenderer?.Draw(gameTime);
            }

            base.Draw(gameTime);
            _characterInfoWindow?.BringToFront();
        }

        private void PreloadSounds()
        {
            SoundController.Instance.PreloadSound("Sound/pDropItem.wav");
            SoundController.Instance.PreloadSound("Sound/pDropMoney.wav");
            SoundController.Instance.PreloadSound("Sound/pGem.wav");
            SoundController.Instance.PreloadSound("Sound/pGetItem.wav");
        }
    }
}