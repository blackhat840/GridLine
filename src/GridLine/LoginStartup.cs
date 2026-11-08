using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace GridLine
{
    internal static class LoginStartup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string EntryName = "GridLine";

        internal static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(EntryName) != null;
        }

        internal static void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
                    if (key != null) key.DeleteValue(EntryName, throwOnMissingValue: false);
                return;
            }

            string command;
            using (var process = Process.GetCurrentProcess()) command = BuildCommand(process.MainModule.FileName);
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                key.SetValue(EntryName, command, RegistryValueKind.String);
        }

        internal static string BuildCommand(string executable)
        {
            string command = "\"" + executable + "\"";
            if (command.Length > 260)
                throw new IOException("Move GridLine to a shorter folder path before enabling Start at Login.");
            return command;
        }
    }

    internal sealed partial class LauncherWindow
    {
        private bool ReadLoginStartup()
        {
            try { return LoginStartup.IsEnabled(); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (SecurityException) { return false; }
        }

        private void ToggleLoginStartup()
        {
            try
            {
                bool enabled = !LoginStartup.IsEnabled();
                LoginStartup.SetEnabled(enabled);
                _message = enabled ? "GridLine will start when you sign in to Windows." : "Start at Login turned off.";
            }
            catch (IOException error) { _message = error.Message; }
            catch (UnauthorizedAccessException) { _message = "Windows denied access to the startup setting."; }
            catch (SecurityException) { _message = "Windows denied access to the startup setting."; }
            Draw(redrawBody: false);
            FocusOverlay();
        }
    }
}
