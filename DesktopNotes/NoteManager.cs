using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace DesktopNotes
{
    public class NoteManager
    {
        private static NoteManager? _instance;
        public static NoteManager Instance => _instance ??= new NoteManager();

        private readonly List<MainWindow> _activeWindows = new List<MainWindow>();

        public IReadOnlyList<MainWindow> ActiveWindows => _activeWindows;

        public void Initialize()
        {
            var config = AppConfig.Load();
            LocalizationManager.SetLanguage(config.Language);

            if (config.Notes == null || config.Notes.Count == 0)
            {
                config.Notes = new List<NoteWindowState> { new NoteWindowState() };
            }

            foreach (var noteState in config.Notes)
            {
                OpenNoteWindow(noteState);
            }
        }

        public MainWindow OpenNoteWindow(NoteWindowState state)
        {
            var win = new MainWindow(state);
            _activeWindows.Add(win);

            win.Closed += (s, e) =>
            {
                _activeWindows.Remove(win);
                SaveState();
            };

            win.Show();
            return win;
        }

        /// <summary>
        /// Создает просто чистое новое окно БЕЗ создания мусорных файлов на диске!
        /// </summary>
        public MainWindow CreateNewEmptyWindow()
        {
            // Смещаем новое окно относительно существующих окон
            double left = -1;
            double top = -1;
            if (_activeWindows.Count > 0)
            {
                var lastWin = _activeWindows.Last();
                left = lastWin.Left + 35;
                top = lastWin.Top + 35;

                if (left + 440 > SystemParameters.VirtualScreenWidth)
                {
                    left = 50;
                    top = 50;
                }
            }

            var state = new NoteWindowState
            {
                FilePath = "",
                FileList = new List<string>(),
                FileIndex = 0,
                Left = left,
                Top = top,
                Width = 440,
                Height = 640,
                IsAlwaysOnTop = false,
                UseMonoFont = false
            };

            var newWin = OpenNoteWindow(state);
            SaveState();
            return newWin;
        }

        public void OpenFilePicker()
        {
            var dlg = new OpenFileDialog
            {
                Title = LocalizationManager.Get("ChooseFileTooltip"),
                Filter = "Markdown & Текст (*.md;*.txt)|*.md;*.txt|Все файлы (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                var existing = _activeWindows.FirstOrDefault(w => string.Equals(w.CurrentFilePath, dlg.FileName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Activate();
                    if (existing.WindowState == WindowState.Minimized)
                        existing.WindowState = WindowState.Normal;
                    return;
                }

                var state = new NoteWindowState
                {
                    FilePath = dlg.FileName,
                    Left = -1,
                    Top = -1
                };
                OpenNoteWindow(state);
                SaveState();
            }
        }

        public void SetAllVisible(bool visible)
        {
            foreach (var win in _activeWindows)
            {
                if (visible)
                {
                    win.Show();
                    if (win.WindowState == WindowState.Minimized)
                        win.WindowState = WindowState.Normal;
                }
                else
                {
                    win.Hide();
                }
            }
        }

        public void SaveState()
        {
            try
            {
                var config = AppConfig.Load();
                config.Notes.Clear();

                foreach (var win in _activeWindows)
                {
                    config.Notes.Add(new NoteWindowState
                    {
                        FilePath = win.CurrentFilePath,
                        FileList = win.CurrentFileList,
                        FileIndex = win.CurrentFileIndex,
                        Left = win.Left,
                        Top = win.Top,
                        Width = win.Width,
                        Height = win.Height,
                        IsAlwaysOnTop = win.Topmost,
                        UseMonoFont = win.UseMonoFont
                    });
                }

                config.Language = LocalizationManager.CurrentLanguage;
                config.AutoStart = AppConfig.IsAutoStartEnabled();
                config.Save();
            }
            catch { }
        }
    }
}
