using System;
using System.IO;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace DesktopNotes
{
    public class TrayService : IDisposable
    {
        private WinForms.NotifyIcon? _notifyIcon;
        private WinForms.ContextMenuStrip? _contextMenu;
        private WinForms.ToolStripMenuItem? _menuAutoStart;
        private WinForms.ToolStripMenuItem? _menuLanguage;
        private WinForms.ToolStripMenuItem? _menuRu;
        private WinForms.ToolStripMenuItem? _menuEn;
        private WinForms.ToolStripMenuItem? _menuShowHide;
        private bool _isDisposed = false;
        private bool _areWindowsVisible = true;

        public event Action? OnNewNoteRequested;
        public event Action? OnOpenFileRequested;
        public event Action<bool>? OnToggleVisibilityRequested;

        public void Initialize()
        {
            _contextMenu = new WinForms.ContextMenuStrip();
            _contextMenu.Renderer = new NotionMenuRenderer();

            // Создаем нативную иконку программы (минималистичный темный квадрат Notion "N")
            Drawing.Icon appIcon = CreateNotionIcon();

            _notifyIcon = new WinForms.NotifyIcon
            {
                Icon = appIcon,
                Text = LocalizationManager.Get("TrayTitle"),
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => ToggleVisibility();

            BuildMenu();
            LocalizationManager.LanguageChanged += BuildMenu;
        }

        private void BuildMenu()
        {
            if (_contextMenu == null) return;
            _contextMenu.Items.Clear();

            // Заголовок в трее (некликабельный бейдж)
            var headerItem = new WinForms.ToolStripMenuItem(" NotionDesk Notes")
            {
                Enabled = false,
                Font = new Drawing.Font("Segoe UI", 9f, Drawing.FontStyle.Bold)
            };
            _contextMenu.Items.Add(headerItem);
            _contextMenu.Items.Add(new WinForms.ToolStripSeparator());

            // ➕ Новая заметка
            var itemNewNote = new WinForms.ToolStripMenuItem(LocalizationManager.Get("TrayNewNote"));
            itemNewNote.Click += (s, e) => OnNewNoteRequested?.Invoke();
            _contextMenu.Items.Add(itemNewNote);

            // 📁 Открыть файл...
            var itemOpenFile = new WinForms.ToolStripMenuItem(LocalizationManager.Get("TrayOpenFile"));
            itemOpenFile.Click += (s, e) => OnOpenFileRequested?.Invoke();
            _contextMenu.Items.Add(itemOpenFile);

            // 👁 Показать / Скрыть заметки
            _menuShowHide = new WinForms.ToolStripMenuItem(
                _areWindowsVisible ? LocalizationManager.Get("TrayHideAll") : LocalizationManager.Get("TrayShowAll")
            );
            _menuShowHide.Click += (s, e) => ToggleVisibility();
            _contextMenu.Items.Add(_menuShowHide);

            _contextMenu.Items.Add(new WinForms.ToolStripSeparator());

            // 🌐 Язык
            _menuLanguage = new WinForms.ToolStripMenuItem(LocalizationManager.Get("TrayLanguage"));
            
            _menuRu = new WinForms.ToolStripMenuItem("🇷🇺 Русский") { Checked = LocalizationManager.CurrentLanguage == "ru" };
            _menuRu.Click += (s, e) => SwitchLanguage("ru");

            _menuEn = new WinForms.ToolStripMenuItem("🇬🇧 English") { Checked = LocalizationManager.CurrentLanguage == "en" };
            _menuEn.Click += (s, e) => SwitchLanguage("en");

            _menuLanguage.DropDownItems.Add(_menuRu);
            _menuLanguage.DropDownItems.Add(_menuEn);
            _contextMenu.Items.Add(_menuLanguage);

            // ⚙ Автозагрузка с Windows
            _menuAutoStart = new WinForms.ToolStripMenuItem(LocalizationManager.Get("TrayAutoStart"))
            {
                Checked = AppConfig.IsAutoStartEnabled()
            };
            _menuAutoStart.Click += (s, e) =>
            {
                bool newState = !_menuAutoStart.Checked;
                _menuAutoStart.Checked = newState;
                AppConfig.SetAutoStart(newState);
            };
            _contextMenu.Items.Add(_menuAutoStart);

            _contextMenu.Items.Add(new WinForms.ToolStripSeparator());

            // ✕ Выход
            var itemExit = new WinForms.ToolStripMenuItem(LocalizationManager.Get("TrayExit"));
            itemExit.ForeColor = Drawing.Color.FromArgb(248, 113, 113); // Красный акцент
            itemExit.Click += (s, e) =>
            {
                if (_notifyIcon != null) _notifyIcon.Visible = false;
                WpfApplication.Current.Dispatcher.Invoke(() =>
                {
                    WpfApplication.Current.Shutdown();
                });
            };
            _contextMenu.Items.Add(itemExit);
        }

        private void SwitchLanguage(string lang)
        {
            LocalizationManager.SetLanguage(lang);
            var config = AppConfig.Load();
            config.Language = lang;
            config.Save();
        }

        private void ToggleVisibility()
        {
            _areWindowsVisible = !_areWindowsVisible;
            OnToggleVisibilityRequested?.Invoke(_areWindowsVisible);
            if (_menuShowHide != null)
            {
                _menuShowHide.Text = _areWindowsVisible 
                    ? LocalizationManager.Get("TrayHideAll") 
                    : LocalizationManager.Get("TrayShowAll");
            }
        }

        private Drawing.Icon CreateNotionIcon()
        {
            using var bmp = new Drawing.Bitmap(32, 32);
            using var g = Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            // Закругленный темный квадрат
            using (var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(28, 28, 28)))
            {
                using var path = new Drawing.Drawing2D.GraphicsPath();
                int r = 8;
                path.AddArc(0, 0, r, r, 180, 90);
                path.AddArc(32 - r, 0, r, r, 270, 90);
                path.AddArc(32 - r, 32 - r, r, r, 0, 90);
                path.AddArc(0, 32 - r, r, r, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }

            // Белая буква N
            using (var font = new Drawing.Font("Segoe UI", 16f, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel))
            using (var textBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(245, 245, 245)))
            {
                var sf = new Drawing.StringFormat
                {
                    Alignment = Drawing.StringAlignment.Center,
                    LineAlignment = Drawing.StringAlignment.Center
                };
                g.DrawString("N", font, textBrush, new Drawing.RectangleF(0, 1, 32, 32), sf);
            }

            IntPtr hIcon = bmp.GetHicon();
            return Drawing.Icon.FromHandle(hIcon);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                _contextMenu?.Dispose();
            }
        }
    }

    /// <summary>
    /// Notion Dark кастомный рендерер меню трея
    /// </summary>
    public class NotionMenuRenderer : WinForms.ToolStripProfessionalRenderer
    {
        public NotionMenuRenderer() : base(new NotionColorTable()) { }

        protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                var rc = new Drawing.Rectangle(Drawing.Point.Empty, e.Item.Size);
                using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(45, 45, 45));
                e.Graphics.FillRectangle(brush, rc);
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.ForeColor == Drawing.Color.FromArgb(248, 113, 113))
            {
                // Сохраняем акцентный цвет для кнопки выхода
                e.TextColor = Drawing.Color.FromArgb(248, 113, 113);
            }
            else if (!e.Item.Enabled)
            {
                e.TextColor = Drawing.Color.FromArgb(120, 120, 120);
            }
            else
            {
                e.TextColor = Drawing.Color.FromArgb(220, 220, 220);
            }
            base.OnRenderItemText(e);
        }
    }

    public class NotionColorTable : WinForms.ProfessionalColorTable
    {
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(26, 26, 26);
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(48, 48, 48);
        public override Drawing.Color MenuItemBorder => Drawing.Color.Transparent;
        public override Drawing.Color MenuItemSelected => Drawing.Color.FromArgb(45, 45, 45);
        public override Drawing.Color MenuStripGradientBegin => Drawing.Color.FromArgb(26, 26, 26);
        public override Drawing.Color MenuStripGradientEnd => Drawing.Color.FromArgb(26, 26, 26);
        public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(26, 26, 26);
        public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.FromArgb(26, 26, 26);
        public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(26, 26, 26);
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(42, 42, 42);
        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(42, 42, 42);
        public override Drawing.Color CheckBackground => Drawing.Color.FromArgb(42, 42, 42);
        public override Drawing.Color CheckSelectedBackground => Drawing.Color.FromArgb(60, 60, 60);
    }
}
