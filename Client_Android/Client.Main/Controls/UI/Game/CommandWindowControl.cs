using Client.Main.Controls.UI.Common;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Networking;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Client.Main.Controls.UI.Game
{
    public class CommandWindowControl : UIControl
    {
        public static CommandWindowControl Instance { get; private set; }

        private const int WINDOW_WIDTH = 200;
        private const int WINDOW_HEIGHT = 280;

        private readonly string[] _commands = new string[]
        {
            "1. Trade (Negociar)",
            "2. Buy (Comprar)",
            "3. Party (Grupo)",
            "4. Whisper (Sussurro)",
            "5. Guild (Convite)",
            "6. Duel (Duelo)"
        };

        private readonly Rectangle[] _btnRects = new Rectangle[6];
        private Rectangle _closeBtnRect;
        private int _selectedIndex = -1;

        public CommandWindowControl()
        {
            Instance = this;
            AutoViewSize = false;
            ViewSize = new Point(WINDOW_WIDTH, WINDOW_HEIGHT);
            Visible = false;
            Interactive = true;
            Status = GameControlStatus.Ready;
            UpdateLayout();
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                UpdateLayout();
                BringToFront();
            }
        }

        private void UpdateLayout()
        {
            int screenW = GraphicsDevice?.Viewport.Width ?? MuGame.Instance.Width;
            int screenH = GraphicsDevice?.Viewport.Height ?? MuGame.Instance.Height;

            // Centered on right side or center of screen
            X = Math.Max(10, screenW / 2 - WINDOW_WIDTH / 2);
            Y = Math.Max(10, screenH / 2 - WINDOW_HEIGHT / 2);

            int startY = 40;
            int itemH = 34;

            for (int i = 0; i < _commands.Length; i++)
            {
                _btnRects[i] = new Rectangle(12, startY + i * (itemH + 4), WINDOW_WIDTH - 24, itemH);
            }

            _closeBtnRect = new Rectangle(WINDOW_WIDTH - 30, 8, 22, 22);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!Visible) return;

            var mouse = MuGame.Instance.Mouse;
            if (mouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
            {
                Point localPt = new Point(mouse.X - X, mouse.Y - Y);

                if (_closeBtnRect.Contains(localPt))
                {
                    Visible = false;
                    SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");
                    return;
                }

                for (int i = 0; i < _btnRects.Length; i++)
                {
                    if (_btnRects[i].Contains(localPt))
                    {
                        ExecuteCommand(i);
                        return;
                    }
                }
            }
        }

        public void ExecuteCommand(int index)
        {
            _selectedIndex = index;
            SoundController.Instance.PlayBuffer("Sound/iButtonClick.wav");

            string cmdName = index switch
            {
                0 => "Trade",
                1 => "Buy",
                2 => "Party",
                3 => "Whisper",
                4 => "Guild",
                5 => "Duel",
                _ => "Comando"
            };

            OnScreenLogger.Log($"Comando selecionado: {cmdName}. Toque no jogador alvo.", LogLevel.Information);
            Visible = false;
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            var sb = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;
            var font = GraphicsManager.Instance.Font;

            if (sb == null || pixel == null) return;

            Rectangle screenRect = new Rectangle(X, Y, WINDOW_WIDTH, WINDOW_HEIGHT);

            using (new SpriteBatchScope(sb, blend: BlendState.NonPremultiplied))
            {
                // Background dark window
                sb.Draw(pixel, screenRect, new Color(15, 18, 25, 235));

                // Golden border
                Color borderColor = new Color(200, 160, 60, 255);
                sb.Draw(pixel, new Rectangle(X, Y, WINDOW_WIDTH, 2), borderColor);
                sb.Draw(pixel, new Rectangle(X, Y + WINDOW_HEIGHT - 2, WINDOW_WIDTH, 2), borderColor);
                sb.Draw(pixel, new Rectangle(X, Y, 2, WINDOW_HEIGHT), borderColor);
                sb.Draw(pixel, new Rectangle(X + WINDOW_WIDTH - 2, Y, 2, WINDOW_HEIGHT), borderColor);

                // Title banner
                sb.Draw(pixel, new Rectangle(X + 2, Y + 2, WINDOW_WIDTH - 4, 30), new Color(40, 45, 65, 255));
                if (font != null)
                {
                    string title = "COMANDOS (D)";
                    Vector2 titleSz = font.MeasureString(title);
                    Vector2 titlePos = new Vector2(X + (WINDOW_WIDTH - titleSz.X) * 0.5f, Y + 6);
                    sb.DrawString(font, title, titlePos + new Vector2(1, 1), Color.Black);
                    sb.DrawString(font, title, titlePos, Color.Gold);
                }

                // Close button [X]
                Rectangle closeScreen = new Rectangle(X + _closeBtnRect.X, Y + _closeBtnRect.Y, _closeBtnRect.Width, _closeBtnRect.Height);
                sb.Draw(pixel, closeScreen, new Color(180, 40, 40, 220));
                if (font != null)
                {
                    Vector2 xSz = font.MeasureString("X");
                    Vector2 xPos = new Vector2(closeScreen.X + (closeScreen.Width - xSz.X) * 0.5f, closeScreen.Y + 2);
                    sb.DrawString(font, "X", xPos, Color.White);
                }

                // Command Action Buttons
                for (int i = 0; i < _commands.Length; i++)
                {
                    Rectangle r = new Rectangle(X + _btnRects[i].X, Y + _btnRects[i].Y, _btnRects[i].Width, _btnRects[i].Height);
                    Color btnBg = (_selectedIndex == i) ? new Color(60, 120, 200, 230) : new Color(30, 36, 50, 220);
                    sb.Draw(pixel, r, btnBg);

                    // Button outline
                    sb.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, 1), Color.Gray);
                    sb.Draw(pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), Color.Gray);
                    sb.Draw(pixel, new Rectangle(r.X, r.Y, 1, r.Height), Color.Gray);
                    sb.Draw(pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), Color.Gray);

                    if (font != null)
                    {
                        string txt = _commands[i];
                        Vector2 sz = font.MeasureString(txt);
                        Vector2 txtPos = new Vector2(r.X + (r.Width - sz.X) * 0.5f, r.Y + (r.Height - sz.Y) * 0.5f);
                        sb.DrawString(font, txt, txtPos + new Vector2(1, 1), Color.Black);
                        sb.DrawString(font, txt, txtPos, Color.White);
                    }
                }
            }
        }
    }
}
