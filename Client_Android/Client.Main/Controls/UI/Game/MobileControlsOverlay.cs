using System;
using System.Collections.Generic;
using System.Linq;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Common;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Objects;
using Client.Main.Objects.Monsters;
using Client.Main.Objects.Player;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Client.Main.Models;
using Client.Main.Helpers;
using Client.Main.Core.Client;
using Microsoft.Extensions.Logging;

namespace Client.Main.Controls.UI.Game
{
    public class MobileControlsOverlay : GameControl
    {
        private readonly GameScene _scene;
        private readonly PlayerObject _hero;
        private readonly InventoryControl _inventory;
        private readonly CharacterInfoWindowControl _charInfo;
        private readonly MoveCommandWindow _warpWindow;
        private readonly CommandWindowControl _commandWindow;

        // Pinch-to-zoom multi-touch gesture
        private bool _isPinching = false;
        private float _prevPinchDist = 0f;

        private ButtonControl _btnInventory;
        private ButtonControl _btnCharacter;
        private ButtonControl _btnWarp;
        private ButtonControl _btnCommand;
        private ButtonControl _btnCloseAll;
        private ButtonControl _btnSkills;
        private ButtonControl _btnLog;

        // Touch skill panel (pre-created rows; Visible toggled on demand)
        private const int SkillRows = 10;
        private ButtonControl _skillPanelBg;
        private ButtonControl _skillPanelClose;
        private readonly List<ButtonControl> _skillRowButtons = new();
        private readonly ushort[] _skillRowIds = new ushort[SkillRows];
        private bool _skillPanelVisible;

        private static readonly Dictionary<ushort, string> SkillNames = new()
        {
            { 1, "Poison" }, { 2, "Meteorite" }, { 3, "Lightning" }, { 4, "Fire Ball" },
            { 5, "Flame" }, { 6, "Teleport" }, { 7, "Ice" }, { 8, "Twister" },
            { 9, "Evil Spirit" }, { 10, "Hellfire" }, { 11, "Power Wave" }, { 12, "Aqua Beam" },
            { 13, "Cometfall" }, { 14, "Inferno" }, { 17, "Energy Ball" }, { 18, "Defense" },
            { 19, "Falling Slash" }, { 20, "Lunge" }, { 21, "Uppercut" }, { 22, "Cyclone" },
            { 23, "Slash" }, { 24, "Triple Shot" }, { 26, "Heal" }, { 27, "Greater Defense" },
            { 28, "Greater Attack" }, { 41, "Twisting Slash" }, { 42, "Rageful Blow" }, { 43, "Death Stab" }
        };

        public MobileControlsOverlay(
            GameScene scene,
            PlayerObject hero,
            InventoryControl inventory,
            CharacterInfoWindowControl charInfo,
            MoveCommandWindow warpWindow,
            CommandWindowControl commandWindow = null)
        {
            _scene = scene;
            _hero = hero;
            _inventory = inventory;
            _charInfo = charInfo;
            _warpWindow = warpWindow;
            _commandWindow = commandWindow;

            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);
            Interactive = false; // Container itself must not capture mouse/touch events across whole screen
            Status = GameControlStatus.Ready;
            Visible = true;

            CreateHudButtons();
            CreateSkillAndLogButtons();
            CreateSkillPanel();
        }

