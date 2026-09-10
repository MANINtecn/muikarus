using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Extensions.Logging;
using Client.Main.Controllers;
using Client.Main.Controls.UI.Common;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Helpers;

namespace Client.Main.Controls.UI.Game
{
    public class ShopItemEntry
    {
        public byte Slot { get; set; }
        public byte Group { get; set; }
        public short Number { get; set; }
        public string Name { get; set; }
        public byte Level { get; set; }
        public byte Durability { get; set; }
        public bool HasSkill { get; set; }
        public bool HasLuck { get; set; }
        public bool HasExcellent { get; set; }
        public int Price { get; set; }
        public byte[] RawData { get; set; }
        public string CategoryName { get; set; }
    }

    /// <summary>
    /// Modern touch-friendly NPC Merchant Shop control for mobile screens.
    /// Connects with OpenMU 0x30/0x31/0x32 shop packets and allows buying items with Zen.
    /// </summary>
    public class NpcShopControl : UIControl
    {
        private static NpcShopControl _instance;
        public static NpcShopControl Instance => _instance ??= new NpcShopControl();

        public const int WND_WIDTH = 540;
        public const int WND_HEIGHT = 430;
        public const int VISIBLE_ITEMS = 6;
        public const int ITEM_ROW_HEIGHT = 48;

        private readonly List<ShopItemEntry> _items = new();
        private int _selectedIndex = -1;
        private int _scrollOffset = 0;

        private ButtonControl _closeBtn;
        private ButtonControl _btnUp;
        private ButtonControl _btnDown;
        private ButtonControl _btnBuy;

        private CharacterState _characterState;
        private ButtonState _prevLeftState = ButtonState.Released;

