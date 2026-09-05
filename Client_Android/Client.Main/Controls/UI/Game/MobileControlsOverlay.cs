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
                if (!_isJoystickActive && (touch.State == TouchLocationState.Pressed || touch.State == TouchLocationState.Moved))
                {
                    if (Vector2.Distance(pos, _joystickCenter) <= JOYSTICK_RADIUS * 2.2f)
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
                if (!_isJoystickActive && Vector2.Distance(mPos, _joystickCenter) <= JOYSTICK_RADIUS * 2.2f)
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

            float stepDist = 3.5f;
            Vector2 targetLocation = new Vector2(
                MathF.Round(_hero.Location.X + moveDir.X * stepDist),
                MathF.Round(_hero.Location.Y + moveDir.Y * stepDist));

            if (_hero.World is WalkableWorldControl walkable && walkable.IsWalkable(targetLocation))
            {
                _hero.MoveTo(targetLocation);
            }
            else
            {
                Vector2 shorterTarget = new Vector2(
                    MathF.Round(_hero.Location.X + moveDir.X * 1.5f),
                    MathF.Round(_hero.Location.Y + moveDir.Y * 1.5f));
                _hero.MoveTo(shorterTarget);
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

        private static Texture2D _joystickBaseTex;
        private static Texture2D _joystickKnobTex;
        private static Texture2D _btnRingTex;

        private void EnsureTextures(GraphicsDevice gd)
        {
            if (_joystickBaseTex != null && !_joystickBaseTex.IsDisposed)
                return;

            _joystickBaseTex = CreateJoystickBaseTexture(gd, 256);
            _joystickKnobTex = CreateJoystickKnobTexture(gd, 128);
            _btnRingTex = CreateButtonRingTexture(gd, 128);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var gd = GraphicsDevice;
            if (gd == null) return;

            EnsureTextures(gd);

            var sb = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            var font = GraphicsManager.Instance.Font;

            if (sb == null || pixel == null) return;

            using (new SpriteBatchScope(sb, blend: BlendState.NonPremultiplied))
            {
                // 1. Draw Virtual Joystick Base (Crisp antialiased textured plate)
                if (_joystickBaseTex != null)
                {
                    Vector2 baseOrigin = new Vector2(_joystickBaseTex.Width * 0.5f, _joystickBaseTex.Height * 0.5f);
                    float baseScale = (JOYSTICK_RADIUS * 2.0f) / _joystickBaseTex.Width;
                    sb.Draw(_joystickBaseTex, _joystickCenter, null, Color.White, 0f, baseOrigin, baseScale, SpriteEffects.None, 0f);
                }

                // 2. Draw Virtual Joystick Knob (3D spherical golden jewel)
                if (_joystickKnobTex != null)
                {
                    Vector2 knobOrigin = new Vector2(_joystickKnobTex.Width * 0.5f, _joystickKnobTex.Height * 0.5f);
                    float knobScale = (KNOB_RADIUS * 2.2f) / _joystickKnobTex.Width;
                    Color knobTint = _isJoystickActive ? new Color(255, 235, 150, 255) : Color.White;
                    sb.Draw(_joystickKnobTex, _knobPosition, null, knobTint, 0f, knobOrigin, knobScale, SpriteEffects.None, 0f);
                }

                // 3. Draw Attack Button (Textured)
                if (_btnRingTex != null)
                {
                    Vector2 atkOrigin = new Vector2(_btnRingTex.Width * 0.5f, _btnRingTex.Height * 0.5f);
                    float atkScale = (ATK_RADIUS * 2.0f) / _btnRingTex.Width;
                    Color atkTint = _atkPressed ? new Color(255, 120, 100, 255) : new Color(220, 60, 50, 240);
                    sb.Draw(_btnRingTex, _atkButtonCenter, null, atkTint, 0f, atkOrigin, atkScale, SpriteEffects.None, 0f);
                }
                if (font != null)
                {
                    string atkText = "ATK";
                    Vector2 textSize = font.MeasureString(atkText);
                    Vector2 textPos = _atkButtonCenter - textSize * 0.5f;
                    sb.DrawString(font, atkText, textPos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, atkText, textPos, Color.Gold);
                }

                // 4. Draw HP Potion Button (Textured)
                if (_btnRingTex != null)
                {
                    Vector2 hpOrigin = new Vector2(_btnRingTex.Width * 0.5f, _btnRingTex.Height * 0.5f);
                    float hpScale = (POTION_RADIUS * 2.0f) / _btnRingTex.Width;
                    Color hpTint = _hpPressed ? new Color(255, 140, 140, 255) : new Color(180, 40, 40, 230);
                    sb.Draw(_btnRingTex, _hpButtonCenter, null, hpTint, 0f, hpOrigin, hpScale, SpriteEffects.None, 0f);
                }
                if (font != null)
                {
                    string hpText = "HP";
                    Vector2 textSize = font.MeasureString(hpText);
                    Vector2 textPos = _hpButtonCenter - textSize * 0.5f;
                    sb.DrawString(font, hpText, textPos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, hpText, textPos, Color.White);
                }

                // 5. Draw MP Potion Button (Textured)
                if (_btnRingTex != null)
                {
                    Vector2 mpOrigin = new Vector2(_btnRingTex.Width * 0.5f, _btnRingTex.Height * 0.5f);
                    float mpScale = (POTION_RADIUS * 2.0f) / _btnRingTex.Width;
                    Color mpTint = _mpPressed ? new Color(140, 200, 255, 255) : new Color(40, 100, 210, 230);
                    sb.Draw(_btnRingTex, _mpButtonCenter, null, mpTint, 0f, mpOrigin, mpScale, SpriteEffects.None, 0f);
                }
                if (font != null)
                {
                    string mpText = "MP";
                    Vector2 textSize = font.MeasureString(mpText);
                    Vector2 textPos = _mpButtonCenter - textSize * 0.5f;
                    sb.DrawString(font, mpText, textPos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, mpText, textPos, Color.White);
                }

                // 6. Draw Top Menu Shortcut Buttons
                DrawPillButton(sb, pixel, font, _invBtnRect, "INV", _invPressed, new Color(40, 140, 80));
                DrawPillButton(sb, pixel, font, _statsBtnRect, "STATS", _statsPressed, new Color(180, 120, 30));
                DrawPillButton(sb, pixel, font, _warpBtnRect, "WARP", _warpPressed, new Color(60, 100, 180));

                // 7. Real-time Diagnostic HUD
                if (font != null)
                {
                    var world = _scene?.World;
                    var terrain = world?.Terrain;
                    string diag1 = $"MAP: {world?.Name ?? "None"} | HERO: ({_hero?.Location.X:F0},{_hero?.Location.Y:F0})";
                    string diag2 = $"JOY: {(_isJoystickActive ? "DRAGGING" : "IDLE")} ({_joystickDir.X:F2},{_joystickDir.Y:F2})";

                    sb.DrawString(font, diag1, new Vector2(11, 11), Color.Black);
                    sb.DrawString(font, diag1, new Vector2(10, 10), Color.Yellow);

                    sb.DrawString(font, diag2, new Vector2(11, 27), Color.Black);
                    sb.DrawString(font, diag2, new Vector2(10, 26), Color.Cyan);
                }
            }
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

        private static Texture2D CreateJoystickBaseTexture(GraphicsDevice gd, int size)
        {
            var tex = new Texture2D(gd, size, size);
            Color[] data = new Color[size * size];
            float center = size / 2f;
            float maxR = center - 4f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR + 2f)
                    {
                        data[y * size + x] = Color.Transparent;
                        continue;
                    }

                    float edgeAlpha = Math.Clamp(maxR + 2f - dist, 0f, 1f);

                    if (dist >= maxR - 12f)
                    {
                        float angle = MathF.Atan2(dy, dx);
                        float light = 0.8f + 0.35f * MathF.Cos(angle + 2.3f);
                        byte r = (byte)Math.Clamp(218 * light, 0, 255);
                        byte g = (byte)Math.Clamp(165 * light, 0, 255);
                        byte b = (byte)Math.Clamp(32 * light, 0, 255);
                        data[y * size + x] = new Color((int)r, (int)g, (int)b, (int)(230 * edgeAlpha));
                    }
                    else if (dist >= maxR - 18f)
                    {
                        data[y * size + x] = new Color(30, 22, 10, (int)(200 * edgeAlpha));
                    }
                    else
                    {
                        float innerRatio = dist / (maxR - 18f);
                        byte r = (byte)(15 + 20 * innerRatio);
                        byte g = (byte)(20 + 25 * innerRatio);
                        byte b = (byte)(32 + 35 * innerRatio);
                        byte a = (byte)(130 + 50 * innerRatio);

                        if (MathF.Abs(dist - maxR * 0.5f) < 2f)
                        {
                            r = (byte)Math.Min(255, r + 40);
                            g = (byte)Math.Min(255, g + 50);
                            b = (byte)Math.Min(255, b + 70);
                            a = (byte)Math.Min(255, a + 60);
                        }

                        if (dist > maxR * 0.65f && dist < maxR * 0.85f)
                        {
                            if (MathF.Abs(dx) < 3f || MathF.Abs(dy) < 3f)
                            {
                                r = 210; g = 175; b = 70; a = 200;
                            }
                        }

                        data[y * size + x] = new Color((int)r, (int)g, (int)b, (int)a);
                    }
                }
            }

            tex.SetData(data);
            return tex;
        }

        private static Texture2D CreateJoystickKnobTexture(GraphicsDevice gd, int size)
        {
            var tex = new Texture2D(gd, size, size);
            Color[] data = new Color[size * size];
            float center = size / 2f;
            float maxR = center - 4f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR + 2f)
                    {
                        data[y * size + x] = Color.Transparent;
                        continue;
                    }

                    float edgeAlpha = Math.Clamp(maxR + 2f - dist, 0f, 1f);

                    if (dist >= maxR - 8f)
                    {
                        float angle = MathF.Atan2(dy, dx);
                        float light = 0.85f + 0.4f * MathF.Cos(angle + 2.3f);
                        byte r = (byte)Math.Clamp(235 * light, 0, 255);
                        byte g = (byte)Math.Clamp(190 * light, 0, 255);
                        byte b = (byte)Math.Clamp(60 * light, 0, 255);
                        data[y * size + x] = new Color((int)r, (int)g, (int)b, (int)(240 * edgeAlpha));
                    }
                    else
                    {
                        float nx = dx / (maxR - 8f);
                        float ny = dy / (maxR - 8f);
                        float nz = MathF.Sqrt(MathF.Max(0f, 1f - nx * nx - ny * ny));

                        float lx = -0.5f, ly = -0.6f, lz = 0.7f;
                        float len = MathF.Sqrt(lx * lx + ly * ly + lz * lz);
                        lx /= len; ly /= len; lz /= len;

                        float diffuse = MathF.Max(0f, nx * lx + ny * ly + nz * lz);
                        float vx = 0, vy = 0, vz = 1;
                        float hx = lx + vx, hy = ly + vy, hz = lz + vz;
                        float hlen = MathF.Sqrt(hx * hx + hy * hy + hz * hz);
                        hx /= hlen; hy /= hlen; hz /= hlen;
                        float spec = MathF.Pow(MathF.Max(0f, nx * hx + ny * hy + nz * hz), 16f);

                        byte r = (byte)Math.Clamp((80 + 130 * diffuse + 80 * spec), 0, 255);
                        byte g = (byte)Math.Clamp((110 + 130 * diffuse + 80 * spec), 0, 255);
                        byte b = (byte)Math.Clamp((180 + 75 * diffuse + 80 * spec), 0, 255);
                        data[y * size + x] = new Color((int)r, (int)g, (int)b, (int)(235 * edgeAlpha));
                    }
                }
            }

            tex.SetData(data);
            return tex;
        }

        private static Texture2D CreateButtonRingTexture(GraphicsDevice gd, int size)
        {
            var tex = new Texture2D(gd, size, size);
            Color[] data = new Color[size * size];
            float center = size / 2f;
            float maxR = center - 4f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    if (dist > maxR + 2f)
                    {
                        data[y * size + x] = Color.Transparent;
                        continue;
                    }

                    float edgeAlpha = Math.Clamp(maxR + 2f - dist, 0f, 1f);

                    if (dist >= maxR - 10f)
                    {
                        float angle = MathF.Atan2(dy, dx);
                        float light = 0.85f + 0.35f * MathF.Cos(angle + 2.3f);
                        byte r = (byte)Math.Clamp(225 * light, 0, 255);
                        byte g = (byte)Math.Clamp(180 * light, 0, 255);
                        byte b = (byte)Math.Clamp(50 * light, 0, 255);
                        data[y * size + x] = new Color((int)r, (int)g, (int)b, (int)(240 * edgeAlpha));
                    }
                    else
                    {
                        float innerRatio = dist / (maxR - 10f);
                        byte r = (byte)(25 + 30 * innerRatio);
                        byte g = (byte)(25 + 30 * innerRatio);
                        byte b = (byte)(35 + 40 * innerRatio);
                        data[y * size + x] = new Color((int)r, (int)g, (int)b, (int)(190 * edgeAlpha));
                    }
                }
            }

            tex.SetData(data);
            return tex;
        }
    }
}
