using System;
using System.IO;
using static Satchel.IoUtils;
namespace CustomKnight
{
    /// <summary>
    ///     The Class that represents all static skins that CustomKnight manages.
    /// </summary>
    internal class StaticSkin : ISelectableSkin, ISupportsOverrides, ISupportsConfig
    {
        private static readonly TextureRemapper HudRemapper = new TextureRemapper("hud_diff.json");
        internal string SkinDirectory = "";
        private SkinConfig skinConfig;
        private SkinSettings skinSettings;
        private static SettingsLoader<SkinConfig> skinConfigLoader = new SettingsLoader<SkinConfig>();
        private static SettingsLoader<SkinSettings> skinSettingsLoader = new SettingsLoader<SkinSettings>();
        public StaticSkin(string DirectoryName)
        {
            SkinDirectory = DirectoryName;
            MigrateCharms();
            skinConfig = skinConfigLoader.Load($"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/skin-config.json".Replace("\\", "/"));
            skinSettings = skinSettingsLoader.Load($"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/skin-settings.json".Replace("\\", "/"));
            if (skinConfig.detectAlts)
            {
                skinConfig.detectAlts = false;
                DetectAlternates();
            }
        }

        private void SaveSettings()
        {
            skinConfigLoader.Save($"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/skin-config.json".Replace("\\", "/"), skinConfig);
            skinSettingsLoader.Save($"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/skin-settings.json".Replace("\\", "/"), skinSettings);
        }

        public bool shouldCache() => true;
        public string GetId() => SkinDirectory;
        public string GetName() => SkinDirectory;
        public bool hasSwapper() => true;
        public string getSwapperPath() => Path.Combine(SkinManager.SKINS_FOLDER, SkinDirectory);

        public SkinConfig GetConfig() => skinConfig;
        public SkinSettings GetSettings() => skinSettings;

        private Dictionary<string, string> CinematicFileUrlCache = new();
        public bool Exists(string FileName)
        {
            string file = $"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/{FileName}".Replace("\\", "/");
            return ResolveFilePath(file) != null;
        }
        public Texture2D GetTexture(string FileName)
        {
            Texture2D texture = null;
            try
            {
                string OverriddenFile = GetOverride(FileName);
                string file = $"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/{OverriddenFile}".Replace("\\", "/");
                var resolvedFile = ResolveFilePath(file);
                if (resolvedFile == null)
                {
                    return null;
                }
                byte[] texBytes = File.ReadAllBytes(resolvedFile);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(texBytes);
                if (string.Equals(Path.GetFileName(resolvedFile), "HUD.png", StringComparison.OrdinalIgnoreCase))
                {
                    if (HudRemapper.TryRemap(texture, this, out var remapped) && remapped != texture)
                    {
                        UnityEngine.Object.Destroy(texture);
                        texture = remapped;
                    }
                }
            }
            catch (Exception e)
            {
                CustomKnight.Instance.Log(e.ToString());
            }
            return texture;
        }
        public byte[] GetFile(string FileName)
        {
            byte[] data = null;
            try
            {
                string OverriddenFile = GetOverride(FileName);
                string file = $"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/{OverriddenFile}".Replace("\\", "/");
                var resolvedFile = ResolveFilePath(file);
                if (resolvedFile == null)
                {
                    return null;
                }
                data = File.ReadAllBytes(resolvedFile);
            }
            catch (Exception e)
            {
                CustomKnight.Instance.Log(e.ToString());
            }
            return data;
        }

        public bool HasCinematic(string CinematicName)
        {
            if (CinematicFileUrlCache.TryGetValue(CinematicName, out var url))
            {
                return url.Length > 0;
            }
            else
            {
                EnsureDirectory($"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/Cinematics/");
                string file = $"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/Cinematics/{CinematicName}".Replace("\\", "/");
                CinematicFileUrlCache[CinematicName] = GetCinematicUrl(CinematicName);
                return CinematicFileUrlCache[CinematicName].Length > 0;
            }

        }

        public string GetCinematicUrl(string CinematicName)
        {
            string path = "";
            string file = $"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/Cinematics/{CinematicName}".Replace("\\", "/");
            var resolvedFile = ResolveFilePath(file + ".webm");
            if (resolvedFile != null)
            {
                path = resolvedFile;
            }
            CustomKnight.Instance.LogFine("[GetCinematicUrl]" + CinematicName + ":" + path);
            return path;
        }

