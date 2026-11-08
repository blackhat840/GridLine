using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GridLine
{
    internal sealed class GameArt
    {
        public BitmapSource Hero;
        public BitmapSource Tile;
        public BitmapSource LibraryFeature;
    }

    internal sealed class Artwork
    {
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };
        private readonly Dictionary<string, BitmapSource> _homeTiles = new Dictionary<string, BitmapSource>();
        private readonly Dictionary<string, BitmapSource> _libraryTiles = new Dictionary<string, BitmapSource>();
        private readonly BitmapSource _acc;
        private readonly BitmapSource _wrc;
        private Dictionary<string, GameArt> _local = new Dictionary<string, GameArt>();

        public Artwork()
        {
            string mockups = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "docs", "design", "mockups"));
            BitmapSource home = ReadImage(Path.Combine(mockups, "Gridline Sim Racing Launcher.png"), 0);
            BitmapSource library = ReadImage(Path.Combine(mockups, "Gridline sim racing library.png"), 0);

            _acc = Crop(home, 717, 72, 955, 524);
            _wrc = Crop(library, 1187, 117, 462, 456);
            _homeTiles["805550"] = Crop(home, 48, 657, 366, 121);
            _homeTiles["244210"] = Crop(home, 462, 657, 348, 121);
            _homeTiles["1849250"] = Crop(home, 859, 656, 345, 122);
            _homeTiles["1066890"] = Crop(home, 1263, 656, 365, 122);
            _libraryTiles["805550"] = Crop(library, 18, 301, 370, 197);
            _libraryTiles["244210"] = Crop(library, 407, 301, 363, 197);
            _libraryTiles["1849250"] = Crop(library, 796, 301, 349, 197);
            _libraryTiles["1066890"] = Crop(library, 18, 576, 370, 187);
            _libraryTiles["284160"] = Crop(library, 407, 576, 363, 187);
            _libraryTiles["211500"] = Crop(library, 793, 576, 354, 187);
        }

        public void UseLocal(Dictionary<string, GameArt> artwork) { _local = artwork; }

        public BitmapSource Hero(Game game)
        {
            GameArt art;
            if (_local.TryGetValue(game.Id, out art) && (art.Hero ?? art.Tile) != null) return art.Hero ?? art.Tile;
            if (game.Id == "805550" && _acc != null) return _acc;
            if (game.Id == "1849250" && _wrc != null) return _wrc;
            return Tile(game, false);
        }

        public BitmapSource LibraryFeature(Game game)
        {
            GameArt art;
            if (_local.TryGetValue(game.Id, out art) && art.LibraryFeature != null) return art.LibraryFeature;
            return Hero(game);
        }

        public BitmapSource Tile(Game game, bool onHome)
        {
            GameArt art;
            if (_local.TryGetValue(game.Id, out art) && (art.Tile ?? art.Hero) != null) return art.Tile ?? art.Hero;
            BitmapSource source;
            if (onHome && _homeTiles.TryGetValue(game.Id, out source) && source != null) return source;
            return _libraryTiles.TryGetValue(game.Id, out source) ? source : null;
        }

        public static Dictionary<string, GameArt> ReadLocal(List<Game> games, string settingsDirectory)
        {
            var steam = GameLibrary.SteamRoots().Concat(games.Where(game => game.SteamExecutable != null)
                .Select(game => Path.GetDirectoryName(game.SteamExecutable)));
            return ReadLocal(games, settingsDirectory, steam);
        }

        internal static Dictionary<string, GameArt> ReadLocal(List<Game> games, string settingsDirectory,
            IEnumerable<string> steamRoots)
        {
            var result = new Dictionary<string, GameArt>();
            string[] caches = steamRoots.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(root => Path.Combine(root, "appcache", "librarycache")).Where(Directory.Exists).ToArray();
            foreach (Game game in games)
            {
                if (!game.Id.All(character => Char.IsLetterOrDigit(character) || character == '-')) continue;
                string custom = Path.Combine(settingsDirectory, "artwork", game.Id);
                var art = new GameArt
                {
                    Hero = ReadFirst(new[] { custom }, new[] { "hero" }, 1440)
                        ?? ReadImage(SteamArtwork.ChosenPath(custom, "hero"), 1440),
                    Tile = ReadFirst(new[] { custom }, new[] { "tile" }, 544)
                        ?? ReadImage(SteamArtwork.ChosenPath(custom, "tile"), 544)
                };
                art.LibraryFeature = art.Hero;
                if (game.Id.All(Char.IsDigit) && (art.Hero == null || art.Tile == null || art.LibraryFeature == null))
                {
                    foreach (string cache in caches)
                    {
                        string gameCache = Path.Combine(cache, game.Id);
                        string[] folders = CacheFolders(gameCache).ToArray();
                        if (art.Hero == null)
                            art.Hero = ReadFirst(folders, new[] { "library_hero", "library_hero_2x", "library_hero@2x" }, 1440)
                                ?? ReadFirst(new[] { cache }, new[] { game.Id + "_library_hero" }, 1440);
                        if (art.Tile == null)
                            art.Tile = ReadFirst(folders, new[] { "library_header", "header" }, 544)
                                ?? ReadFirst(new[] { cache }, new[] { game.Id + "_library_header", game.Id + "_header" }, 544);
                        if (art.LibraryFeature == null)
                            art.LibraryFeature = ReadFirst(folders, new[] { "library_600x900", "library_capsule" }, 360)
                                ?? ReadFirst(new[] { cache }, new[] { game.Id + "_library_600x900", game.Id + "_library_capsule" }, 360);
                        if (art.Hero != null && art.Tile != null && art.LibraryFeature != null) break;
                    }
                }
                if (art.Hero != null || art.Tile != null || art.LibraryFeature != null) result[game.Id] = art;
            }
            return result;
        }

        private static IEnumerable<string> CacheFolders(string gameCache)
        {
            yield return gameCache;
            string[] nested;
            try { nested = Directory.Exists(gameCache) ? Directory.GetDirectories(gameCache) : new string[0]; }
            catch (IOException) { yield break; }
            catch (UnauthorizedAccessException) { yield break; }
            foreach (string folder in nested) yield return folder;
        }

        private static BitmapSource ReadFirst(string[] folders, string[] names, int width)
        {
            foreach (string folder in folders)
                foreach (string name in names)
                    foreach (string extension in ImageExtensions)
                    {
                        BitmapSource image = ReadImage(Path.Combine(folder, name + extension), width);
                        if (image != null) return image;
                    }
            return null;
        }

        public static List<BitmapSource> ReadScreenshots(string settingsDirectory, string gameId)
        {
            var screenshots = new List<BitmapSource>();
            if (!gameId.All(character => Char.IsLetterOrDigit(character) || character == '-')) return screenshots;
            string folder = Path.Combine(settingsDirectory, "artwork", gameId, "screenshots");
            try
            {
                if (!Directory.Exists(folder)) return screenshots;
                foreach (string file in Directory.EnumerateFiles(folder)
                    .Where(file => ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    .Take(8).OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
                {
                    BitmapSource image = ReadImage(file, 1280);
                    if (image != null) screenshots.Add(image);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return screenshots;
        }

        private static BitmapSource ReadImage(string path, int width)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = file;
                    if (width > 0) image.DecodePixelWidth = width;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (FormatException) { return null; }
            catch (System.Runtime.InteropServices.COMException) { return null; }
        }

        private static BitmapSource Crop(BitmapSource image, int x, int y, int width, int height)
        {
            if (image == null || image.PixelWidth < x + width || image.PixelHeight < y + height) return null;
            var crop = new CroppedBitmap(image, new Int32Rect(x, y, width, height));
            crop.Freeze();
            return crop;
        }
    }
}
