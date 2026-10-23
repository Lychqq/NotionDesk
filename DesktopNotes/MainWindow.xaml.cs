using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Win32;

// Разрешение неоднозначностей с System.Windows.Forms
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DesktopNotes
{
    public partial class MainWindow : Window
    {
        private static readonly Regex UrlRegex = new Regex(@"(https?://[^\s]+)", RegexOptions.Compiled);
        private static readonly Regex EmojiRegex = new Regex(@"(?:[\uD83C-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF]|\p{So})(?:[\uFE00-\uFE0F])?", RegexOptions.Compiled);
        private static readonly FontFamily EmojiFont = new FontFamily("Segoe UI Emoji, Segoe UI Symbol");
        private static readonly MarkdownPipeline MdPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        private NoteWindowState _state;
        private FileSystemWatcher? _watcher;
        private CancellationTokenSource? _loadCts;
        private System.Windows.Threading.DispatcherTimer? _debounceTimer;

        public string CurrentFilePath => _state.FilePath;
        public List<string> CurrentFileList => _state.FileList;
        public int CurrentFileIndex => _state.FileIndex;
        public bool UseMonoFont => _state.UseMonoFont;

        public MainWindow() : this(new NoteWindowState())
        {
        }

        public MainWindow(NoteWindowState state)
        {
            InitializeComponent();
            _state = state ?? new NoteWindowState();

            // Восстановление размеров окна
            this.Width = Math.Max(320, _state.Width);
            this.Height = Math.Max(380, _state.Height);

            // Восстановление позиции окна
            if (_state.Left >= 0 && _state.Top >= 0)
            {
                this.Left = _state.Left;
                this.Top = _state.Top;
            }
            else
            {
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                this.Left = Math.Max(50, screenWidth - this.Width - 25);
                this.Top = 45;
            }

            this.Topmost = _state.IsAlwaysOnTop;
            UpdatePinState();
            UpdateFontState();
            UpdateLocalizationUI();

            // Настройка Drag & Drop перехвата на 100% площади окна
            SetupDragAndDrop();

            LocalizationManager.LanguageChanged += UpdateLocalizationUI;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_state.FilePath) && File.Exists(_state.FilePath))
            {
                if (_state.FileList == null || _state.FileList.Count == 0)
                {
                    PopulateFileListFromPath(_state.FilePath);
                }
                LoadCurrentFile();
                SetupFileWatcher();
            }
            else
            {
                ShowEmptyScreen();
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _loadCts?.Cancel();
            LocalizationManager.LanguageChanged -= UpdateLocalizationUI;
            _watcher?.Dispose();

            _state.Left = this.Left;
            _state.Top = this.Top;
            _state.Width = this.Width;
            _state.Height = this.Height;
            _state.IsAlwaysOnTop = this.Topmost;
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
                _state.Left = this.Left;
                _state.Top = this.Top;
            }
        }

        #region Drag & Drop на ВСЮ площадь окна

        private void SetupDragAndDrop()
        {
            this.AddHandler(UIElement.PreviewDragEnterEvent, new DragEventHandler(OnWindowPreviewDragEnter), true);
            this.AddHandler(UIElement.PreviewDragOverEvent, new DragEventHandler(OnWindowPreviewDragOver), true);

            DragOverlay.PreviewDragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }
            };

            DragOverlay.PreviewDragLeave += (s, e) =>
            {
                Point pt = e.GetPosition(this);
                if (pt.X <= 0 || pt.Y <= 0 || pt.X >= this.ActualWidth || pt.Y >= this.ActualHeight)
                {
                    DragOverlay.Visibility = Visibility.Collapsed;
                }
            };

            DragOverlay.PreviewDrop += (s, e) =>
            {
                DragOverlay.Visibility = Visibility.Collapsed;
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Handled = true;
                    string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (items != null && items.Length > 0)
                    {
                        ProcessDroppedItems(items);
                    }
                }
            };
        }

        private void OnWindowPreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DragOverlay.Visibility = Visibility.Visible;
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void OnWindowPreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (DragOverlay.Visibility != Visibility.Visible)
                {
                    DragOverlay.Visibility = Visibility.Visible;
                }
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void ProcessDroppedItems(string[] items)
        {
            var foundFiles = new List<string>();

            foreach (var item in items)
            {
                if (Directory.Exists(item))
                {
                    try
                    {
                        var files = Directory.GetFiles(item, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => IsSupportedNoteFile(f))
                            .OrderBy(f => f)
                            .ToList();
                        foundFiles.AddRange(files);
                    }
                    catch { }
                }
                else if (File.Exists(item) && IsSupportedNoteFile(item))
                {
                    foundFiles.Add(item);
                }
            }

            if (foundFiles.Count == 0)
            {
                MessageBox.Show(
                    LocalizationManager.CurrentLanguage == "ru" 
                        ? "В перетащенной папке или файлах не найдены заметки (.md, .txt)!" 
                        : "No supported notes (.md, .txt) found in dropped items!",
                    "NotionDesk", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (items.Length == 1 && File.Exists(items[0]))
            {
                PopulateFileListFromPath(items[0]);
            }
            else
            {
                _state.FileList = foundFiles;
                _state.FileIndex = 0;
                _state.FilePath = foundFiles[0];
            }

            NoteManager.Instance.SaveState();
            SetupFileWatcher();
            LoadCurrentFile();
        }

        private void PopulateFileListFromPath(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath)!;
                var siblings = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => IsSupportedNoteFile(f))
                    .OrderBy(f => f)
                    .ToList();

                if (siblings.Count == 0) siblings.Add(filePath);

                _state.FileList = siblings;
                _state.FileIndex = Math.Max(0, siblings.FindIndex(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase)));
                _state.FilePath = filePath;
            }
            catch
            {
                _state.FileList = new List<string> { filePath };
                _state.FileIndex = 0;
                _state.FilePath = filePath;
            }
        }

        private static bool IsSupportedNoteFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".md" || ext == ".txt" || ext == ".markdown";
        }

        #endregion

        #region Пагинация (Переключение заметок в подвале)

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            NavigatePage(-1);
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            NavigatePage(1);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                NavigatePage(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                NavigatePage(1);
                e.Handled = true;
            }
        }

        private void NavigatePage(int delta)
        {
            if (_state.FileList == null || _state.FileList.Count <= 1) return;

            int newIndex = _state.FileIndex + delta;
            if (newIndex < 0) newIndex = _state.FileList.Count - 1;
            if (newIndex >= _state.FileList.Count) newIndex = 0;

            _state.FileIndex = newIndex;
            _state.FilePath = _state.FileList[newIndex];

            NoteManager.Instance.SaveState();
            SetupFileWatcher();
            LoadCurrentFile();
        }

        private void UpdatePaginationUI()
        {
            int total = _state.FileList?.Count ?? 0;
            int current = total > 0 ? _state.FileIndex + 1 : 0;

            PageIndicatorText.Text = total > 0 ? $"{current} / {total}" : "0 / 0";
            BtnPrevPage.IsEnabled = total > 1;
            BtnNextPage.IsEnabled = total > 1;

            BtnPrevPage.ToolTip = LocalizationManager.Get("PrevPageTooltip");
            BtnNextPage.ToolTip = LocalizationManager.Get("NextPageTooltip");

            if (!string.IsNullOrEmpty(_state.FilePath) && File.Exists(_state.FilePath))
            {
                FooterFileName.Text = Path.GetFileName(_state.FilePath);
            }
            else
            {
                FooterFileName.Text = "";
            }
        }

        #endregion

        #region Управление и кнопки панели

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            NoteManager.Instance.CreateNewEmptyWindow();
        }

        private void ChooseFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = LocalizationManager.Get("ChooseFileTooltip"),
                Filter = "Markdown & Текст (*.md;*.txt)|*.md;*.txt|Все файлы (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                PopulateFileListFromPath(dlg.FileName);
                NoteManager.Instance.SaveState();
                SetupFileWatcher();
                LoadCurrentFile();
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_state.FilePath) && File.Exists(_state.FilePath))
                {
                    Process.Start(new ProcessStartInfo(_state.FilePath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show(LocalizationManager.Get("EmptyTitle"), "NotionDesk", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия: {ex.Message}");
            }
        }

        private void TogglePin_Click(object sender, RoutedEventArgs e)
        {
            _state.IsAlwaysOnTop = !_state.IsAlwaysOnTop;
            this.Topmost = _state.IsAlwaysOnTop;
            UpdatePinState();
            NoteManager.Instance.SaveState();
        }

        private void UpdatePinState()
        {
            if (_state.IsAlwaysOnTop)
            {
                PinBtn.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                PinBtn.ToolTip = LocalizationManager.Get("PinOnTooltip");
            }
            else
            {
                PinBtn.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                PinBtn.ToolTip = LocalizationManager.Get("PinOffTooltip");
            }
        }

        private void ToggleFont_Click(object sender, RoutedEventArgs e)
        {
            _state.UseMonoFont = !_state.UseMonoFont;
            UpdateFontState();
            LoadCurrentFile();
            NoteManager.Instance.SaveState();
        }

        private void UpdateFontState()
        {
            FontBtn.Foreground = _state.UseMonoFont 
                ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) 
                : new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        private void ToggleLang_Click(object sender, RoutedEventArgs e)
        {
            string nextLang = LocalizationManager.CurrentLanguage == "ru" ? "en" : "ru";
            LocalizationManager.SetLanguage(nextLang);
            var config = AppConfig.Load();
            config.Language = nextLang;
            config.Save();
        }

        private void UpdateLocalizationUI()
        {
            LangBtn.Content = LocalizationManager.CurrentLanguage.ToUpperInvariant();
            BtnNewNote.ToolTip = LocalizationManager.Get("NewNoteTooltip");
            BtnChooseFile.ToolTip = LocalizationManager.Get("ChooseFileTooltip");
            BtnOpenFile.ToolTip = LocalizationManager.Get("OpenFileTooltip");
            FontBtn.ToolTip = LocalizationManager.Get("FontTooltip");
            BtnClose.ToolTip = LocalizationManager.Get("CloseTooltip");
            DragOverlayLabel.Text = LocalizationManager.Get("DragOverlayText");

            UpdatePinState();
            UpdatePaginationUI();

            if (string.IsNullOrEmpty(_state.FilePath) || !File.Exists(_state.FilePath))
            {
                ShowEmptyScreen();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        [System.Runtime.InteropServices.DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        public static void FlushMemory()
        {
            Task.Run(() =>
            {
                Thread.Sleep(300); // Даем WPF завершить верстку
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                try
                {
                    EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                }
                catch { }
            });
        }

        #region Высокопроизводительный асинхронный парсинг документа

        private async void LoadCurrentFile()
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            string path = _state.FilePath;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                FileNameTitle.Text = LocalizationManager.Get("EmptyTitle");
                FileExtBadge.Text = "...";
                ShowEmptyScreen();
                UpdatePaginationUI();
                return;
            }

            FileNameTitle.Text = Path.GetFileName(path);
            FileExtBadge.Text = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();

            try
            {
                // Читаем файл в фоне без блокировки интерфейса
                string rawText = await Task.Run(() =>
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs, Encoding.UTF8, true);
                    return reader.ReadToEnd();
                }, token);

                if (token.IsCancellationRequested) return;

                bool isMarkdown = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || 
                                  path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) || 
                                  rawText.Contains("```");

                var doc = isMarkdown ? BuildMarkdownDocument(rawText) : BuildTextDocument(rawText);

                if (!token.IsCancellationRequested)
                {
                    DocViewer.Document = doc;
                    FlushMemory();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                var doc = new FlowDocument();
                doc.Blocks.Add(new System.Windows.Documents.Paragraph(new Run($"Ошибка чтения: {ex.Message}")) 
                { 
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)) 
                });
                DocViewer.Document = doc;
            }

            UpdatePaginationUI();
        }

        private void ShowEmptyScreen()
        {
            var doc = new FlowDocument
            {
                Background = Brushes.Transparent,
                FontFamily = new FontFamily("Segoe UI"),
                PagePadding = new Thickness(10),
                IsOptimalParagraphEnabled = false,
                IsHyphenationEnabled = false
            };

            var p = new System.Windows.Documents.Paragraph
            {
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 45, 0, 0),
                LineHeight = 20
            };
            p.Inlines.Add(new Run(LocalizationManager.Get("EmptyMessage")));
            doc.Blocks.Add(p);
            DocViewer.Document = doc;

            FileNameTitle.Text = LocalizationManager.Get("EmptyTitle");
            FileExtBadge.Text = "...";
            UpdatePaginationUI();
        }

        /// <summary>
        /// Полноценный высокоскоростной Markdown парсер на базе Markdig
        /// </summary>
        private FlowDocument BuildMarkdownDocument(string markdownText)
        {
            var doc = new FlowDocument
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontFamily = _state.UseMonoFont ? new FontFamily("Cascadia Code, Consolas, Segoe UI Emoji") : new FontFamily("Segoe UI, Segoe UI Emoji"),
                FontSize = 11,
                PagePadding = new Thickness(0),
                // Отключаем ресурсоёмкие расчёты переносов и абзацев для моментального рендеринга
                IsOptimalParagraphEnabled = false,
                IsHyphenationEnabled = false
            };

            var parsed = Markdown.Parse(markdownText, MdPipeline);

            const int maxBlocks = 1200; // Ограничение на количество блоков для защиты от зависаний
            int blockCount = 0;

            foreach (var block in parsed)
            {
                blockCount++;
                if (blockCount > maxBlocks)
                {
                    var notice = new System.Windows.Documents.Paragraph(
                        new Run($"⚡ Показано {maxBlocks} блоков заметки. Для просмотра гигантского файла целиком нажмите 📝 Блокнот."))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21)),
                        FontStyle = FontStyles.Italic,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    doc.Blocks.Add(notice);
                    break;
                }

                if (block is HeadingBlock heading)
                {
                    var p = new System.Windows.Documents.Paragraph
                    {
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 12, 0, 4),
                        FontSize = heading.Level switch { 1 => 16, 2 => 14, 3 => 12.5, _ => 11.5 }
                    };
                    AppendInlines(p, heading.Inline);
                    doc.Blocks.Add(p);
                }
                else if (block is FencedCodeBlock codeBlock)
                {
                    string codeContent = codeBlock.Lines.ToString().TrimEnd();
                    string lang = codeBlock.Info ?? "code";
                    doc.Blocks.Add(CreateCodeBlockUI(codeContent, lang));
                }
                else if (block is ParagraphBlock pb)
                {
                    var p = new System.Windows.Documents.Paragraph
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                        FontSize = 11,
                        Margin = new Thickness(0, 2, 0, 3),
                        LineHeight = 17
                    };
                    AppendInlines(p, pb.Inline);
                    doc.Blocks.Add(p);
                }
                else if (block is ThematicBreakBlock)
                {
                    var hr = new System.Windows.Documents.BlockUIContainer(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                        Margin = new Thickness(0, 8, 0, 8)
                    });
                    doc.Blocks.Add(hr);
                }
                else if (block is ListBlock listBlock)
                {
                    foreach (var item in listBlock)
                    {
                        if (item is ListItemBlock lib)
                        {
                            var p = new System.Windows.Documents.Paragraph
                            {
                                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                                Margin = new Thickness(12, 1, 0, 1),
                                FontSize = 11
                            };
                            p.Inlines.Add(new Run("•  ") { Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)) });
                            foreach (var sub in lib)
                            {
                                if (sub is ParagraphBlock subPb) AppendInlines(p, subPb.Inline);
                            }
                            doc.Blocks.Add(p);
                        }
                    }
                }
            }

            return doc;
        }

        private void AppendInlines(System.Windows.Documents.Paragraph p, ContainerInline? container)
        {
            if (container == null) return;

            foreach (var inline in container)
            {
                if (inline is LiteralInline lit)
                {
                    AppendTextWithAutoLinks(p, lit.Content.ToString());
                }
                else if (inline is LinkInline link)
                {
                    string url = link.Url ?? "";
                    string linkText = link.FirstChild?.ToString() ?? url;

                    var hl = new Hyperlink(new Run(linkText))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                        TextDecorations = TextDecorations.Underline,
                        Cursor = Cursors.Hand
                    };
                    hl.Click += (s, e) => SafeOpenUrl(url);
                    p.Inlines.Add(hl);
                }
                else if (inline is CodeInline code)
                {
                    var inlineCodeBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(38, 38, 38)),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(2, 0, 2, 0),
                        Child = new TextBlock
                        {
                            Text = code.Content,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113))
                        }
                    };
                    p.Inlines.Add(new InlineUIContainer(inlineCodeBorder));
                }
                else if (inline is EmphasisInline emp)
                {
                    var span = new Span();
                    if (emp.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold;
                    if (emp.DelimiterCount == 1 || emp.DelimiterCount == 3) span.FontStyle = FontStyles.Italic;
                    foreach (var sub in emp)
                    {
                        if (sub is LiteralInline l) span.Inlines.Add(new Run(l.Content.ToString()));
                    }
                    p.Inlines.Add(span);
                }
                else if (inline is LineBreakInline)
                {
                    p.Inlines.Add(new LineBreak());
                }
            }
        }

        private void AppendTextWithAutoLinks(System.Windows.Documents.Paragraph p, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (!text.Contains("http://") && !text.Contains("https://"))
            {
                AppendTextWithEmojis(p, text);
                return;
            }

            var parts = UrlRegex.Split(text);
            foreach (var part in parts)
            {
                if (UrlRegex.IsMatch(part))
                {
                    string url = part;
                    var hl = new Hyperlink(new Run(url))
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                        TextDecorations = TextDecorations.Underline,
                        Cursor = Cursors.Hand
                    };
                    hl.Click += (s, e) => SafeOpenUrl(url);
                    p.Inlines.Add(hl);
                }
                else
                {
                    AppendTextWithEmojis(p, part);
                }
            }
        }

        private void AppendTextWithEmojis(System.Windows.Documents.Paragraph p, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var matches = EmojiRegex.Matches(text);
            if (matches.Count == 0)
            {
                p.Inlines.Add(new Run(text));
                return;
            }

            int lastIndex = 0;
            foreach (Match m in matches)
            {
                if (m.Index > lastIndex)
                {
                    p.Inlines.Add(new Run(text.Substring(lastIndex, m.Index - lastIndex)));
                }

                var emojiRun = new Run(m.Value)
                {
                    FontFamily = EmojiFont,
                    BaselineAlignment = BaselineAlignment.Center
                };
                p.Inlines.Add(emojiRun);
                lastIndex = m.Index + m.Length;
            }

            if (lastIndex < text.Length)
            {
                p.Inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }

        /// <summary>
        /// Оптимизированный парсер .txt с плавной порционной группировкой строк
        /// </summary>
        private FlowDocument BuildTextDocument(string rawText)
        {
            var doc = new FlowDocument
            {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontFamily = _state.UseMonoFont ? new FontFamily("Cascadia Code, Consolas, Segoe UI Emoji") : new FontFamily("Segoe UI, Segoe UI Emoji"),
                FontSize = 11,
                PagePadding = new Thickness(0),
                IsOptimalParagraphEnabled = false,
                IsHyphenationEnabled = false
            };

            var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int totalLines = lines.Length;
            int limit = Math.Min(totalLines, 3000);

            System.Windows.Documents.Paragraph? currentTextParagraph = null;
            int linesInCurrentParagraph = 0;

            void FlushTextParagraph()
            {
                if (currentTextParagraph != null && currentTextParagraph.Inlines.Count > 0)
                {
                    doc.Blocks.Add(currentTextParagraph);
                    currentTextParagraph = null;
                    linesInCurrentParagraph = 0;
                }
            }

            int i = 0;
            while (i < limit)
            {
                string rawLine = lines[i];
                string line = rawLine.Trim();

                // Многострочный блок команд (например, ЧИНИМ АНТИГРАВИТИ)
                if (line.StartsWith("ЧИНИМ", StringComparison.OrdinalIgnoreCase))
                {
                    FlushTextParagraph();
                    var cmdList = new List<string>();
                    i++;
                    while (i < limit)
                    {
                        string sub = lines[i].Trim();
                        if (string.IsNullOrEmpty(sub)) { i++; break; }
                        if (sub.StartsWith("agy ", StringComparison.OrdinalIgnoreCase)) break;
                        cmdList.Add(sub);
                        i++;
                    }

                    string fullCode = string.Join("\r\n", cmdList);
                    doc.Blocks.Add(CreateCodeBlockUI(fullCode, "PowerShell"));
                    continue;
                }

                // Одиночные команды
                if (line.StartsWith("agy ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("set HTTP", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("python ", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("$ ", StringComparison.OrdinalIgnoreCase))
                {
                    FlushTextParagraph();
                    doc.Blocks.Add(CreateCodeBlockUI(line, "CLI"));
                    i++;
                    continue;
                }

                // Разделители и заголовки
                if (line.StartsWith("###") || line.StartsWith("──"))
                {
                    FlushTextParagraph();
                    var hr = new System.Windows.Documents.BlockUIContainer(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                        Margin = new Thickness(0, 6, 0, 6)
                    });
                    doc.Blocks.Add(hr);

                    string t = line.Replace("#", "").Replace("──", "").Trim();
                    if (!string.IsNullOrEmpty(t))
                    {
                        var h = new System.Windows.Documents.Paragraph(new Run(t))
                        {
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 12,
                            Margin = new Thickness(0, 4, 0, 4)
                        };
                        doc.Blocks.Add(h);
                    }
                    i++;
                    continue;
                }

                if (string.IsNullOrEmpty(line))
                {
                    FlushTextParagraph();
                    i++;
                    continue;
                }

                // Порционная группировка (максимум 45 строк на абзац для мгновенного и плавного выделения мышкой!)
                if (currentTextParagraph == null || linesInCurrentParagraph >= 45)
                {
                    FlushTextParagraph();
                    currentTextParagraph = new System.Windows.Documents.Paragraph
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(165, 165, 165)),
                        Margin = new Thickness(0, 1, 0, 3),
                        FontSize = 10.5
                    };
                }
                else
                {
                    currentTextParagraph.Inlines.Add(new LineBreak());
                }

                AppendTextWithAutoLinks(currentTextParagraph, rawLine);
                linesInCurrentParagraph++;
                i++;
            }

            FlushTextParagraph();

            if (totalLines > limit)
            {
                var notice = new System.Windows.Documents.Paragraph(
                    new Run($"⚡ Показано {limit} строк из {totalLines}. Нажмите 📝 для открытия в Блокноте."))
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21)),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 8, 0, 8)
                };
                doc.Blocks.Add(notice);
            }

            return doc;
        }

        /// <summary>
        /// Создает стильный Notion Code Card с текстом, который МОЖНО выделять, и кнопкой копирования
        /// </summary>
        private System.Windows.Documents.BlockUIContainer CreateCodeBlockUI(string codeText, string lang)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 3, 0, 4)
            };

            var stack = new StackPanel();

            var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var langBadge = new TextBlock
            {
                Text = lang.ToUpperInvariant(),
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(langBadge, 0);
            topGrid.Children.Add(langBadge);

            var copyBtn = new Button
            {
                Content = LocalizationManager.Get("Copy"),
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderThickness = new Thickness(1),
                FontSize = 9.5,
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand
            };

            var btnStyle = new Style(typeof(Border));
            btnStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(3)));
            copyBtn.Resources.Add(typeof(Border), btnStyle);

            string textToCopy = codeText;
            copyBtn.Click += (s, e) =>
            {
                Clipboard.SetText(textToCopy);
                copyBtn.Content = LocalizationManager.Get("Copied");
                copyBtn.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                copyBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128));
                Task.Delay(1300).ContinueWith(_ => Dispatcher.Invoke(() =>
                {
                    copyBtn.Content = LocalizationManager.Get("Copy");
                    copyBtn.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                    copyBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                }));
            };

            Grid.SetColumn(copyBtn, 1);
            topGrid.Children.Add(copyBtn);
            stack.Children.Add(topGrid);

            var textBox = new TextBox
            {
                Text = codeText,
                IsReadOnly = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(0),
                SelectionBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))
            };
            stack.Children.Add(textBox);

            card.Child = stack;
            return new System.Windows.Documents.BlockUIContainer(card);
        }

        private void SafeOpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        #endregion

        #region File Watcher с Debounce

        private void SetupFileWatcher()
        {
            _watcher?.Dispose();
            _watcher = null;

            try
            {
                if (!string.IsNullOrEmpty(_state.FilePath) && File.Exists(_state.FilePath))
                {
                    string dir = Path.GetDirectoryName(_state.FilePath)!;
                    string file = Path.GetFileName(_state.FilePath);

                    _watcher = new FileSystemWatcher(dir, file)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                    };

                    _watcher.Changed += (s, e) => TriggerDebouncedReload();
                    _watcher.EnableRaisingEvents = true;
                }
            }
            catch { }
        }

        private void TriggerDebouncedReload()
        {
            Dispatcher.Invoke(() =>
            {
                _debounceTimer?.Stop();
                _debounceTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _debounceTimer.Tick += (s, e) =>
                {
                    _debounceTimer.Stop();
                    LoadCurrentFile();
                };
                _debounceTimer.Start();
            });
        }

        #endregion
    }
}
