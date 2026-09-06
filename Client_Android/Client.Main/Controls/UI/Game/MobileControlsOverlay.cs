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
        private readonly InventoryControl _inventory;
        private readonly CharacterInfoWindowControl _charInfo;
        private readonly MoveCommandWindow _warpWindow;
        private readonly CommandWindowControl _commandWindow;

        // Contextual action (Attack vs NPC Talk)
        private NPCObject _nearbyNpc = null;
        private const float NPC_INTERACT_TILES = 3.5f;

        // Joystick configuration (Upper-left, enlarged for comfortable thumb reach)
        private const float JOYSTICK_RADIUS = 90f;
        private const float KNOB_RADIUS = 40f;
        private const float DEAD_ZONE = 0.45f;
        private const float MOVE_INTERVAL_MS = 400f;

        private Vector2 _joystickCenter;
        private Vector2 _knobPosition;
        private int _joystickTouchId = -1;
        private bool _isJoystickActive = false;
        private Vector2 _joystickDir = Vector2.Zero;
        private float _lastMoveSentTime = 0f;

        // Pinch-to-zoom multi-touch gesture
        private bool _isPinching = false;
        private float _prevPinchDist = 0f;

        // Button configurations (doubled attack/talk button, enlarged skills in arc)
        private Vector2 _atkButtonCenter;
        private const float ATK_RADIUS = 68f;
        private bool _atkPressed = false;

        private Vector2 _hpButtonCenter;
        private const float POTION_RADIUS = 32f;
        private bool _hpPressed = false;

        private Vector2 _mpButtonCenter;
        private bool _mpPressed = false;

        // Skill button configurations
        private Vector2 _skill1ButtonCenter;
        private Vector2 _skill2ButtonCenter;
        private Vector2 _skill3ButtonCenter;
        private const float SKILL_RADIUS = 42f;
        private bool _skill1Pressed = false;
        private bool _skill2Pressed = false;
        private bool _skill3Pressed = false;

        // Selected skill slot (0 = none/regular attack, 1 = Skill 1, 2 = Skill 2, 3 = Skill 3)
        private int _selectedSkillSlot = 0;

        // Menu shortcut buttons (top-right)
        private Rectangle _invBtnRect;
        private Rectangle _statsBtnRect;
        private Rectangle _warpBtnRect;
        private Rectangle _cmdBtnRect;
        private bool _invPressed = false;
        private bool _statsPressed = false;
        private bool _warpPressed = false;
        private bool _cmdPressed = false;

        public bool IsTouchOverControls(Vector2 pos)
        {
            Point pt = pos.ToPoint();
            return Vector2.Distance(pos, _joystickCenter) <= JOYSTICK_RADIUS * 2.2f
                || Vector2.Distance(pos, _atkButtonCenter) <= ATK_RADIUS
                || Vector2.Distance(pos, _hpButtonCenter) <= POTION_RADIUS
                || Vector2.Distance(pos, _mpButtonCenter) <= POTION_RADIUS
                || Vector2.Distance(pos, _skill1ButtonCenter) <= SKILL_RADIUS
                || Vector2.Distance(pos, _skill2ButtonCenter) <= SKILL_RADIUS
                || Vector2.Distance(pos, _skill3ButtonCenter) <= SKILL_RADIUS
                || _cmdBtnRect.Contains(pt)
                || _invBtnRect.Contains(pt)
                || _statsBtnRect.Contains(pt)
                || _warpBtnRect.Contains(pt);
        }

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
            Interactive = true;
            Status = GameControlStatus.Ready;
            Visible = true;

            UpdateLayoutPositions();
        }

        private void UpdateLayoutPositions()
        {
            int w = GraphicsDevice?.Viewport.Width ?? MuGame.Instance.Width;
            int h = GraphicsDevice?.Viewport.Height ?? MuGame.Instance.Height;

            // Joystick in upper-left corner
            _joystickCenter = new Vector2(150f, 160f);
            if (!_isJoystickActive)
                _knobPosition = _joystickCenter;

            // Large Action/Attack/Talk button at bottom-right
            _atkButtonCenter = new Vector2(w - 120f, h - 120f);

            // Skills arrayed in generous arc around the attack button (radius 145px)
            _skill1ButtonCenter = new Vector2(_atkButtonCenter.X - 145f, _atkButtonCenter.Y);
            _skill2ButtonCenter = new Vector2(_atkButtonCenter.X - 105f, _atkButtonCenter.Y - 105f);
            _skill3ButtonCenter = new Vector2(_atkButtonCenter.X, _atkButtonCenter.Y - 145f);

            // Potions placed comfortably above skill arc
            _hpButtonCenter = new Vector2(_atkButtonCenter.X - 185f, _atkButtonCenter.Y - 95f);
            _mpButtonCenter = new Vector2(_atkButtonCenter.X - 95f, _atkButtonCenter.Y - 185f);

            // Menu shortcuts at top-right
            int btnW = 68;
            int btnH = 36;
            int topY = 24;
            _warpBtnRect = new Rectangle(w - 74, topY, btnW, btnH);
            _statsBtnRect = new Rectangle(w - 148, topY, btnW, btnH);
            _invBtnRect = new Rectangle(w - 222, topY, btnW, btnH);
            _cmdBtnRect = new Rectangle(w - 296, topY, btnW, btnH);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            UpdateLayoutPositions();
            UpdateNearbyNpc();

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

            // Handle Joystick Walking: only queue the next single-tile step once the
            // previous one has fully finished (no pending path, not mid-move). This is
            // what stops the hero from "walking further than expected" when the joystick
            // is held - commands never stack up faster than the character can execute them.
            if (_isJoystickActive && _joystickDir.Length() > DEAD_ZONE)
            {
                bool previousStepFinished = _hero == null || (_hero.RemainingPathSteps == 0 && !_hero.IsMoving);
                if (previousStepFinished && totalMs - _lastMoveSentTime >= MOVE_INTERVAL_MS)
                {
                    _lastMoveSentTime = totalMs;
                    SendJoystickMovement();
                }
            }
        }

        private void UpdateNearbyNpc()
        {
            _nearbyNpc = null;
            if (_hero == null || _scene?.World == null)
                return;

            var walkers = _scene.World.WalkerObjectsById;
            if (walkers == null || walkers.Count == 0)
                return;

            float closestTileDist = NPC_INTERACT_TILES;
            NPCObject closest = null;

            foreach (var walker in walkers.Values)
            {
                if (walker is NPCObject npc && npc.Visible && !npc.Hidden)
                {
                    float dist = Vector2.Distance(_hero.Location, npc.Location);
                    if (dist < closestTileDist)
                    {
                        closestTileDist = dist;
                        closest = npc;
                    }
                }
            }

            _nearbyNpc = closest;
        }

        private void ProcessTouchInput(TouchCollection touches)
        {
            bool joystickTouchFound = false;

            // Pinch-to-zoom multi-touch handling
            if (touches.Count >= 2)
            {
                var t1 = touches[0];
                var t2 = touches[1];
                bool t1Over = IsTouchOverControls(t1.Position);
                bool t2Over = IsTouchOverControls(t2.Position);

                if (!t1Over && !t2Over &&
                    (t1.State == TouchLocationState.Moved || t2.State == TouchLocationState.Moved))
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
                else if (t1Over || t2Over)
                {
                    _isPinching = false;
                }
            }
            else
            {
                _isPinching = false;
            }

            foreach (var touch in touches)
            {
                Vector2 pos = touch.Position;

                if (IsTouchOverControls(pos))
                {
                    _scene?.SetMouseInputConsumed();
                }

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

                // 5. Skill 1 Button
                if (Vector2.Distance(pos, _skill1ButtonCenter) <= SKILL_RADIUS)
                {
                    if (touch.State == TouchLocationState.Pressed)
                    {
                        _skill1Pressed = true;
                        ExecuteSkill(1);
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _skill1Pressed = false;
                    }
                    continue;
                }

                // 6. Skill 2 Button
                if (Vector2.Distance(pos, _skill2ButtonCenter) <= SKILL_RADIUS)
                {
                    if (touch.State == TouchLocationState.Pressed)
                    {
                        _skill2Pressed = true;
                        ExecuteSkill(2);
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _skill2Pressed = false;
                    }
                    continue;
                }

                // 7. Skill 3 Button
                if (Vector2.Distance(pos, _skill3ButtonCenter) <= SKILL_RADIUS)
                {
                    if (touch.State == TouchLocationState.Pressed)
                    {
                        _skill3Pressed = true;
                        ExecuteSkill(3);
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _skill3Pressed = false;
                    }
                    continue;
                }

                if (_cmdBtnRect.Contains((int)pos.X, (int)pos.Y))
                {
                    if (touch.State == TouchLocationState.Pressed && !_cmdPressed)
                    {
                        _cmdPressed = true;
                        ToggleCommand();
                    }
                    else if (touch.State == TouchLocationState.Released)
                    {
                        _cmdPressed = false;
                    }
                    continue;
                }

                if (_invBtnRect.Contains((int)pos.X, (int)pos.Y))
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

                if (_statsBtnRect.Contains((int)pos.X, (int)pos.Y))
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

                if (_warpBtnRect.Contains((int)pos.X, (int)pos.Y))
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

            if (isLeftDown && IsTouchOverControls(mPos))
            {
                _scene?.SetMouseInputConsumed();
            }

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
                    else if (Vector2.Distance(mPos, _skill1ButtonCenter) <= SKILL_RADIUS)
                    {
                        _skill1Pressed = true;
                        ExecuteSkill(1);
                    }
                    else if (Vector2.Distance(mPos, _skill2ButtonCenter) <= SKILL_RADIUS)
                    {
                        _skill2Pressed = true;
                        ExecuteSkill(2);
                    }
                    else if (Vector2.Distance(mPos, _skill3ButtonCenter) <= SKILL_RADIUS)
                    {
                        _skill3Pressed = true;
                        ExecuteSkill(3);
                    }
                    else if (_cmdBtnRect.Contains(mouse.Position))
                    {
                        _cmdPressed = true;
                        ToggleCommand();
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
                _skill1Pressed = false;
                _skill2Pressed = false;
                _skill3Pressed = false;
                _cmdPressed = false;
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

            // Single-tile step per tick: keeps the joystick feeling precise and 1:1 with the
            // held direction instead of launching a multi-tile pathfind that overshoots.
            const float stepDist = 1f;
            Vector2 targetLocation = new Vector2(
                MathF.Round(_hero.Location.X + moveDir.X * stepDist),
                MathF.Round(_hero.Location.Y + moveDir.Y * stepDist));

            if (targetLocation == _hero.Location)
                return;

            if (_hero.World is WalkableWorldControl walkable && !walkable.IsWalkable(targetLocation))
                return;

            _hero.MoveTo(targetLocation);
        }

        public (ushort skillId, string name) GetSkillForSlot(int slot)
        {
            var skills = MuGame.Network?.GetCharacterState()?.GetSkills()?.ToList();
            if (skills != null && skills.Count >= slot)
            {
                var skillEntry = skills[slot - 1];
                string name = GetSkillNameById(skillEntry.SkillId);
                return (skillEntry.SkillId, name);
            }

            // Default fallback based on character class
            var cls = _hero?.CharacterClass ?? MUnique.OpenMU.Network.Packets.CharacterClassNumber.DarkKnight;
            if (cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.DarkWizard ||
                cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.SoulMaster ||
                cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.GrandMaster)
            {
                return slot switch
                {
                    1 => (4, "FIRE"),
                    2 => (9, "EVIL"),
                    3 => (10, "HELL"),
                    _ => (0, "SKILL")
                };
            }
            if (cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.FairyElf ||
                cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.MuseElf ||
                cls == MUnique.OpenMU.Network.Packets.CharacterClassNumber.HighElf)
            {
                return slot switch
                {
                    1 => (24, "TRIPLE"),
                    2 => (26, "HEAL"),
                    3 => (27, "BUFF"),
                    _ => (0, "SKILL")
                };
            }

            // Dark Knight default
            return slot switch
            {
                1 => (19, "SLASH"),
                2 => (41, "TWIST"),
                3 => (42, "RAGE"),
                _ => (0, "SKILL")
            };
        }

        private static string GetSkillNameById(ushort id) => id switch
        {
            1 => "POISON",
            2 => "METEOR",
            3 => "LIGHTN",
            4 => "FIRE",
            5 => "FLAME",
            6 => "TELE",
            7 => "ICE",
            8 => "TWIST",
            9 => "EVIL",
            10 => "HELL",
            14 => "INFERN",
            19 => "SLASH",
            22 => "CYCLONE",
            24 => "TRIPLE",
            26 => "HEAL",
            27 => "DEF+",
            28 => "DMG+",
            41 => "TWIST",
            42 => "RAGE",
            43 => "STAB",
            48 => "SWELL",
            55 => "F.SLASH",
            61 => "F.BURST",
            _ => $"SKL {id}"
        };

        private void ExecuteAttack()
        {
            if (_hero == null || _hero.World == null)
                return;

            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");

            if (_nearbyNpc != null)
            {
                _nearbyNpc.OnClick();
                Helpers.OnScreenLogger.Log($"Falando com {_nearbyNpc.DisplayName}!");
            }
            else if (_selectedSkillSlot > 0)
            {
                var (skillId, name) = GetSkillForSlot(_selectedSkillSlot);
                _hero.UseSkill(_selectedSkillSlot, skillId);
                Helpers.OnScreenLogger.Log($"Usando {name}!");
            }
            else
            {
                _hero.ManualAttack();
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

        private void ExecuteSkill(int skillSlot)
        {
            if (_hero == null || _hero.World == null)
                return;

            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");

            var (skillId, name) = GetSkillForSlot(skillSlot);

            if (_selectedSkillSlot == skillSlot)
            {
                // Already selected: fire it directly!
                _hero.UseSkill(skillSlot, skillId);
                Helpers.OnScreenLogger.Log($"Disparando {name}!");
            }
            else
            {
                // Select this skill!
                _selectedSkillSlot = skillSlot;
                Helpers.OnScreenLogger.Log($"Skill selecionada: {name}!");
            }
        }

        private void ToggleCommand()
        {
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            if (_commandWindow != null)
            {
                _commandWindow.Toggle();
            }
            else
            {
                CommandWindowControl.Instance?.Toggle();
            }
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

                // 3. Draw Attack / Interaction Button (Contextual)
                if (_btnRingTex != null)
                {
                    Vector2 atkOrigin = new Vector2(_btnRingTex.Width * 0.5f, _btnRingTex.Height * 0.5f);
                    float atkScale = (ATK_RADIUS * 2.0f) / _btnRingTex.Width;
                    Color atkTint;
                    if (_nearbyNpc != null)
                    {
                        // Emerald green interaction button
                        atkTint = _atkPressed ? new Color(130, 255, 180, 255) : new Color(35, 205, 125, 240);
                    }
                    else if (_selectedSkillSlot > 0)
                    {
                        Color skillColor = _selectedSkillSlot switch
                        {
                            1 => new Color(240, 160, 30, 240),
                            2 => new Color(170, 70, 230, 240),
                            3 => new Color(30, 180, 210, 240),
                            _ => new Color(220, 60, 50, 240)
                        };
                        atkTint = _atkPressed ? Color.Lerp(skillColor, Color.White, 0.4f) : skillColor;
                    }
                    else
                    {
                        // Red attack button
                        atkTint = _atkPressed ? new Color(255, 120, 100, 255) : new Color(220, 60, 50, 240);
                    }
                    sb.Draw(_btnRingTex, _atkButtonCenter, null, atkTint, 0f, atkOrigin, atkScale, SpriteEffects.None, 0f);
                }
                if (font != null)
                {
                    string btnText;
                    Color textColor;
                    if (_nearbyNpc != null)
                    {
                        btnText = "TALK";
                        textColor = Color.White;
                    }
                    else if (_selectedSkillSlot > 0)
                    {
                        btnText = GetSkillForSlot(_selectedSkillSlot).name;
                        textColor = Color.White;
                    }
                    else
                    {
                        btnText = "ATK";
                        textColor = Color.Gold;
                    }

                    Vector2 textSize = font.MeasureString(btnText);
                    Vector2 textPos = _atkButtonCenter - textSize * 0.5f;
                    sb.DrawString(font, btnText, textPos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, btnText, textPos, textColor);

                    if (_nearbyNpc != null && !string.IsNullOrEmpty(_nearbyNpc.DisplayName))
                    {
                        string npcName = _nearbyNpc.DisplayName;
                        Vector2 nameSize = font.MeasureString(npcName);
                        Vector2 namePos = new Vector2(_atkButtonCenter.X - nameSize.X * 0.5f, _atkButtonCenter.Y - ATK_RADIUS - nameSize.Y - 6f);
                        sb.DrawString(font, npcName, namePos + new Vector2(1, 1), Color.Black);
                        sb.DrawString(font, npcName, namePos, Color.Cyan);
                    }
                    else if (_selectedSkillSlot > 0)
                    {
                        string activeLabel = $"[SKL {_selectedSkillSlot}]";
                        Vector2 labelSize = font.MeasureString(activeLabel);
                        Vector2 labelPos = new Vector2(_atkButtonCenter.X - labelSize.X * 0.5f, _atkButtonCenter.Y - ATK_RADIUS - labelSize.Y - 4f);
                        sb.DrawString(font, activeLabel, labelPos + new Vector2(1, 1), Color.Black);
                        sb.DrawString(font, activeLabel, labelPos, Color.Yellow);
                    }
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

                // 6. Draw Skill 1 Button (Amber/Gold)
                var (s1Id, s1Name) = GetSkillForSlot(1);
                if (_btnRingTex != null)
                {
                    Vector2 s1Origin = new Vector2(_btnRingTex.Width * 0.5f, _btnRingTex.Height * 0.5f);
                    float s1Scale = (SKILL_RADIUS * 2.0f) / _btnRingTex.Width;
                    if (_selectedSkillSlot == 1)
                    {
                        // Glowing outer halo for selected skill
                        sb.Draw(_btnRingTex, _skill1ButtonCenter, null, new Color(255, 230, 80, 200), 0f, s1Origin, s1Scale * 1.3f, SpriteEffects.None, 0f);
                    }
                    Color s1Tint = _skill1Pressed ? new Color(255, 220, 120, 255) : (_selectedSkillSlot == 1 ? new Color(255, 200, 50, 255) : new Color(230, 150, 30, 240));
                    sb.Draw(_btnRingTex, _skill1ButtonCenter, null, s1Tint, 0f, s1Origin, s1Scale, SpriteEffects.None, 0f);
                }
                if (font != null)
                {
                    Vector2 s1Size = font.MeasureString(s1Name);
                    Vector2 s1Pos = _skill1ButtonCenter - s1Size * 0.5f;
                    sb.DrawString(font, s1Name, s1Pos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, s1Name, s1Pos, _selectedSkillSlot == 1 ? Color.White : Color.Gold);
                }

                // 7. Draw Skill 2 Button (Purple/Violet)
                var (s2Id, s2Name) = GetSkillForSlot(2);
                if (_btnRingTex != null)
                {
                    Vector2 s2Origin = new Vector2(_btnRingTex.Width * 0.5f, _btnRingTex.Height * 0.5f);
                    float s2Scale = (SKILL_RADIUS * 2.0f) / _btnRingTex.Width;
                    if (_selectedSkillSlot == 2)
                    {
                        // Glowing outer halo for selected skill
                        sb.Draw(_btnRingTex, _skill2ButtonCenter, null, new Color(255, 120, 255, 200), 0f, s2Origin, s2Scale * 1.3f, SpriteEffects.None, 0f);
                    }
                    Color s2Tint = _skill2Pressed ? new Color(230, 150, 255, 255) : (_selectedSkillSlot == 2 ? new Color(220, 110, 255, 255) : new Color(170, 70, 230, 240));
                    sb.Draw(_btnRingTex, _skill2ButtonCenter, null, s2Tint, 0f, s2Origin, s2Scale, SpriteEffects.None, 0f);
                }
                if (font != null)
                {
                    Vector2 s2Size = font.MeasureString(s2Name);
                    Vector2 s2Pos = _skill2ButtonCenter - s2Size * 0.5f;
                    sb.DrawString(font, s2Name, s2Pos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, s2Name, s2Pos, _selectedSkillSlot == 2 ? Color.White : Color.Violet);
                }

                // 8. Draw Skill 3 Button (Cyan/Teal)
                var (s3Id, s3Name) = GetSkillForSlot(3);
                if (_btnRingTex != null)
                {
                    Vector2 s3Origin = new Vector2(_btnRingTex.Width * 0.5f, _btnRingTex.Height * 0.5f);
                    float s3Scale = (SKILL_RADIUS * 2.0f) / _btnRingTex.Width;
                    if (_selectedSkillSlot == 3)
                    {
                        // Glowing outer halo for selected skill
                        sb.Draw(_btnRingTex, _skill3ButtonCenter, null, new Color(100, 240, 255, 200), 0f, s3Origin, s3Scale * 1.3f, SpriteEffects.None, 0f);
                    }
                    Color s3Tint = _skill3Pressed ? new Color(150, 240, 255, 255) : (_selectedSkillSlot == 3 ? new Color(60, 220, 255, 255) : new Color(30, 180, 210, 240));
                    sb.Draw(_btnRingTex, _skill3ButtonCenter, null, s3Tint, 0f, s3Origin, s3Scale, SpriteEffects.None, 0f);
                }
                if (font != null)
                {
                    Vector2 s3Size = font.MeasureString(s3Name);
                    Vector2 s3Pos = _skill3ButtonCenter - s3Size * 0.5f;
                    sb.DrawString(font, s3Name, s3Pos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, s3Name, s3Pos, _selectedSkillSlot == 3 ? Color.White : Color.Cyan);
                }

                // 9. Draw Top Menu Shortcut Buttons
                DrawPillButton(sb, pixel, font, _cmdBtnRect, "CMD (D)", _cmdPressed, new Color(140, 60, 180));
                DrawPillButton(sb, pixel, font, _invBtnRect, "INVEN", _invPressed, new Color(40, 140, 80));
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
