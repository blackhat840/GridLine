using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace GridLine
{
    [DataContract]
    internal sealed class ArtCandidate
    {
        [DataMember(Name = "id")] public int Id { get; set; }
        [DataMember(Name = "url")] public string Url { get; set; }
        [DataMember(Name = "thumb")] public string Thumbnail { get; set; }
        [DataMember(Name = "width")] public int Width { get; set; }
        [DataMember(Name = "height")] public int Height { get; set; }
        [DataMember(Name = "author")] public ArtAuthor Author { get; set; }
        public BitmapSource Preview;

        public string Credit
        {
            get { return Author == null || String.IsNullOrWhiteSpace(Author.Name) ? "SteamGridDB community" : Author.Name; }
        }
    }

    [DataContract]
    internal sealed class ArtAuthor
    {
        [DataMember(Name = "name")] public string Name { get; set; }
    }

    [DataContract]
    internal sealed class ChosenArtwork
    {
        [DataMember] public int Id;
        [DataMember] public string FileName;
        [DataMember] public string Credit;
    }

    internal static class SteamArtwork
    {
        private static string Folder(string gameFolder)
        {
            return Path.Combine(gameFolder, "steamgriddb");
        }

        public static ChosenArtwork Chosen(string gameFolder, string slot)
        {
            return JsonDisk.Load<ChosenArtwork>(Path.Combine(Folder(gameFolder), slot + ".json"));
        }

        public static string ChosenPath(string gameFolder, string slot)
        {
            var artwork = Chosen(gameFolder, slot);
            if (artwork == null || String.IsNullOrEmpty(artwork.FileName) ||
                !artwork.FileName.StartsWith(slot + "-", StringComparison.Ordinal) ||
                artwork.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !artwork.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return null;
            return Path.Combine(Folder(gameFolder), artwork.FileName);
        }

        public static async Task<List<ArtCandidate>> Find(string id, string slot, string key, CancellationToken cancel)
        {
            bool tile = slot == "tile";
            string query = "https://www.steamgriddb.com/api/v2/" + (tile ? "grids" : "heroes") + "/steam/" + id +
                "?nsfw=false&humor=false&epilepsy=false&types=static&mimes=image%2Fpng%2Cimage%2Fjpeg";
            var result = await SteamServices.GetJson<ArtResponse>(query, "Authorization", "Bearer " + key, cancel).ConfigureAwait(false);
            if (result == null || !result.Success || result.Items == null) return new List<ArtCandidate>();
            var candidates = result.Items.Where(candidate => candidate != null && candidate.Id > 0 &&
                candidate.Width > 0 && candidate.Height > 0 &&
                (!tile || candidate.Width > candidate.Height) && IsCdnImage(candidate.Url))
                .Take(4).ToList();
            foreach (ArtCandidate candidate in candidates)
            {
                if (!IsCdnImage(candidate.Thumbnail)) continue;
                try { candidate.Preview = await ReadImage(candidate.Thumbnail, 400, cancel).ConfigureAwait(false); }
                catch (ProviderException) { }
                catch (HttpRequestException) { }
                catch (IOException) { }
                catch (InvalidDataException) { }
                catch (NotSupportedException) { }
                catch (FileFormatException) { }
                catch (System.Runtime.InteropServices.COMException) { }
            }
            return candidates;
        }

        internal static bool IsCdnImage(string url)
        {
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
                uri.IsDefaultPort && uri.Host == "cdn.steamgriddb.com" && String.IsNullOrEmpty(uri.UserInfo);
        }

        private static async Task<BitmapSource> ReadImage(string url, int width, CancellationToken cancel)
        {
            if (!IsCdnImage(url)) throw new InvalidDataException("Unexpected SteamGridDB image address.");
            byte[] bytes = await SteamServices.GetImage(new Uri(url), cancel).ConfigureAwait(false);
            using (var input = new MemoryStream(bytes))
            {
                var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                if (frame.PixelWidth > 6000 || frame.PixelHeight > 4500)
                    throw new InvalidDataException("This image exceeds the local artwork size limit.");
                input.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = Math.Min(frame.PixelWidth, width);
                image.StreamSource = input;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        public static async Task Choose(string gameFolder, string slot, ArtCandidate candidate, CancellationToken cancel)
        {
            var bitmap = await ReadImage(candidate.Url, slot == "hero" ? 1440 : 720, cancel).ConfigureAwait(false);
            cancel.ThrowIfCancellationRequested();
            string folder = Folder(gameFolder);
            Directory.CreateDirectory(folder);
            string previous = ChosenPath(gameFolder, slot);
            string name = slot + "-" + Guid.NewGuid().ToString("N") + ".png";
            string path = Path.Combine(folder, name);
            bool saved = false;
            try
            {
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(path)) png.Save(file);
                cancel.ThrowIfCancellationRequested();
                JsonDisk.Save(Path.Combine(folder, slot + ".json"), new ChosenArtwork
                {
                    Id = candidate.Id, FileName = name, Credit = candidate.Credit
                });
                saved = true;
            }
            finally { DeleteImage(saved ? previous : path); }
        }

        public static void Clear(string gameFolder)
        {
            foreach (string slot in new[] { "hero", "tile" })
            {
                string path = ChosenPath(gameFolder, slot);
                File.Delete(Path.Combine(Folder(gameFolder), slot + ".json"));
                DeleteImage(path);
            }
        }

        private static void DeleteImage(string path)
        {
            if (path == null) return;
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [DataContract]
        private sealed class ArtResponse
        {
            [DataMember(Name = "success")] public bool Success { get; set; }
            [DataMember(Name = "data")] public List<ArtCandidate> Items { get; set; }
        }
    }
}
