using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Scenes
{
    public class LoadScene : BaseScene
    {
        #region ── stałe & pola ──────────────────────────────────────────────

        private const int BufferSize = 1 * 1024 * 1024;              // 1 MB
        private static readonly TimeSpan ProgressTick = TimeSpan.FromMilliseconds(200);

        private static readonly HttpClient Http;

        private LabelControl _statusLabel;
        private float _progress;        // 0-1
        private string _statusText;

        private Texture2D _backgroundTexture;
        private BasicEffect _basicEffect;

        private const int ProgressBarHeight = 30;
        private const int ProgressBarY = 700;
        private readonly string _dataPathUrl = Constants.DataPathUrl;

        #endregion

        #region ── HttpClient ─────────────────────

        static LoadScene()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                EnableMultipleHttp2Connections = true,
                SslOptions =
                {
                    RemoteCertificateValidationCallback = (_,__,___,____) => true // DEV-only
                }
            };

            Http = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan,
                DefaultRequestVersion = HttpVersion.Version30,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            Http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("MuClient", "1.0"));
        }

        #endregion

        #region

        public LoadScene()
        {
            _progress = 0f;
            _statusText = "Initializing...";

            _statusLabel = new LabelControl
            {
                Text = _statusText,
                X = 50, // Margin from left
                Y = MuGame.Instance.Height - 80, // Position above progress bar
                FontSize = 20, // Slightly smaller for more text
                TextColor = Color.White,
                ShadowColor = Color.Black * 0.7f,
                HasShadow = true,
                ShadowOffset = new Vector2(1, 1)
            };
            Controls.Add(_statusLabel);
        }

        #endregion

        #region

        public override async Task Load()
        {
            await base.Load();
            _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");

            _basicEffect = new BasicEffect(MuGame.Instance.GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter
                             (0, MuGame.Instance.Width, MuGame.Instance.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };

            var loadWorld = new LoadWorld();
            Controls.Add(loadWorld);
            await loadWorld.Initialize();
            World = loadWorld;
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _ = PerformInitialLoadAndTransitionAsync();
        }

        #endregion

        #region Core Loading Orchestration

        private void NormalizeDirectory(string dirPath, Action<string, float> report = null)
        {
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return;

            try
            {
                // 1. Move any files sitting inside a subfolder named "Data" up to dirPath
                string nestedData = Path.Combine(dirPath, "Data");
                if (Directory.Exists(nestedData))
                {
                    report?.Invoke("Organizando pasta Data...", 0.1f);
                    foreach (var file in Directory.GetFiles(nestedData, "*", SearchOption.AllDirectories))
                    {
                        string rel = Path.GetRelativePath(nestedData, file).Replace('\\', '/');
                        string dest = Path.Combine(dirPath, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(file, dest);
                    }
                    try { Directory.Delete(nestedData, true); } catch { /* ignore */ }
                }

                // 2. Scan all files in dirPath for backslashes in filenames (from Windows zips on Linux/Android)
                var allFiles = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
                var backslashedFiles = allFiles.Where(f => Path.GetFileName(f).Contains('\\')).ToList();

                if (backslashedFiles.Count > 0)
                {
                    report?.Invoke($"Reorganizando {backslashedFiles.Count} arquivos existentes...", 0.2f);
                    int count = backslashedFiles.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var file = backslashedFiles[i];
                        string fileName = Path.GetFileName(file);
                        string cleanRel = fileName.Replace('\\', '/');
                        if (cleanRel.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
                            cleanRel = cleanRel.Substring(5);

                        string dest = Path.Combine(dirPath, cleanRel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(file, dest);

                        if (i % 200 == 0)
                        {
                            float pr = (float)i / count;
                            report?.Invoke($"Reorganizando dados ({i}/{count})...", pr);
                        }
                    }
                    report?.Invoke("Arquivos reorganizados com sucesso!", 1f);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NormalizeDirectory] Error: {ex.Message}");
            }
        }

        private static bool CheckAssetsComplete(string dataPath)
        {
            if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath))
                return false;

            string checkFileWorld95 = Path.Combine(dataPath, "World95", "EncTerrain95.att");
            string checkFileWorld1 = Path.Combine(dataPath, "World1", "EncTerrain1.att");

            return (File.Exists(checkFileWorld95) || !string.IsNullOrEmpty(Utils.GetActualPath(checkFileWorld95))) &&
                   (File.Exists(checkFileWorld1) || !string.IsNullOrEmpty(Utils.GetActualPath(checkFileWorld1)));
        }

        private async Task PerformInitialLoadAndTransitionAsync()
        {
            string localZip = Path.Combine(Constants.DataPath, "Data.zip");
            string extractPath = Constants.DataPath;
            string url = _dataPathUrl;

            // Normalize existing files across possible Android directories
#if ANDROID
            try
            {
                string externalData = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath, "Data");
                NormalizeDirectory(externalData, UpdateStatus);
            }
            catch { }
            try
            {
                string internalData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                NormalizeDirectory(internalData, UpdateStatus);
            }
            catch { }
#endif
            NormalizeDirectory(Constants.DataPath, UpdateStatus);
            Utils.ClearPathCache();

            bool alreadyHaveAssets = CheckAssetsComplete(Constants.DataPath);

#if ANDROID
            if (!alreadyHaveAssets)
            {
                string internalData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                string externalData = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath, "Data");
                if (CheckAssetsComplete(internalData))
                {
                    Constants.DataPath = internalData;
                    alreadyHaveAssets = true;
                }
                else if (CheckAssetsComplete(externalData))
                {
                    Constants.DataPath = externalData;
                    alreadyHaveAssets = true;
                }
            }
