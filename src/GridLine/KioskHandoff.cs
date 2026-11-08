using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace GridLine
{
    internal sealed class KioskLaunchSession
    {
        private readonly string _gameExecutable;
        private readonly string _alternateExecutable;
        private readonly string _gameFolder;
        private readonly string _steamExecutable;
        private readonly HashSet<uint> _preexisting;
        private readonly HashSet<uint> _family = new HashSet<uint>();
        private readonly Dictionary<uint, string> _paths = new Dictionary<uint, string>();
        private readonly Dictionary<IntPtr, uint> _targetWindows = new Dictionary<IntPtr, uint>();
        private readonly HashSet<IntPtr> _handedOff = new HashSet<IntPtr>();

        internal readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        internal readonly bool WasTopmost;
        internal bool Covering = true;
        internal double? LastTargetSeen;
        internal bool GameProcessAlive, LauncherProcessAlive;
        internal IntPtr LastSessionForeground;

        internal KioskLaunchSession(Game game, GameLaunchApp alternate, bool wasTopmost)
        {
            _gameExecutable = game.Executable;
            _alternateExecutable = alternate == null ? null : alternate.Executable;
            _steamExecutable = game.SteamExecutable;
            if (!String.IsNullOrEmpty(game.InstallPath))
                _gameFolder = game.InstallPath.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            WasTopmost = wasTopmost;
            _preexisting = new HashSet<uint>(KioskWindows.Processes().Select(entry => entry.Key));
        }

        // Launchers and games started directly by GridLine; their child processes count as part of the session.
        internal void AddRoot(int processId)
        {
            _family.Add((uint)processId);
        }

        internal bool IsSteamExecutable(string file)
        {
            return !String.IsNullOrEmpty(file) && SameExecutable(file, _steamExecutable);
        }

        internal bool IsTargetWindow(IntPtr window)
        {
            return window != IntPtr.Zero && _targetWindows.ContainsKey(window);
        }

        // Refreshes target processes and windows. Returns windows seen for the first time.
        internal List<IntPtr> Refresh(uint ownProcess)
        {
            var parents = KioskWindows.Processes();
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var entry in parents)
                    if (!_preexisting.Contains(entry.Key) && _family.Contains(entry.Value) && _family.Add(entry.Key)) grew = true;
            }
            foreach (uint gone in _paths.Keys.Where(id => !parents.ContainsKey(id)).ToList()) _paths.Remove(gone);
            _family.IntersectWith(parents.Keys);

            GameProcessAlive = LauncherProcessAlive = false;
            var targets = new HashSet<uint>();
            foreach (uint processId in parents.Keys)
            {
                if (processId == ownProcess) continue;
                string path = PathOf(processId);
                bool game = IsGameExecutable(path);
                if (!game && !_family.Contains(processId) && !SameExecutable(path, _alternateExecutable)) continue;
                targets.Add(processId);
                if (game) GameProcessAlive = true;
                else LauncherProcessAlive = true;
            }

            var fresh = new List<IntPtr>();
            foreach (var window in KioskWindows.TopLevelWindows())
            {
                uint processId = KioskWindows.ProcessId(window);
                if (!targets.Contains(processId) || _targetWindows.ContainsKey(window)) continue;
                _targetWindows[window] = processId;
                fresh.Add(window);
            }
            foreach (var pair in _targetWindows.ToArray())
                if (!KioskWindows.IsWindowVisible(pair.Key) || KioskWindows.ProcessId(pair.Key) != pair.Value)
                    _targetWindows.Remove(pair.Key);
            return fresh;
        }

        internal bool HasOpenTargetWindow() { return _targetWindows.Count > 0; }

        internal IntPtr NewestTargetWindow() { return _targetWindows.Keys.LastOrDefault(); }

        // Gives a new target window the foreground once, so Windows' foreground lock cannot strand it behind GridLine.
        internal bool ShouldHandOff(IntPtr window)
        {
            return _handedOff.Add(window);
        }

        private string PathOf(uint processId)
        {
            string path;
            if (!_paths.TryGetValue(processId, out path))
                _paths[processId] = path = KioskWindows.Executable(processId);
            return path;
        }

        private bool IsGameExecutable(string file)
        {
            return !String.IsNullOrEmpty(file) && !IsSteamExecutable(file) &&
                (SameExecutable(file, _gameExecutable) ||
                    (_gameFolder != null && file.StartsWith(_gameFolder, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool SameExecutable(string first, string second)
        {
            return first != null && String.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class KioskWindows
    {
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int capacity);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size, Usage, ProcessId;
            public IntPtr HeapId;
            public uint ModuleId, Threads, ParentProcessId;
            public int BasePriority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);

        internal static bool IsDesktop(IntPtr window)
        {
            if (window == IntPtr.Zero || window == GetShellWindow()) return true;
            var name = new StringBuilder(80);
            GetClassName(window, name, name.Capacity);
            string windowClass = name.ToString();
            return windowClass == "Progman" || windowClass == "WorkerW" ||
                windowClass == "Shell_TrayWnd" || windowClass == "Shell_SecondaryTrayWnd";
        }

        internal static bool Exists(IntPtr window)
        {
            return window != IntPtr.Zero && IsWindow(window) && IsWindowVisible(window);
        }

        internal static uint ProcessId(IntPtr window)
        {
            uint processId;
            GetWindowThreadProcessId(window, out processId);
            return processId;
        }

        internal static string Executable(uint processId)
        {
            if (processId == 0) return null;
            IntPtr process = OpenProcess(0x1000, false, processId);
            if (process == IntPtr.Zero) return null;
            try
            {
                var name = new StringBuilder(1024);
                uint size = (uint)name.Capacity;
                return QueryFullProcessImageName(process, 0, name, ref size) ? name.ToString() : null;
            }
            finally { CloseHandle(process); }
        }

        // Process ID to parent process ID for every running process.
        internal static Dictionary<uint, uint> Processes()
        {
            var result = new Dictionary<uint, uint>();
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1)) return result;
            try
            {
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                for (bool more = Process32First(snapshot, ref entry); more; more = Process32Next(snapshot, ref entry))
                    result[entry.ProcessId] = entry.ParentProcessId;
            }
            finally { CloseHandle(snapshot); }
            return result;
        }

        // Visible, unowned, uncloaked top-level windows in Z order.
        internal static List<IntPtr> TopLevelWindows()
        {
            var windows = new List<IntPtr>();
            EnumWindows((window, parameter) =>
            {
                int cloaked;
                if (IsWindowVisible(window) && GetWindow(window, 4) == IntPtr.Zero &&
                    !(DwmGetWindowAttribute(window, 14, out cloaked, 4) == 0 && cloaked != 0))
                    windows.Add(window);
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        // Moves the foreground to a window when GridLine owns the foreground or a session window just closed.
        // Windows can still refuse.
        internal static bool Bring(IntPtr window)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == window) return true;
            uint ignored;
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out ignored);
            uint thread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != thread && AttachThreadInput(thread, foregroundThread, true);
            try
            {
                BringWindowToTop(window);
                SetForegroundWindow(window);
            }
            finally { if (attached) AttachThreadInput(thread, foregroundThread, false); }
            return GetForegroundWindow() == window;
        }
    }

    internal sealed partial class LauncherWindow
    {
        private KioskLaunchSession _kioskSession;
        private DispatcherTimer _kioskTimer;
        private IntPtr _kioskWindowHandle;
        private static readonly uint KioskProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        private void ToggleKioskHandoff()
        {
            _settings.KioskHandoff = !_settings.KioskHandoff;
            if (!SaveSettings()) _settings.KioskHandoff = !_settings.KioskHandoff;
            else
            {
                if (!_settings.KioskHandoff) StopKioskHandoff();
                _message = _settings.KioskHandoff ? "GridLine will cover the desktop during game handoffs." : "Kiosk Handoff turned off.";
            }
            Draw(redrawBody: false);
            FocusOverlay();
        }

        private void BeginKioskHandoff(Game game, GameLaunchApp alternate)
        {
            if (!_settings.KioskHandoff || !IsActive) return;
            StopKioskHandoff();
            _kioskWindowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (_kioskWindowHandle == IntPtr.Zero) return;
            _kioskSession = new KioskLaunchSession(game, alternate, Topmost);
            _kioskSession.LastSessionForeground = _kioskWindowHandle;
            WindowState = WindowState.Maximized;
            Topmost = true;
            _kioskTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(175) };
            _kioskTimer.Tick += WatchKioskHandoff;
            _kioskTimer.Start();
        }

        // Steam's own client is not tracked by process tree; its game is matched by install folder instead.
        private void TrackKioskProcess(System.Diagnostics.Process process, bool steam)
        {
            if (_kioskSession != null && process != null && !steam) _kioskSession.AddRoot(process.Id);
        }

        private void WatchKioskHandoff(object sender, EventArgs args)
        {
            var session = _kioskSession;
            if (session == null) return;
            if (!IsVisible || WindowState == WindowState.Minimized)
            {
                StopKioskHandoff();
                return;
            }

            double seconds = session.Clock.Elapsed.TotalSeconds;
            var fresh = session.Refresh(KioskProcessId);
            IntPtr foreground = KioskWindows.GetForegroundWindow();
            bool ours = foreground == _kioskWindowHandle || session.IsTargetWindow(foreground);
            bool desktop = !ours && KioskWindows.IsDesktop(foreground);
            string foregroundFile = ours || desktop ? null : KioskWindows.Executable(KioskWindows.ProcessId(foreground));
            bool steam = session.IsSteamExecutable(foregroundFile);

            // Windows moved focus elsewhere because the session window in front closed, not because the user switched.
            IntPtr last = session.LastSessionForeground;
            bool closedAway = !ours && last != IntPtr.Zero && last != _kioskWindowHandle && !KioskWindows.Exists(last);

            foreach (IntPtr window in fresh)
            {
                if (!session.ShouldHandOff(window) || (session.IsTargetWindow(foreground) && foreground != window)) continue;
                if ((foreground == _kioskWindowHandle || desktop || steam || closedAway) && KioskWindows.Bring(window))
                {
                    foreground = window;
                    ours = true;
                    closedAway = false;
                }
            }

            if (session.Covering && ((foreground != _kioskWindowHandle && !desktop) || seconds >= 8))
                ReleaseKioskCover(session);

            bool active = session.HasOpenTargetWindow() || session.GameProcessAlive;
            if (active) session.LastTargetSeen = seconds;
            double grace = session.LauncherProcessAlive ? 20 : 2;
            if (!active && session.LastTargetSeen.HasValue && seconds - session.LastTargetSeen.Value >= grace)
            {
                StopKioskHandoff();
                if (foreground == _kioskWindowHandle || desktop || steam || closedAway) KioskWindows.Bring(_kioskWindowHandle);
                if (KioskWindows.GetForegroundWindow() == _kioskWindowHandle) Activate();
                return;
            }
            if (!active && !session.LastTargetSeen.HasValue && seconds >= (session.LauncherProcessAlive ? 300 : 60))
            {
                StopKioskHandoff();
                return;
            }

            if (closedAway && !steam)
            {
                IntPtr target = session.HasOpenTargetWindow() ? session.NewestTargetWindow() : _kioskWindowHandle;
                if (KioskWindows.Bring(target)) { foreground = target; ours = true; }
            }
            if (ours) session.LastSessionForeground = foreground;
            else if (!desktop && !steam && !closedAway) session.LastSessionForeground = IntPtr.Zero;
        }

        private void ReleaseKioskCover(KioskLaunchSession session)
        {
            if (!session.Covering) return;
            session.Covering = false;
            Topmost = session.WasTopmost;
        }

        private void StopKioskHandoff()
        {
            if (_kioskTimer != null)
            {
                _kioskTimer.Stop();
                _kioskTimer.Tick -= WatchKioskHandoff;
                _kioskTimer = null;
            }
            if (_kioskSession != null)
            {
                ReleaseKioskCover(_kioskSession);
                _kioskSession = null;
            }
        }
    }
}