        private void CreateHudButtons()
        {
            int btnW = 54;
            int btnH = 34;
            int margin = 6;
            int screenW = MuGame.Instance.Width;
            int screenH = MuGame.Instance.Height;

            // Horizontal bar in the bottom right corner (above the bottom edge)
            int startX = screenW - (btnW + margin) * 5 - 12;
            int posY = screenH - btnH - 12;

            // 1. Inventory Button
            _btnInventory = new ButtonControl
            {
                Text = "INV",
                FontSize = 13f,
                TextColor = Color.Gold,
                BackgroundColor = new Color(20, 20, 35, 230),
                HoverBackgroundColor = new Color(40, 40, 70, 255),
                PressedBackgroundColor = new Color(10, 10, 20, 255),
                BorderThickness = 1,
                BorderColor = Color.DarkGoldenrod,
                X = startX,
                Y = posY,
                ControlSize = new Point(btnW, btnH),
                ViewSize = new Point(btnW, btnH),
                Visible = true
            };
            _btnInventory.Click += (s, e) =>
            {
                if (_inventory != null)
                {
                    _inventory.Visible = !_inventory.Visible;
                    if (_inventory.Visible) _inventory.BringToFront();
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            };
            Controls.Add(_btnInventory);

            // 2. Character Info Button
            _btnCharacter = new ButtonControl
            {
                Text = "CHAR",
                FontSize = 13f,
                TextColor = Color.Gold,
                BackgroundColor = new Color(20, 20, 35, 230),
                HoverBackgroundColor = new Color(40, 40, 70, 255),
                PressedBackgroundColor = new Color(10, 10, 20, 255),
                BorderThickness = 1,
                BorderColor = Color.DarkGoldenrod,
                X = startX + (btnW + margin) * 1,
                Y = posY,
                ControlSize = new Point(btnW, btnH),
                ViewSize = new Point(btnW, btnH),
                Visible = true
            };
            _btnCharacter.Click += (s, e) =>
            {
                if (_charInfo != null)
                {
                    _charInfo.Visible = !_charInfo.Visible;
                    if (_charInfo.Visible) _charInfo.BringToFront();
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            };
            Controls.Add(_btnCharacter);

            // 3. Move / Warp Button
            _btnWarp = new ButtonControl
            {
                Text = "MAP",
                FontSize = 13f,
                TextColor = Color.Gold,
                BackgroundColor = new Color(20, 20, 35, 230),
                HoverBackgroundColor = new Color(40, 40, 70, 255),
                PressedBackgroundColor = new Color(10, 10, 20, 255),
                BorderThickness = 1,
                BorderColor = Color.DarkGoldenrod,
                X = startX + (btnW + margin) * 2,
                Y = posY,
                ControlSize = new Point(btnW, btnH),
                ViewSize = new Point(btnW, btnH),
                Visible = true
            };
            _btnWarp.Click += (s, e) =>
            {
                if (_warpWindow != null)
                {
                    _warpWindow.Visible = !_warpWindow.Visible;
                    if (_warpWindow.Visible) _warpWindow.BringToFront();
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            };
            Controls.Add(_btnWarp);

            // 4. Command Window Button
            _btnCommand = new ButtonControl
            {
                Text = "CMD",
                FontSize = 13f,
                TextColor = Color.Gold,
                BackgroundColor = new Color(20, 20, 35, 230),
                HoverBackgroundColor = new Color(40, 40, 70, 255),
                PressedBackgroundColor = new Color(10, 10, 20, 255),
                BorderThickness = 1,
                BorderColor = Color.DarkGoldenrod,
                X = startX + (btnW + margin) * 3,
                Y = posY,
                ControlSize = new Point(btnW, btnH),
                ViewSize = new Point(btnW, btnH),
                Visible = true
            };
            _btnCommand.Click += (s, e) =>
            {
                if (_commandWindow != null)
                {
                    _commandWindow.Toggle();
                }
            };
            Controls.Add(_btnCommand);

            // 5. Close All Active Windows Button
            _btnCloseAll = new ButtonControl
            {
                Text = "X",
                FontSize = 14f,
                TextColor = Color.White,
                BackgroundColor = new Color(160, 30, 30, 230),
                HoverBackgroundColor = new Color(210, 50, 50, 255),
                PressedBackgroundColor = new Color(100, 15, 15, 255),
                BorderThickness = 1,
                BorderColor = Color.Red,
                X = startX + (btnW + margin) * 4,
                Y = posY,
                ControlSize = new Point(btnW - 14, btnH),
                ViewSize = new Point(btnW - 14, btnH),
                Visible = true
            };
            _btnCloseAll.Click += (s, e) =>
            {
                if (_inventory != null) _inventory.Visible = false;
                if (_charInfo != null) _charInfo.Visible = false;
                if (_warpWindow != null) _warpWindow.Visible = false;
                if (_commandWindow != null) _commandWindow.Visible = false;
                if (NpcShopControl.Instance != null) NpcShopControl.Instance.Visible = false;
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            };
            Controls.Add(_btnCloseAll);
        }

        private ButtonControl MakeButton(string text, int x, int y, int w, int h, Color textColor, Color bg, Color border, float fontSize = 13f)
        {
            return new ButtonControl
            {
                Text = text,
                FontSize = fontSize,
                TextColor = textColor,
                BackgroundColor = bg,
                HoverBackgroundColor = new Color(Math.Min(bg.R + 25, 255), Math.Min(bg.G + 25, 255), Math.Min(bg.B + 35, 255), 255),
                PressedBackgroundColor = new Color(bg.R / 2, bg.G / 2, bg.B / 2, 255),
                BorderThickness = 1,
                BorderColor = border,
                X = x,
                Y = y,
                ControlSize = new Point(w, h),
                ViewSize = new Point(w, h),
                Visible = true
            };
        }

        private void CreateSkillAndLogButtons()
        {
            int btnW = 54, btnH = 34, margin = 6;
            int screenW = MuGame.Instance.Width;
            int screenH = MuGame.Instance.Height;
            int startX = screenW - (btnW + margin) * 5 - 12;
            int posY = screenH - btnH - 12;

            // Skills button sits immediately left of the INV button
            _btnSkills = MakeButton("HAB", startX - (btnW + margin), posY, btnW, btnH,
                Color.Gold, new Color(20, 20, 35, 230), Color.DarkGoldenrod);
            _btnSkills.Click += (s, e) => ToggleSkillPanel();
            Controls.Add(_btnSkills);

            // LOG button (top-right): shares the full session log via Android share sheet
            _btnLog = MakeButton("LOG", screenW - 54 - 8, 8, 54, 30,
                Color.White, new Color(30, 60, 110, 220), Color.CornflowerBlue, 12f);
            _btnLog.Click += (s, e) =>
            {
                OnScreenLogger.Log("[LOG] Abrindo compartilhamento do log...", LogLevel.Information);
                OnScreenLogger.ShareLog();
            };
            Controls.Add(_btnLog);
        }

        private void CreateSkillPanel()
        {
            int panelW = 320;
            int rowH = 32;
            int titleH = 34;
            int panelH = titleH + SkillRows * (rowH + 2) + 46;
            int px = (MuGame.Instance.Width - panelW) / 2;
            int py = Math.Max(4, (MuGame.Instance.Height - panelH) / 2 - 20);

            _skillPanelBg = MakeButton("HABILIDADES", px, py, panelW, panelH,
                Color.Gold, new Color(12, 16, 28, 245), Color.Goldenrod, 14f);
            _skillPanelBg.Visible = false;
            Controls.Add(_skillPanelBg);

            for (int i = 0; i < SkillRows; i++)
            {
                int idx = i;
                var row = MakeButton("", px + 10, py + titleH + i * (rowH + 2), panelW - 20, rowH,
                    Color.White, new Color(35, 45, 70, 235), Color.Gray, 12f);
                row.Visible = false;
                row.Click += (s, e) => OnSkillRowClicked(idx);
                _skillRowButtons.Add(row);
                Controls.Add(row);
            }

            _skillPanelClose = MakeButton("FECHAR", px + panelW / 2 - 60, py + panelH - 40, 120, 32,
                Color.White, new Color(130, 30, 30, 235), Color.Red, 13f);
            _skillPanelClose.Visible = false;
            _skillPanelClose.Click += (s, e) => SetSkillPanelVisible(false);
            Controls.Add(_skillPanelClose);
        }

        public void ToggleSkillPanel() => SetSkillPanelVisible(!_skillPanelVisible);

        private void SetSkillPanelVisible(bool visible)
        {
            _skillPanelVisible = visible;
            if (visible) RefreshSkillRows();

            _skillPanelBg.Visible = visible;
            _skillPanelClose.Visible = visible;
            if (!visible)
            {
                foreach (var r in _skillRowButtons) r.Visible = false;
            }
            else
            {
                _skillPanelBg.BringToFront();
                foreach (var r in _skillRowButtons) r.BringToFront();
                _skillPanelClose.BringToFront();
            }
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
        }

        private static string SkillName(ushort id) =>
            SkillNames.TryGetValue(id, out var n) ? n : $"Skill #{id}";

        private void RefreshSkillRows()
        {
            var state = MuGame.Network?.GetCharacterState();
            var skills = state?.GetSkills().ToList() ?? new List<SkillEntryState>();
            OnScreenLogger.Log($"[SKILL] {skills.Count} habilidade(s) no personagem. Ids: {string.Join(",", skills.Select(s => s.SkillId))}", LogLevel.Information);

            for (int i = 0; i < SkillRows; i++)
            {
                var btn = _skillRowButtons[i];
                if (i < skills.Count)
                {
                    var sk = skills[i];
                    _skillRowIds[i] = sk.SkillId;
                    bool selected = state?.SelectedSkillId == sk.SkillId;
                    btn.Text = $"{(selected ? "> " : "")}{SkillName(sk.SkillId)}  (Nv {sk.SkillLevel})";
                    btn.TextColor = selected ? Color.Gold : Color.White;
                    btn.BorderColor = selected ? Color.Gold : Color.Gray;
                    btn.Visible = true;
                }
                else if (i == 0)
                {
                    _skillRowIds[i] = 0;
                    btn.Text = "Nenhuma habilidade recebida do servidor";
                    btn.TextColor = Color.LightGray;
                    btn.BorderColor = Color.Gray;
                    btn.Visible = true;
                }
                else
                {
                    _skillRowIds[i] = 0;
                    btn.Visible = false;
                }
            }
        }

        private void OnSkillRowClicked(int index)
        {
            ushort id = _skillRowIds[index];
            if (id == 0) return;

            var state = MuGame.Network?.GetCharacterState();
            if (state == null) return;

            // Toggle: tapping the selected skill again goes back to the basic attack.
            if (state.SelectedSkillId == id)
            {
                state.SelectedSkillId = null;
                OnScreenLogger.Log("[SKILL] Habilidade desmarcada (ataque basico).", LogLevel.Information);
            }
            else
            {
                state.SelectedSkillId = id;
                OnScreenLogger.Log($"[SKILL] Selecionada: {SkillName(id)} (id {id}). Toque num monstro para usar.", LogLevel.Information);
            }
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            RefreshSkillRows();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Process TouchPanel inputs (pinch-to-zoom)
            var touchCollection = TouchPanel.GetState();
            if (touchCollection.Count >= 2)
            {
                var t1 = touchCollection[0];
                var t2 = touchCollection[1];

                if (t1.State == TouchLocationState.Moved || t2.State == TouchLocationState.Moved)
                {
                    float currentDist = Vector2.Distance(t1.Position, t2.Position);
                    if (_isPinching)
                    {
                        float delta = currentDist - _prevPinchDist;
                        if (MathF.Abs(delta) > 1.5f)
                        {
                            _hero?.ZoomCamera(delta * 2.5f);
                            _scene?.SetMouseInputConsumed();
                        }
                    }
                    _prevPinchDist = currentDist;
                    _isPinching = true;
                }
            }
            else
            {
                _isPinching = false;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
