using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Data;
using Client.Data.Texture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Extensions.Logging;

namespace Client.Main.Content
{
    public class TextureLoader
    {
        public static TextureLoader Instance { get; } = new TextureLoader();

        public Func<TextureData, byte[]> CustomDecompressFunction = null;

        private readonly ConcurrentDictionary<string, Task<TextureData>> _textureTasks = new();
        private readonly ConcurrentDictionary<string, ClientTexture> _textures = new();
        private GraphicsDevice _graphicsDevice;

        private readonly Dictionary<string, BaseReader<TextureData>> _readers = new()
        {
            { ".ozt", new OZTReader() },
            { ".tga", new OZTReader() },
            { ".ozj", new OZJReader() },
            { ".jpg", new OZJReader() },
            { ".ozp", new OZPReader() },
            { ".png", new OZPReader() },
            { ".ozd", new OZDReader() },
            { ".dds", new OZDReader() }
        };

        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<TextureLoader>();

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice) => _graphicsDevice = graphicsDevice;

        public Task<TextureData> Prepare(string path)
        { 
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

            string normalizedKey = NormalizePath(path);

            if (_textureTasks.TryGetValue(normalizedKey, out var task))
                return task;

            task = InternalPrepare(path);
            _textureTasks.TryAdd(normalizedKey, task);
            return task;
        }

        public async Task<Texture2D> PrepareAndGetTexture(string path)
        {
            await Prepare(path);
            return GetTexture2D(path);
        }

