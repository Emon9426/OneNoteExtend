using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OneExtend.CodeBlock;
using OneExtend.CodeBlock.Formatting;
using OneExtend.CodeBlock.Grammar;
using OneExtend.CodeBlock.OneNoteXml;
using OneExtend.CodeBlock.Theme;
using OneExtend.Infrastructure.Settings;

namespace OneExtend.CodeBlock.UI
{
    public sealed class InsertCodeBlockResult
    {
        public string Source { get; set; }
        public string LanguageId { get; set; }
        public CodeBlockOptions Options { get; set; }
    }

    /// <summary>
    /// The main "Insert code block" dialog (mockup 5-2 of the requirements doc).
    /// Built entirely in code so the project needs no XAML markup compiler and
    /// builds with the plain .NET SDK.
    /// </summary>
    public sealed class InsertCodeBlockWindow : Window
    {
        private readonly LanguageRegistry _languages;
        private readonly ThemeLoader _themes;
        private readonly AppSettings _settings;

        private TextBox _searchBox;
        private ListBox _languageList;
        private TextBox _titleBox;
        private TextBox _sourceBox;
        private CheckBox _lineNumbersCheck;
        private TextBox _lineStartBox;
        private TextBox _lineStepBox;
        private ComboBox _separatorBox;
        private ComboBox _lineModeBox;
        private CheckBox _beautifyCheck;
        private ComboBox _indentBox;
        private ComboBox _keywordCaseBox;
        private ComboBox _themeBox;
        private FlowDocumentScrollViewer _preview;
        private DispatcherTimer _previewTimer;

        private bool _titleTouched;
        private bool _languageTouched;
        private string _detectedLanguage;

        public InsertCodeBlockResult Result { get; private set; }

