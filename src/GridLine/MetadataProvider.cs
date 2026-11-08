using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace GridLine
{
    [DataContract]
    internal sealed class MetadataCache
    {
        [DataMember] public GameInformation Info;
        [DataMember] public DateTime FetchedUtc;
    }

    internal sealed partial class LauncherWindow
    {
        private CancellationTokenSource _metadataRequest;
        private string _metadataId;
        private GameInformation _metadata;
        private DateTime _metadataBackoff;

        private void ChangeIgdb()
        {
            _settings.IgdbEnabled = !_settings.IgdbEnabled;
            if (!SaveSettings()) _settings.IgdbEnabled = !_settings.IgdbEnabled;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void CancelMetadataRequest()
        {
            if (_metadataRequest != null)
            {
                _metadataRequest.Cancel();
                _metadataRequest = null;
            }
        }

        private async void LoadMetadata(Game game)
        {
            if (game == null || !game.IsSteam || !_settings.IgdbEnabled) return;
            CancelMetadataRequest();
            string path = Path.Combine(DataDirectory, "cache", "igdb", game.Id + ".json");
            var cached = JsonDisk.Load<MetadataCache>(path);
            _metadataId = game.Id;
            _metadata = cached == null ? null : cached.Info;
            if (_metadata != null) RedrawDetailsWithFocus();
            if ((cached != null && DateTime.UtcNow - cached.FetchedUtc < TimeSpan.FromDays(7)) ||
                _metadataBackoff > DateTime.UtcNow) return;

            var request = new CancellationTokenSource();
            _metadataRequest = request;
            try
            {
                var information = await SteamServices.GetJson<GameInformation>("http://127.0.0.1:8766/games/" + game.Id,
                    null, null, request.Token);
                if (request.IsCancellationRequested || information == null) return;
                // The bridge supplies facts only. Manual metadata controls links.
                _metadata = new GameInformation
                {
                    Summary = information.Summary, Developer = information.Developer,
                    Publisher = information.Publisher, Release = information.Release
                };
                var entry = new MetadataCache { Info = _metadata, FetchedUtc = DateTime.UtcNow };
                var selected = CurrentGame(VisibleGames());
                if (_overlay == Overlay.Details && selected != null && selected.Id == game.Id) RedrawDetailsWithFocus();
                await Task.Run(() => JsonDisk.Save(path, entry));
            }
            catch (ProviderException error)
            {
                if (error.Status == 429 || error.Status == 503) _metadataBackoff = DateTime.UtcNow.AddMinutes(2);
            }
            catch (OperationCanceledException) { }
            catch (HttpRequestException) { _metadataBackoff = DateTime.UtcNow.AddMinutes(1); }
            catch (SerializationException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                if (ReferenceEquals(_metadataRequest, request)) _metadataRequest = null;
                request.Dispose();
            }
        }

        private void RedrawDetailsWithFocus()
        {
            var current = Keyboard.FocusedElement as Button;
            string name = current == null ? null : AutomationProperties.GetName(current);
            Draw(redrawBody: false);
            if (name == null) return;
            var replacement = _overlayLayer.Children.OfType<Button>()
                .FirstOrDefault(button => AutomationProperties.GetName(button) == name);
            if (replacement != null) replacement.Focus();
            else FocusOverlay();
        }
    }
}
