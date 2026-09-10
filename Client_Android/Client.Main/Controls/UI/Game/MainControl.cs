using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI.Game
{
    public enum PotionType
    {
        Health,
        Mana,
        AntidoteOrComplex,
        SpecialOrPortal
    }

    public class MainControl : DynamicLayoutControl
    {
        // Resource paths
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.MainRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/main_IE.ozd";

        // UI components
        private readonly MainHPControl _hp;
        private readonly MainMPControl _mp;
        private readonly CharacterState _state;

        private KeyboardState _prevKeyboardState;

        // Window toggle events
        public event Action InventoryRequested;
        public event Action CharacterInfoRequested;
        public event Action CommandWindowRequested;

        public MainControl(CharacterState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            Interactive = true;

            // Semi-transparent hotkeys
            foreach (var key in new[] { "ActiveSkill_1", "ActiveSkill_2", "0", "1", "2", "3", "4", "5" })
            {
                AlphaOverrides[key] = 0.7f;
            }

            // HP control
            _hp = new MainHPControl { Name = "HP_1" };
            _hp.Tag = new LayoutInfo
            {
                ScreenX = 292,
                ScreenY = 604,
                Width = 100,
                Height = 100,
                Z = -5
            };
            if (_hp is ExtendedUIControl extHp && _hp.Tag is LayoutInfo lhp)
            {
                extHp.RenderOrder = lhp.Z;
            }

            // MP control
            _mp = new MainMPControl { Name = "MP_1" };
            _mp.Tag = new LayoutInfo
            {
                ScreenX = 860,
                ScreenY = 604,
                Width = 100,
                Height = 100,
                Z = -5
            };
            if (_mp is ExtendedUIControl extMp && _mp.Tag is LayoutInfo lmp)
            {
                extMp.RenderOrder = lmp.Z;
            }

            // Initial values from CharacterState (if available)
            _hp.SetValues((int)state.CurrentHp, (int)state.MaxHp);
            _mp.SetValues((int)state.CurrentMana, (int)state.MaxMp);

            // Update when server sends changes
            state.HealthChanged += (cur, max) => _hp.SetValues((int)cur, (int)max);
            state.ManaChanged += (cur, max) => _mp.SetValues((int)cur, (int)max);

            Controls.Clear();
            Controls.Add(_mp);
            Controls.Add(_hp);

            CreateControls();
            UpdateLayout();
        }

        public override void UpdateLayout()
        {
            base.UpdateLayout();

            int currentWidth = MuGame.Instance.GraphicsDevice.Viewport.Width;
            int currentHeight = MuGame.Instance.GraphicsDevice.Viewport.Height;
            float scaleX = (float)currentWidth / DesignWidth;
            float scaleY = (float)currentHeight / DesignHeight;
            float uniformScale = Math.Min(scaleX, scaleY) * CustomScale;

            X = 0;
            Y = 0;
            ViewSize = new Point(currentWidth, currentHeight);
            Scale = 1.0f;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            int currentWidth = MuGame.Instance.GraphicsDevice.Viewport.Width;
            int currentHeight = MuGame.Instance.GraphicsDevice.Viewport.Height;
            float scaleX = (float)currentWidth / DesignWidth;
            float scaleY = (float)currentHeight / DesignHeight;
            float uniformScale = Math.Min(scaleX, scaleY) * CustomScale;

            int hudTop = (int)(590 * uniformScale);

            var mouse = MuGame.Instance.Mouse;
            var prevMouse = MuGame.Instance.PrevMouseState;

            bool isOverHud = mouse.Position.Y >= hudTop;
            bool isPressed = mouse.LeftButton == ButtonState.Pressed;
            bool isJustReleased = mouse.LeftButton == ButtonState.Released && prevMouse.LeftButton == ButtonState.Pressed;

            if (isOverHud)
            {
                IsMouseOver = true;
                if (Scene is Client.Main.Scenes.BaseScene bs)
                {
                    bs.MouseHoverControl = this;
                    bs.MouseControl = this;

                    // Consume touch immediately so terrain raycaster never triggers click-to-move
                    if (isPressed || isJustReleased)
                    {
                        bs.SetMouseInputConsumed();
                    }
                }

                if (isJustReleased)
                {
                    HandleHudClick(mouse.Position);
                }
            }

            // Keyboard hotkeys (for testing and bluetooth keyboards)
            var kb = Keyboard.GetState();
            if (kb.IsKeyDown(Keys.Q) && !_prevKeyboardState.IsKeyDown(Keys.Q)) TryConsumePotion(PotionType.Health);
            if (kb.IsKeyDown(Keys.W) && !_prevKeyboardState.IsKeyDown(Keys.W)) TryConsumePotion(PotionType.Mana);
            if (kb.IsKeyDown(Keys.E) && !_prevKeyboardState.IsKeyDown(Keys.E)) TryConsumePotion(PotionType.AntidoteOrComplex);
            if (kb.IsKeyDown(Keys.R) && !_prevKeyboardState.IsKeyDown(Keys.R)) TryConsumePotion(PotionType.SpecialOrPortal);

            if (kb.IsKeyDown(Keys.D1) && !_prevKeyboardState.IsKeyDown(Keys.D1)) SelectSkillIndex(0);
            if (kb.IsKeyDown(Keys.D2) && !_prevKeyboardState.IsKeyDown(Keys.D2)) SelectSkillIndex(1);
            if (kb.IsKeyDown(Keys.D3) && !_prevKeyboardState.IsKeyDown(Keys.D3)) SelectSkillIndex(2);
            if (kb.IsKeyDown(Keys.D4) && !_prevKeyboardState.IsKeyDown(Keys.D4)) SelectSkillIndex(3);
            if (kb.IsKeyDown(Keys.D5) && !_prevKeyboardState.IsKeyDown(Keys.D5)) SelectSkillIndex(4);
            _prevKeyboardState = kb;
        }

        private void HandleHudClick(Point pos)
        {
            // 1. Check QWE (Potions Q, W, E)
            var qwe = Controls.FirstOrDefault(c => c.Name == "QWE");
            if (qwe != null && qwe.DisplayRectangle.Contains(pos))
            {
                float slotW = qwe.DisplayRectangle.Width / 3.0f;
                float relX = pos.X - qwe.DisplayRectangle.X;
                if (relX < slotW)
                    TryConsumePotion(PotionType.Health);
                else if (relX < slotW * 2f)
                    TryConsumePotion(PotionType.Mana);
                else
                    TryConsumePotion(PotionType.AntidoteOrComplex);
                return;
            }

            // 2. Check RS (Potion/Scroll R / Shield S)
            var rs = Controls.FirstOrDefault(c => c.Name == "RS");
            if (rs != null && rs.DisplayRectangle.Contains(pos))
            {
                float slotW = rs.DisplayRectangle.Width / 2.0f;
                float relX = pos.X - rs.DisplayRectangle.X;
                if (relX < slotW)
                    TryConsumePotion(PotionType.SpecialOrPortal);
                else
                    TryConsumePotion(PotionType.SpecialOrPortal);
                return;
            }

            // 3. Check Skills 1 to 5
            for (int i = 1; i <= 5; i++)
            {
                var skillCtrl = Controls.FirstOrDefault(c => c.Name == i.ToString());
                if (skillCtrl != null)
                {
                    var hitRect = new Rectangle(
                        skillCtrl.DisplayRectangle.X - 5,
                        skillCtrl.DisplayRectangle.Y - 5,
                        skillCtrl.DisplayRectangle.Width + 10,
                        skillCtrl.DisplayRectangle.Height + 15);

                    if (hitRect.Contains(pos))
                    {
                        SelectSkillIndex(i - 1);
                        return;
                    }
                }
            }

            // 4. Check Active Skills
            var as1 = Controls.FirstOrDefault(c => c.Name == "ActiveSkill_1");
            if (as1 != null && as1.DisplayRectangle.Contains(pos))
            {
                SelectSkillIndex(0);
                return;
            }

            var as2 = Controls.FirstOrDefault(c => c.Name == "ActiveSkill_2");
            if (as2 != null && as2.DisplayRectangle.Contains(pos))
            {
                SelectSkillIndex(1);
                return;
            }

            // 5. Check Interface Buttons
            var btnInv = Controls.FirstOrDefault(c => c.Name == "button3");
            if (btnInv != null && btnInv.DisplayRectangle.Contains(pos))
            {
                InventoryRequested?.Invoke();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                return;
            }

            var btnChar = Controls.FirstOrDefault(c => c.Name == "button2");
            if (btnChar != null && btnChar.DisplayRectangle.Contains(pos))
            {
                CharacterInfoRequested?.Invoke();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                return;
            }

            var btnCmd = Controls.FirstOrDefault(c => c.Name == "Button_1");
            if (btnCmd != null && btnCmd.DisplayRectangle.Contains(pos))
            {
                CommandWindowRequested?.Invoke();
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                return;
            }

            var btnSettings = Controls.FirstOrDefault(c => c.Name == "SettingsButton");
            if (btnSettings != null && btnSettings.DisplayRectangle.Contains(pos))
            {
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                OnScreenLogger.Log("[CONFIG] Abrindo configuracoes", LogLevel.Info);
                return;
            }

            // 6. HP / MP Globes
            if (_hp.DisplayRectangle.Contains(pos))
            {
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                OnScreenLogger.Log($"[HP] Vida: {_state.CurrentHp} / {_state.MaxHp}", LogLevel.Info);
                return;
            }

            if (_mp.DisplayRectangle.Contains(pos))
            {
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                OnScreenLogger.Log($"[MP] Mana: {_state.CurrentMana} / {_state.MaxMp}", LogLevel.Info);
                return;
            }

            // 7. Exp Bar
            var exp = Controls.FirstOrDefault(c => c.Name == "ExpBar");
            if (exp != null && exp.DisplayRectangle.Contains(pos))
            {
                OnScreenLogger.Log($"[EXP] Nivel: {_state.Level} | Exp: {_state.Experience} / {_state.ExperienceForNextLevel}", LogLevel.Info);
                return;
            }
        }

        public bool TryConsumePotion(PotionType type)
        {
            var items = _state.GetInventoryItems();
            byte? targetSlot = null;
            string itemName = null;
            bool isApple = false;

            foreach (var kvp in items)
            {
                byte slot = kvp.Key;
                if (slot < 12) continue; // Skip equipment slots

                byte[] itemData = kvp.Value;
                if (itemData == null || itemData.Length < 3) continue;

                if (!ItemDataParser.TryGetGroupAndNumber(itemData, out byte group, out short number))
                {
                    group = (byte)(itemData.Length >= 6 ? (itemData[5] >> 4) : 0);
                    number = itemData[0];
                }

                string name = ItemDatabase.GetItemName(group, number) ?? string.Empty;
                bool match = false;

                switch (type)
                {
                    case PotionType.Health:
                        // Group 14: 0 = Apple, 1 = Small Healing, 2 = Medium Healing, 3 = Large Healing
                        match = (group == 14 && (number >= 0 && number <= 3)) ||
                                name.Contains("Healing", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Apple", StringComparison.OrdinalIgnoreCase);
                        if (match && (number == 0 || name.Contains("Apple", StringComparison.OrdinalIgnoreCase)))
                            isApple = true;
                        break;

                    case PotionType.Mana:
                        // Group 14: 4 = Small Mana, 5 = Medium Mana, 6 = Large Mana
                        match = (group == 14 && (number >= 4 && number <= 6)) ||
                                name.Contains("Mana", StringComparison.OrdinalIgnoreCase);
                        break;

                    case PotionType.AntidoteOrComplex:
                        // Group 14: 8 = Antidote, 35..40 = Complex / SD Potions
                        match = (group == 14 && (number == 8 || (number >= 35 && number <= 40))) ||
                                name.Contains("Antidote", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Complex", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("SD", StringComparison.OrdinalIgnoreCase);
                        break;

                    case PotionType.SpecialOrPortal:
                        // Group 14: 7 = Town Portal Scroll, 9 = Ale, 10 = Scroll of Escape
                        match = (group == 14 && (number == 7 || number == 9 || number == 10 || number == 20)) ||
                                name.Contains("Portal", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Scroll", StringComparison.OrdinalIgnoreCase);
                        break;
                }

                if (match)
                {
                    targetSlot = slot;
                    itemName = string.IsNullOrEmpty(name) ? $"Item {group}/{number}" : name;
                    break;
                }
            }

            if (targetSlot.HasValue)
            {
                byte slot = targetSlot.Value;
                if (isApple)
                    SoundController.Instance.PlayBuffer("Sound/pEatApple.wav");
                else
                    SoundController.Instance.PlayBuffer("Sound/pDrink.wav");

                var charService = MuGame.Network?.GetCharacterService();
                if (charService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await charService.SendConsumeItemRequestAsync(slot);
                    });
                }

                OnScreenLogger.Log($"[POTION] Usou {itemName} (slot {slot})!", LogLevel.Info);
                return true;
            }
            else
            {
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                string typeName = type switch
                {
                    PotionType.Health => "Vida (Q)",
                    PotionType.Mana => "Mana (W)",
                    PotionType.AntidoteOrComplex => "Antidoto/SD (E)",
                    PotionType.SpecialOrPortal => "Portal/Item (R)",
                    _ => "Pocao"
                };
                OnScreenLogger.Log($"[POTION] Nenhuma pocao de {typeName} na mochila!", LogLevel.Warning);
                return false;
            }
        }

        private void SelectSkillIndex(int skillSlotIndex)
        {
            var skills = _state.GetSkills().ToList();
            if (skills.Count > skillSlotIndex)
            {
                var chosen = skills[skillSlotIndex];
                _state.SelectedSkillId = chosen.SkillId;
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                OnScreenLogger.Log($"[SKILL] Habilidade: ID {chosen.SkillId} (Nv {chosen.SkillLevel})", LogLevel.Info);
            }
            else if (skills.Count > 0)
            {
                var fallback = skills[0];
                _state.SelectedSkillId = fallback.SkillId;
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                OnScreenLogger.Log($"[SKILL] Habilidade: ID {fallback.SkillId}", LogLevel.Info);
            }
            else
            {
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                OnScreenLogger.Log($"[SKILL] Atalho {skillSlotIndex + 1} selecionado", LogLevel.Info);
            }
        }

        public int CountPotions(PotionType type)
        {
            var items = _state.GetInventoryItems();
            int count = 0;

            foreach (var kvp in items)
            {
                if (kvp.Key < 12) continue;
                byte[] itemData = kvp.Value;
                if (itemData == null || itemData.Length < 3) continue;

                if (!ItemDataParser.TryGetGroupAndNumber(itemData, out byte group, out short number))
                {
                    group = (byte)(itemData.Length >= 6 ? (itemData[5] >> 4) : 0);
                    number = itemData[0];
                }

                string name = ItemDatabase.GetItemName(group, number) ?? string.Empty;
                bool match = false;

                switch (type)
                {
                    case PotionType.Health:
                        match = (group == 14 && (number >= 0 && number <= 3)) ||
                                name.Contains("Healing", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Apple", StringComparison.OrdinalIgnoreCase);
                        break;
                    case PotionType.Mana:
                        match = (group == 14 && (number >= 4 && number <= 6)) ||
                                name.Contains("Mana", StringComparison.OrdinalIgnoreCase);
                        break;
                    case PotionType.AntidoteOrComplex:
                        match = (group == 14 && (number == 8 || (number >= 35 && number <= 40))) ||
                                name.Contains("Antidote", StringComparison.OrdinalIgnoreCase) ||
                                name.Contains("Complex", StringComparison.OrdinalIgnoreCase);
                        break;
                    case PotionType.SpecialOrPortal:
                        match = (group == 14 && (number == 7 || number == 9 || number == 10)) ||
                                name.Contains("Portal", StringComparison.OrdinalIgnoreCase);
                        break;
                }

                if (match)
                {
                    ItemDataParser.TryGetDurability(itemData, out byte dur);
                    count += Math.Max(1, (int)dur);
                }
            }

            return count;
        }

        public override void Draw(GameTime gameTime)
        {
            var sb = GraphicsManager.Instance.Sprite;

            foreach (var c in Controls.OrderBy(x => (x as ExtendedUIControl)?.RenderOrder ?? 0))
            {
                if (c is TextureControl)
                {
                    using (new SpriteBatchScope(
                           sb,
                           SpriteSortMode.Deferred,
                           BlendState.NonPremultiplied,
                           SamplerState.PointClamp))
                    {
                        c.Draw(gameTime);
                    }
                }
                else
                    c.Draw(gameTime);
            }

            _hp.DrawLabel(gameTime);
            _mp.DrawLabel(gameTime);

            DrawPotionBadges(sb);
        }

        private void DrawPotionBadges(SpriteBatch sb)
        {
            var font = GraphicsManager.Instance.Font;
            if (font == null) return;

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp))
            {
                var qwe = Controls.FirstOrDefault(c => c.Name == "QWE");
                if (qwe != null)
                {
                    float slotW = qwe.DisplayRectangle.Width / 3.0f;
                    int countQ = CountPotions(PotionType.Health);
                    int countW = CountPotions(PotionType.Mana);
                    int countE = CountPotions(PotionType.AntidoteOrComplex);

                    DrawBadge(sb, font, (int)(qwe.DisplayRectangle.X + slotW * 0.70f), qwe.DisplayRectangle.Bottom - 18, countQ, Color.LimeGreen);
                    DrawBadge(sb, font, (int)(qwe.DisplayRectangle.X + slotW * 1.70f), qwe.DisplayRectangle.Bottom - 18, countW, Color.DeepSkyBlue);
                    DrawBadge(sb, font, (int)(qwe.DisplayRectangle.X + slotW * 2.70f), qwe.DisplayRectangle.Bottom - 18, countE, Color.Gold);
                }

                var rs = Controls.FirstOrDefault(c => c.Name == "RS");
                if (rs != null)
                {
                    float slotW = rs.DisplayRectangle.Width / 2.0f;
                    int countR = CountPotions(PotionType.SpecialOrPortal);
                    DrawBadge(sb, font, (int)(rs.DisplayRectangle.X + slotW * 0.65f), rs.DisplayRectangle.Bottom - 18, countR, Color.Orange);
                }
            }
        }

        private void DrawBadge(SpriteBatch sb, SpriteFont font, int x, int y, int count, Color color)
        {
            if (count <= 0) return;

            string text = count > 99 ? "99+" : count.ToString();
            var pos = new Vector2(x, y);

            // Draw shadow for readability
            sb.DrawString(font, text, pos + new Vector2(1, 1), Color.Black);
            sb.DrawString(font, text, pos, color);
        }
    }
}