        private static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/').ToLowerInvariant() ?? string.Empty;
        }

        private async Task<TextureData> InternalPrepare(string path)
        {
            try
            {
                string dataPath = Path.IsPathRooted(path) ? path : Path.Combine(Constants.DataPath, path);
                string ext = Path.GetExtension(path)?.ToLowerInvariant();

                if (string.IsNullOrEmpty(ext) || !_readers.TryGetValue(ext, out var reader))
                {
                    _logger?.LogDebug($"Unsupported file extension: {ext}");
                    return null;
                }

                string fullPath = FindTexturePath(dataPath, ext);
                if (fullPath == null) return null;

                var data = await reader.Load(fullPath);
                if (data == null)
                {
                    _logger?.LogDebug($"Failed to load texture data from: {fullPath}");
                    return null;
                }

                var clientTexture = new ClientTexture
                {
                    Info = data,
                    Script = ParseScript(path)
                };

                _textures.TryAdd(NormalizePath(path), clientTexture);
                return clientTexture.Info;
            }
            catch (Exception e)
            {
                _logger?.LogDebug($"Failed to load asset {path}: {e.Message}");
                return null;
            }
        }

        private string FindTexturePath(string dataPath, string ext)
        {
            if (!_readers.TryGetValue(ext, out var reader)) return null;

            string expectedExtension = reader.GetType().Name.ToLowerInvariant().Replace("reader", "");
            string expectedFilePath = Path.ChangeExtension(dataPath, expectedExtension);

            string actualPath = Utils.GetActualPath(expectedFilePath);
            if (actualPath != null && File.Exists(actualPath))
                return actualPath;

            actualPath = Utils.GetActualPath(dataPath);
            if (actualPath != null && File.Exists(actualPath))
                return actualPath;

            string parentFolder = Path.GetDirectoryName(expectedFilePath);
            if (!string.IsNullOrEmpty(parentFolder))
            {
                string newFullPath = Path.Combine(parentFolder, "texture", Path.GetFileName(expectedFilePath));
                actualPath = Utils.GetActualPath(newFullPath);
                if (actualPath != null && File.Exists(actualPath))
                    return actualPath;
            }

            _logger?.LogDebug($"Texture file not found: {expectedFilePath}");
            return null;
        }

        private static TextureScript ParseScript(string fileName)
        {
            if (fileName.Contains("mu_rgb_lights.jpg", StringComparison.OrdinalIgnoreCase))
                return new TextureScript { Bright = true };

            var tokens = Path.GetFileNameWithoutExtension(fileName).Split('_');

            if (tokens.Length > 1)
            {
                var script = new TextureScript();
                var token = tokens[^1].ToLowerInvariant();

                switch (token)
                {
                    case "a": script.Alpha = true; break;
                    case "r": script.Bright = true; break;
                    case "h": script.HiddenMesh = true; break;
                    case "s": script.StreamMesh = true; break;
                    case "n": script.NoneBlendMesh = true; break;
                    case "dc": script.ShadowMesh = 1; break; // NoneTexture
                    case "dt": script.ShadowMesh = 2; break; // Texture
                    default: return null;
                }

                return script;
            }

            return null;
        }

        public TextureData Get(string path) =>
            string.IsNullOrWhiteSpace(path) ? null :
            _textures.TryGetValue(NormalizePath(path), out var value) ? value.Info : null;

        public TextureScript GetScript(string path) =>
            string.IsNullOrWhiteSpace(path) ? null :
            _textures.TryGetValue(NormalizePath(path), out var value) ? value.Script : null;

        public Texture2D GetTexture2D(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string normalizedKey = NormalizePath(path);

            if (!_textures.TryGetValue(normalizedKey, out ClientTexture textureData))
                return null;

            if (textureData.Texture != null && !textureData.Texture.IsDisposed)
                return textureData.Texture;

            if (textureData.Info?.Width == 0 || textureData.Info?.Height == 0 || textureData.Info.Data == null)
                return null;

            if (_graphicsDevice == null)
                return null;

            lock (textureData)
            {
                if (textureData.Texture != null && !textureData.Texture.IsDisposed)
                    return textureData.Texture;

                try
                {
                    Texture2D texture = null;
                    if (textureData.Info.IsCompressed)
                    {
                        byte[] decompressed = null;
                        if (CustomDecompressFunction != null)
                        {
                            decompressed = CustomDecompressFunction(textureData.Info);
                        }
                        else
                        {
                            if (textureData.Info.Format == TextureSurfaceFormat.Dxt1)
                                decompressed = DxtDecoder.DecompressDXT1(textureData.Info.Data, (int)textureData.Info.Width, (int)textureData.Info.Height);
                            else if (textureData.Info.Format == TextureSurfaceFormat.Dxt3)
                                decompressed = DxtDecoder.DecompressDXT3(textureData.Info.Data, (int)textureData.Info.Width, (int)textureData.Info.Height);
                            else if (textureData.Info.Format == TextureSurfaceFormat.Dxt5)
                                decompressed = DxtDecoder.DecompressDXT5(textureData.Info.Data, (int)textureData.Info.Width, (int)textureData.Info.Height);
                        }

                        if (decompressed != null)
                        {
                            texture = new Texture2D(_graphicsDevice, (int)textureData.Info.Width, (int)textureData.Info.Height, false, SurfaceFormat.Color);
                            texture.SetData(decompressed);
                        }
                    }
                    else
                    {
                        int pixelCount = (int)(textureData.Info.Width * textureData.Info.Height);
                        int components = textureData.Info.Components;

                        if (components != 3 && components != 4)
                        {
                            _logger?.LogDebug($"Unsupported texture components: {components} for texture {path}");
                            return null;
                        }

                        texture = new Texture2D(_graphicsDevice, (int)textureData.Info.Width, (int)textureData.Info.Height);

                        var pool = System.Buffers.ArrayPool<Color>.Shared;
                        Color[] pixelData = pool.Rent(pixelCount);
                        try
                        {
                            byte[] data = textureData.Info.Data;
                            for (int i = 0; i < pixelCount; i++)
                            {
                                int dataIndex = i * components;
                                byte r = data[dataIndex];
                                byte g = data[dataIndex + 1];
                                byte b = data[dataIndex + 2];
                                byte a = components == 4 ? data[dataIndex + 3] : (byte)255;
                                pixelData[i] = new Color(r, g, b, a);
                            }
                            texture.SetData(pixelData, 0, pixelCount);
                        }
                        finally
                        {
                            pool.Return(pixelData);
                        }
                    }

                    textureData.Texture = texture;
                    return texture;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug($"Failed to create Texture2D for {path}: {ex.Message}");
                    return null;
                }
            }
        }
    }
}
