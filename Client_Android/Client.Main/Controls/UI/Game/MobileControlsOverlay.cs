using System;
using System.Collections.Generic;
using System.Linq;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Objects.Monsters;
using Client.Main.Objects.Player;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Client.Main.Models;

namespace Client.Main.Controls.UI.Game
{
    public class MobileControlsOverlay : GameControl
    {
        private readonly GameScene _scene;
        private readonly PlayerObject _hero;
        private readonly InventoryControl _inventory;
        private readonly CharacterInfoWindowControl _charInfo;
        private readonly MoveCommandWindow _warpWindow;

        // Joystick configuration
        private const float JOYSTICK_RADIUS = 75f;
        private const float KNOB_RADIUS = 32f;
        private const float DEAD_ZONE = 0.15f;
        private const float MOVE_INTERVAL_MS = 180f;

        private Vector2 _joystickCenter;
        private Vector2 _knobPosition;
        private int _joystickTouchId = -1;
        private bool _isJoystickActive = false;
        private Vector2 _joystickDir = Vector2.Zero;
        private float _lastMoveSentTime = 0f;

        // Button configurations
        private Vector2 _atkButtonCenter;
        private const float ATK_RADIUS = 46f;
        private bool _atkPressed = false;

        private Vector2 _hpButtonCenter;
        private const float POTION_RADIUS = 28f;
        private bool _hpPressed = false;

        private Vector2 _mpButtonCenter;
        private bool _mpPressed = false;

        // Menu shortcut buttons (top-right)
        private Rectangle _invBtnRect;
        private Rectangle _statsBtnRect;
        private Rectangle _warpBtnRect;
        private bool _invPressed = false;
        private bool _statsPressed = false;
        private bool _warpPressed = false;

        public MobileControlsOverlay(
            GameScene scene,
            PlayerObject hero,
            InventoryControl inventory,
            CharacterInfoWindowControl charInfo,
            MoveCommandWindow warpWindow)
        {
            _scene = scene;
            _hero = hero;
            _inventory = inventory;
            _charInfo = charInfo;
            _warpWindow = warpWindow;

            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);
            Interactive = true;
            Status = GameControlStatus.Ready;
            Visible = true;

            UpdateLayoutPositions();
        }

        private void UpdateLayoutPositions()
        {
            int w = GraphicsDevice?.Viewport.Width ?? MuGame.Instance.Width;
            int h = GraphicsDevice?.Viewport.Height ?? MuGame.Instance.Height;

            // Joystick at bottom-left
            _joystickCenter = new Vector2(140f, h - 140f);
            if (!_isJoystickActive)
                _knobPosition = _joystickCenter;

            // Action buttons at bottom-right
            _atkButtonCenter = new Vector2(w - 110f, h - 125f);
            _hpButtonCenter = new Vector2(w - 60f, h - 215f);
            _mpButtonCenter = new Vector2(w - 135f, h - 215f);

            // Menu shortcuts at top-right
            int btnW = 60;
            int btnH = 32;
            int topY = 48;
            _warpBtnRect = new Rectangle(w - 70, topY, btnW, btnH);
            _statsBtnRect = new Rectangle(w - 140, topY, btnW, btnH);
            _invBtnRect = new Rectangle(w - 210, topY, btnW, btnH);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            UpdateLayoutPositions();

            float totalMs = (float)gameTime.TotalGameTime.TotalMilliseconds;

            // Process TouchPanel inputs (primary mobile multi-touch)
            var touchCollection = TouchPanel.GetState();
            bool hasTouches = touchCollection.Count > 0;

            if (hasTouches)
            {
                ProcessTouchInput(touchCollection);
            }
            else
            {
                ProcessMouseFallback();
            }

            // Handle Joystick Walking
            if (_isJoystickActive && _joystickDir.Length() > DEAD_ZONE)
            {
                if (totalMs - _lastMoveSentTime >= MOVE_INTERVAL_MS)
                {
                    _lastMoveSentTime = totalMs;
                    SendJoystickMovement();
                }
            }
        }

