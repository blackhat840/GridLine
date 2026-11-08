using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using Microsoft.Win32;

namespace GridLine
{
    [DataContract]
    internal sealed class GameLaunchApp
    {
        [DataMember] public string GameId;
        [DataMember] public string Name;
        [DataMember] public string Executable;
        [DataMember] public bool Alternate;

        internal ProcessStartInfo GetLaunchCommand()
        {
            if (String.IsNullOrEmpty(Executable) || !Path.IsPathRooted(Executable) ||
                !String.Equals(Path.GetExtension(Executable), ".exe", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(Executable))
                throw new IOException(Name + " executable is unavailable. Check Launch Setup.");

            return new ProcessStartInfo(Executable)
            {
                WorkingDirectory = Path.GetDirectoryName(Executable),
                UseShellExecute = true
            };
        }

        internal static bool IsRunning(string executable)
        {
            var candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable));
            try
            {
                foreach (var process in candidates)
                {
                    try
                    {
                        if (String.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                    catch (NotSupportedException) { }
                }
                return false;
            }
            finally
            {
                foreach (var process in candidates) process.Dispose();
            }
        }
    }

    internal sealed partial class LauncherWindow
    {
        private int _launchSetupPage;

        private List<GameLaunchApp> GetLaunchApps(Game game)
        {
            return _settings.LaunchApps.FindAll(app => app != null && app.GameId == game.Id &&
                !String.IsNullOrWhiteSpace(app.Name) && !String.IsNullOrWhiteSpace(app.Executable));
        }

        private List<string> StartCompanions(Game game, string primaryExecutable)
        {
            var skipped = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryExecutable };
            foreach (var app in GetLaunchApps(game))
            {
                if (app.Alternate || !seen.Add(app.Executable)) continue;
                try
                {
                    var command = app.GetLaunchCommand();
                    if (!GameLaunchApp.IsRunning(command.FileName))
                        using (Process process = Process.Start(command)) { }
                }
                catch (IOException) { skipped.Add(app.Name); }
                catch (ArgumentException) { skipped.Add(app.Name); }
                catch (Win32Exception) { skipped.Add(app.Name); }
                catch (InvalidOperationException) { skipped.Add(app.Name); }
            }
            return skipped;
        }

        private void OpenLaunchSetup()
        {
            _launchSetupPage = 0;
            ShowOverlay(Overlay.LaunchSetup);
        }

        private void DrawLaunchSetup(Game game)
        {
            DrawWidePanel();
            Label("LAUNCH SETUP", new Rect(234, 142, 660, 64), 46, _ink, true);
            Label(game.Title, new Rect(236, 205, 1160, 34), 21, _accent, false);
            DetailButton("Add Companion", 234, 256, true, true, null, () => AddLaunchApp(game, false));
            DetailButton("Add Launcher", 536, 256, false, true, null, () => AddLaunchApp(game, true));
            if (game.Id == "244210")
            {
                DetailButton("Set Up No Hesi", 838, 256, false, true, null, () => AddLaunchApp(game, true, "No Hesi"));
                DetailButton("Content Manager", 1140, 256, false, true, null,
                    () => AddLaunchApp(game, true, "Content Manager"));
            }
            Label("Companions start before the selected game or launcher. Alternate launchers start in place of the game.",
                new Rect(236, 317, 1185, 35), 19, _muted, false);

            var apps = GetLaunchApps(game);
            const int pageSize = 5;
            int lastPage = Math.Max(0, (apps.Count - 1) / pageSize);
            _launchSetupPage = Math.Min(_launchSetupPage, lastPage);
            int start = _launchSetupPage * pageSize;
            if (apps.Count == 0)
                Label("No companion apps or alternate launchers have been added for this game.",
                    new Rect(236, 420, 1185, 44), 24, _muted, false);
            for (int i = start; i < Math.Min(start + pageSize, apps.Count); i++)
            {
                GameLaunchApp app = apps[i];
                int y = 366 + (i - start) * 58;
                Label(app.Alternate ? "LAUNCHER" : "COMPANION", new Rect(237, y, 154, 42), 16, _accent, false);
                Label(app.Name, new Rect(401, y, 530, 42), 22, _ink, false);
                if (app.Alternate)
                    ButtonShape("Start", new Rect(966, y, 205, 43), true, game.Installed && !_scanning,
                        () => LaunchGame(game, app), 20, "Start " + app.Name);
                ButtonShape("Remove", new Rect(1200, y, 223, 43), false, true,
                    () => RemoveLaunchApp(app), 20, "Remove " + app.Name);
            }
            DetailButton("Back", 234, 717, false, true, null, Back);
            if (lastPage > 0)
            {
                DetailButton("Previous", 536, 717, false, _launchSetupPage > 0, null,
                    () => MoveLaunchSetupPage(-1));
                DetailButton("Next", 838, 717, false, _launchSetupPage < lastPage, null,
                    () => MoveLaunchSetupPage(1));
            }
            if (game.Id == "244210")
                DetailButton("No Hesi Website", 1140, 717, false, true, Brand.External,
                    () => OpenGameLink("https://nohesi.gg/launcher"));
            DrawWideMessage();
        }

        private void MoveLaunchSetupPage(int delta)
        {
            _launchSetupPage += delta;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void AddLaunchApp(Game game, bool alternate, string name = null)
        {
            var dialog = new OpenFileDialog
            {
                Title = name != null ? "Locate the " + name + " executable" :
                    alternate ? "Choose an alternate launcher" : "Choose a companion app",
                Filter = "Applications (*.exe)|*.exe",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;
            string file = dialog.FileName;
            name = name ?? Path.GetFileNameWithoutExtension(file);
            var apps = _settings.LaunchApps;
            int existing = apps.FindIndex(app => app != null && app.GameId == game.Id && app.Alternate == alternate &&
                (String.Equals(app.Executable, file, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(app.Name, name, StringComparison.OrdinalIgnoreCase)));
            var entry = new GameLaunchApp { GameId = game.Id, Name = name, Executable = file, Alternate = alternate };
            var old = existing < 0 ? null : apps[existing];
            if (old == null) apps.Add(entry);
            else apps[existing] = entry;

            if (SaveSettings())
            {
                _message = name + " saved for " + game.Title + ".";
                _launchSetupPage = GetLaunchApps(game).IndexOf(entry) / 5;
            }
            else if (old == null) apps.Remove(entry);
            else apps[existing] = old;
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void RemoveLaunchApp(GameLaunchApp app)
        {
            int index = _settings.LaunchApps.IndexOf(app);
            if (index < 0) return;
            _settings.LaunchApps.RemoveAt(index);
            if (SaveSettings()) _message = app.Name + " removed.";
            else _settings.LaunchApps.Insert(index, app);
            Draw(redrawBody: false);
            FocusOverlay();
        }
    }
}
