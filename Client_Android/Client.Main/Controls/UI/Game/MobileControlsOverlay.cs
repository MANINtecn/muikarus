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