        private void ProcessTouchInput(TouchCollection touches)
        {
            bool joystickTouchFound = false;

            foreach (var touch in touches)
            {
                Vector2 pos = touch.Position;

                // 1. Joystick touch tracking
                if (_isJoystickActive && touch.Id == _joystickTouchId)
                {
                    if (touch.State == TouchLocationState.Moved || touch.State == TouchLocationState.Pressed)
                    {
                        joystickTouchFound = true;
                        UpdateJoystickKnob(pos);
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        ReleaseJoystick();
                    }
                    continue;
                }

                // Check for new touch on joystick base
                if (!_isJoystickActive && touch.State == TouchLocationState.Pressed)
                {
                    if (Vector2.Distance(pos, _joystickCenter) <= JOYSTICK_RADIUS * 1.4f)
                    {
                        _isJoystickActive = true;
                        _joystickTouchId = touch.Id;
                        joystickTouchFound = true;
                        UpdateJoystickKnob(pos);
                        continue;
                    }
                }

                // 2. Attack Button
                if (Vector2.Distance(pos, _atkButtonCenter) <= ATK_RADIUS)
                {
                    if (touch.State == TouchLocationState.Pressed)
                    {
                        _atkPressed = true;
                        ExecuteAttack();
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _atkPressed = false;
                    }
                    continue;
                }

                // 3. HP Potion Button
                if (Vector2.Distance(pos, _hpButtonCenter) <= POTION_RADIUS)
                {
                    if (touch.State == TouchLocationState.Pressed)
                    {
                        _hpPressed = true;
                        ExecuteHpPotion();
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _hpPressed = false;
                    }
                    continue;
                }

                // 4. MP Potion Button
                if (Vector2.Distance(pos, _mpButtonCenter) <= POTION_RADIUS)
                {
                    if (touch.State == TouchLocationState.Pressed)
                    {
                        _mpPressed = true;
                        ExecuteMpPotion();
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _mpPressed = false;
                    }
                    continue;
                }

                // 5. Top Menu Shortcuts
                Point pt = pos.ToPoint();
                if (_invBtnRect.Contains(pt))
                {
                    if (touch.State == TouchLocationState.Pressed && !_invPressed)
                    {
                        _invPressed = true;
                        ToggleInventory();
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _invPressed = false;
                    }
                    continue;
                }

                if (_statsBtnRect.Contains(pt))
                {
                    if (touch.State == TouchLocationState.Pressed && !_statsPressed)
                    {
                        _statsPressed = true;
                        ToggleStats();
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _statsPressed = false;
                    }
                    continue;
                }

                if (_warpBtnRect.Contains(pt))
                {
                    if (touch.State == TouchLocationState.Pressed && !_warpPressed)
                    {
                        _warpPressed = true;
                        ToggleWarp();
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _warpPressed = false;
                    }
                    continue;
                }
            }

            if (_isJoystickActive && !joystickTouchFound)
            {
                ReleaseJoystick();
            }
        }

        private void ProcessMouseFallback()
        {
            var mouse = MuGame.Instance.Mouse;
            var prevMouse = MuGame.Instance.PrevMouseState;
            Vector2 mPos = mouse.Position.ToVector2();

            bool isLeftDown = mouse.LeftButton == ButtonState.Pressed;
            bool wasLeftDown = prevMouse.LeftButton == ButtonState.Pressed;

            if (isLeftDown)
            {
                if (!_isJoystickActive && !wasLeftDown && Vector2.Distance(mPos, _joystickCenter) <= JOYSTICK_RADIUS * 1.3f)
                {
                    _isJoystickActive = true;
                }

                if (_isJoystickActive)
                {
                    UpdateJoystickKnob(mPos);
                }

                if (!wasLeftDown)
                {
                    if (Vector2.Distance(mPos, _atkButtonCenter) <= ATK_RADIUS)
                    {
                        _atkPressed = true;
                        ExecuteAttack();
                    }
                    else if (Vector2.Distance(mPos, _hpButtonCenter) <= POTION_RADIUS)
                    {
                        _hpPressed = true;
                        ExecuteHpPotion();
                    }
                    else if (Vector2.Distance(mPos, _mpButtonCenter) <= POTION_RADIUS)
                    {
                        _mpPressed = true;
                        ExecuteMpPotion();
                    }
                    else if (_invBtnRect.Contains(mouse.Position))
                    {
                        _invPressed = true;
                        ToggleInventory();
                    }
                    else if (_statsBtnRect.Contains(mouse.Position))
                    {
                        _statsPressed = true;
                        ToggleStats();
                    }
                    else if (_warpBtnRect.Contains(mouse.Position))
                    {
                        _warpPressed = true;
                        ToggleWarp();
                    }
                }
            }
            else
            {
                if (_isJoystickActive)
                    ReleaseJoystick();

                _atkPressed = false;
                _hpPressed = false;
                _mpPressed = false;
                _invPressed = false;
                _statsPressed = false;
                _warpPressed = false;
            }
        }

        private void UpdateJoystickKnob(Vector2 touchPos)
        {
            Vector2 delta = touchPos - _joystickCenter;
            float dist = delta.Length();

            if (dist > JOYSTICK_RADIUS)
            {
                delta = Vector2.Normalize(delta) * JOYSTICK_RADIUS;
            }

            _knobPosition = _joystickCenter + delta;
            _joystickDir = delta / JOYSTICK_RADIUS;
        }

        private void ReleaseJoystick()
        {
            _isJoystickActive = false;
            _joystickTouchId = -1;
            _joystickDir = Vector2.Zero;
            _knobPosition = _joystickCenter;
        }

