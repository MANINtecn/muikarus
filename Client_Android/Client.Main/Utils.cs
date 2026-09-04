using System;
using System.IO;
using Client.Main.Objects;
using Client.Main.Objects.Effects;

namespace Client.Main
{
    public static class Utils
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _pathCache = new(StringComparer.OrdinalIgnoreCase);

        public static string GetActualPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (_pathCache.TryGetValue(path, out var cached))
                return cached;

            if (File.Exists(path) || Directory.Exists(path))
            {
                _pathCache.TryAdd(path, path);
                return path;
            }

            try
            {
                // Normalize separators
                string normalized = path.Replace('\\', '/');
                string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

                string current = normalized.StartsWith('/') ? "/" : string.Empty;

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    string candidate = string.IsNullOrEmpty(current) || current == "/" 
                        ? current + part 
                        : Path.Combine(current, part);

                    if (Directory.Exists(candidate) || (i == parts.Length - 1 && File.Exists(candidate)))
                    {
                        current = candidate;
                        continue;
                    }

                    // Look for case-insensitive match in current directory
                    string parent = string.IsNullOrEmpty(current) ? "." : current;
                    if (Directory.Exists(parent))
                    {
                        bool found = false;
                        if (i < parts.Length - 1)
                        {
                            foreach (var dir in Directory.GetDirectories(parent))
                            {
                                if (string.Equals(Path.GetFileName(dir), part, StringComparison.OrdinalIgnoreCase))
                                {
                                    current = dir;
                                    found = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Could be file or directory
                            foreach (var entry in Directory.GetFileSystemEntries(parent))
                            {
                                if (string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                                {
                                    current = entry;
                                    found = true;
                                    break;
                                }
                            }
                        }

                        if (!found)
                        {
                            current = candidate;
                        }
                    }
                    else
                    {
                        current = candidate;
                    }
                }

                if (File.Exists(current) || Directory.Exists(current))
                {
                    _pathCache.TryAdd(path, current);
                }
                return current;
            }
            catch
            {
                return path;
            }
        }

        public static void ClearPathCache()
        {
            _pathCache.Clear();
        }
        public static SpriteObject GetEffectByCode(EffectType e)
        {
            switch (e)
            {
                case EffectType.Light:
                    return new LightEffect();
                case EffectType.TargetPosition1:
                    return new TargetPosition1();
                default:
                    throw new Exception("Effect code now exists");
            }
        }

    }
}