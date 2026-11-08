using System;
using System.Windows;

namespace GridLine
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var app = new Application();
            app.Run(new LauncherWindow());
        }
    }
}
