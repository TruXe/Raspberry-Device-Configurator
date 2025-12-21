using System;

namespace DeviceConfigurator
{
    /// <summary>
    /// Statická třída pro debug logování.
    /// </summary>
    public static class DebugLogger
    {
        private static DebugConsole? _console;
        private static bool _enabled = true;

        /// <summary>
        /// Zobrazí nebo skryje debug konzoli.
        /// </summary>
        public static void ShowConsole()
        {
            if (_console == null)
            {
                _console = new DebugConsole();
            }
            _console.Show();
            _console.BringToFront();
        }

        /// <summary>
        /// Skryje debug konzoli.
        /// </summary>
        public static void HideConsole()
        {
            _console?.Hide();
        }

        /// <summary>
        /// Zapne nebo vypne debug logování.
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        /// <summary>
        /// Přidá debug zprávu.
        /// </summary>
        public static void Log(string message)
        {
            if (!_enabled)
                return;

            // Zápis do Debug okna Visual Studio
            System.Diagnostics.Debug.WriteLine(message);

            // Zápis do debug konzole
            if (_console != null && !_console.IsDisposed)
            {
                _console.AddMessage(message);
            }
        }

        /// <summary>
        /// Přidá debug zprávu s formátováním.
        /// </summary>
        public static void Log(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }
    }
}