        private void SendJoystickMovement()
        {
            if (_hero == null || _hero.World == null)
                return;

            // Isometric Camera Vectors
            Vector3 camFwd = Vector3.Normalize(new Vector3(Camera.Instance.Target.X - Camera.Instance.Position.X, Camera.Instance.Target.Y - Camera.Instance.Position.Y, 0));
            Vector3 camRight = Vector3.Normalize(Vector3.Cross(camFwd, Vector3.UnitZ));

            // Map Joystick 2D screen vector (X = right, Y = down) to MU isometric 3D space
            Vector3 moveDir = camRight * _joystickDir.X - camFwd * _joystickDir.Y;

            Vector2 targetLocation = _hero.Location + new Vector2(moveDir.X, moveDir.Y) * 2.8f;

            if (_hero.World is WalkableWorldControl walkable && walkable.IsWalkable(targetLocation))
            {
                _hero.MoveTo(targetLocation);
            }
            else
            {
                _hero.MoveTo(targetLocation);
            }
        }

        private void ExecuteAttack()
        {
            if (_hero == null || _hero.World == null)
                return;

            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");

            // Look for closest monster
            var nearestMonster = _hero.World.Objects
                .OfType<MonsterObject>()
                .OrderBy(m => Vector2.Distance(_hero.Location, m.Location))
                .FirstOrDefault();

            if (nearestMonster != null && Vector2.Distance(_hero.Location, nearestMonster.Location) <= 8f)
            {
                _hero.Attack(nearestMonster);
            }
            else
            {
                // Attack in current direction
                _hero.PlayAction((ushort)_hero.GetAttackAnimation());
            }
        }

        private void ExecuteHpPotion()
        {
            SoundController.Instance.PlayBuffer("Sound/pDrink.wav");
            Helpers.OnScreenLogger.Log("HP Potion consumida!");
        }

        private void ExecuteMpPotion()
        {
            SoundController.Instance.PlayBuffer("Sound/pDrink.wav");
            Helpers.OnScreenLogger.Log("MP Potion consumida!");
        }

        private void ToggleInventory()
        {
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            if (_inventory != null)
            {
                if (_inventory.Visible) _inventory.Hide();
                else _inventory.Show();
            }
        }

        private void ToggleStats()
        {
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            if (_charInfo != null)
            {
                if (_charInfo.Visible) _charInfo.HideWindow();
                else _charInfo.ShowWindow();
            }
        }