        public NpcShopControl()
        {
            ControlSize = new Point(WND_WIDTH, WND_HEIGHT);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false;

            X = 50;
            Y = 40;

            // [X] Close button
            _closeBtn = new ButtonControl
            {
                Text = "X",
                FontSize = 14f,
                TextColor = Color.White,
                BackgroundColor = new Color(190, 35, 35, 240),
                HoverBackgroundColor = new Color(230, 50, 50, 255),
                PressedBackgroundColor = new Color(130, 20, 20, 255),
                X = WND_WIDTH - 44,
                Y = 8,
                ControlSize = new Point(34, 34),
                ViewSize = new Point(34, 34),
                Visible = true
            };
            _closeBtn.Click += (s, e) =>
            {
                Visible = false;
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            };
            Controls.Add(_closeBtn);

            // Up scroll button
            _btnUp = new ButtonControl
            {
                Text = "▲",
                FontSize = 13f,
                TextColor = Color.Gold,
                BackgroundColor = new Color(32, 42, 58, 230),
                HoverBackgroundColor = new Color(50, 70, 95, 255),
                PressedBackgroundColor = new Color(20, 28, 40, 255),
                X = 278,
                Y = 56,
                ControlSize = new Point(36, 32),
                ViewSize = new Point(36, 32),
                Visible = true
            };
            _btnUp.Click += (s, e) =>
            {
                if (_scrollOffset > 0)
                {
                    _scrollOffset--;
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            };
            Controls.Add(_btnUp);

            // Down scroll button
            _btnDown = new ButtonControl
            {
                Text = "▼",
                FontSize = 13f,
                TextColor = Color.Gold,
                BackgroundColor = new Color(32, 42, 58, 230),
                HoverBackgroundColor = new Color(50, 70, 95, 255),
                PressedBackgroundColor = new Color(20, 28, 40, 255),
                X = 278,
                Y = 56 + (VISIBLE_ITEMS - 1) * ITEM_ROW_HEIGHT + 14,
                ControlSize = new Point(36, 32),
                ViewSize = new Point(36, 32),
                Visible = true
            };
            _btnDown.Click += (s, e) =>
            {
                if (_scrollOffset + VISIBLE_ITEMS < _items.Count)
                {
                    _scrollOffset++;
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                }
            };
            Controls.Add(_btnDown);

            // Big Buy Button
            _btnBuy = new ButtonControl
            {
                Text = "COMPRAR",
                FontSize = 14f,
                TextColor = Color.White,
                BackgroundColor = new Color(36, 145, 65, 245),
                HoverBackgroundColor = new Color(48, 185, 82, 255),
                PressedBackgroundColor = new Color(22, 95, 42, 255),
                X = 335,
                Y = WND_HEIGHT - 65,
                ControlSize = new Point(185, 46),
                ViewSize = new Point(185, 46),
                Visible = true
            };
            _btnBuy.Click += (s, e) =>
            {
                ExecuteBuy();
            };
            Controls.Add(_btnBuy);

            EnsureCharacterState();
        }

        private void EnsureCharacterState()
        {
            if (_characterState == null)
            {
                _characterState = MuGame.Network?.GetCharacterState();
                if (_characterState != null)
                {
                    _characterState.ShopItemsChanged += OnShopItemsChanged;
                    RefreshShopItems();
                }
            }
        }

        private void OnShopItemsChanged()
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                RefreshShopItems();
            });
        }

        public void RefreshShopItems()
        {
            if (_characterState == null) return;

            _items.Clear();
            var rawItems = _characterState.GetShopItems();

            foreach (var kvp in rawItems)
            {
                byte slot = kvp.Key;
                byte[] data = kvp.Value;
                if (data == null || data.Length == 0) continue;

                byte group = 0;
                short number = 0;
                byte level = 0;
                byte durability = 255;
                bool hasSkill = false;
                bool hasLuck = false;
                bool hasExcellent = false;

                if (ItemDataParser.TryParseExtendedItemData(data, out var ext))
                {
                    group = ext.Group;
                    number = ext.Number;
                    level = ext.Level;
                    durability = ext.Durability;
                    hasSkill = ext.HasSkill;
                    hasLuck = ext.HasLuck;
                    hasExcellent = ext.HasExcellent;
                }
                else if (ItemDataParser.TryGetGroupAndNumber(data, out group, out number))
                {
                    ItemDataParser.TryGetDurability(data, out durability);
                    if (data.Length > 2) level = (byte)((data[1] >> 3) & 0x0F);
                }

                string name = ItemDatabase.GetItemName(group, number);
                if (string.IsNullOrEmpty(name))
                    name = $"Item [{group}-{number}]";

                int price = EstimateItemPrice(group, number, level, hasExcellent);

                _items.Add(new ShopItemEntry
                {
                    Slot = slot,
                    Group = group,
                    Number = number,
                    Name = name,
                    Level = level,
                    Durability = durability,
                    HasSkill = hasSkill,
                    HasLuck = hasLuck,
                    HasExcellent = hasExcellent,
                    Price = price,
                    RawData = data,
                    CategoryName = GetCategoryName(group)
                });
            }

            _items.Sort((a, b) => a.Slot.CompareTo(b.Slot));

            if (_selectedIndex >= _items.Count)
                _selectedIndex = _items.Count > 0 ? 0 : -1;
            else if (_selectedIndex < 0 && _items.Count > 0)
                _selectedIndex = 0;

            _scrollOffset = 0;
        }

        private static string GetCategoryName(byte group)
        {
            return group switch
            {
                0 => "Espada",
                1 => "Machado",
                2 => "Massa",
                3 => "Lança",
                4 => "Arco / Besta",
                5 => "Cajado",
                6 => "Escudo",
                7 => "Capacete",
                8 => "Armadura",
                9 => "Calças",
                10 => "Luvas",
                11 => "Botas",
                12 => "Asas / Orbes",
                13 => "Pet / Acessório",
                14 => "Poção / Pergaminho",
                15 => "Scroll / Magia",
                _ => "Geral"
            };
        }

        private static int EstimateItemPrice(byte group, short number, byte level, bool isExcellent)
        {
            if (group == 14) // Potions & Scrolls
            {
                return number switch
                {
                    0 => 100,      // Apple
                    1 => 160,      // Small Healing Potion
                    2 => 640,      // Medium Healing Potion
                    3 => 2000,     // Large Healing Potion
                    4 => 320,      // Small Mana Potion
                    5 => 1200,     // Medium Mana Potion
                    6 => 3800,     // Large Mana Potion
                    8 => 600,      // Antidote
                    9 => 400,      // Ale
                    10 => 2000,    // Town Portal Scroll
                    _ => 1000
                };
            }
            if (group == 4 && (number == 7 || number == 15)) // Arrows / Bolts
            {
                return 350;
            }

            int basePrice = 1200 + (group * 1800) + (number * 1400) + (level * 2200);
            if (isExcellent) basePrice *= 3;
            return Math.Max(200, basePrice);
        }

        private void ExecuteBuy()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
            {
                OnScreenLogger.Log("[LOJA] Toque em um item da lista para selecionar.", LogLevel.Warning, "LOJA");
                return;
            }

            var item = _items[_selectedIndex];
            uint playerZen = _characterState?.InventoryZen ?? 0;
            if (playerZen < item.Price)
            {
                OnScreenLogger.Log($"[LOJA] Zen insuficiente! Custa {item.Price:N0} Zen (Você tem {playerZen:N0}).", LogLevel.Warning, "LOJA");
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                return;
            }

            var charService = MuGame.Network?.GetCharacterService();
            if (charService != null)
            {
                _ = charService.SendBuyItemFromNpcRequestAsync(item.Slot);
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                OnScreenLogger.Log($"[LOJA] Comprando {item.Name} (Slot {item.Slot})...", LogLevel.Information, "LOJA");
            }
            else
            {
                OnScreenLogger.Log("[LOJA] Não conectado ao servidor.", LogLevel.Error, "LOJA");
            }
        }

        public override void Update(GameTime gameTime)
        {
            EnsureCharacterState();

            if (!Visible)
            {
                _prevLeftState = Mouse.GetState().LeftButton;
                base.Update(gameTime);
                return;
            }

            // Reposition window comfortably centered on mobile screen
            int sw = MuGame.Instance.Width;
            int sh = MuGame.Instance.Height;
            X = Math.Max(10, (sw - WND_WIDTH) / 2);
            Y = Math.Max(10, (sh - WND_HEIGHT) / 2);

            KeyboardState keyState = Keyboard.GetState();
            if (keyState.IsKeyDown(Keys.Escape))
            {
                Visible = false;
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
            }

            var mouse = Mouse.GetState();
            Point mousePos = mouse.Position;
            bool isInsideWindow = DisplayRectangle.Contains(mousePos);

            if (isInsideWindow)
            {
                IsMouseOver = true;
                Scene?.SetMouseInputConsumed();

                // Handle row clicks
                bool leftJustPressed = mouse.LeftButton == ButtonState.Pressed && _prevLeftState == ButtonState.Released;
                if (leftJustPressed)
                {
                    int startX = DisplayRectangle.X + 15;
                    int startY = DisplayRectangle.Y + 52;
                    int rowW = 258;

                    for (int i = 0; i < VISIBLE_ITEMS; i++)
                    {
                        int itemIdx = _scrollOffset + i;
                        if (itemIdx >= _items.Count) break;

                        var rowRect = new Rectangle(startX, startY + (i * ITEM_ROW_HEIGHT), rowW, ITEM_ROW_HEIGHT - 4);
                        if (rowRect.Contains(mousePos))
                        {
                            _selectedIndex = itemIdx;
                            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                            break;
                        }
                    }
                }
            }
            else
            {
                IsMouseOver = false;
            }

            _prevLeftState = mouse.LeftButton;

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var gm = GraphicsManager.Instance;
            var spriteBatch = gm?.Sprite;
            var pixel = gm?.Pixel;
            var font = gm?.Font;

            if (spriteBatch == null || pixel == null)
            {
                base.Draw(gameTime);
                return;
            }

            using (new SpriteBatchScope(spriteBatch, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                Rectangle bounds = DisplayRectangle;

                // 1. Background window frame (Dark fantasy theme)
                spriteBatch.Draw(pixel, bounds, new Color(12, 16, 24, 250));

                // Golden outer border
                DrawBorder(spriteBatch, pixel, bounds, new Color(212, 175, 85), 2);

                // 2. Header Area
                var headerRect = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, 42);
                spriteBatch.Draw(pixel, headerRect, new Color(24, 32, 46, 255));
                spriteBatch.Draw(pixel, new Rectangle(bounds.X + 10, bounds.Y + 43, bounds.Width - 20, 1), new Color(212, 175, 85, 180));

                if (font != null)
                {
                    // Header title
                    string title = "LOJA DO COMERCIANTE";
                    spriteBatch.DrawString(font, title, new Vector2(bounds.X + 18, bounds.Y + 12), Color.Gold, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

                    // Player Zen
                    uint zen = _characterState?.InventoryZen ?? 0;
                    string zenText = $"Seu Zen: {zen:N0}";
                    spriteBatch.DrawString(font, zenText, new Vector2(bounds.X + 260, bounds.Y + 14), Color.LightYellow, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
                }

                // 3. Left Pane: Items list
                var listFrame = new Rectangle(bounds.X + 12, bounds.Y + 48, 305, bounds.Height - 58);
                spriteBatch.Draw(pixel, listFrame, new Color(16, 21, 30, 230));
                DrawBorder(spriteBatch, pixel, listFrame, new Color(55, 68, 90), 1);

                if (_items.Count == 0)
                {
                    if (font != null)
                    {
                        string emptyMsg = "Aguardando itens do comerciante...";
                        spriteBatch.DrawString(font, emptyMsg, new Vector2(listFrame.X + 15, listFrame.Y + 100), Color.Gray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
                    }
                }
                else
                {
                    int startX = listFrame.X + 5;
                    int startY = listFrame.Y + 5;
                    int rowW = 256;

                    for (int i = 0; i < VISIBLE_ITEMS; i++)
                    {
                        int itemIdx = _scrollOffset + i;
                        if (itemIdx >= _items.Count) break;

                        var item = _items[itemIdx];
                        bool isSelected = itemIdx == _selectedIndex;

                        var rowRect = new Rectangle(startX, startY + (i * ITEM_ROW_HEIGHT), rowW, ITEM_ROW_HEIGHT - 4);
                        Color rowBg = isSelected ? new Color(55, 75, 110, 240) : new Color(22, 28, 40, 210);
                        spriteBatch.Draw(pixel, rowRect, rowBg);

                        Color rowBorder = isSelected ? Color.Gold : new Color(42, 52, 70);
                        DrawBorder(spriteBatch, pixel, rowRect, rowBorder, isSelected ? 2 : 1);

                        if (font != null)
                        {
                            // Slot tag
                            string slotTag = $"[#{item.Slot}]";
                            spriteBatch.DrawString(font, slotTag, new Vector2(rowRect.X + 6, rowRect.Y + 6), Color.DarkGray, 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);

                            // Item Name
                            Color nameColor = item.HasExcellent ? Color.LightGreen : (item.Level > 0 ? Color.Gold : (item.HasSkill ? Color.SkyBlue : Color.White));
                            string displayName = item.Name;
                            if (displayName.Length > 22) displayName = displayName.Substring(0, 20) + "...";
                            if (item.Level > 0) displayName += $" +{item.Level}";

                            spriteBatch.DrawString(font, displayName, new Vector2(rowRect.X + 46, rowRect.Y + 6), nameColor, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

                            // Category & Price
                            string subText = $"{item.CategoryName} • {item.Price:N0} Zen";
                            spriteBatch.DrawString(font, subText, new Vector2(rowRect.X + 46, rowRect.Y + 25), Color.Khaki, 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
                        }
                    }
                }

                // 4. Right Pane: Selected Item Details & Action
                var detailFrame = new Rectangle(bounds.X + 322, bounds.Y + 48, bounds.Width - 334, bounds.Height - 58);
                spriteBatch.Draw(pixel, detailFrame, new Color(16, 21, 30, 230));
                DrawBorder(spriteBatch, pixel, detailFrame, new Color(55, 68, 90), 1);

                if (font != null)
                {
                    string detailsTitle = "DETALHES DO ITEM";
                    spriteBatch.DrawString(font, detailsTitle, new Vector2(detailFrame.X + 15, detailFrame.Y + 12), Color.Gold, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(pixel, new Rectangle(detailFrame.X + 12, detailFrame.Y + 34, detailFrame.Width - 24, 1), new Color(212, 175, 85, 120));

                    if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                    {
                        var sel = _items[_selectedIndex];

                        Color titleCol = sel.HasExcellent ? Color.LightGreen : (sel.Level > 0 ? Color.Gold : Color.White);
                        string titleStr = sel.Name;
                        if (sel.Level > 0) titleStr += $" +{sel.Level}";
                        spriteBatch.DrawString(font, titleStr, new Vector2(detailFrame.X + 15, detailFrame.Y + 45), titleCol, 0f, Vector2.Zero, 0.50f, SpriteEffects.None, 0f);

                        int infoY = detailFrame.Y + 75;
                        spriteBatch.DrawString(font, $"Categoria: {sel.CategoryName}", new Vector2(detailFrame.X + 15, infoY), Color.LightGray, 0f, Vector2.Zero, 0.40f, SpriteEffects.None, 0f);
                        infoY += 22;

                        if (sel.Durability < 255 && sel.Durability > 0)
                        {
                            spriteBatch.DrawString(font, $"Durabilidade: {sel.Durability}", new Vector2(detailFrame.X + 15, infoY), Color.LightGray, 0f, Vector2.Zero, 0.40f, SpriteEffects.None, 0f);
                            infoY += 22;
                        }

                        if (sel.HasSkill)
                        {
                            spriteBatch.DrawString(font, "• Habilidade Especial", new Vector2(detailFrame.X + 15, infoY), Color.SkyBlue, 0f, Vector2.Zero, 0.40f, SpriteEffects.None, 0f);
                            infoY += 22;
                        }

                        if (sel.HasLuck)
                        {
                            spriteBatch.DrawString(font, "• Sorte (+25% Taxa Crítica)", new Vector2(detailFrame.X + 15, infoY), Color.SkyBlue, 0f, Vector2.Zero, 0.40f, SpriteEffects.None, 0f);
                            infoY += 22;
                        }

                        if (sel.HasExcellent)
                        {
                            spriteBatch.DrawString(font, "• Item Excelente!", new Vector2(detailFrame.X + 15, infoY), Color.LightGreen, 0f, Vector2.Zero, 0.40f, SpriteEffects.None, 0f);
                            infoY += 22;
                        }

                        // Price
                        infoY += 8;
                        spriteBatch.Draw(pixel, new Rectangle(detailFrame.X + 12, infoY, detailFrame.Width - 24, 1), new Color(70, 85, 110));
                        infoY += 10;

                        spriteBatch.DrawString(font, "Preço de Compra:", new Vector2(detailFrame.X + 15, infoY), Color.DarkGray, 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);
                        infoY += 18;
                        spriteBatch.DrawString(font, $"{sel.Price:N0} Zen", new Vector2(detailFrame.X + 15, infoY), Color.Gold, 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);
                    }
                    else
                    {
                        spriteBatch.DrawString(font, "Selecione um item da lista\npara ver os detalhes e\ncomprar com Zen.", new Vector2(detailFrame.X + 15, detailFrame.Y + 60), Color.DarkGray, 0f, Vector2.Zero, 0.40f, SpriteEffects.None, 0f);
                    }
                }
            }

            base.Draw(gameTime);
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_characterState != null)
            {
                _characterState.ShopItemsChanged -= OnShopItemsChanged;
            }
        }
    }
}