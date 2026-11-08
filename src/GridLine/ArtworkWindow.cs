using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace GridLine
{
    internal sealed partial class LauncherWindow
    {
        private CancellationTokenSource _artworkRequest;
        private List<ArtCandidate> _artworkCandidates;
        private string _artworkSlot;
        private DateTime _artworkBackoff;

        private string ArtworkKeyPath { get { return Path.Combine(DataDirectory, "steamgriddb-key.json"); } }

        private static bool IsArtworkKey(string key)
        {
            return !String.IsNullOrWhiteSpace(key) && key.Length <= 160 &&
                System.Linq.Enumerable.All(key, c => Char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        private void CancelArtworkRequest()
        {
            if (_artworkRequest != null) _artworkRequest.Cancel();
            _artworkCandidates = null;
        }

        private string ArtworkFolder(Game game) { return Path.Combine(DataDirectory, "artwork", game.Id); }

        private void OpenArtworkPicker(Game game)
        {
            if (!game.IsSteam) return;
            _artworkCandidates = null;
            ShowOverlay(Overlay.ArtPicker);
        }

        private async void FindArtwork(Game game, string slot)
        {
            if (_artworkRequest != null) return;
            if (_artworkBackoff > DateTime.UtcNow)
            {
                _message = "SteamGridDB limited requests. Try again in a few minutes.";
                Draw(redrawBody: false);
                FocusOverlay();
                return;
            }
            string key = SteamServices.ReadKey(JsonDisk.Load<string>(ArtworkKeyPath));
            if (!IsArtworkKey(key))
            {
                var input = Prompt("Connect SteamGridDB", "Enter your SteamGridDB API key to preview community artwork on this PC.",
                    null, null, "SteamGridDB API key", "Find Artwork");
                if (input == null) return;
                key = input.Item2.Trim();
            }
            if (!IsArtworkKey(key))
            {
                _message = "Enter a valid SteamGridDB API key.";
                Draw(redrawBody: false);
                FocusOverlay();
                return;
            }
            var request = new CancellationTokenSource();
            _artworkRequest = request;
            _artworkCandidates = null;
            _artworkSlot = slot;
            _message = "Looking up SteamGridDB artwork...";
            Draw(redrawBody: false);
            try
            {
                var candidates = await SteamArtwork.Find(game.Id, slot, key, request.Token);
                if (request.IsCancellationRequested) return;
                JsonDisk.Save(ArtworkKeyPath, SteamServices.ProtectKey(key));
                _artworkCandidates = candidates;
                _message = candidates.Count == 0 ? "No matching artwork was returned for this title." : null;
            }
            catch (ProviderException error)
            {
                if (error.Status == 429) _artworkBackoff = DateTime.UtcNow.AddMinutes(5);
                _message = error.Status == 401 || error.Status == 403 ?
                    "SteamGridDB refused this key. Use Forget Key to replace it." :
                    error.Status == 429 ? "SteamGridDB limited requests. Try again later." :
                    "SteamGridDB could not find artwork for this title.";
            }
            catch (OperationCanceledException) { if (!request.IsCancellationRequested) _message = "SteamGridDB timed out."; }
            catch (HttpRequestException) { _message = "SteamGridDB is unreachable."; }
            catch (SerializationException) { _message = "SteamGridDB returned an unreadable response."; }
            catch (IOException) { _message = "Windows could not save the artwork key."; }
            catch (UnauthorizedAccessException) { _message = "Windows could not save the artwork key."; }
            catch (CryptographicException) { _message = "Windows could not protect the artwork key."; }
            finally { FinishArtworkRequest(request); }
        }

        private async void ApplyArtwork(Game game, ArtCandidate candidate)
        {
            if (_artworkRequest != null || _scanning) return;
            var request = new CancellationTokenSource();
            _artworkRequest = request;
            _message = "Downloading selected artwork...";
            Draw(redrawBody: false);
            try
            {
                await SteamArtwork.Choose(ArtworkFolder(game), _artworkSlot, candidate, request.Token);
                if (!request.IsCancellationRequested) Refresh(false, "Artwork applied from SteamGridDB.");
            }
            catch (OperationCanceledException) { if (!request.IsCancellationRequested) _message = "The download timed out."; }
            catch (HttpRequestException) { _message = "The artwork could not be downloaded."; }
            catch (IOException) { _message = "The artwork could not be saved."; }
            catch (UnauthorizedAccessException) { _message = "Windows could not save the artwork."; }
            catch (SerializationException) { _message = "Windows could not save artwork attribution."; }
            catch (InvalidDataException error) { _message = error.Message; }
            catch (NotSupportedException) { _message = "The image format could not be opened."; }
            catch (FileFormatException) { _message = "The artwork could not be decoded."; }
            catch (System.Runtime.InteropServices.COMException) { _message = "The artwork could not be decoded."; }
            finally { FinishArtworkRequest(request); }
        }

        private void FinishArtworkRequest(CancellationTokenSource request)
        {
            _artworkRequest = null;
            request.Dispose();
            if (_overlay == Overlay.ArtPicker)
            {
                Draw(redrawBody: false);
                FocusOverlay();
            }
        }

        private void ResetArtwork(Game game)
        {
            try { SteamArtwork.Clear(ArtworkFolder(game)); }
            catch (IOException) { _message = "Windows could not clear the artwork."; Draw(redrawBody: false); return; }
            catch (UnauthorizedAccessException) { _message = "Windows could not clear the artwork."; Draw(redrawBody: false); return; }
            Refresh(false, "SteamGridDB artwork cleared.");
        }

        private void ForgetArtworkKey()
        {
            try { File.Delete(ArtworkKeyPath); _message = "SteamGridDB key cleared."; }
            catch (IOException) { _message = "Windows could not clear the artwork key."; }
            catch (UnauthorizedAccessException) { _message = "Windows could not clear the artwork key."; }
            _artworkCandidates = null;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void DrawArtworkPicker(Game game)
        {
            DrawWidePanel();
            Label("STEAMGRIDDB ARTWORK", new Rect(231, 135, 1190, 61), 43, _ink, true);
            Label("Choose community artwork for this PC. Local hero and tile overrides take priority.",
                new Rect(235, 198, 1165, 37), 21, _muted, false);
            bool ready = _artworkRequest == null && !_scanning;
            DetailButton("Find Heroes", 234, 250, true, ready, Brand.Gallery, () => FindArtwork(game, "hero"));
            DetailButton("Find Tiles", 536, 250, false, ready, Brand.Gallery, () => FindArtwork(game, "tile"));
            DetailButton("Get API Key", 838, 250, false, ready, Brand.External,
                () => OpenGameLink("https://www.steamgriddb.com/profile/preferences/api"));
            DetailButton("Forget Key", 1140, 250, false, ready, null, ForgetArtworkKey);

            if (_artworkCandidates != null)
                for (int i = 0; i < _artworkCandidates.Count; i++)
                {
                    ArtCandidate candidate = _artworkCandidates[i];
                    int left = 234 + i * 302;
                    var area = new Rect(left, 332, 283, 185);
                    Point[] shape = Chamfer(area.Width, area.Height, 10);
                    DrawLibraryBackdrop(area, shape);
                    if (candidate.Preview != null)
                        DrawLibraryArtwork(candidate.Preview, area, shape, Stretch.Uniform);
                    Polygon(area, null, _muted, shape);
                    Label(candidate.Credit, new Rect(left, 525, 283, 38), 18, _muted, false, TextAlignment.Center);
                    DetailButton("Use Artwork " + (i + 1), left, 576, false, ready, Brand.Download,
                        () => ApplyArtwork(game, candidate));
                }
            var hero = SteamArtwork.Chosen(ArtworkFolder(game), "hero");
            var tile = SteamArtwork.Chosen(ArtworkFolder(game), "tile");
            if (hero != null || tile != null)
                Label("Selected artwork  /  " + (hero == null ? "" : "Hero by " + hero.Credit + "    ") +
                    (tile == null ? "" : "Tile by " + tile.Credit), new Rect(234, 657, 1170, 36), 19, _muted, false);
            DetailButton("Back", 234, 717, false, true, null, Back);
            if (hero != null || tile != null)
                DetailButton("Reset Artwork", 536, 717, false, ready, null, () => ResetArtwork(game));
            DetailButton("SteamGridDB", 1140, 717, false, true, Brand.External,
                () => OpenGameLink("https://www.steamgriddb.com/"));
            DrawWideMessage();
        }
    }
}
