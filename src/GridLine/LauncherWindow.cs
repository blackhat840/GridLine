using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GridLine
{
    internal sealed partial class LauncherWindow : Window
    {
        private enum Screen { Play, Library }
        private enum Overlay { None, Settings, Details, HiddenGames, Steam, SteamPreview, SteamCategory, Gallery, ArtPicker, LaunchSetup }

        private static readonly string[] Filters =
        {
            "All Sims", "Circuit", "Rally", "Arcade", "Trucking", "Bus & Transit", "Installed", "Owned", "Catalog"
        };

        private readonly Canvas _stage = new Canvas { Width = 1672, Height = 941, ClipToBounds = true };
        private readonly LauncherSettings _settings;
        private readonly string _settingsPath;
        private readonly Artwork _art = new Artwork();
        private readonly DispatcherTimer _padTimer;
        private readonly DispatcherTimer _headerSweepTimer;
        private List<Game> _games;
        private Screen _screen;
        private Overlay _overlay;
        private string _homeSelection = "805550";
        private string _librarySelection = "1849250";
        private int _filter;
        private int _hiddenPage;
        private bool _scanning;
        private string _message;
        private ushort _padButtons;
        private bool _padReady;
        private DateTime _lastLaunch;

        public LauncherWindow() : this(LauncherSettings.DefaultPath, GameLibrary.CreateCatalog(), true) { }

        internal LauncherWindow(string settingsPath, List<Game> games, bool discover)
        {
            _settingsPath = settingsPath;
            _settings = LauncherSettings.Load(settingsPath);
            _steamStatePath = Path.Combine(Path.GetDirectoryName(settingsPath), "steam.json");
            _steamState = SteamState.Load(_steamStatePath);
            _games = games;
            Title = "GridLine";
            Icon = Brand.WindowIcon();
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Background = Brushes.Black;
            Content = new Viewbox { Stretch = Stretch.Uniform, Child = _stage };
            KeyboardNavigation.SetTabNavigation(_overlayLayer, KeyboardNavigationMode.Cycle);
            PreviewKeyDown += HandleKey;
            PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs args)
            {
                if (_startupIntro != null || _overlay != Overlay.None) return;
                MoveSelection((args.Delta < 0 ? 1 : -1) * (_screen == Screen.Library ? 6 : 4));
                args.Handled = true;
            };
            _padTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            _padTimer.Tick += PollController;
            _headerSweepTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _headerSweepTimer.Tick += delegate { RunHeaderSweep(); };
            Closing += delegate { StopKioskHandoff(); };
            Closed += delegate { DiscardStartupIntro(); StopActiveEffects(); CancelProviderRequests(); };
            if (discover)
            {
                Activated += delegate
                {
                    BeginStartupIntro();
                    _padReady = false;
                    _padTimer.Start();
                    RunHeaderSweep();
                    _headerSweepTimer.Start();
                    UpdatePlayBackdrop();
                    if (_awaitingSteamInstall && !_scanning)
                    {
                        _awaitingSteamInstall = false;
                        Refresh();
                    }
                };
                Deactivated += delegate { PauseStartupIntro(); StopActiveEffects(); };
                StateChanged += delegate
                {
                    if (WindowState == WindowState.Minimized) PauseStartupIntro();
                    else BeginStartupIntro();
                };
                Loaded += delegate { BeginStartupIntro(); Refresh(true); };
            }
            Draw();
            if (discover) PrepareStartupIntro();
        }

        private List<Game> VisibleGames()
        {
            var games = _settings.HiddenGameIds.Count == 0 ? _games : _games.FindAll(IsGameVisible);
            if (_settings.InstalledOnly) games = games.FindAll(game => game.Installed);
            string filter = _screen == Screen.Play ? "All Sims" : Filters[_filter];
            if (filter == "Installed") return games.FindAll(game => game.Installed);
            if (filter == "Owned") return games.FindAll(game => game.Owned);
            if (filter == "Catalog") return games;
            if (_steamState.SyncedUtc != default(DateTime))
                games = games.FindAll(game => game.Installed || game.Owned || game.Manual ||
                    (_screen == Screen.Library && game.FreeToPlay) ||
                    (_screen == Screen.Play && IsFavorite(game)));
            return filter == "All Sims" ? games : games.FindAll(game => game.Category == filter);
        }

        private bool IsGameVisible(Game game)
        {
            return !_settings.HiddenGameIds.Contains(game.Id);
        }

        private bool IsFavorite(Game game)
        {
            return _settings.FavoriteGameIds.Contains(game.Id);
        }

        private void ToggleFavorite(Game game)
        {
            var favorites = _settings.FavoriteGameIds;
            bool added = !favorites.Remove(game.Id);
            if (added) favorites.Add(game.Id);
            if (SaveSettings())
                _message = added ? "Added to Play favorites." : "Removed from Play favorites.";
            else if (added) favorites.Remove(game.Id);
            else favorites.Add(game.Id);
            Draw();
            FocusOverlay();
        }

        private Game CurrentGame(List<Game> visible)
        {
            string id = _screen == Screen.Play ? _homeSelection : _librarySelection;
            return visible.Find(game => game.Id == id) ?? visible.FirstOrDefault();
        }

        private void SelectGame(Game game)
        {
            if (CurrentGame(VisibleGames()) == game) return;
            if (_screen == Screen.Play) _homeSelection = game.Id;
            else _librarySelection = game.Id;
            _message = null;
            Draw(Motion.Selection);
        }

        private void ShowScreen(Screen screen)
        {
            if (_screen == screen && _overlay == Overlay.None) return;
            LeaveOverlay(Overlay.None);
            _screen = screen;
            _overlay = Overlay.None;
            _message = null;
            Draw(Motion.Screen);
        }

        private void ChangeFilter(int filter)
        {
            int nextFilter = (filter + Filters.Length) % Filters.Length;
            if (_filter == nextFilter) return;
            _filter = nextFilter;
            _message = null;
            Draw(Motion.Screen);
        }

        private void MoveSelection(int delta)
        {
            var visible = VisibleGames();
            Game current = CurrentGame(visible);
            if (current == null) return;
            if (_screen == Screen.Play) visible = visible.FindAll(IsFavorite);
            if (visible.Count == 0) return;
            int index = visible.IndexOf(current);
            int target = index < 0 && Math.Abs(delta) == 1 ? 0 : Math.Max(0, index) + delta;
            int position = Math.Max(0, Math.Min(visible.Count - 1, target));
            if (visible[position] != current) SelectGame(visible[position]);
        }

        private void ShowOverlay(Overlay overlay)
        {
            LeaveOverlay(overlay);
            _overlay = overlay;
            if (overlay == Overlay.HiddenGames) _hiddenPage = 0;
            _message = null;
            Draw(overlay == Overlay.None ? Motion.None : Motion.Overlay, false);
            if (overlay != Overlay.None) FocusOverlay();
            if (overlay == Overlay.Details)
            {
                Game game = CurrentGame(VisibleGames());
                LoadRating(game);
                LoadMetadata(game);
            }
        }

        private void Back()
        {
            if (_overlay == Overlay.HiddenGames || _overlay == Overlay.Steam) ShowOverlay(Overlay.Settings);
            else if (_overlay == Overlay.SteamCategory) ShowOverlay(Overlay.SteamPreview);
            else if (_overlay == Overlay.SteamPreview) ShowOverlay(Overlay.Steam);
            else if (_overlay == Overlay.Gallery || _overlay == Overlay.ArtPicker || _overlay == Overlay.LaunchSetup) ShowOverlay(Overlay.Details);
            else if (_overlay != Overlay.None) ShowOverlay(Overlay.None);
            else if (_screen == Screen.Library) ShowScreen(Screen.Play);
        }

        private async void Refresh(bool initial = false, string note = null)
        {
            if (_scanning) return;
            _scanning = true;
            _message = "Searching for installed games...";
            Draw();

            try
            {
                var manual = _settings.Games.ToArray();
                var approved = _steamState.Approved.ToArray();
                var owned = _steamState.OwnedIds.ToArray();
                var result = await Task.Run(() =>
                {
                    var games = GameLibrary.Scan(manual, approved, owned);
                    return Tuple.Create(games, Artwork.ReadLocal(games, Path.GetDirectoryName(_settingsPath)));
                });
                _games = result.Item1;
                _art.UseLocal(result.Item2);
                if (initial && !_games.Any(game => game.Id == _homeSelection && game.Installed && IsGameVisible(game)))
                {
                    Game firstInstalled = _games.Find(game => game.Installed && IsGameVisible(game));
                    if (firstInstalled != null) _homeSelection = firstInstalled.Id;
                }
                int count = _games.Count(game => game.Installed && IsGameVisible(game));
                _message = count == 0 ? "No visible installed games. Add or restore a game in Settings."
                    : count + (count == 1 ? " installed game found." : " installed games found.");
            }
            catch (IOException) { _message = "Game discovery could not finish. Rescan in Settings."; }
            catch (UnauthorizedAccessException) { _message = "One or more install paths were inaccessible."; }
            catch (ArgumentException) { _message = "A game install path was invalid. Rescan in Settings."; }
            finally
            {
                _scanning = false;
                if (note != null) _message = note;
                Draw();
            }
        }

        private void LaunchCurrent()
        {
            Game game = CurrentGame(VisibleGames());
            if (game != null && !_scanning) PrimaryGameAction(game);
        }

        private void LaunchGame(Game game, GameLaunchApp alternate = null)
        {
            if (_scanning || (_kioskSession != null && _kioskSession.Covering) ||
                DateTime.UtcNow - _lastLaunch < TimeSpan.FromSeconds(2)) return;
            if (!game.Installed)
            {
                _message = "Game not detected. Use Locate Game in Game Details.";
                Draw(redrawBody: false);
                return;
            }

            try
            {
                var command = alternate == null ? game.GetLaunchCommand() : alternate.GetLaunchCommand();
                BeginKioskHandoff(game, alternate);
                var skipped = StartCompanions(game, command.FileName);
                using (Process process = Process.Start(command))
                    TrackKioskProcess(process, alternate == null && game.SteamExecutable != null);
                _lastLaunch = DateTime.UtcNow;
                _message = "Starting " + (alternate == null ? game.Title : alternate.Name) + "...";
                if (skipped.Count > 0) _message += " Could not start: " + String.Join(", ", skipped) + ".";
            }
            catch (IOException error) { StopKioskHandoff(); _message = error.Message; }
            catch (Win32Exception error) { StopKioskHandoff(); _message = "Could not start " + game.Title + ". " + error.Message; }
            catch (InvalidOperationException) { StopKioskHandoff(); _message = "Windows could not start " + game.Title + "."; }
            Draw(redrawBody: false);
        }

        private void AddGame(Game knownGame)
        {
            if (_scanning) return;
            var dialog = new OpenFileDialog
            {
                Title = knownGame == null ? "Add a game executable" : "Locate " + knownGame.Title,
                Filter = "Game executables (*.exe)|*.exe",
                CheckFileExists = true,
                Multiselect = false
            };
            if (knownGame != null && knownGame.Installed)
                dialog.InitialDirectory = knownGame.InstallPath;
            if (dialog.ShowDialog(this) != true) return;

            string file = dialog.FileName;
            string id = knownGame == null ? "manual-" + Guid.NewGuid().ToString("N") : knownGame.Id;
            ManualGame prior = _settings.Games.Find(game => game != null &&
                String.Equals(game.Executable, file, StringComparison.OrdinalIgnoreCase));
            if (knownGame == null && prior != null) id = prior.Id;

            string title = knownGame != null ? knownGame.Title : Path.GetFileNameWithoutExtension(file);
            if (knownGame == null)
            {
                try
                {
                    string product = FileVersionInfo.GetVersionInfo(file).ProductName;
                    if (!String.IsNullOrWhiteSpace(product)) title = product.Trim();
                }
                catch (IOException) { }
                catch (Win32Exception) { }
            }

            _settings.Games.RemoveAll(game => game != null && (game.Id == id ||
                String.Equals(game.Executable, file, StringComparison.OrdinalIgnoreCase)));
            _settings.Games.Add(new ManualGame
            {
                Id = id, Title = title, Executable = file,
                Category = knownGame == null ? "Arcade" : knownGame.Category
            });
            bool saved = SaveSettings();
            if (knownGame == null)
            {
                _screen = Screen.Library;
                _filter = 0;
                _librarySelection = id;
            }
            LeaveOverlay(Overlay.None);
            _overlay = Overlay.None;
            Refresh(false, saved ? null : "The game was added for this run. Windows could not save settings.");
        }

        private void SetGameHidden(Game game, bool hidden)
        {
            var hiddenIds = _settings.HiddenGameIds;
            if (hidden) hiddenIds.Add(game.Id);
            else hiddenIds.Remove(game.Id);

            if (SaveSettings())
            {
                _message = game.Title + (hidden ? " hidden. Restore it in Settings." : " restored.");
                if (hidden)
                {
                    LeaveOverlay(Overlay.None);
                    _overlay = Overlay.None;
                }
            }
            else if (hidden) hiddenIds.Remove(game.Id);
            else hiddenIds.Add(game.Id);

            Draw();
            if (_overlay != Overlay.None) FocusOverlay();
        }

        private bool SaveSettings()
        {
            try { _settings.Save(_settingsPath); return true; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (SerializationException) { }
            _message = "Windows could not save your settings.";
            return false;
        }

        private void ChangeTheme(string theme)
        {
            _settings.Theme = theme;
            SaveSettings();
            Draw();
        }

        private void HandleKey(object sender, KeyEventArgs args)
        {
            if (_startupIntro != null)
            {
                args.Handled = true;
                if (!args.IsRepeat && (args.Key == Key.Escape || args.Key == Key.Enter || args.Key == Key.Space))
                    EndStartupIntro();
                return;
            }
            if (args.Key == Key.Enter)
            {
                args.Handled = args.IsRepeat || ActivateFocusedButton() || HandleNavigation(args.Key);
                return;
            }
            args.Handled = HandleNavigation(args.Key);
        }

        private static bool ActivateFocusedButton()
        {
            var button = Keyboard.FocusedElement as Button;
            if (button == null || !button.IsEnabled) return false;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
            return true;
        }

        private bool HandleNavigation(Key key)
        {
            if (key == Key.Escape && _kioskSession != null && _kioskSession.Covering)
            {
                StopKioskHandoff();
                _message = null;
                Draw(redrawBody: false);
                return true;
            }
            if (key == Key.Escape) { Back(); return true; }
            if (key == Key.F1) { ShowOverlay(Overlay.Settings); return true; }
            if (_overlay != Overlay.None)
            {
                if (key == Key.Enter) return ActivateFocusedButton();
                if (key == Key.Left || key == Key.Up)
                    return MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
                if (key == Key.Right || key == Key.Down)
                    return MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                return false;
            }

            switch (key)
            {
                case Key.Enter: LaunchCurrent(); return true;
                case Key.Left: MoveSelection(-1); return true;
                case Key.Right: MoveSelection(1); return true;
                case Key.Up: MoveSelection(_screen == Screen.Library ? -3 : -4); return true;
                case Key.Down: MoveSelection(_screen == Screen.Library ? 3 : 4); return true;
                case Key.PageUp:
                    if (_screen == Screen.Library) ChangeFilter(_filter - 1);
                    else ShowScreen(Screen.Library);
                    return true;
                case Key.PageDown:
                    if (_screen == Screen.Library) ChangeFilter(_filter + 1);
                    else ShowScreen(Screen.Library);
                    return true;
                case Key.Y:
                    if (CurrentGame(VisibleGames()) != null) ShowOverlay(Overlay.Details);
                    return true;
            }
            return false;
        }

        private void StopActiveEffects()
        {
            _padTimer.Stop();
            _headerSweepTimer.Stop();
            StopHeaderSweep();
            SetPlayBackdropMotion(false);
        }

        private void PollController(object sender, EventArgs args)
        {
            if (!IsActive) return;
            ushort current = 0;
            try
            {
                for (uint index = 0; index < 4; index++)
                {
                    XInputState state;
                    if (XInputGetState(index, out state) != 0) continue;
                    current = state.Buttons;
                    break;
                }
            }
            catch (DllNotFoundException) { _padTimer.Stop(); return; }
            catch (EntryPointNotFoundException) { _padTimer.Stop(); return; }

            if (!_padReady)
            {
                _padReady = true;
                _padButtons = current;
                return;
            }
            int pressed = current & ~_padButtons;
            _padButtons = current;
            if (_startupIntro != null)
            {
                if ((pressed & 0x3010) != 0) EndStartupIntro();
                return;
            }
            if ((pressed & 0x2000) != 0) HandleNavigation(Key.Escape);
            else if ((pressed & 0x0010) != 0) HandleNavigation(Key.F1);
            else if ((pressed & 0x1000) != 0) HandleNavigation(Key.Enter);
            else if ((pressed & 0x8000) != 0) HandleNavigation(Key.Y);
            else if ((pressed & 0x0001) != 0) HandleNavigation(Key.Up);
            else if ((pressed & 0x0002) != 0) HandleNavigation(Key.Down);
            else if ((pressed & 0x0004) != 0) HandleNavigation(Key.Left);
            else if ((pressed & 0x0008) != 0) HandleNavigation(Key.Right);
            else if ((pressed & 0x0100) != 0) HandleNavigation(Key.PageUp);
            else if ((pressed & 0x0200) != 0) HandleNavigation(Key.PageDown);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint Packet;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short LeftX, LeftY, RightX, RightY;
        }

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputGetState(uint index, out XInputState state);
    }
}
