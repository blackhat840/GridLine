using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GridLine
{
    internal sealed partial class LauncherWindow
    {
        private CancellationTokenSource _detailsRequest;
        private string _ratingId;
        private SteamRating _rating;
        private string _ratingStatus;
        private DateTime _reviewsBackoff;
        private List<BitmapSource> _gallery;
        private int _galleryIndex;
        private int _galleryRequest;

        private string DataDirectory { get { return Path.GetDirectoryName(_settingsPath); } }

        private void CancelDetailsRequest()
        {
            if (_detailsRequest != null)
            {
                _detailsRequest.Cancel();
                _detailsRequest = null;
            }
            CancelMetadataRequest();
            _galleryRequest++;
            _gallery = null;
        }

        private async void LoadRating(Game game)
        {
            if (game == null || !game.IsSteam) return;
            if (_detailsRequest != null) _detailsRequest.Cancel();
            _ratingId = game.Id;
            string path = Path.Combine(DataDirectory, "cache", "steam-reviews", game.Id + ".json");
            _rating = JsonDisk.Load<SteamRating>(path);
            _ratingStatus = null;
            if ((_rating != null && DateTime.UtcNow - _rating.FetchedUtc < TimeSpan.FromDays(1)) ||
                _reviewsBackoff > DateTime.UtcNow)
            {
                Draw(redrawBody: false);
                FocusOverlay();
                return;
            }

            var request = new CancellationTokenSource();
            _detailsRequest = request;
            _ratingStatus = "Checking Steam...";
            Draw(redrawBody: false);
            FocusOverlay();
            try
            {
                SteamRating latest = await SteamServices.GetRating(game.Id, request.Token);
                if (request.IsCancellationRequested) return;
                if (latest != null)
                {
                    _rating = latest;
                    await Task.Run(() => JsonDisk.Save(path, latest));
                }
            }
            catch (ProviderException error)
            {
                if (error.Status == 429) _reviewsBackoff = DateTime.UtcNow.AddMinutes(5);
            }
            catch (OperationCanceledException) { }
            catch (HttpRequestException) { _reviewsBackoff = DateTime.UtcNow.AddMinutes(1); }
            catch (SerializationException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                if (ReferenceEquals(_detailsRequest, request))
                {
                    _detailsRequest = null;
                    _ratingStatus = null;
                    var selected = CurrentGame(VisibleGames());
                    if (_overlay == Overlay.Details && selected != null && selected.Id == game.Id)
                        UpdateRatingLabel(game);
                }
                request.Dispose();
            }
        }

        private System.Windows.Controls.TextBlock _ratingLabel;

        private void UpdateRatingLabel(Game game)
        {
            if (_ratingLabel == null) return;
            string value = _ratingId == game.Id && _ratingStatus != null ? _ratingStatus : "No review summary available";
            if (_ratingId == game.Id && _rating != null && _rating.Total > 0)
            {
                double share = (double)_rating.Positive / _rating.Total;
                value = share.ToString("P0", CultureInfo.CurrentCulture) + " positive  /  " +
                    _rating.Total.ToString("N0", CultureInfo.CurrentCulture) + " reviews  /  " +
                    _rating.FetchedUtc.ToLocalTime().ToString("MMM d", CultureInfo.CurrentCulture);
            }
            _ratingLabel.Text = value;
        }

        private void DrawGameDetails(Game game)
        {
            DrawWidePanel();
            var info = GameInformation.For(game, DataDirectory, _metadataId == game.Id && _settings.IgdbEnabled ? _metadata : null);
            string state = game.Installed ? "INSTALLED VIA " + game.Source.ToUpperInvariant() :
                game.Owned ? "OWNED / NOT INSTALLED" : game.FreeToPlay ? "FREE TO PLAY" : "NOT INSTALLED";
            Label(game.Category.ToUpperInvariant() + "  /  " + state,
                new Rect(234, 145, 393, 34), 19, _accent, false);
            bool favorite = IsFavorite(game);
            ButtonShape(favorite ? "Remove Favorite" : "Add Favorite", new Rect(632, 143, 239, 38),
                favorite, true, () => ToggleFavorite(game), 17, icon: Brand.Star);
            ButtonShape("Launch Setup", new Rect(1194, 122, 239, 38), false, true, OpenLaunchSetup, 18);
            FittedTitle(game.Title, new Rect(230, 187, 650, 106), 48, 24);
            Label(info.Summary ?? "A driving title in your GridLine catalog.", new Rect(235, 303, 638, 99), 23, _ink, false,
                TextAlignment.Left, TextWrapping.Wrap);

            InfoRow("DEVELOPER", info.Developer, 416);
            InfoRow("PUBLISHER", info.Publisher, 455);
            InfoRow("RELEASE", info.Release, 494);
            if (_settings.IgdbEnabled && _metadataId == game.Id && _metadata != null)
                ButtonShape("Metadata / IGDB", new Rect(645, 526, 220, 36), false, true,
                    () => OpenGameLink("https://www.igdb.com/"), 16);
            Label("RIG SUPPORT", new Rect(235, 544, 635, 29), 17, _accent, false);
            string support = "Wheel  /  " + (info.Wheel ?? "Unverified") + "     VR  /  " + (info.VR ?? "Unverified") +
                "     Telemetry  /  " + (info.Telemetry ?? "Unverified");
            Label(support, new Rect(235, 575, 640, 52), 17, _muted, false, TextAlignment.Left, TextWrapping.Wrap);

            var picture = new Rect(900, 167, 533, 321);
            Point[] outline = Chamfer(picture.Width, picture.Height, 18);
            var photo = _art.Hero(game) ?? _art.LibraryFeature(game);
            if (photo != null)
            {
                DrawLibraryArtwork(photo, picture, outline, Stretch.UniformToFill, .17);
                DrawLibraryArtwork(photo, picture, outline, Stretch.Uniform);
            }
            else DrawLibraryBackdrop(picture, outline);
            Polygon(picture, null, _muted, outline);
            if (game.IsSteam)
            {
                Label("STEAM USER REVIEWS", new Rect(900, 512, 533, 27), 17, _accent, false);
                _ratingLabel = Label(String.Empty, new Rect(900, 544, 533, 39), 24, _ink, false);
                UpdateRatingLabel(game);
            }
            else _ratingLabel = null;
            if (info.OfficialSite != null)
                ButtonShape("Official Site", new Rect(900, 590, 165, 38), false, true, () => OpenGameLink(info.OfficialSite), 17);
            if (game.IsSteam)
                ButtonShape("Rig Guide", new Rect(1078, 590, 165, 38), false, true,
                    () => OpenGameLink(info.RigGuide ?? "https://www.pcgamingwiki.com/wiki/Special:Search?search=" +
                        Uri.EscapeDataString(game.Title)), 17);
            if (game.IsSteam && (game.Installed || game.CanInstallViaSteam || game.Manual))
                ButtonShape("Steam Store", new Rect(1256, 590, 177, 38), false, true,
                    () => OpenSteamStore(game), 17);

            DetailButton(PrimaryGameLabel(game), 234, 650, true, !_scanning,
                game.Installed ? Brand.Play : game.CanInstallViaSteam ? Brand.Download : Brand.External, () => PrimaryGameAction(game));
            DetailButton("Screenshots", 536, 650, false, true, Brand.Gallery, () => OpenGallery(game));
            if (IsTrailerLink(info.Trailer))
                DetailButton("Watch Trailer", 838, 650, false, true, Brand.External, () => OpenGameLink(info.Trailer));
            if (game.IsSteam)
                DetailButton("SteamDB", 1140, 650, false, true, Brand.External,
                    () => OpenGameLink("https://steamdb.info/app/" + game.Id + "/"));
            DetailButton("Back", 234, 717, false, true, null, Back);
            if (game.Installed || game.Owned || (game.IsSteam && !game.Manual))
                DetailButton("Locate Game", 536, 717, false, !_scanning, null, () => AddGame(game));
            DetailButton("Hide Game", 838, 717, false, true, null, () => SetGameHidden(game, true));
            if (game.IsSteam)
                DetailButton("Artwork", 1140, 717, false, true, Brand.Gallery, () => OpenArtworkPicker(game));
            DrawWideMessage();
        }

        private static bool IsTrailerLink(string url)
        {
            Uri trailer;
            return Uri.TryCreate(url, UriKind.Absolute, out trailer) && trailer.Scheme == Uri.UriSchemeHttps &&
                !trailer.Host.Equals("store.steampowered.com", StringComparison.OrdinalIgnoreCase);
        }

        private void DrawWidePanel()
        {
            var panel = new Rect(191, 110, 1290, 720);
            Polygon(panel, _panel, _accent, Chamfer(panel.Width, panel.Height, 23));
        }

        private void InfoRow(string label, string value, int y)
        {
            Label(label, new Rect(235, y, 140, 34), 17, _muted, false);
            Label(String.IsNullOrWhiteSpace(value) ? "Unverified" : value, new Rect(385, y, 480, 34), 21, _ink, false);
        }

        private void DetailButton(string name, int left, int top, bool primary, bool enabled, Geometry icon, Action action)
        {
            var bounds = new Rect(left, top, 283, 52);
            ButtonShape(name, bounds, primary, enabled, action, 21, icon: icon);
        }

        private void DrawWideMessage()
        {
            if (!String.IsNullOrWhiteSpace(_message))
                Label(_message, new Rect(234, 782, 1200, 35), 18, _muted, false);
        }

        private async void OpenGallery(Game game)
        {
            ShowOverlay(Overlay.Gallery);
            int request = ++_galleryRequest;
            _galleryIndex = 0;
            var screenshots = await Task.Run(() => Artwork.ReadScreenshots(DataDirectory, game.Id));
            if (_overlay != Overlay.Gallery || request != _galleryRequest) return;
            _gallery = screenshots;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void DrawGallery(Game game)
        {
            DrawWidePanel();
            ButtonShape("Launch Setup", new Rect(1194, 122, 239, 38), false, true, OpenLaunchSetup, 18);
            FittedTitle(game.Title, new Rect(232, 134, 1190, 62), 42, 24);
            var image = new Rect(234, 210, 1200, 477);
            if (_gallery != null && _gallery.Count > 0)
                DrawLibraryArtwork(_gallery[_galleryIndex], image, Chamfer(image.Width, image.Height, 15), Stretch.Uniform);
            else
                Label(_gallery == null ? "Loading local screenshots..." :
                    "Add PNG or JPEG files to the game's artwork screenshots folder. Steam screenshots are also available in the browser.",
                    image, 23, _muted, false, TextAlignment.Center, TextWrapping.Wrap);
            DetailButton("Back", 234, 717, false, true, null, Back);
            if (_gallery != null && _gallery.Count > 1)
            {
                DetailButton("Previous", 536, 717, false, _galleryIndex > 0, null, () => ChangeScreenshot(-1));
                DetailButton("Next", 838, 717, false, _galleryIndex < _gallery.Count - 1, null, () => ChangeScreenshot(1));
            }
            if (game.IsSteam)
                DetailButton("Steam Screenshots", 1140, 717, false, true, Brand.External,
                    () => OpenGameLink("https://steamcommunity.com/app/" + game.Id + "/screenshots/"));
            DrawWideMessage();
        }

        private void ChangeScreenshot(int offset)
        {
            _galleryIndex += offset;
            Draw(redrawBody: false);
            FocusOverlay();
        }
    }
}