#endif

            if (alreadyHaveAssets)
            {
                UpdateStatus("Arquivos identificados! Pulando download.", 1);
                await Task.Delay(500);
            }
            else
            {
                // Check if Data.zip was already downloaded on the device
                if (File.Exists(localZip) && new FileInfo(localZip).Length > 500_000_000)
                {
                    UpdateStatus("Data.zip encontrado no aparelho! Extraindo...", 0);
                    try
                    {
                        await ExtractZipFileWithProgressAsync(localZip, extractPath, UpdateStatus);
                        NormalizeDirectory(Constants.DataPath, UpdateStatus);
                        Utils.ClearPathCache();
                        alreadyHaveAssets = CheckAssetsComplete(Constants.DataPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao extrair zip local: {ex.Message}");
                    }
                }

                if (!alreadyHaveAssets)
                {
                    string tempZip = localZip + ".tmp";
                    try
                    {
                        UpdateStatus("Baixando dados do jogo (1.7 GB)...", 0);
                        await DownloadFileWithProgressAsync(url, tempZip, UpdateStatus);
                        if (File.Exists(localZip)) File.Delete(localZip);
                        File.Move(tempZip, localZip);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Primary URL failed: {ex.Message}");
                        url = Constants.DefaultDataPathUrl;
                        UpdateStatus("Tentando URL alternativa...", 0);
                        await DownloadFileWithProgressAsync(url, tempZip, UpdateStatus);
                        if (File.Exists(localZip)) File.Delete(localZip);
                        File.Move(tempZip, localZip);
                    }

                    UpdateStatus("Extraindo arquivos...", 0);
                    await ExtractZipFileWithProgressAsync(localZip, extractPath, UpdateStatus);
                    NormalizeDirectory(Constants.DataPath, UpdateStatus);
                    Utils.ClearPathCache();
                }

                UpdateStatus("Limpando temporários…", 1);
                if (File.Exists(localZip)) File.Delete(localZip);
            }

            await TransitionToEntrySceneAsync();
        }

        private async Task TransitionToEntrySceneAsync()
        {
            Type nextSceneType = Constants.ENTRY_SCENE == typeof(LoadScene)
                                   ? typeof(LoginScene)
                                   : Constants.ENTRY_SCENE;

            UpdateStatus($"Loading {nextSceneType.Name}…", 0);

            var nextScene = (BaseScene)Activator.CreateInstance(nextSceneType)!;
            await nextScene.InitializeWithProgressReporting(UpdateStatus);

            UpdateStatus("Transitioning…", 1);
            await Task.Delay(300);
            MuGame.Instance.ChangeScene(nextScene);
        }

        #endregion

        #region ── DownloadFileWithProgressAsync ─────────────────────────────

        private async Task DownloadFileWithProgressAsync(
            string url, string destination,
            Action<string, float> report,
            CancellationToken ct = default)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                using var resp = await Http.GetAsync(url,
                               HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                long total = resp.Content.Headers.ContentLength ?? -1;
                long done = 0;
                var sw = Stopwatch.StartNew();

                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(destination, FileMode.Create,
                                    FileAccess.Write, FileShare.None,
                                    BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                while (true)
                {
                    int n = await src.ReadAsync(buffer.AsMemory(0, BufferSize), ct);
                    if (n == 0)
                    {
                        if (total > 0 && done < total)
                            throw new Exception($"Download incompleto! Baixou {done / 1_048_576:F1} de {total / 1_048_576:F1} MB.");
                        
                        if (done < 10_000_000) // Less than 10MB
                            throw new Exception($"Arquivo falso ({done / 1_048_576:F1} MB). O seu Repositório do GitHub está PRIVADO! Deixe ele Público.");
                        
                        break;
                    }

                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    done += n;

                    if (sw.Elapsed >= ProgressTick)
                    {
                        sw.Restart();
                        float pr = total > 0 ? (float)done / total : 0;
                        report?.Invoke($"Downloading... {pr * 100:F0}% ({done / 1_048_576:F1} MB)", pr);
                    }
                }
                await dst.FlushAsync(ct);
                report?.Invoke("Download complete.", 1);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        #endregion

        #region ── ExtractZipFileWithProgressAsync ───────────────────────────

        private async Task ExtractZipFileWithProgressAsync(
            string zip, string outDir,
            Action<string, float> report,
            CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var archive = ZipFile.OpenRead(zip);
                    var files = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
                    int done = 0; var sw = Stopwatch.StartNew();

                    foreach (var entry in files)
                    {
                        ct.ThrowIfCancellationRequested();

                        string rel = entry.FullName.Replace('\\', '/');
                        if (rel.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
                            rel = rel.Substring(5);

                        string full = Path.Combine(outDir, rel);

                        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                        entry.ExtractToFile(full, true);

                        done++;
                        if (sw.Elapsed >= ProgressTick)
                        {
                            sw.Restart();
                            float pr = (float)done / files.Length;
                            report?.Invoke($"Otimizando texturas... {pr * 100:F0}%", pr);
                        }
                    }
                    report?.Invoke("Otimização concluída.", 1);
                }
                catch (Exception ex)
                {
                    report?.Invoke($"Erro ao extrair: {ex.Message}", 0);
                    throw; // rethrow to stop process
                }
            }, ct);
        }

        #endregion

        #region ── UpdateStatus ──────────────────────

        private void UpdateStatus(string text, float progress)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _statusText = text;
                _progress = MathHelper.Clamp(progress, 0, 1);
                _statusLabel.Text = _statusText;
            });
        }

        public override void Update(GameTime gameTime)
        {
            if (Status == GameControlStatus.NonInitialized)
                _ = Initialize();

            base.Update(gameTime);
        }

        private VertexPositionColor[] CreateRect(Vector2 pos, Vector2 size, Color col) =>
        [
            new(new Vector3(pos.X,           pos.Y,            0), col),
            new(new Vector3(pos.X+size.X,    pos.Y,            0), col),
            new(new Vector3(pos.X,           pos.Y+size.Y,     0), col),
            new(new Vector3(pos.X+size.X,    pos.Y+size.Y,     0), col)
        ];

        public override void Draw(GameTime gameTime)
        {
            if (_basicEffect == null || _backgroundTexture == null)
            {
                GraphicsDevice.Clear(Color.Black);
                if (_statusLabel.Status == GameControlStatus.Ready)
                {
                    using (new SpriteBatchScope(GraphicsManager.Instance.Sprite))
                        _statusLabel.Draw(gameTime);
                }
                return;
            }

            DrawSceneBackground();
            DrawProgressBar();

            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite,
                                        SpriteSortMode.Deferred,
                                        BlendState.AlphaBlend,
                                        SamplerState.PointClamp,
                                        DepthStencilState.None))
            {
                if (_statusLabel.Visible) _statusLabel.Draw(gameTime);
            }
        }

        private void DrawSceneBackground()
        {
            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite))
            {
                GraphicsManager.Instance.Sprite.Draw(
                    _backgroundTexture,
                    new Rectangle(0, 0, MuGame.Instance.Width, MuGame.Instance.Height),
                    Color.White);
            }
        }

        private void DrawProgressBar()
        {
            int w = MuGame.Instance.Width - 100;
            int x = 50;

            var bg = CreateRect(new Vector2(x, ProgressBarY),
                                     new Vector2(w, ProgressBarHeight),
                                     Color.DarkSlateGray);
            var prog = CreateRect(new Vector2(x, ProgressBarY),
                                     new Vector2(w * _progress, ProgressBarHeight),
                                     Color.ForestGreen);

            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bg, 0, 2);
                if (_progress > 0)
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, prog, 0, 2);
            }
        }
        #endregion
    }
}