using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GridLine
{
    internal sealed partial class LauncherWindow
    {
        private static readonly string[] DrivingCategories = { "Circuit", "Rally", "Arcade", "Trucking", "Bus & Transit" };
        private readonly string _steamStatePath;
        private SteamState _steamState;
        private SteamState _steamDraft;
        private List<SteamEntry> _steamPreview;
        private SteamEntry _steamCandidate;
        private CancellationTokenSource _steamRequest;
        private bool _awaitingSteamInstall;
        private bool _showUnmatched;
        private int _previewPage;
        private string _candidateSearch;

        private static bool IsSteamOverlay(Overlay overlay)
        {
            return overlay == Overlay.Steam || overlay == Overlay.SteamPreview || overlay == Overlay.SteamCategory;
        }

        private void LeaveOverlay(Overlay next)
        {
            if (_overlay == Overlay.Steam && next != Overlay.Steam && _steamRequest != null)
                _steamRequest.Cancel();
            if (!IsSteamOverlay(next) || next == Overlay.Steam)
            {
                _steamDraft = null;
                _steamPreview = null;
                _steamCandidate = null;
            }
            if (next != Overlay.Details) CancelDetailsRequest();
            if (next != Overlay.ArtPicker) CancelArtworkRequest();
        }

        private void CancelProviderRequests()
        {
            if (_steamRequest != null) _steamRequest.Cancel();
            CancelDetailsRequest();
            CancelArtworkRequest();
        }

        private void FocusOverlay()
        {
            _overlayLayer.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }

        private void ChangeInstalledOnly()
        {
            _settings.InstalledOnly = !_settings.InstalledOnly;
            if (!SaveSettings()) _settings.InstalledOnly = !_settings.InstalledOnly;
            else _message = _settings.InstalledOnly ? "Play and Library now show installed games only." : null;
            Draw();
            FocusOverlay();
        }

        private async void RequestSteamSync(bool configure)
        {
            if (_steamRequest != null || _scanning) return;
            string account = _steamState.AccountId;
            string key = SteamServices.ReadKey(_steamState.EncryptedKey);
            if (configure || !SteamServices.IsAccountId(account) || !SteamServices.IsKey(key))
            {
                var input = Prompt("Connect Steam", "Enter your SteamID64 and Steam Web API user key. Windows protects the key for this account.",
                    account, "SteamID64", "Steam Web API key", "Fetch Library");
                if (input == null) return;
                account = input.Item1.Trim();
                if (!String.IsNullOrWhiteSpace(input.Item2) || account != _steamState.AccountId) key = input.Item2.Trim();
            }
            if (!SteamServices.IsAccountId(account) || !SteamServices.IsKey(key))
            {
                _message = "Enter a 17-digit SteamID64 and a 32-character Steam Web API key.";
                Draw(redrawBody: false);
                FocusOverlay();
                return;
            }

            var request = new CancellationTokenSource();
            _steamRequest = request;
            _message = "Checking Steam ownership...";
            Draw(redrawBody: false);
            try
            {
                var games = await SteamServices.GetOwned(account, key, request.Token);
                if (request.IsCancellationRequested || _overlay != Overlay.Steam) return;
                PrepareSteamPreview(account, SteamServices.ProtectKey(key), games);
            }
            catch (OperationCanceledException)
            {
                if (!request.IsCancellationRequested) _message = "Steam timed out. Your saved library is available offline.";
            }
            catch (InvalidDataException error) { _message = error.Message; }
            catch (ProviderException error)
            {
                _message = error.Status == 429 ? "Steam limited requests. Try again later." :
                    error.Status == 401 || error.Status == 403 ? "Steam refused access. Check your key and Game Details privacy." :
                    "Steam could not return the library. Your saved library is still available.";
            }
            catch (HttpRequestException) { _message = "Steam is unreachable. Your saved library is still available."; }
            catch (SerializationException) { _message = "Steam sent an unreadable response. Your saved library is still available."; }
            catch (CryptographicException) { _message = "Windows could not protect the Steam key."; }
            finally
            {
                _steamRequest = null;
                request.Dispose();
                if (_overlay == Overlay.Steam)
                {
                    Draw(redrawBody: false);
                    FocusOverlay();
                }
            }
        }

        private void PrepareSteamPreview(string account, string protectedKey, List<SteamEntry> games)
        {
            _steamDraft = new SteamState
            {
                AccountId = account, EncryptedKey = protectedKey, SyncedUtc = DateTime.UtcNow,
                OwnedIds = games.Select(game => game.Id).ToList(), Approved = _steamState.Approved.ToList()
            };
            _steamPreview = games.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            _showUnmatched = false;
            _previewPage = 0;
            _candidateSearch = String.Empty;
            ShowOverlay(Overlay.SteamPreview);
        }

        private HashSet<string> KnownSteamIds()
        {
            var known = new HashSet<string>(GameLibrary.CreateCatalog().Select(game => game.Id), StringComparer.Ordinal);
            foreach (SteamApproval approval in _steamDraft.Approved)
                if (approval != null) known.Add(approval.Id);
            return known;
        }

        private void ApplySteamImport()
        {
            if (_steamDraft == null || _scanning) return;
            if (!SaveSteamState(_steamDraft)) return;
            ShowOverlay(Overlay.Steam);
            Refresh(false, "Steam ownership updated.");
        }

        private bool SaveSteamState(SteamState state)
        {
            try { JsonDisk.Save(_steamStatePath, state); }
            catch (IOException) { return SteamSaveFailed(); }
            catch (UnauthorizedAccessException) { return SteamSaveFailed(); }
            catch (SerializationException) { return SteamSaveFailed(); }
            _steamState = state;
            return true;
        }

        private bool SteamSaveFailed()
        {
            _message = "Windows could not save the Steam library. Your previous library is intact.";
            Draw(redrawBody: false);
            FocusOverlay();
            return false;
        }

        private void DisconnectSteam()
        {
            if (_scanning || _steamRequest != null) return;
            var answer = MessageBox.Show(this,
                "Keep the cached ownership list on this PC? Choose Yes to keep it, or No to clear it. Approved titles remain in your driving catalog.",
                "Disconnect Steam", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return;
            var next = new SteamState { Approved = _steamState.Approved };
            if (answer == MessageBoxResult.Yes)
            {
                next.AccountId = _steamState.AccountId;
                next.OwnedIds = _steamState.OwnedIds;
                next.SyncedUtc = _steamState.SyncedUtc;
            }
            if (SaveSteamState(next)) Refresh(false, "Steam disconnected.");
        }

        private void ApproveSteamCandidate(string category)
        {
            if (_steamCandidate == null || _steamDraft == null) return;
            _steamDraft.Approved.Add(new SteamApproval
            {
                Id = _steamCandidate.Id, Title = _steamCandidate.Name, Category = category
            });
            _steamCandidate = null;
            ShowOverlay(Overlay.SteamPreview);
        }

        private void SearchSteamCandidates()
        {
            var input = Prompt("Find a game", "Search the unmatched Steam titles by name or App ID.",
                _candidateSearch, "Game name or App ID", null, "Search");
            if (input == null) return;
            _candidateSearch = input.Item1.Trim();
            _previewPage = 0;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private string PrimaryGameLabel(Game game)
        {
            if (game.Installed) return "Play Game";
            if (game.CanInstallViaSteam) return "Install via Steam";
            return game.IsSteam && !game.Manual ? "View on Steam" : "Locate Game";
        }

        private void PrimaryGameAction(Game game)
        {
            if (game.Installed) LaunchGame(game);
            else if (game.CanInstallViaSteam)
            {
                if (!HasSteamClient())
                {
                    _message = "Install the Steam desktop client, then try again.";
                    Draw(redrawBody: false);
                    return;
                }
                _awaitingSteamInstall = OpenGameLink("steam://install/" + game.Id);
            }
            else if (game.IsSteam && !game.Manual) OpenSteamStore(game);
            else AddGame(game);
        }

        private static bool HasSteamClient()
        {
            return GameLibrary.SteamRoots().Any(root => File.Exists(Path.Combine(root, "steam.exe")));
        }

        private void OpenSteamStore(Game game)
        {
            OpenGameLink("https://store.steampowered.com/app/" + game.Id + "/");
        }

        private bool OpenGameLink(string url)
        {
            if (TryOpenGameLink(url)) return true;
            _message = "Windows could not open this link.";
            Draw(redrawBody: false);
            FocusOverlay();
            return false;
        }

        private static bool TryOpenGameLink(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            bool steamAction = uri.Scheme == "steam" && uri.Host == "install" &&
                SteamServices.IsAppId(uri.AbsolutePath.Trim('/')) && String.IsNullOrEmpty(uri.Query) &&
                String.IsNullOrEmpty(uri.Fragment) && String.IsNullOrEmpty(uri.UserInfo);
            if (uri.Scheme != Uri.UriSchemeHttps && !steamAction) return false;
            try
            {
                using (Process process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })) { }
                return true;
            }
            catch (Win32Exception) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private Tuple<string, string> Prompt(string title, string guidance, string current,
            string textLabel, string secretLabel, string action)
        {
            var dialog = new Window
            {
                Owner = this, Title = "GridLine / " + title, Icon = Icon, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 530, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false, Background = _panel, Foreground = _ink, FontSize = 16
            };
            var stack = new StackPanel { Margin = new Thickness(26) };
            dialog.Content = stack;
            stack.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.Bold });
            stack.Children.Add(new TextBlock
            {
                Text = guidance, Foreground = _muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16)
            });
            var text = new TextBox { Text = current ?? String.Empty, MaxLength = 160, Margin = new Thickness(0, 5, 0, 14), MinHeight = 28 };
            if (textLabel != null)
            {
                stack.Children.Add(new TextBlock { Text = textLabel });
                AutomationProperties.SetName(text, textLabel);
                stack.Children.Add(text);
            }
            var password = new PasswordBox { MaxLength = 160, Margin = new Thickness(0, 5, 0, 14), MinHeight = 28 };
            if (secretLabel != null)
            {
                stack.Children.Add(new TextBlock { Text = secretLabel });
                AutomationProperties.SetName(password, secretLabel);
                stack.Children.Add(password);
            }
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var accept = new Button { Content = action, IsDefault = true, Padding = new Thickness(14, 6, 14, 6), Background = _accent };
            accept.Click += delegate { dialog.DialogResult = true; };
            buttons.Children.Add(accept);
            buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(10, 0, 0, 0) });
            stack.Children.Add(buttons);
            dialog.Loaded += delegate
            {
                if (textLabel == null) password.Focus();
                else { text.Focus(); text.SelectAll(); }
            };
            bool accepted = dialog.ShowDialog() == true;
            var result = accepted ? Tuple.Create(text.Text, password.Password) : null;
            password.Clear();
            return result;
        }

        private void DrawSteamSettings()
        {
            Label("STEAM LIBRARY", new Rect(478, 193, 705, 68), 52, _ink, true);
            string state = _steamState.Connected ? "CONNECTED" :
                _steamState.SyncedUtc == default(DateTime) ? "LOCAL DISCOVERY" : "OFFLINE SNAPSHOT";
            Label(state, new Rect(481, 270, 695, 30), 19, _accent, false);
            Label(_steamState.SyncedUtc == default(DateTime) ? "Connect Steam to preview owned driving games." :
                "Last sync " + _steamState.SyncedUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture) +
                "  /  " + _steamState.OwnedIds.Count + " Steam titles checked.",
                new Rect(481, 306, 700, 38), 24, _ink, false);
            Label("Only approved driving titles appear. Hidden games stay hidden after sync.",
                new Rect(481, 353, 700, 68), 22, _muted, false, TextAlignment.Left, TextWrapping.Wrap);
            bool ready = _steamRequest == null && !_scanning;
            ButtonShape(_steamState.Connected ? "Sync Owned Games" : "Connect Steam", new Rect(479, 435, 342, 59),
                true, ready, () => RequestSteamSync(false), 25);
            ButtonShape(_steamState.Connected ? "Change Account" : "Get a Steam API Key", new Rect(853, 435, 343, 59),
                false, ready, () =>
                {
                    if (_steamState.Connected) RequestSteamSync(true);
                    else OpenGameLink("https://steamcommunity.com/dev/apikey");
                }, 24);
            if (_steamState.Connected || _steamState.SyncedUtc != default(DateTime))
                ButtonShape(_steamState.Connected ? "Disconnect Steam" : "Clear Cached Sync", new Rect(479, 513, 342, 58),
                    false, ready, DisconnectSteam, 24);
            ButtonShape("Back", new Rect(479, 686, 342, 63), false, true, Back, 28);
        }

        private void DrawSteamPreview()
        {
            if (_steamDraft == null || _steamPreview == null) return;
            var known = KnownSteamIds();
            int matched = _steamPreview.Count(entry => known.Contains(entry.Id));
            var results = _steamPreview.Where(entry => known.Contains(entry.Id) != _showUnmatched &&
                (!_showUnmatched || entry.Name.IndexOf(_candidateSearch, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    entry.Id.Contains(_candidateSearch))).ToList();
            const int pageSize = 4;
            int lastPage = Math.Max(0, (results.Count - 1) / pageSize);
            _previewPage = Math.Min(_previewPage, lastPage);
            Label("STEAM IMPORT", new Rect(478, 186, 705, 65), 48, _ink, true);
            Label(matched + " driving games matched  /  " + (_steamPreview.Count - matched) + " unmatched",
                new Rect(481, 256, 703, 35), 21, _muted, false);
            ButtonShape("Matched", new Rect(479, 300, 221, 42), !_showUnmatched, true, () => ChangePreviewList(false), 21);
            ButtonShape("Review Unmatched", new Rect(719, 300, 257, 42), _showUnmatched, true, () => ChangePreviewList(true), 21);
            ButtonShape("Search", new Rect(991, 300, 205, 42), false, _showUnmatched, SearchSteamCandidates, 21);
            if (_showUnmatched && !String.IsNullOrWhiteSpace(_candidateSearch))
                Label("Search  /  " + _candidateSearch, new Rect(481, 348, 705, 27), 17, _accent, false);
            if (results.Count == 0)
                Label(_showUnmatched ? "No titles match this search." : "No known driving games matched. Review unmatched titles.",
                    new Rect(481, 397, 700, 46), 22, _muted, false);
            int start = _previewPage * pageSize;
            for (int index = 0; index < pageSize && start + index < results.Count; index++)
            {
                var entry = results[start + index];
                int top = 380 + index * 48;
                string title = entry.Name + (_settings.HiddenGameIds.Contains(entry.Id) ? "  /  Hidden on rig" : "");
                Label(title, new Rect(481, top, _showUnmatched ? 500 : 695, 41), 23, _ink, false);
                if (_showUnmatched)
                    ButtonShape("Classify", new Rect(1006, top, 190, 40), false, true,
                        () => { _steamCandidate = entry; ShowOverlay(Overlay.SteamCategory); }, 20, "Classify " + entry.Name);
            }
            if (lastPage > 0)
            {
                ButtonShape("<", new Rect(970, 585, 45, 35), false, _previewPage > 0, () => ChangePreviewPage(-1), 22);
                Label((_previewPage + 1) + " / " + (lastPage + 1), new Rect(1018, 585, 126, 35),
                    18, _muted, false, TextAlignment.Center);
                ButtonShape(">", new Rect(1151, 585, 45, 35), false, _previewPage < lastPage, () => ChangePreviewPage(1), 22);
            }
            ButtonShape("Back", new Rect(479, 686, 342, 63), false, true, Back, 28);
            ButtonShape("Apply Import", new Rect(853, 686, 343, 63), true, !_scanning, ApplySteamImport, 28);
        }

        private void ChangePreviewList(bool unmatched)
        {
            _showUnmatched = unmatched;
            _previewPage = 0;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void ChangePreviewPage(int change)
        {
            _previewPage += change;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void DrawSteamCategory()
        {
            if (_steamCandidate == null) return;
            Label("APPROVE A DRIVING GAME", new Rect(478, 193, 710, 65), 40, _ink, true);
            FittedTitle(_steamCandidate.Name, new Rect(481, 273, 707, 86), 36, 20);
            Label("Choose the category for this full game.", new Rect(481, 368, 700, 40), 23, _muted, false);
            for (int index = 0; index < DrivingCategories.Length; index++)
            {
                string category = DrivingCategories[index];
                ButtonShape(category, new Rect(479 + 247 * (index % 3), 426 + 72 * (index / 3), 221, 55),
                    false, true, () => ApproveSteamCandidate(category), 24);
            }
            ButtonShape("Back", new Rect(479, 686, 342, 63), false, true, Back, 28);
        }
    }
}
