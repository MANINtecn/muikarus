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
        private const float DEAD_ZONE = 0.15f;
        private const float MOVE_INTERVAL_MS = 380f;

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
            
            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);
            Interactive = true;
            Status = GameControlStatus.Ready;
            Visible = true;
        }

        private void UpdateLayoutPositions()
        {
            // Empty - removed all UI buttons and joystick
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
    }
}
