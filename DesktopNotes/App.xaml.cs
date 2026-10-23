using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace DesktopNotes
{
    public partial class App : WpfApplication
    {
        private TrayService? _trayService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), args.ExceptionObject.ToString());
                }
                catch { }
            };

            this.DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), args.Exception.ToString());
                }
                catch { }
                args.Handled = true;
            };

            // Инициализируем системный трей Windows
            _trayService = new TrayService();
            _trayService.OnNewNoteRequested += () => Dispatcher.Invoke(() => NoteManager.Instance.CreateNewEmptyWindow());
            _trayService.OnOpenFileRequested += () => Dispatcher.Invoke(() => NoteManager.Instance.OpenFilePicker());
            _trayService.OnToggleVisibilityRequested += (visible) => Dispatcher.Invoke(() => NoteManager.Instance.SetAllVisible(visible));
            _trayService.Initialize();

            // Инициализируем окна заметок
            NoteManager.Instance.Initialize();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                NoteManager.Instance.SaveState();
                _trayService?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }
    }
}