        public InsertCodeBlockWindow(
            LanguageRegistry languages,
            ThemeLoader themes,
            AppSettings settings,
            string prefillSource = null,
            string prefillLanguageId = null,
            CodeBlockOptions prefillOptions = null)
        {
            _languages = languages;
            _themes = themes;
            _settings = settings;

            Title = "插入代码块 Insert Code Block — OneExtend";
            Width = 980;
            Height = 660;
            MinWidth = 760;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            var prefill = prefillOptions ?? DefaultOptions();

            var root = new Grid { Margin = new Thickness(10) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            // ---------- main area ----------
            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230, GridUnitType.Pixel) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(main, 0);
            root.Children.Add(main);

            // left: language search + list
            var left = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
            main.Children.Add(left);

            _searchBox = new TextBox { Margin = new Thickness(0, 0, 0, 6) };
            _searchBox.TextChanged += (s, e) => RefreshLanguageList();
            DockPanel.SetDock(_searchBox, Dock.Top);
            left.Children.Add(_searchBox);

            _languageList = new ListBox
            {
                FontSize = 13
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_languageList, ScrollBarVisibility.Disabled);
            _languageList.SelectionChanged += (s, e) => { _languageTouched = true; SchedulePreview(); SyncAutoTitle(); };
            left.Children.Add(_languageList);

            // right: title, source, options, preview
            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            main.Children.Add(right);

            var titleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(titleRow, 0);
            right.Children.Add(titleRow);
            var titleLabel = new TextBlock { Text = "名称 Title:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            titleRow.Children.Add(titleLabel);
            _titleBox = new TextBox { FontSize = 13 };
            _titleBox.TextChanged += (s, e) => _titleTouched = true;
            titleRow.Children.Add(_titleBox);

            _sourceBox = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                AcceptsReturn = true,
                AcceptsTab = false,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xD1, 0xD9))
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_sourceBox, ScrollBarVisibility.Auto);
            _sourceBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Tab)
                {
                    var caret = _sourceBox.CaretIndex;
                    _sourceBox.Text = _sourceBox.Text.Insert(caret, "    ");
                    _sourceBox.CaretIndex = caret + 4;
                    e.Handled = true;
                }
            };
            _sourceBox.TextChanged += (s, e) => { SchedulePreview(); ScheduleDetect(); };
            Grid.SetRow(_sourceBox, 1);
            right.Children.Add(_sourceBox);

            var options = BuildOptionsPanel(prefill);
            Grid.SetRow(options, 2);
            right.Children.Add(options);

            var previewHost = new GroupBox { Header = "预览 Preview（与插入效果一致 / WYSIWYG）", Margin = new Thickness(0, 8, 0, 0) };
            _preview = new FlowDocumentScrollViewer
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12.5,
                IsToolBarVisible = false,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            previewHost.Content = _preview;
            Grid.SetRow(previewHost, 3);
            right.Children.Add(previewHost);

            // ---------- footer ----------
            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(footer, 1);
            root.Children.Add(footer);

            var hint = new TextBlock
            {
                Text = "Ctrl+Enter 插入 · Esc 取消",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0),
                Foreground = Brushes.Gray,
                FontSize = 12
            };
            var cancelBtn = new Button { Content = "取消 Cancel", Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(6, 0, 0, 0) };
            cancelBtn.Click += (s, e) => Close();
            var insertBtn = new Button
            {
                Content = "插入 Insert",
                Padding = new Thickness(24, 4, 24, 4),
                Margin = new Thickness(6, 0, 0, 0),
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(0x77, 0x19, 0xAA)),
                Foreground = Brushes.White
            };
            insertBtn.Click += (s, e) => Accept();

            // hint sits left of the buttons inside footer's right alignment: wrap in a dock
            var footerDock = new DockPanel();
            footerDock.Children.Add(hint);
            footerDock.Children.Add(cancelBtn);
            footerDock.Children.Add(insertBtn);
            footer.Children.Add(footerDock);

            // ---------- wiring ----------
            _previewTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _previewTimer.Tick += (s, e) =>
            {
                _previewTimer.Stop();
                RefreshPreview();
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Accept();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };

            // initial state
            _sourceBox.Text = prefillSource ?? string.Empty;
            ApplyPrefill(prefill, prefillLanguageId);
            RefreshLanguageList();
            Loaded += (s, e) => { _sourceBox.Focus(); SchedulePreview(); };
        }

        private CodeBlockOptions DefaultOptions()
        {
            var s = _settings;
            return new CodeBlockOptions
            {
                LanguageId = s?.General?.DefaultLanguage ?? "plsql",
                ThemeId = s?.Appearance?.Theme ?? "light",
                Beautify = true,
                Format = new FormatOptions
                {
                    IndentWidth = s?.Formatting?.IndentWidth ?? 4,
                    KeywordCase = s?.Formatting?.KeywordCase ?? KeywordCase.Preserve,
                    CommaPosition = s?.Formatting?.CommaPosition ?? CommaPosition.Trailing
                },
                LineNumbers = new LineNumberOptions
                {
                    Enabled = s?.LineNumbers?.Enabled ?? true,
                    Start = s?.LineNumbers?.Start ?? 1,
                    Step = s?.LineNumbers?.Step ?? 1,
                    Mode = s?.LineNumbers?.Mode ?? LineNumberMode.Column,
                    Separator = s?.LineNumbers?.Separator ?? LineNumberSeparator.Bar
                }
            };
        }

        private void ApplyPrefill(CodeBlockOptions prefill, string prefillLanguageId)
        {
            _titleTouched = prefillLanguageId != null;
            if (!string.IsNullOrEmpty(prefillLanguageId))
                _languageTouched = true;
        }

        private StackPanel BuildOptionsPanel(CodeBlockOptions prefill)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

            _lineNumbersCheck = new CheckBox
            {
                Content = "行号",
                IsChecked = prefill.LineNumbers.Enabled,
                VerticalAlignment = VerticalAlignment.Center
            };
            _lineNumbersCheck.Checked += (s, e) => SchedulePreview();
            _lineNumbersCheck.Unchecked += (s, e) => SchedulePreview();
            panel.Children.Add(_lineNumbersCheck);

            panel.Children.Add(MakeLabel("起始"));
            _lineStartBox = MakeNumberBox(prefill.LineNumbers.Start.ToString());
            _lineStartBox.TextChanged += (s, e) => SchedulePreview();
            panel.Children.Add(_lineStartBox);

            panel.Children.Add(MakeLabel("步长"));
            _lineStepBox = MakeNumberBox(prefill.LineNumbers.Step.ToString());
            _lineStepBox.TextChanged += (s, e) => SchedulePreview();
            panel.Children.Add(_lineStepBox);

            panel.Children.Add(MakeLabel("分隔"));
            _separatorBox = new ComboBox { Width = 62, Margin = new Thickness(4, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            _separatorBox.Items.Add("竖线 │");
            _separatorBox.Items.Add("点 ·");
            _separatorBox.Items.Add("无");
            _separatorBox.SelectedIndex = (int)prefill.LineNumbers.Separator;
            _separatorBox.SelectionChanged += (s, e) => SchedulePreview();
            panel.Children.Add(_separatorBox);

            panel.Children.Add(MakeLabel("模式"));
            _lineModeBox = new ComboBox { Width = 76, Margin = new Thickness(4, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            _lineModeBox.Items.Add("双列对齐");
            _lineModeBox.Items.Add("内联前缀");
            _lineModeBox.SelectedIndex = prefill.LineNumbers.Mode == LineNumberMode.Inline ? 1 : 0;
            _lineModeBox.SelectionChanged += (s, e) => SchedulePreview();
            panel.Children.Add(_lineModeBox);

            _beautifyCheck = new CheckBox
            {
                Content = "自动美化",
                IsChecked = prefill.Beautify,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _beautifyCheck.Checked += (s, e) => SchedulePreview();
            _beautifyCheck.Unchecked += (s, e) => SchedulePreview();
            panel.Children.Add(_beautifyCheck);

            panel.Children.Add(MakeLabel("缩进"));
            _indentBox = new ComboBox { Width = 74, Margin = new Thickness(4, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            _indentBox.Items.Add("2 空格");
            _indentBox.Items.Add("4 空格");
            _indentBox.Items.Add("8 空格");
            _indentBox.SelectedIndex = prefill.Format.IndentWidth == 2 ? 0 : prefill.Format.IndentWidth == 8 ? 2 : 1;
            _indentBox.SelectionChanged += (s, e) => SchedulePreview();
            panel.Children.Add(_indentBox);

            panel.Children.Add(MakeLabel("关键字"));
            _keywordCaseBox = new ComboBox { Width = 76, Margin = new Thickness(4, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            _keywordCaseBox.Items.Add("保持");
            _keywordCaseBox.Items.Add("大写");
            _keywordCaseBox.Items.Add("小写");
            _keywordCaseBox.Items.Add("首字母");
            _keywordCaseBox.SelectedIndex = (int)prefill.Format.KeywordCase;
            _keywordCaseBox.SelectionChanged += (s, e) => SchedulePreview();
            panel.Children.Add(_keywordCaseBox);

            panel.Children.Add(MakeLabel("主题"));
            _themeBox = new ComboBox { Width = 90, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            foreach (var t in _themes.All)
                _themeBox.Items.Add(t.Name ?? t.Id);
            var selectedTheme = _themes.All.FirstOrDefault(t =>
                string.Equals(t.Id, prefill.ThemeId, StringComparison.OrdinalIgnoreCase)) ?? _themes.GetDefault();
            _themeBox.SelectedItem = selectedTheme.Name ?? selectedTheme.Id;
            _themeBox.SelectionChanged += (s, e) => SchedulePreview();
            panel.Children.Add(_themeBox);

            return panel;
        }

        private static TextBlock MakeLabel(string text) =>
            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), FontSize = 12.5 };

        private static TextBox MakeNumberBox(string text) =>
            new TextBox { Width = 42, Margin = new Thickness(4, 0, 0, 0), Text = text, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };

        // ---------- language list ----------

        private void RefreshLanguageList()
        {
            var filter = (_searchBox.Text ?? string.Empty).Trim();
            var recent = _settings?.RecentLanguages ?? new List<string>();
            IEnumerable<GrammarPack> packs = _languages.All;
            if (filter.Length > 0)
                packs = packs.Where(p => p.Display.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0);

            var ordered = packs
                .OrderByDescending(p => recent.Take(3).Any(r => string.Equals(r, p.Id, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(p => recent.Take(3).Any(r => string.Equals(r, p.Id, StringComparison.OrdinalIgnoreCase)) ? -recent.IndexOf(p.Id) : 0)
                .ThenBy(p => p.Display, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _languageList.Items.Clear();
            foreach (var p in ordered)
                _languageList.Items.Add(new LanguageItem(p, recent.Contains(p.Id)));

            var wanted = SelectedLanguageId ?? _detectedLanguage ?? _settings?.General?.DefaultLanguage;
            var match = ordered.FirstOrDefault(p => string.Equals(p.Id, wanted, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                _languageList.SelectedItem = new LanguageItem(match, recent.Contains(match.Id));
            else if (_languageList.Items.Count > 0 && _languageList.SelectedItem == null)
                _languageList.SelectedIndex = 0;
        }

        private string SelectedLanguageId => (_languageList?.SelectedItem as LanguageItem)?.Pack?.Id;

        private sealed class LanguageItem
        {
            public GrammarPack Pack { get; }
            private readonly bool _recent;
            public LanguageItem(GrammarPack pack, bool recent) { Pack = pack; _recent = recent; }
            public override string ToString() => _recent ? "★ " + Pack.Display : Pack.Display;
            public override bool Equals(object obj) => obj is LanguageItem other && string.Equals(other.Pack?.Id, Pack?.Id, StringComparison.OrdinalIgnoreCase);
            public override int GetHashCode() => Pack?.Id?.GetHashCode() ?? 0;
        }

        // ---------- preview ----------

        private void SchedulePreview() => _previewTimer?.Restart();

        private void ScheduleDetect()
        {
            if (_languageTouched)
                return;
            if (_detectHost == null)
                CreateDetectTimer();
            _detectHost?.Stop();
            _detectHost?.Start();
        }

        private DispatcherTimer _detectHost;
        private void CreateDetectTimer()
        {
            if (_detectHost != null)
                return;
            _detectHost = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(400) };
            _detectHost.Tick += (s, e) =>
            {
                _detectHost.Stop();
                var id = _languages.DetectLanguage(_sourceBox.Text);
                if (id == null || string.Equals(id, SelectedLanguageId, StringComparison.OrdinalIgnoreCase))
                    return;
                _detectedLanguage = id;
                for (var i = 0; i < _languageList.Items.Count; i++)
                {
                    if (_languageList.Items[i] is LanguageItem item &&
                        string.Equals(item.Pack.Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        _languageList.SelectedIndex = i;
                        break;
                    }
                }
                SyncAutoTitle();
            };
        }

        private void SyncAutoTitle()
        {
            if (_titleTouched)
                return;
            var pack = (_languageList?.SelectedItem as LanguageItem)?.Pack;
            if (pack != null)
                _titleBox.Text = pack.Display;
        }

        private void RefreshPreview()
        {
            if (_preview == null)
                return;
            var source = _sourceBox.Text;
            var options = CollectOptions(includeSource: false);
            var doc = new FlowDocument(new Paragraph());
            doc.PagePadding = new Thickness(6);

            if (string.IsNullOrEmpty(source))
            {
                doc.Blocks.Add(new Paragraph(new Run("粘贴或输入代码后此处显示预览…") { Foreground = Brushes.Gray }));
                _preview.Document = doc;
                return;
            }

            if (!_languages.TryGet(options.LanguageId, out var pack))
            {
                doc.Blocks.Add(new Paragraph(new Run("(未知语言，按纯文本插入)") { Foreground = Brushes.Gray }));
                _preview.Document = doc;
                return;
            }

            try
            {
                var renderer = new CodeBlockRenderer(pack, _themes.Get(options.ThemeId));
                var runs = renderer.BuildPreviewRuns(source, options);
                var numbers = OneNoteXmlBuilder.NumberTexts(options.LineNumbers, runs.Count);
                for (var i = 0; i < runs.Count; i++)
                {
                    var p = new Paragraph { Margin = new Thickness(0, 0, 0, 0), LineHeight = 18 };
                    if (options.LineNumbers.Enabled)
                    {
                        p.Inlines.Add(new Run(numbers[i])
                        {
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_themes.Get(options.ThemeId).Colors != null && _themes.Get(options.ThemeId).Colors.TryGetValue("lineNumber", out var c) ? c : "#8B949E"))
                        });
                    }
                    foreach (var run in runs[i])
                    {
                        p.Inlines.Add(new Run(run.Text)
                        {
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(run.Color))
                        });
                    }
                    doc.Blocks.Add(p);
                }
            }
            catch (Exception ex)
            {
                doc.Blocks.Add(new Paragraph(new Run("预览出错: " + ex.Message) { Foreground = Brushes.OrangeRed }));
            }
            _preview.Document = doc;
        }

        // ---------- accept ----------

        private void Accept()
        {
            var source = _sourceBox.Text;
            if (string.IsNullOrWhiteSpace(source))
            {
                MessageBox.Show(this, "请先输入或粘贴代码。", "OneExtend", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var options = CollectOptions(includeSource: false);
            options.Title = string.IsNullOrWhiteSpace(_titleBox.Text) ? null : _titleBox.Text.Trim();
            Result = new InsertCodeBlockResult
            {
                Source = source,
                LanguageId = options.LanguageId,
                Options = options
            };
            DialogResult = true;
            Close();
        }

        private CodeBlockOptions CollectOptions(bool includeSource)
        {
            var options = new CodeBlockOptions
            {
                Title = _titleBox?.Text,
                LanguageId = SelectedLanguageId ?? "plain",
                ThemeId = "light",
                Beautify = _beautifyCheck?.IsChecked == true,
                Format = new FormatOptions
                {
                    IndentWidth = _indentBox?.SelectedIndex == 0 ? 2 : _indentBox?.SelectedIndex == 2 ? 8 : 4,
                    KeywordCase = (KeywordCase)Math.Max(0, _keywordCaseBox?.SelectedIndex ?? 0),
                    CommaPosition = _settings?.Formatting?.CommaPosition ?? CommaPosition.Trailing
                },
                LineNumbers = new LineNumberOptions
                {
                    Enabled = _lineNumbersCheck?.IsChecked == true,
                    Start = ParseInt(_lineStartBox?.Text, 1),
                    Step = ParseInt(_lineStepBox?.Text, 1),
                    Mode = _lineModeBox?.SelectedIndex == 1 ? LineNumberMode.Inline : LineNumberMode.Column,
                    Separator = (LineNumberSeparator)Math.Max(0, _separatorBox?.SelectedIndex ?? 0)
                }
            };
            var themeName = _themeBox?.SelectedItem as string;
            var theme = _themes.All.FirstOrDefault(t => string.Equals(t.Name ?? t.Id, themeName, StringComparison.OrdinalIgnoreCase));
            if (theme != null)
                options.ThemeId = theme.Id;
            return options;
        }

        private static int ParseInt(string text, int fallback) =>
            int.TryParse(text, out var n) && n >= 0 ? n : fallback;
    }

    internal static class DispatcherTimerExtensions
    {
        public static void Restart(this DispatcherTimer timer)
        {
            timer.Stop();
            timer.Start();
        }
    }
}
