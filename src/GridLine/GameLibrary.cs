using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security;
using Microsoft.Win32;

namespace GridLine
{
    internal sealed class Game
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string CardTitle;
        public readonly string Category;
        public readonly string HeroLabel;
        public readonly bool FreeToPlay;
        public string InstallPath;
        public string Executable;
        public string SteamExecutable;
        public bool Owned;
        public bool Manual;
        public bool IsSteam { get { return SteamServices.IsAppId(Id); } }
        public bool CanInstallViaSteam { get { return IsSteam && (Owned || FreeToPlay); } }

        public Game(string id, string title, string category, string cardTitle = null, string heroLabel = null, bool freeToPlay = false)
        {
            Id = id;
            Title = title;
            Category = category;
            CardTitle = cardTitle ?? title;
            HeroLabel = heroLabel ?? category;
            FreeToPlay = freeToPlay;
        }

        public bool Installed { get { return InstallPath != null; } }
        public string Source { get { return SteamExecutable != null ? "Steam" : "Windows"; } }

        public ProcessStartInfo GetLaunchCommand()
        {
            if (!Installed || !Directory.Exists(InstallPath))
                throw new IOException("The install folder is unavailable. Rescan or locate the game in Settings.");

            string file = SteamExecutable ?? Executable;
            if (!File.Exists(file))
                throw new IOException("The launch executable is unavailable. Rescan or locate the game in Settings.");

            var start = new ProcessStartInfo(file)
            {
                WorkingDirectory = Path.GetDirectoryName(file),
                UseShellExecute = true
            };
            if (SteamExecutable != null)
                start.Arguments = "-applaunch " + Id;

            return start;
        }
    }

    internal static class GameLibrary
    {
        public static List<Game> Scan(IEnumerable<ManualGame> manual, IEnumerable<SteamApproval> approved = null,
            IEnumerable<string> owned = null)
        {
            var games = CreateCatalog();
            if (approved != null)
                foreach (SteamApproval entry in approved)
                {
                    if (entry == null || !SteamServices.IsAppId(entry.Id) || String.IsNullOrWhiteSpace(entry.Title) ||
                        games.Any(game => game.Id == entry.Id)) continue;
                    games.Add(new Game(entry.Id, entry.Title, entry.Category ?? "Arcade"));
                }
            foreach (string steam in SteamRoots().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string steamExe = Path.Combine(steam, "steam.exe");
                if (!File.Exists(steamExe)) continue;
                ScanSteam(games, steam, steamExe);
            }

            Game iracing = games.Find(game => game.Id == "266410");
            if (!iracing.Installed)
            {
                string location = RegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\iRacing.com", "InstallLocation");
                foreach (string root in Paths(location,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "iRacing"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "iRacing")))
                {
                    string file = Path.Combine(root, "iRacingUI.exe");
                    if (File.Exists(file)) { SetExecutable(iracing, file); break; }
                    file = Path.Combine(root, "ui", "iRacingUI.exe");
                    if (File.Exists(file)) { SetExecutable(iracing, file); break; }
                }
            }

            foreach (ManualGame entry in manual)
            {
                if (entry == null || String.IsNullOrWhiteSpace(entry.Id) || String.IsNullOrWhiteSpace(entry.Title)) continue;
                Game game = games.Find(candidate => candidate.Id == entry.Id);
                if (game == null)
                {
                    game = new Game(entry.Id, entry.Title, entry.Category ?? "Arcade");
                    games.Add(game);
                }
                game.Manual = true;
                if (File.Exists(entry.Executable))
                    SetExecutable(game, entry.Executable);
            }
            if (owned != null)
            {
                var ids = new HashSet<string>(owned, StringComparer.Ordinal);
                foreach (Game game in games) game.Owned = game.IsSteam && ids.Contains(game.Id);
            }
            return games;
        }

        internal static void ScanSteam(List<Game> games, string steam, string steamExe)
        {
            var libraries = new List<string> { steam };
            var metadata = KeyValues.ReadFile(Path.Combine(steam, "steamapps", "libraryfolders.vdf"));
            var folders = KeyValues.Section(metadata, "libraryfolders");
            foreach (var item in folders)
            {
                int index;
                if (!Int32.TryParse(item.Key, out index)) continue;
                var folder = item.Value as Dictionary<string, object>;
                var path = folder == null ? item.Value as string : KeyValues.String(folder, "path");
                if (!String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path))
                    libraries.Add(path);
            }

            foreach (string library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (Game game in games)
                {
                    if (game.Installed || !game.IsSteam) continue;
                    string apps = Path.Combine(library, "steamapps");
                    var manifest = KeyValues.ReadFile(Path.Combine(apps, "appmanifest_" + game.Id + ".acf"));
                    var app = KeyValues.Section(manifest, "AppState");
                    if (KeyValues.String(app, "appid") != game.Id) continue;

                    string folder = KeyValues.String(app, "installdir");
                    if (String.IsNullOrWhiteSpace(folder) || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        folder == "." || folder == "..") continue;
                    int flags;
                    if (Int32.TryParse(KeyValues.String(app, "StateFlags"), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out flags) && (flags & 4) == 0) continue;

                    string location = Path.Combine(apps, "common", folder);
                    if (!HasContents(location)) continue;
                    game.InstallPath = location;
                    game.SteamExecutable = steamExe;
                }
            }
        }

        private static bool HasContents(string path)
        {
            try { return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any(); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void SetExecutable(Game game, string executable)
        {
            game.InstallPath = Path.GetDirectoryName(executable);
            game.Executable = executable;
            game.SteamExecutable = null;
        }

        internal static IEnumerable<string> SteamRoots()
        {
            return Paths(
                RegistryValue(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath"),
                RegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Valve\Steam", "InstallPath"),
                RegistryValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Valve\Steam", "InstallPath"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        }

        private static IEnumerable<string> Paths(params string[] paths)
        {
            return paths.Where(path => !String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path));
        }

        private static string RegistryValue(RegistryHive hive, RegistryView view, string key, string name)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var entry = root.OpenSubKey(key))
                    return entry == null ? null : entry.GetValue(name) as string;
            }
            catch (UnauthorizedAccessException) { return null; }
            catch (SecurityException) { return null; }
            catch (IOException) { return null; }
        }

        internal static List<Game> CreateCatalog()
        {
            return new List<Game>
            {
                new Game("805550", "Assetto Corsa Competizione", "Circuit", "ACC", "GT Racing"),
                new Game("244210", "Assetto Corsa", "Circuit"),
                new Game("1849250", "EA SPORTS WRC", "Rally"),
                new Game("1066890", "Automobilista 2", "Circuit"),
                new Game("284160", "BeamNG.drive", "Arcade"),
                new Game("211500", "RaceRoom", "Circuit", freeToPlay: true),
                new Game("266410", "iRacing", "Circuit"),
                new Game("3058630", "Assetto Corsa EVO", "Circuit"),
                new Game("2399420", "Le Mans Ultimate", "Circuit"),
                new Game("365960", "rFactor 2", "Circuit"),
                new Game("690790", "DiRT Rally 2.0", "Rally"),
                new Game("1551360", "Forza Horizon 5", "Arcade"),
                new Game("270880", "American Truck Simulator", "Trucking", "American Truck Simulator"),
                new Game("227300", "Euro Truck Simulator 2", "Trucking"),
                new Game("491540", "The Bus", "Bus & Transit"),
                new Game("252530", "OMSI 2", "Bus & Transit"),
                new Game("976590", "Bus Simulator 21 Next Stop", "Bus & Transit", "Bus Simulator 21"),
                new Game("427100", "Fernbus Simulator", "Bus & Transit"),
                new Game("953580", "Tourist Bus Simulator", "Bus & Transit")
            };
        }
    }

    internal static class KeyValues
    {
        internal static Dictionary<string, object> ReadFile(string path)
        {
            try
            {
                if (File.Exists(path)) return new Parser(File.ReadAllText(path)).Read(false, 0);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (FormatException) { }
            return Empty();
        }

        internal static Dictionary<string, object> Section(Dictionary<string, object> root, string name)
        {
            object value;
            return root.TryGetValue(name, out value) ? value as Dictionary<string, object> ?? Empty() : Empty();
        }

        internal static string String(Dictionary<string, object> root, string name)
        {
            object value;
            return root.TryGetValue(name, out value) ? value as string : null;
        }

        private static Dictionary<string, object> Empty()
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _offset;

            public Parser(string text) { _text = text; }

            public Dictionary<string, object> Read(bool nested, int depth)
            {
                if (depth > 24) throw new FormatException("Steam metadata is nested too deeply.");
                var result = Empty();
                string key;
                while ((key = Token()) != null)
                {
                    if (key == "}" && nested) return result;
                    if (key == "{" || key == "}") throw new FormatException("Invalid Steam metadata key.");
                    string value = Token();
                    if (value == null || value == "}") throw new FormatException("Missing Steam metadata value.");
                    result[key] = value == "{" ? (object)Read(true, depth + 1) : value;
                }
                if (nested) throw new FormatException("Steam metadata ended early.");
                return result;
            }

            private string Token()
            {
                while (_offset < _text.Length)
                {
                    if (Char.IsWhiteSpace(_text[_offset])) { _offset++; continue; }
                    if (_text[_offset] != '/' || _offset + 1 == _text.Length || _text[_offset + 1] != '/') break;
                    while (_offset < _text.Length && _text[_offset] != '\n') _offset++;
                }
                if (_offset == _text.Length) return null;
                char first = _text[_offset++];
                if (first == '{' || first == '}') return first.ToString();
                if (first != '"')
                {
                    int start = _offset - 1;
                    while (_offset < _text.Length && !Char.IsWhiteSpace(_text[_offset]) &&
                        _text[_offset] != '{' && _text[_offset] != '}') _offset++;
                    return _text.Substring(start, _offset - start);
                }

                var token = new System.Text.StringBuilder();
                while (_offset < _text.Length)
                {
                    char current = _text[_offset++];
                    if (current == '"') return token.ToString();
                    if (current == '\\' && _offset < _text.Length &&
                        (_text[_offset] == '\\' || _text[_offset] == '"')) current = _text[_offset++];
                    token.Append(current);
                }
                throw new FormatException("Unterminated Steam metadata string.");
            }
        }
    }

    [DataContract]
    internal sealed class ManualGame
    {
        [DataMember] public string Id;
        [DataMember] public string Title;
        [DataMember] public string Category;
        [DataMember] public string Executable;
    }

    [DataContract]
    internal sealed class LauncherSettings
    {
        [DataMember] public string Theme = "lime";
        [DataMember] public bool InstalledOnly;
        [DataMember] public bool KioskHandoff;
        [DataMember] public bool IgdbEnabled;
        [DataMember] public List<ManualGame> Games = new List<ManualGame>();
        [DataMember] public List<string> HiddenGameIds = new List<string>();
        [DataMember] public List<string> FavoriteGameIds = new List<string>();
        [DataMember] public List<GameLaunchApp> LaunchApps = new List<GameLaunchApp>();

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GridLine", "settings.json");
            }
        }

        public static LauncherSettings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (var file = File.OpenRead(path))
                    {
                        var settings = new DataContractJsonSerializer(typeof(LauncherSettings))
                            .ReadObject(file) as LauncherSettings;
                        if (settings != null && settings.Games != null)
                        {
                            if (settings.HiddenGameIds == null) settings.HiddenGameIds = new List<string>();
                            if (settings.FavoriteGameIds == null) settings.FavoriteGameIds = new List<string>();
                            if (settings.LaunchApps == null) settings.LaunchApps = new List<GameLaunchApp>();
                            return settings;
                        }
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (SerializationException) { }
            return new LauncherSettings();
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = File.Create(pending))
                    new DataContractJsonSerializer(typeof(LauncherSettings)).WriteObject(file, this);
                if (File.Exists(path)) File.Replace(pending, path, null);
                else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
