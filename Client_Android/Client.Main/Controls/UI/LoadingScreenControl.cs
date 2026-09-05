using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Game
{
    public class LoadingScreenControl : GameControl
    {
        private SpriteFont _font;
        private string _pendingMessage = "Loading…";
        private float _progress = 0f;
        private BasicEffect _basicEffect;

        private const int ProgressBarHeight = 18;
        private const int ProgressBarMargin = 40;
        private const int ProgressBarYOffset = 30;

        private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        private float _visibleDuration => (float)_stopwatch.Elapsed.TotalSeconds;
        public float AutoDismissTimeout { get; set; } = 0f;

        private Rectangle _dismissButtonRect;

        public string Message
        {
            get => _pendingMessage;
            set => _pendingMessage = value ?? "Loading…";
        }

        public float Progress
        {
            get => _progress;
            set => _progress = MathHelper.Clamp(value, 0f, 1f);
        }

        public void Dismiss()
        {
            if (!Visible) return;
            Visible = false;
            OnScreenLogger.Log(">>> LoadingScreen dispensado. Forcando entrada no mundo!", LogLevel.Warning);
            MuGame.ScheduleOnMainThread(() =>
            {
                if (Parent != null)
                {
                    Parent.Controls.Remove(this);
                    Dispose();
                }
            });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Visible)
            {
                // Check touch / click on Dismiss button
                var mouse = MuGame.Instance.Mouse;
                if (mouse.LeftButton == ButtonState.Pressed && _dismissButtonRect.Contains(mouse.Position))
                {
                    Dismiss();
                    return;
                }

                if (AutoDismissTimeout > 0f && _visibleDuration >= AutoDismissTimeout)
                {
                    OnScreenLogger.Log($"LoadingScreen timeout ({AutoDismissTimeout}s) atingido. Auto-dispensando...", LogLevel.Warning);
                    Dismiss();
                }
            }
        }

        public override async Task Load()
        {
            _font = GraphicsManager.Instance.Font;
            _basicEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };
            await base.Load();
        }

        private VertexPositionColor[] CreateRectangleVertices(Vector2 pos, Vector2 size, Color color)
        {
            return
            [
                new VertexPositionColor(new Vector3(pos.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X, pos.Y + size.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y + size.Y, 0), color)
            ];
        }

        private void DrawProgressBar()
        {
            if (_basicEffect == null || Progress <= 0f) return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            int barWidth = gd.Viewport.Width - (ProgressBarMargin * 2);
            int barX = ProgressBarMargin;
            int barY = gd.Viewport.Height - ProgressBarHeight - ProgressBarYOffset;

            var bgPos = new Vector2(barX, barY);
            var bgSize = new Vector2(barWidth, ProgressBarHeight);
            var progressFillSize = new Vector2(barWidth * Progress, ProgressBarHeight);

            var bgVertices = CreateRectangleVertices(bgPos, bgSize, Color.DarkSlateGray * 0.8f);
            var progressVertices = CreateRectangleVertices(bgPos, progressFillSize, Color.ForestGreen * 0.95f);

            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, bgVertices, 0, 2);
                if (Progress > 0)
                {
                    gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, progressVertices, 0, 2);
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || Status == GameControlStatus.Disposed) return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var spriteBatch = GraphicsManager.Instance.Sprite;
            var pixel = GraphicsManager.Instance.Pixel;

            // Failsafe auto-dismiss check during draw loop
            if (AutoDismissTimeout > 0f && _visibleDuration >= AutoDismissTimeout)
            {
                Dismiss();
                return;
            }

            // Define button rect in top right
            int btnWidth = 180;
            int btnHeight = 32;
            _dismissButtonRect = new Rectangle(gd.Viewport.Width - btnWidth - 16, 12, btnWidth, btnHeight);

            // Check touch in Draw loop as well (for guaranteed responsiveness)
            var mouse = MuGame.Instance.Mouse;
            if (mouse.LeftButton == ButtonState.Pressed && _dismissButtonRect.Contains(mouse.Position))
            {
                Dismiss();
                return;
            }

            using (new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone))
            {
                // Fullscreen dark background
                spriteBatch.Draw(pixel, new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height), Color.Black * 0.88f);

                if (_font != null)
                {
                    // 1. Top Title
                    string title = $"[IKARUS MU v1.23] CARREGANDO MUNDO ({_visibleDuration:F1}s)";
                    spriteBatch.DrawString(_font, title, new Vector2(20, 16), Color.Goldenrod);

                    // 2. Dismiss Button (Top Right)
                    spriteBatch.Draw(pixel, _dismissButtonRect, Color.DarkRed * 0.9f);
                    // Button border
                    spriteBatch.Draw(pixel, new Rectangle(_dismissButtonRect.X, _dismissButtonRect.Y, _dismissButtonRect.Width, 2), Color.Gold);
                    spriteBatch.Draw(pixel, new Rectangle(_dismissButtonRect.X, _dismissButtonRect.Bottom - 2, _dismissButtonRect.Width, 2), Color.Gold);
                    spriteBatch.Draw(pixel, new Rectangle(_dismissButtonRect.X, _dismissButtonRect.Y, 2, _dismissButtonRect.Height), Color.Gold);
                    spriteBatch.Draw(pixel, new Rectangle(_dismissButtonRect.Right - 2, _dismissButtonRect.Y, 2, _dismissButtonRect.Height), Color.Gold);

                    string btnText = "[ ENTRAR (X) ]";
                    Vector2 btnTextSize = _font.MeasureString(btnText);
                    Vector2 btnTextPos = new Vector2(
                        _dismissButtonRect.X + (_dismissButtonRect.Width - btnTextSize.X) * 0.5f,
                        _dismissButtonRect.Y + (_dismissButtonRect.Height - btnTextSize.Y) * 0.5f
                    );
                    spriteBatch.DrawString(_font, btnText, btnTextPos, Color.White);

                    // 3. On-Screen Log Box (Center)
                    int logBoxX = 20;
                    int logBoxY = 60;
                    int logBoxW = gd.Viewport.Width - 40;
                    int logBoxH = gd.Viewport.Height - 145;
                    spriteBatch.Draw(pixel, new Rectangle(logBoxX, logBoxY, logBoxW, logBoxH), Color.Black * 0.75f);
                    // Log box top header
                    spriteBatch.Draw(pixel, new Rectangle(logBoxX, logBoxY, logBoxW, 22), Color.DarkSlateGray * 0.9f);
                    spriteBatch.DrawString(_font, ">>> DIAGNOSTICO EM TEMPO REAL (TIRE PRINT SE TRAVAR AQUI) <<<", new Vector2(logBoxX + 10, logBoxY + 3), Color.Gold);

                    // Print log entries
                    var entries = OnScreenLogger.GetEntries();
                    int currentY = logBoxY + 28;
                    int lineHeight = 18;

                    for (int i = 0; i < entries.Length && currentY + lineHeight < logBoxY + logBoxH; i++)
                    {
                        var entry = entries[i];
                        Color msgColor = Color.White;
                        if (entry.Level >= LogLevel.Error)
                            msgColor = Color.Red;
                        else if (entry.Level == LogLevel.Warning)
                            msgColor = Color.Yellow;
                        else if (entry.Message.Contains("sucesso") || entry.Message.Contains("pronto") || entry.Message.Contains("OK") || entry.Message.Contains("concluido"))
                            msgColor = Color.LightGreen;

                        string lineText = $"[{entry.Timestamp:HH:mm:ss}] {entry.Message}";
                        spriteBatch.DrawString(_font, lineText, new Vector2(logBoxX + 10, currentY), msgColor);
                        currentY += lineHeight;
                    }

                    // 4. Loading Status Message & Progress Text (Bottom, above progress bar)
                    string statusText = $"{Message} ({(int)(Progress * 100)}%)";
                    Vector2 statusSize = _font.MeasureString(statusText);
                    Vector2 statusPos = new Vector2(
                        (gd.Viewport.Width - statusSize.X) * 0.5f,
                        gd.Viewport.Height - ProgressBarHeight - ProgressBarYOffset - statusSize.Y - 6
                    );
                    spriteBatch.DrawString(_font, statusText, statusPos + Vector2.One, Color.Black * 0.8f);
                    spriteBatch.DrawString(_font, statusText, statusPos, Color.Cyan);
                }
            }

            // 5. Draw Progress Bar
            DrawProgressBar();
        }
    }
}