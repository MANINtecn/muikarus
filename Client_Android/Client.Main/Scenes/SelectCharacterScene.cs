using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Core.Client;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MUnique.OpenMU.Network.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class SelectCharacterScene : BaseScene
    {
        // Fields
        private readonly List<(string Name, CharacterClassNumber Class, ushort Level)> _characters;
        private SelectWorld _selectWorld;
        private LabelControl _infoLabel;
        private NetworkManager _networkManager;
        private ILogger<SelectCharacterScene> _logger;
        private (string Name, CharacterClassNumber Class, ushort Level)? _selectedCharacterInfo = null;
        private LoadingScreenControl _loadingScreen;
        private bool _initialLoadComplete = false;
        private float _selectionElapsed = 0f;
        private readonly List<GameControl> _mobileButtons = new();
        private GameControl _creationModal = null;
        private GameControl _deleteConfirmModal = null;
        private string _lastSelectedCharForDelete = null;

        // Constructors
        public SelectCharacterScene(List<(string Name, CharacterClassNumber Class, ushort Level)> characters)
        {
            _characters = characters ?? new List<(string Name, CharacterClassNumber Class, ushort Level)>();
            _networkManager = MuGame.Network;
            _logger = MuGame.AppLoggerFactory.CreateLogger<SelectCharacterScene>();

            _infoLabel = new LabelControl
            {
                Text = "Preparing character selection...",
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                Margin = new Margin { Top = 20 },
                FontSize = 16,
                TextColor = Color.LightGray,
                Visible = false
            };
            Controls.Add(_infoLabel);

            _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Characters..." };
            Controls.Add(_loadingScreen);
            _loadingScreen.BringToFront();

            SubscribeToNetworkEvents();
        }

        private void UpdateLoadProgress(string message, float progress)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_loadingScreen != null && _loadingScreen.Visible)
                {
                    _loadingScreen.Message = message;
                    _loadingScreen.Progress = progress;
                }
            });
        }

        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            UpdateLoadProgress("Initializing Character Selection...", 0.0f);
            _logger.LogInformation(">>> SelectCharacterScene LoadSceneContentWithProgress starting...");

            try
            {
                UpdateLoadProgress("Creating Select World...", 0.05f);
                _selectWorld = new SelectWorld();
                Controls.Add(_selectWorld);

                UpdateLoadProgress("Initializing Select World (Graphics)...", 0.1f);
                await _selectWorld.Initialize();
                World = _selectWorld;
                UpdateLoadProgress("Select World Initialized.", 0.35f); // Zwiększony postęp po inicjalizacji świata
                _logger.LogInformation("--- SelectCharacterScene: SelectWorld initialized and set.");

                if (_selectWorld != null && _characters.Any())
                {
                    UpdateLoadProgress("Preparing Character Data...", 0.40f);
                    await _selectWorld.CreateCharacterObjects(_characters);

                    float characterCreationStartProgress = 0.45f;
                    float characterCreationEndProgress = 0.85f;
                    float totalCharacterProgressSpan = characterCreationEndProgress - characterCreationStartProgress;

                    if (_characters.Count > 0)
                    {
                        float progressPerCharacter = totalCharacterProgressSpan / _characters.Count;
                        for (int i = 0; i < _characters.Count; i++)
                        {
                            UpdateLoadProgress($"Configuring character {i + 1}/{_characters.Count}...", characterCreationStartProgress + (i + 1) * progressPerCharacter);
                        }
                    }
                    else
                    {
                        UpdateLoadProgress("No characters to configure.", characterCreationEndProgress);
                    }


                    if (_infoLabel != null) _infoLabel.Text = "Select your character";
                    UpdateLoadProgress("Character Objects Ready.", 0.90f);
                    _logger.LogInformation("--- SelectCharacterScene: CreateCharacterObjects finished.");
                }
                else
                {
                    string message = _characters.Any()
                        ? "Error creating character objects."
                        : "No characters found on this account.";
                    if (_infoLabel != null) _infoLabel.Text = message;
                    _logger.LogWarning("--- SelectCharacterScene: {Message}", message);
                    UpdateLoadProgress(message, 0.90f);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "!!! SelectCharacterScene: Error during world initialization or character creation.");
                if (_infoLabel != null) _infoLabel.Text = "Error loading character selection.";
                UpdateLoadProgress("Error loading character selection.", 1.0f);
            }
            finally
            {
                _initialLoadComplete = true;
                UpdateLoadProgress("Character Selection Ready.", 1.0f);
                _logger.LogInformation("<<< SelectCharacterScene LoadSceneContentWithProgress finished.");
            }
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _logger.LogInformation("SelectCharacterScene.AfterLoad() called.");
            if (_loadingScreen != null)
            {
                MuGame.ScheduleOnMainThread(() =>
                {
                    if (_loadingScreen != null)
                    {
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                        _infoLabel.Visible = true;
                        CreateMobileCharacterButtons();
                        Cursor?.BringToFront();
                        _infoLabel?.BringToFront();
                        DebugPanel?.BringToFront();
                    }
                });
            }
        }

        private void CreateMobileCharacterButtons()
        {
            // Clear existing dynamic buttons
            foreach (var b in _mobileButtons)
            {
                Controls.Remove(b);
                b.Dispose();
            }
            _mobileButtons.Clear();

            int btnWidth = 190;
            int actionBtnWidth = 100;
            int btnHeight = 44;
            int spacing = 10;
            int btnY = MuGame.Instance.Height - 58;

            if (_characters != null && _characters.Any())
            {
                int totalWidth = (_characters.Count * (btnWidth + spacing)) + ((actionBtnWidth + spacing) * 2) - spacing;
                int startX = Math.Max(10, (MuGame.Instance.Width - totalWidth) / 2);
                int curX = startX;

                // 1. Existing Character Buttons
                for (int i = 0; i < _characters.Count; i++)
                {
                    var charInfo = _characters[i];
                    string charName = charInfo.Name;
                    var charBtn = new LabelControl
                    {
                        Text = $"[ {charInfo.Name}  Lv.{charInfo.Level} ]",
                        FontSize = 14,
                        TextColor = new Color(255, 215, 0),
                        BackgroundColor = new Color(15, 20, 30, 225),
                        BorderColor = new Color(255, 215, 0, 180),
                        BorderThickness = 2,
                        HasShadow = true,
                        ShadowColor = Color.Black,
                        TextAlign = HorizontalAlign.Center,
                        AutoViewSize = false,
                        ViewSize = new Point(btnWidth, btnHeight),
                        X = curX,
                        Y = btnY,
                        Interactive = true
                    };

                    charBtn.Click += (s, e) =>
                    {
                        _lastSelectedCharForDelete = charName;
                        CharacterSelected(charName);
                    };

                    Controls.Add(charBtn);
                    charBtn.BringToFront();
                    _mobileButtons.Add(charBtn);

                    curX += btnWidth + spacing;
                }

                // 2. Button [ + CRIAR ]
                var createBtn = new LabelControl
                {
                    Text = "+ CRIAR",
                    FontSize = 14,
                    TextColor = Color.White,
                    BackgroundColor = new Color(25, 110, 45, 235),
                    BorderColor = Color.LimeGreen,
                    BorderThickness = 2,
                    HasShadow = true,
                    ShadowColor = Color.Black,
                    TextAlign = HorizontalAlign.Center,
                    AutoViewSize = false,
                    ViewSize = new Point(actionBtnWidth, btnHeight),
                    X = curX,
                    Y = btnY,
                    Interactive = true
                };
                createBtn.Click += (s, e) => ShowCharacterCreationDialog();
                Controls.Add(createBtn);
                createBtn.BringToFront();
                _mobileButtons.Add(createBtn);

                curX += actionBtnWidth + spacing;

                // 3. Button [ DELETAR ]
                var deleteBtn = new LabelControl
                {
                    Text = "DELETAR",
                    FontSize = 14,
                    TextColor = Color.White,
                    BackgroundColor = new Color(140, 25, 25, 235),
                    BorderColor = Color.Red,
                    BorderThickness = 2,
                    HasShadow = true,
                    ShadowColor = Color.Black,
                    TextAlign = HorizontalAlign.Center,
                    AutoViewSize = false,
                    ViewSize = new Point(actionBtnWidth, btnHeight),
                    X = curX,
                    Y = btnY,
                    Interactive = true
                };
                deleteBtn.Click += (s, e) =>
                {
                    string toDelete = _lastSelectedCharForDelete ?? _characters.FirstOrDefault().Name;
                    if (!string.IsNullOrEmpty(toDelete))
                    {
                        ShowDeleteConfirmDialog(toDelete);
                    }
                };
                Controls.Add(deleteBtn);
                deleteBtn.BringToFront();
                _mobileButtons.Add(deleteBtn);
            }
            else
            {
                // No characters: Large prominent button
                int bigWidth = 280;
                var createBigBtn = new LabelControl
                {
                    Text = "[ + CRIAR PERSONAGEM ]",
                    FontSize = 16,
                    TextColor = Color.White,
                    BackgroundColor = new Color(25, 125, 45, 245),
                    BorderColor = Color.Gold,
                    BorderThickness = 2,
                    HasShadow = true,
                    ShadowColor = Color.Black,
                    TextAlign = HorizontalAlign.Center,
                    AutoViewSize = false,
                    ViewSize = new Point(bigWidth, 52),
                    X = (MuGame.Instance.Width - bigWidth) / 2,
                    Y = btnY - 8,
                    Interactive = true
                };
                createBigBtn.Click += (s, e) => ShowCharacterCreationDialog();
                Controls.Add(createBigBtn);
                createBigBtn.BringToFront();
                _mobileButtons.Add(createBigBtn);
            }
        }

        private void ShowCharacterCreationDialog()
        {
            if (_creationModal != null) return;

            int modalW = 460;
            int modalH = 260;
            int modalX = (MuGame.Instance.Width - modalW) / 2;
            int modalY = (MuGame.Instance.Height - modalH) / 2;

            var modal = new LabelControl
            {
                X = modalX,
                Y = modalY,
                ViewSize = new Point(modalW, modalH),
                AutoViewSize = false,
                Interactive = true
            };

            var bg = new LabelControl
            {
                Text = "",
                BackgroundColor = new Color(12, 18, 28, 248),
                BorderColor = Color.Goldenrod,
                BorderThickness = 3,
                AutoViewSize = false,
                ViewSize = new Point(modalW, modalH),
                Interactive = true
            };
            modal.Controls.Add(bg);

            var title = new LabelControl
            {
                Text = "CRIAR NOVO PERSONAGEM",
                FontSize = 16,
                TextColor = Color.Gold,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(modalW, 30),
                Y = 12
            };
            modal.Controls.Add(title);

            CharacterClassNumber selectedClass = CharacterClassNumber.DarkKnight;
            string newCharName = "Ikarus" + Random.Shared.Next(100, 999);

            var classLabel = new LabelControl
            {
                Text = "Escolha sua Classe:",
                FontSize = 13,
                TextColor = Color.LightGray,
                X = 20,
                Y = 48
            };
            modal.Controls.Add(classLabel);

            int classBtnW = 128;
            int classBtnH = 38;
            int classBtnY = 74;

            var dkBtn = new LabelControl
            {
                Text = "Dark Knight",
                FontSize = 12,
                TextColor = Color.Gold,
                BackgroundColor = new Color(50, 70, 100, 220),
                BorderColor = Color.Gold,
                BorderThickness = 2,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(classBtnW, classBtnH),
                X = 20,
                Y = classBtnY,
                Interactive = true
            };

            var dwBtn = new LabelControl
            {
                Text = "Dark Wizard",
                FontSize = 12,
                TextColor = Color.LightGray,
                BackgroundColor = new Color(30, 30, 40, 200),
                BorderColor = Color.Gray,
                BorderThickness = 1,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(classBtnW, classBtnH),
                X = 20 + classBtnW + 16,
                Y = classBtnY,
                Interactive = true
            };

            var elfBtn = new LabelControl
            {
                Text = "Fairy Elf",
                FontSize = 12,
                TextColor = Color.LightGray,
                BackgroundColor = new Color(30, 30, 40, 200),
                BorderColor = Color.Gray,
                BorderThickness = 1,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(classBtnW, classBtnH),
                X = 20 + (classBtnW + 16) * 2,
                Y = classBtnY,
                Interactive = true
            };

            void UpdateClassHighlight()
            {
                dkBtn.BorderColor = selectedClass == CharacterClassNumber.DarkKnight ? Color.Gold : Color.Gray;
                dkBtn.TextColor = selectedClass == CharacterClassNumber.DarkKnight ? Color.Gold : Color.LightGray;
                dkBtn.BorderThickness = selectedClass == CharacterClassNumber.DarkKnight ? 2 : 1;

                dwBtn.BorderColor = selectedClass == CharacterClassNumber.DarkWizard ? Color.Gold : Color.Gray;
                dwBtn.TextColor = selectedClass == CharacterClassNumber.DarkWizard ? Color.Gold : Color.LightGray;
                dwBtn.BorderThickness = selectedClass == CharacterClassNumber.DarkWizard ? 2 : 1;

                elfBtn.BorderColor = selectedClass == CharacterClassNumber.FairyElf ? Color.Gold : Color.Gray;
                elfBtn.TextColor = selectedClass == CharacterClassNumber.FairyElf ? Color.Gold : Color.LightGray;
                elfBtn.BorderThickness = selectedClass == CharacterClassNumber.FairyElf ? 2 : 1;
            }

            dkBtn.Click += (s, e) => { selectedClass = CharacterClassNumber.DarkKnight; UpdateClassHighlight(); };
            dwBtn.Click += (s, e) => { selectedClass = CharacterClassNumber.DarkWizard; UpdateClassHighlight(); };
            elfBtn.Click += (s, e) => { selectedClass = CharacterClassNumber.FairyElf; UpdateClassHighlight(); };

            modal.Controls.Add(dkBtn);
            modal.Controls.Add(dwBtn);
            modal.Controls.Add(elfBtn);

            var nameHeader = new LabelControl
            {
                Text = $"Nome: {newCharName}",
                FontSize = 14,
                TextColor = Color.Cyan,
                X = 20,
                Y = 128
            };
            modal.Controls.Add(nameHeader);

            var randomNameBtn = new LabelControl
            {
                Text = "[ Sortear Outro Nome ]",
                FontSize = 12,
                TextColor = Color.Goldenrod,
                BackgroundColor = new Color(40, 40, 50, 190),
                BorderColor = Color.DarkGoldenrod,
                BorderThickness = 1,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(170, 30),
                X = 260,
                Y = 124,
                Interactive = true
            };
            randomNameBtn.Click += (s, e) =>
            {
                newCharName = "Ikarus" + Random.Shared.Next(100, 999);
                nameHeader.Text = $"Nome: {newCharName}";
            };
            modal.Controls.Add(randomNameBtn);

            var confirmBtn = new LabelControl
            {
                Text = "CONFIRMAR CRIACAO",
                FontSize = 13,
                TextColor = Color.White,
                BackgroundColor = new Color(20, 120, 40, 240),
                BorderColor = Color.LimeGreen,
                BorderThickness = 2,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(190, 42),
                X = 25,
                Y = modalH - 55,
                Interactive = true
            };
            confirmBtn.Click += (s, e) =>
            {
                CloseCreationDialog();
                OnScreenLogger.Log($"Enviando criacao de '{newCharName}' ({selectedClass})...");
                var svc = _networkManager?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendCreateCharacterRequestAsync(newCharName, selectedClass);
                }
            };
            modal.Controls.Add(confirmBtn);

            var cancelBtn = new LabelControl
            {
                Text = "CANCELAR",
                FontSize = 13,
                TextColor = Color.White,
                BackgroundColor = new Color(90, 20, 20, 230),
                BorderColor = Color.Red,
                BorderThickness = 2,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(190, 42),
                X = modalW - 25 - 190,
                Y = modalH - 55,
                Interactive = true
            };
            cancelBtn.Click += (s, e) => CloseCreationDialog();
            modal.Controls.Add(cancelBtn);

            _creationModal = modal;
            Controls.Add(modal);
            modal.BringToFront();
            Cursor?.BringToFront();
        }

        private void CloseCreationDialog()
        {
            if (_creationModal != null)
            {
                Controls.Remove(_creationModal);
                _creationModal.Dispose();
                _creationModal = null;
            }
        }

        private void ShowDeleteConfirmDialog(string characterName)
        {
            if (_deleteConfirmModal != null) return;

            int modalW = 420;
            int modalH = 180;
            int modalX = (MuGame.Instance.Width - modalW) / 2;
            int modalY = (MuGame.Instance.Height - modalH) / 2;

            var modal = new LabelControl
            {
                X = modalX,
                Y = modalY,
                ViewSize = new Point(modalW, modalH),
                AutoViewSize = false,
                Interactive = true
            };

            var bg = new LabelControl
            {
                Text = "",
                BackgroundColor = new Color(25, 10, 10, 248),
                BorderColor = Color.Red,
                BorderThickness = 3,
                AutoViewSize = false,
                ViewSize = new Point(modalW, modalH),
                Interactive = true
            };
            modal.Controls.Add(bg);

            var title = new LabelControl
            {
                Text = "DELETAR PERSONAGEM",
                FontSize = 16,
                TextColor = Color.Red,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(modalW, 30),
                Y = 14
            };
            modal.Controls.Add(title);

            var msg = new LabelControl
            {
                Text = $"Tem certeza que deseja DELETAR '{characterName}'?",
                FontSize = 13,
                TextColor = Color.White,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(modalW, 30),
                Y = 55
            };
            modal.Controls.Add(msg);

            var confirmBtn = new LabelControl
            {
                Text = "SIM, DELETAR",
                FontSize = 13,
                TextColor = Color.White,
                BackgroundColor = new Color(140, 20, 20, 240),
                BorderColor = Color.Red,
                BorderThickness = 2,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(170, 40),
                X = 25,
                Y = modalH - 52,
                Interactive = true
            };
            confirmBtn.Click += (s, e) =>
            {
                CloseDeleteConfirmDialog();
                OnScreenLogger.Log($"Solicitando delecao de '{characterName}'...");
                var svc = _networkManager?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendDeleteCharacterRequestAsync(characterName, "1234567");
                }
            };
            modal.Controls.Add(confirmBtn);

            var cancelBtn = new LabelControl
            {
                Text = "CANCELAR",
                FontSize = 13,
                TextColor = Color.White,
                BackgroundColor = new Color(50, 50, 60, 220),
                BorderColor = Color.Gray,
                BorderThickness = 2,
                TextAlign = HorizontalAlign.Center,
                AutoViewSize = false,
                ViewSize = new Point(170, 40),
                X = modalW - 25 - 170,
                Y = modalH - 52,
                Interactive = true
            };
            cancelBtn.Click += (s, e) => CloseDeleteConfirmDialog();
            modal.Controls.Add(cancelBtn);

            _deleteConfirmModal = modal;
            Controls.Add(modal);
            modal.BringToFront();
            Cursor?.BringToFront();
        }

        private void CloseDeleteConfirmDialog()
        {
            if (_deleteConfirmModal != null)
            {
                Controls.Remove(_deleteConfirmModal);
                _deleteConfirmModal.Dispose();
                _deleteConfirmModal = null;
            }
        }

        public override async Task Load()
        {
            if (Status == GameControlStatus.Initializing)
            {
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
            else
            {
                _logger.LogDebug("SelectCharacterScene.Load() called outside of InitializeWithProgressReporting flow. Re-routing to progressive load.");
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
        }


        public void CharacterSelected(string characterName)
        {
            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                _logger.LogInformation("Character selection attempted while loading screen is visible. Ignoring.");
                return;
            }

            _selectedCharacterInfo = _characters.FirstOrDefault(c => c.Name == characterName);

            if (!_selectedCharacterInfo.HasValue)
            {
                _logger.LogError("Character '{CharacterName}' selected, but not found in the character list.", characterName);
                MessageWindow.Show($"Error selecting character '{characterName}'.");
                return;
            }

            ClientConnectionState currentState = _networkManager.CurrentState;
            bool canSelect = currentState == ClientConnectionState.ConnectedToGameServer ||
                             currentState == ClientConnectionState.SelectingCharacter;

            if (!canSelect)
            {
                _logger.LogWarning("Character selection attempted but NetworkManager state is not ConnectedToGameServer or SelectingCharacter. State: {State}", currentState);
                MessageWindow.Show($"Cannot select character. Invalid network state: {currentState}");
                _selectedCharacterInfo = null;
                return;
            }

            _logger.LogInformation("Character '{CharacterName}' (Class: {Class}) selected in scene. Sending request...",
                                   _selectedCharacterInfo.Value.Name, _selectedCharacterInfo.Value.Class);

            DisableInteractionDuringSelection(characterName);
            _ = _networkManager.SendSelectCharacterRequestAsync(characterName);
        }

        public override void Dispose()
        {
            _logger.LogDebug("Disposing SelectCharacterScene.");
            UnsubscribeFromNetworkEvents();
            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
            base.Dispose();
        }

        private void SubscribeToNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame += HandleEnteredGame;
                _networkManager.ErrorOccurred += HandleNetworkError;
                _networkManager.ConnectionStateChanged += HandleConnectionStateChange;
                _networkManager.CharacterListReceived += HandleCharacterListReceived;
                _logger.LogDebug("SelectCharacterScene subscribed to NetworkManager events.");
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_networkManager != null)
            {
                _networkManager.EnteredGame -= HandleEnteredGame;
                _networkManager.ErrorOccurred -= HandleNetworkError;
                _networkManager.ConnectionStateChanged -= HandleConnectionStateChange;
                _networkManager.CharacterListReceived -= HandleCharacterListReceived;
                _logger.LogDebug("SelectCharacterScene unsubscribed from NetworkManager events.");
            }
        }

        private void HandleCharacterListReceived(object sender, List<(string Name, CharacterClassNumber Class, ushort Level)> updatedList)
        {
            MuGame.ScheduleOnMainThread(async () =>
            {
                _logger.LogInformation("SelectCharacterScene received updated character list with {Count} characters.", updatedList?.Count ?? 0);
                _characters.Clear();
                if (updatedList != null)
                {
                    _characters.AddRange(updatedList);
                }

                CreateMobileCharacterButtons();

                if (_selectWorld != null)
                {
                    try
                    {
                        await _selectWorld.CreateCharacterObjects(_characters);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing 3D characters in SelectWorld.");
                    }
                }
            });
        }

        private void HandleEnteredGame(object sender, EventArgs e)
        {
            _logger.LogInformation(">>> SelectCharacterScene.HandleEnteredGame: Event received.");

            if (!_selectedCharacterInfo.HasValue)
            {
                var charState = _networkManager?.GetCharacterState();
                if (charState != null && !string.IsNullOrEmpty(charState.Name))
                {
                    _selectedCharacterInfo = (charState.Name, charState.Class, charState.Level);
                }
                else if (_characters.Any())
                {
                    _selectedCharacterInfo = _characters.First();
                }
            }

            if (!_selectedCharacterInfo.HasValue)
            {
                _logger.LogError("!!! SelectCharacterScene.HandleEnteredGame: EnteredGame event received, but _selectedCharacterInfo is null. Cannot change to GameScene.");
                if (_loadingScreen != null)
                {
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        Controls.Remove(_loadingScreen);
                        _loadingScreen.Dispose();
                        _loadingScreen = null;
                        EnableInteractionAfterSelection();
                    });
                }
                return;
            }

            var characterInfo = _selectedCharacterInfo.Value;
            OnScreenLogger.Log($"Servidor autorizou entrada de {characterInfo.Name}! Trocando para GameScene...");
            _logger.LogInformation("--- SelectCharacterScene.HandleEnteredGame: Scheduling scene change to GameScene for character: {Name} ({Class})",
                characterInfo.Name, characterInfo.Class);

            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogInformation("--- SelectCharacterScene.HandleEnteredGame (UI Thread): Executing scheduled scene change...");
                if (MuGame.Instance.ActiveScene == this)
                {
                    try
                    {
                        MuGame.Instance.ChangeScene(new GameScene(characterInfo));
                        _logger.LogInformation("<<< SelectCharacterScene.HandleEnteredGame (UI Thread): ChangeScene to GameScene call completed.");
                    }
                    catch (Exception ex)
                    {
                        OnScreenLogger.Log($"ERRO ao trocar para GameScene: {ex.Message}", LogLevel.Error);
                        _logger.LogError(ex, "!!! SelectCharacterScene.HandleEnteredGame (UI Thread): Exception during ChangeScene to GameScene.");
                        EnableInteractionAfterSelection();
                    }
                }
                else
                {
                    _logger.LogWarning("<<< SelectCharacterScene.HandleEnteredGame (UI Thread): Scene changed before execution. Aborting change to GameScene.");
                }
            });
        }

        private void HandleNetworkError(object sender, string errorMessage)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                OnScreenLogger.Log($"Erro de rede na selecao: {errorMessage}", LogLevel.Error);
                _logger.LogError("SelectCharacterScene received NetworkError: {Error}", errorMessage);
                MessageWindow.Show($"Network Error: {errorMessage}");
                EnableInteractionAfterSelection();
                if (MuGame.Instance.ActiveScene == this)
                {
                    MuGame.Instance.ChangeScene<LoginScene>();
                }
            });
        }

        private void HandleConnectionStateChange(object sender, ClientConnectionState newState)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _logger.LogDebug("SelectCharacterScene received ConnectionStateChanged: {NewState}", newState);
                if (newState == ClientConnectionState.Disconnected)
                {
                    OnScreenLogger.Log("Desconectado do servidor.", LogLevel.Warning);
                    _logger.LogWarning("Disconnected while in character selection. Returning to LoginScene.");
                    MessageWindow.Show("Connection lost.");
                    if (MuGame.Instance.ActiveScene == this)
                    {
                        MuGame.Instance.ChangeScene<LoginScene>();
                    }
                }
            });
        }

        private void DisableInteractionDuringSelection(string characterName)
        {
            OnScreenLogger.Log($"Personagem selecionado: {characterName}. Solicitando entrada ao servidor...");
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = false;
                foreach (var charObj in _selectWorld.Objects.OfType<PlayerObject>())
                {
                    charObj.Interactive = false;
                }
                var characterLabels = _selectWorld.GetCharacterLabels();
                foreach (var label in characterLabels.Values)
                {
                    label.Visible = false;
                }
            }
            if (_infoLabel != null)
            {
                _infoLabel.Text = $"Selecting {characterName}...";
                _infoLabel.Visible = true;
            }

            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl { Visible = true, AutoDismissTimeout = 20.0f };
                Controls.Add(_loadingScreen);
            }
            else
            {
                _loadingScreen.AutoDismissTimeout = 20.0f;
            }
            _loadingScreen.Message = $"Entering game as {characterName}...";
            _loadingScreen.Progress = 0f;
            _loadingScreen.Visible = true;
            _loadingScreen.BringToFront();
            Cursor?.BringToFront();
        }

        private void EnableInteractionAfterSelection()
        {
            _selectionElapsed = 0f;
            if (_selectWorld != null)
            {
                _selectWorld.Interactive = true;
                foreach (var charObj in _selectWorld.Objects.OfType<PlayerObject>())
                {
                    charObj.Interactive = true;
                }
                var characterLabels = _selectWorld.GetCharacterLabels();
                foreach (var label in characterLabels.Values)
                {
                    label.Visible = true;
                }
            }
            if (_infoLabel != null)
            {
                _infoLabel.Text = "Select your character";
            }
            _selectedCharacterInfo = null;

            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (_selectedCharacterInfo.HasValue)
            {
                _selectionElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_selectionElapsed > 7f)
                {
                    _logger?.LogWarning("Selection request timed out after 7s. Re-enabling interaction.");
                    _selectionElapsed = 0f;
                    EnableInteractionAfterSelection();
                }
            }
            else
            {
                _selectionElapsed = 0f;
            }

            if (_loadingScreen != null && _loadingScreen.Visible)
            {
                _loadingScreen.Update(gameTime);
                Cursor?.Update(gameTime);
                DebugPanel?.Update(gameTime);
                return;
            }
            if (!_initialLoadComplete && Status == GameControlStatus.Initializing)
            {
                Cursor?.Update(gameTime);
                DebugPanel?.Update(gameTime);
                return;
            }
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}