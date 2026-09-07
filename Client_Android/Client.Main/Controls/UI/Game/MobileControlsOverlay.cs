using System;
using System.Collections.Generic;
using System.Linq;
using Client.Main.Controllers;
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
        
        // Pinch-to-zoom multi-touch gesture
        private bool _isPinching = false;
        private float _prevPinchDist = 0f;

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
            
            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);
            Interactive = false;
            Status = GameControlStatus.Ready;
            Visible = true;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Process TouchPanel inputs (primary mobile multi-touch)
            var touchCollection = TouchPanel.GetState();
            bool hasTouches = touchCollection.Count > 0;

            if (hasTouches)
            {
                ProcessTouchInput(touchCollection);
            }
        }

        private void ProcessTouchInput(TouchCollection touches)
        {
            // Pinch-to-zoom multi-touch handling
            if (touches.Count >= 2)
            {
                var t1 = touches[0];
                var t2 = touches[1];

                if (t1.State == TouchLocationState.Moved || t2.State == TouchLocationState.Moved)
                {
                    float currentDist = Vector2.Distance(t1.Position, t2.Position);
                    if (_isPinching)
                    {
                        float delta = currentDist - _prevPinchDist;
                        if (MathF.Abs(delta) > 1.5f)
                        {
                            _hero?.ZoomCamera(delta * 2.5f);
                            _scene?.SetMouseInputConsumed(); // Prevent passing touch to world raycast while zooming
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
            // Empty draw
        }
    }
}