        private void ToggleWarp()
        {
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            if (_warpWindow != null)
            {
                _warpWindow.ToggleVisibility();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var sb = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            var font = GraphicsManager.Instance.Font;

            if (sb == null || pixel == null) return;

            sb.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            // 1. Draw Virtual Joystick
            // Outer Ring
            DrawCircle(sb, pixel, _joystickCenter, JOYSTICK_RADIUS, new Color(15, 20, 30, 140), true);
            DrawCircle(sb, pixel, _joystickCenter, JOYSTICK_RADIUS, new Color(200, 220, 255, 180), false, 2f);
            DrawCircle(sb, pixel, _joystickCenter, JOYSTICK_RADIUS * 0.5f, new Color(100, 140, 200, 60), false, 1f);

            // Joystick Knob
            Color knobColor = _isJoystickActive ? new Color(100, 180, 255, 220) : new Color(180, 190, 210, 160);
            DrawCircle(sb, pixel, _knobPosition, KNOB_RADIUS, knobColor, true);
            DrawCircle(sb, pixel, _knobPosition, KNOB_RADIUS, Color.White * 0.8f, false, 2f);

            // 2. Draw Attack Button
            Color atkBg = _atkPressed ? new Color(220, 60, 60, 220) : new Color(160, 30, 30, 180);
            DrawCircle(sb, pixel, _atkButtonCenter, ATK_RADIUS, atkBg, true);
            DrawCircle(sb, pixel, _atkButtonCenter, ATK_RADIUS, new Color(255, 200, 100, 230), false, 3f);
            if (font != null)
            {
                string atkText = "ATK";
                Vector2 textSize = font.MeasureString(atkText);
                Vector2 textPos = _atkButtonCenter - textSize * 0.5f;
                sb.DrawString(font, atkText, textPos + new Vector2(1, 1), Color.Black);
                sb.DrawString(font, atkText, textPos, Color.Gold);
            }

            // 3. Draw HP Potion Button
            Color hpBg = _hpPressed ? new Color(255, 80, 80, 220) : new Color(180, 30, 30, 180);
            DrawCircle(sb, pixel, _hpButtonCenter, POTION_RADIUS, hpBg, true);
            DrawCircle(sb, pixel, _hpButtonCenter, POTION_RADIUS, Color.Red, false, 2f);
            if (font != null)
            {
                string hpText = "HP";
                Vector2 textSize = font.MeasureString(hpText);
                Vector2 textPos = _hpButtonCenter - textSize * 0.5f;
                sb.DrawString(font, hpText, textPos + new Vector2(1, 1), Color.Black);
                sb.DrawString(font, hpText, textPos, Color.White);
            }

            // 4. Draw MP Potion Button
            Color mpBg = _mpPressed ? new Color(80, 140, 255, 220) : new Color(30, 80, 190, 180);
            DrawCircle(sb, pixel, _mpButtonCenter, POTION_RADIUS, mpBg, true);
            DrawCircle(sb, pixel, _mpButtonCenter, POTION_RADIUS, Color.DeepSkyBlue, false, 2f);
            if (font != null)
            {
                string mpText = "MP";
                Vector2 textSize = font.MeasureString(mpText);
                Vector2 textPos = _mpButtonCenter - textSize * 0.5f;
                sb.DrawString(font, mpText, textPos + new Vector2(1, 1), Color.Black);
                sb.DrawString(font, mpText, textPos, Color.White);
            }

            // 5. Draw Top Menu Shortcut Buttons
            DrawPillButton(sb, pixel, font, _invBtnRect, "INV", _invPressed, new Color(40, 140, 80));
            DrawPillButton(sb, pixel, font, _statsBtnRect, "STATS", _statsPressed, new Color(180, 120, 30));
            DrawPillButton(sb, pixel, font, _warpBtnRect, "WARP", _warpPressed, new Color(60, 100, 180));

            // 6. Real-time Diagnostic HUD (requested by user)
            if (font != null)
            {
                var world = _scene?.World;
                var terrain = world?.Terrain;
                string diag1 = $"MAP: {world?.Name ?? "None"} (W:{world?.WorldIndex}, St:{world?.Status}) | TER: {terrain?.Status} (Vis:{terrain?.Visible}) | HERO: ({_hero?.Location.X:F0},{_hero?.Location.Y:F0})";
                string diag2 = $"CAM: ({Camera.Instance.Position.X:F0},{Camera.Instance.Position.Y:F0},{Camera.Instance.Position.Z:F0}) -> ({Camera.Instance.Target.X:F0},{Camera.Instance.Target.Y:F0})";

                sb.DrawString(font, diag1, new Vector2(11, 11), Color.Black);
                sb.DrawString(font, diag1, new Vector2(10, 10), Color.Yellow);

                sb.DrawString(font, diag2, new Vector2(11, 27), Color.Black);
                sb.DrawString(font, diag2, new Vector2(10, 26), Color.Cyan);
            }

            sb.End();

            base.Draw(gameTime);
        }

        private static void DrawPillButton(SpriteBatch sb, Texture2D pixel, SpriteFont font, Rectangle rect, string text, bool pressed, Color accent)
        {
            Color bgColor = pressed ? accent * 0.9f : new Color(15, 20, 30, 190);
            sb.Draw(pixel, rect, bgColor);

            // Border
            Color border = pressed ? Color.White : accent;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), border);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), border);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), border);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), border);

            if (font != null)
            {
                Vector2 size = font.MeasureString(text);
                Vector2 pos = new Vector2(rect.X + (rect.Width - size.X) * 0.5f, rect.Y + (rect.Height - size.Y) * 0.5f);
                sb.DrawString(font, text, pos + new Vector2(1, 1), Color.Black);
                sb.DrawString(font, text, pos, Color.White);
            }
        }

        private static void DrawCircle(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, Color color, bool fill, float thickness = 1f)
        {
            int segments = 32;
            float step = MathHelper.TwoPi / segments;

            if (fill)
            {
                // Approximate filled circle with scanlines or triangle fans
                int r = (int)radius;
                for (int y = -r; y <= r; y++)
                {
                    int halfW = (int)Math.Sqrt(r * r - y * y);
                    sb.Draw(pixel, new Rectangle((int)(center.X - halfW), (int)(center.Y + y), halfW * 2, 1), color);
                }
            }
            else
            {
                for (int i = 0; i < segments; i++)
                {
                    float a1 = i * step;
                    float a2 = (i + 1) * step;

                    Vector2 p1 = center + new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1)) * radius;
                    Vector2 p2 = center + new Vector2((float)Math.Cos(a2), (float)Math.Sin(a2)) * radius;

                    DrawLine(sb, pixel, p1, p2, color, thickness);
                }
            }
        }

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 p1, Vector2 p2, Color color, float thickness)
        {
            Vector2 edge = p2 - p1;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();

            sb.Draw(pixel,
                new Rectangle((int)p1.X, (int)p1.Y, (int)length, (int)thickness),
                null,
                color,
                angle,
                Vector2.Zero,
                SpriteEffects.None,
                0);
        }
    }
}