        private static string ResolveFilePath(string path)
        {
            if (File.Exists(path))
            {
                return path;
            }

            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
            {
                return null;
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            return null;
        }

        public bool HasOverrides(string FileName)
        {
            CustomKnight.Instance.Log($" {FileName} : count");

            if (skinConfig.alternates != null && skinConfig.alternates.TryGetValue(FileName, out var overrides))
            {
                CustomKnight.Instance.Log($"{overrides.Count} : count");

                return overrides.Count > 1;
            }
            return false;
        }

        public string[] GetAllOverrides(string FileName)
        {
            if (skinConfig.alternates != null && skinConfig.alternates.TryGetValue(FileName, out var overrides))
            {
                return overrides.ToArray();
            }
            return new string[] { };
        }

        public void SetOverride(string FileName, string AlternateFileName)
        {
            if (skinSettings.selectedAlternates == null)
            {
                skinSettings.selectedAlternates = new Dictionary<string, string>();
            }
            skinSettings.selectedAlternates[FileName] = AlternateFileName;
            skinSettingsLoader.Save($"{SkinManager.SKINS_FOLDER}/{SkinDirectory}/skin-settings.json".Replace("\\", "/"), skinSettings);
        }

        public string GetOverride(string FileName)
        {
            if (skinSettings.selectedAlternates != null && skinSettings.selectedAlternates.TryGetValue(FileName, out var alternate))
            {
                return alternate;
            }

            var overrides = GetAllOverrides(FileName);
            if (overrides.Length > 0)
            {
                return overrides[0];
            }

            return FileName;
        }

        private void MigrateCharms()
        {
            var skinFolder = Path.Combine(SkinManager.SKINS_FOLDER, SkinDirectory);
            var charmsFolder = Path.Combine(skinFolder, "Charms");
            EnsureDirectory(charmsFolder);
            string[] files = Directory.GetFiles(skinFolder);
            foreach (string file in files)
            {
                if (!Path.GetFileName(file).StartsWith("Charm_"))
                {
                    continue;
                }
                try
                {
                    File.Move(file, Path.Combine(charmsFolder, Path.GetFileName(file)));
                }
                catch (Exception e)
                {
                    CustomKnight.Instance.LogError("A File could not be Copied : " + e.ToString());
                }
            }
        }

        private void DetectAlternates()
        {
            var skinFolder = Path.Combine(SkinManager.SKINS_FOLDER, SkinDirectory);
            var charmsFolder = Path.Combine(skinFolder, "Charms");
            var inventoryFolder = Path.Combine(skinFolder, "Inventory");
            EnsureDirectory(charmsFolder);
            EnsureDirectory(inventoryFolder);

            string[] files = Directory.GetFiles(skinFolder);

            // base skin
            foreach (var kvp in SkinManager.Skinables)
            {
                var name = kvp.Value.name + ".png";
                var possibleAlts = Array.FindAll(files, (file) => Path.GetFileName(file).Contains(kvp.Value.name) && !skinConfig.alternates[name].Contains(Path.GetFileName(file)));
                foreach (var possibleAlt in possibleAlts)
                {
                    skinConfig.alternates[name].Add(Path.GetFileName(possibleAlt));
                }
            }
            // charms 
            files = Directory.GetFiles(charmsFolder);
            foreach (var kvp in SkinManager.Skinables)
            {
                if (!kvp.Value.name.StartsWith("Charms/"))
                {
                    continue;
                }
                var baseFileName = kvp.Value.name.Substring("Charms/".Length);
                var name = baseFileName + ".png";
                var possibleAlts = Array.FindAll(files, (file) => Path.GetFileName(file).Contains(baseFileName) && !skinConfig.alternates[kvp.Value.name + ".png"].Contains("Charms/" + Path.GetFileName(file)));
                foreach (var possibleAlt in possibleAlts)
                {
                    skinConfig.alternates[kvp.Value.name + ".png"].Add("Charms/" + Path.GetFileName(possibleAlt));
                }
            }
            // inventory
            files = Directory.GetFiles(inventoryFolder);
            foreach (var kvp in SkinManager.Skinables)
            {

                if (!kvp.Value.name.StartsWith("Inventory/"))
                {
                    continue;
                }
                var baseFileName = kvp.Value.name.Substring("Inventory/".Length);
                var name = baseFileName + ".png";
                var possibleAlts = Array.FindAll(files, (file) => Path.GetFileName(file).Contains(baseFileName) && !skinConfig.alternates[kvp.Value.name + ".png"].Contains("Inventory/" + Path.GetFileName(file)));
                foreach (var possibleAlt in possibleAlts)
                {
                    skinConfig.alternates[kvp.Value.name + ".png"].Add("Inventory/" + Path.GetFileName(possibleAlt));
                }
            }
            SaveSettings();
        }

    }

}
