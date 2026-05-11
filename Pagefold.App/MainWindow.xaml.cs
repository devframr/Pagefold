using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pagefold.Core.Documents;
using Pagefold.Core.Services;

namespace Pagefold.App;

public partial class MainWindow : Window
{
    private readonly IComicDocumentReader _reader = new ComicDocumentReader();
    private readonly LibraryScanner _scanner = new();
    private readonly ObservableCollection<DocumentListItem> _library = new();
    private readonly ObservableCollection<DocumentListItem> _history = new();
    private readonly ObservableCollection<string> _bookmarkLabels = new();
    private readonly List<ReadingBookmark> _bookmarks = new();
    private readonly AppState _state;
    private PagePreloadQueue _preloadQueue;
    private ComicDocument? _document;
    private int _pageIndex;
    private double _zoom = 1;
    private double _lastFitZoom = 1;
    private bool _fitToWindow = true;
    private bool _isPanning;
    private bool _resizeReflowQueued;
    private System.Windows.Point _lastPanPoint;
    private CancellationTokenSource? _preloadCts;

    public MainWindow()
    {
        _state = AppStateStore.Load();
        InitializeComponent();
        LibraryList.ItemsSource = _library;
        HistoryList.ItemsSource = _history;
        BookmarksList.ItemsSource = _bookmarkLabels;
        _preloadQueue = new PagePreloadQueue(_reader);
        RestoreStateToUi();
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open comic",
            Filter = "Comics|*.cbz;*.cbr;*.cb7;*.cbt;*.pdf|CBZ|*.cbz|CBR|*.cbr|CB7|*.cb7|CBT|*.cbt|PDF|*.pdf"
        };

        if (dialog.ShowDialog(this) == true)
        {
            SelectTab(ReaderTab);
            await OpenDocumentAsync(dialog.FileName);
            }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select comic library folder"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        StatusText.Text = "Scanning folder...";
        _library.Clear();
        if (!_state.LibraryFolders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
        {
            _state.LibraryFolders.Add(dialog.FolderName);
        }

        var files = await _scanner.ScanAsync(dialog.FolderName);
        foreach (var file in files)
        {
            AddUnique(_library, file);
        }

        StatusText.Text = $"{files.Count} supported documents found";
        SaveStateFromUi();
        SelectTab(files.Count > 0 ? LibraryTab : ReaderTab);
    }

    private async void LibraryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is DocumentListItem item)
        {
            SelectTab(ReaderTab);
            await OpenDocumentAsync(item.Path);
        }
    }

    private async void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is DocumentListItem item)
        {
            SelectTab(ReaderTab);
            await OpenDocumentAsync(item.Path);
        }
    }

    private async Task OpenDocumentAsync(string path)
    {
        try
        {
            _preloadCts?.Cancel();
            _document = await _reader.OpenAsync(path);
            _pageIndex = _state.Settings.ResumeLastPage && _state.LastPages.TryGetValue(path, out var savedPage)
                ? savedPage
                : 0;
            _zoom = 1;
            _fitToWindow = true;
            EmptyState.Visibility = Visibility.Collapsed;
            ApplyDefaultReadingMode();

            AddRecent(path);

            TitleText.Text = BuildTitle(_document);

            if (_document.Format == ComicBookFormat.Pdf)
            {
                await ShowPdfAsync(path);
                SaveStateFromUi();
                return;
            }

            PdfBrowser.Visibility = Visibility.Collapsed;
            ImageScroll.Visibility = Visibility.Visible;
            await RenderCurrentPageAsync();
            SaveStateFromUi();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Pagefold", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RenderCurrentPageAsync()
    {
        if (_document is null || _document.Pages.Count == 0)
        {
            StatusText.Text = "No pages found";
            return;
        }

        _pageIndex = Math.Clamp(_pageIndex, 0, _document.Pages.Count - 1);
        var primary = _document.Pages[_pageIndex];
        PrimaryPage.Source = await LoadBitmapAsync(primary);

        var showSecond = DoublePageToggle.IsChecked == true && _pageIndex + 1 < _document.Pages.Count;
        SecondaryPage.Visibility = showSecond ? Visibility.Visible : Visibility.Collapsed;
        SecondaryPage.Source = showSecond ? await LoadBitmapAsync(_document.Pages[_pageIndex + 1]) : null;

        ApplyZoom();
        UpdateReadingStatus();
        _state.LastPages[_document.Path] = _pageIndex;
        StartPreload();
    }

    private async Task<BitmapImage> LoadBitmapAsync(ComicPage page)
    {
        if (_document is null)
        {
            throw new InvalidOperationException("No document is open.");
        }

        byte[] bytes;
        if (!_preloadQueue.TryGet(_document, page, out bytes))
        {
            await using var stream = await _reader.OpenPageStreamAsync(_document, page);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            bytes = memory.ToArray();
        }

        var bitmap = new BitmapImage();
        using var bitmapStream = new MemoryStream(bytes);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = bitmapStream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private async Task ShowPdfAsync(string path)
    {
        ImageScroll.Visibility = Visibility.Collapsed;
        PdfBrowser.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        await PdfBrowser.EnsureCoreWebView2Async();
        PdfBrowser.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
        StatusText.Text = "PDF loaded";
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        var step = DoublePageToggle.IsChecked == true ? 2 : 1;
        _pageIndex += MangaToggle.IsChecked == true ? -step : step;
        await RenderCurrentPageAsync();
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        var step = DoublePageToggle.IsChecked == true ? 2 : 1;
        _pageIndex += MangaToggle.IsChecked == true ? step : -step;
        await RenderCurrentPageAsync();
    }

    private async void ModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_document?.Format != ComicBookFormat.Pdf)
        {
            await RenderCurrentPageAsync();
        }
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_document is null)
        {
            return;
        }

        _bookmarks.Add(new ReadingBookmark(_document.Path, _pageIndex, string.Empty, DateTimeOffset.Now));
        _bookmarkLabels.Insert(0, $"{Path.GetFileName(_document.Path)} - page {_pageIndex + 1}");
        _state.Bookmarks.Insert(0, new BookmarkState
        {
            DocumentPath = _document.Path,
            PageIndex = _pageIndex,
            CreatedAt = DateTimeOffset.Now
        });
        SaveStateFromUi();
        StatusText.Text = $"Bookmark saved at page {_pageIndex + 1}";
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowStyle = WindowStyle == WindowStyle.None ? WindowStyle.SingleBorderWindow : WindowStyle.None;
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        var baseline = _fitToWindow ? _lastFitZoom : _zoom;
        SetManualZoom(baseline * 1.15);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        var baseline = _fitToWindow ? _lastFitZoom : _zoom;
        SetManualZoom(baseline / 1.15);
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        SetFitZoom();
    }

    private async void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Right or Key.Space)
        {
            Next_Click(sender, e);
        }
        else if (e.Key is Key.Left or Key.Back)
        {
            Previous_Click(sender, e);
        }
        else if (e.Key == Key.Home)
        {
            _pageIndex = 0;
            await RenderCurrentPageAsync();
        }
        else if (e.Key == Key.End && _document is not null)
        {
            _pageIndex = _document.Pages.Count - 1;
            await RenderCurrentPageAsync();
        }
        else if (e.Key == Key.F11)
        {
            Fullscreen_Click(sender, e);
        }
    }

    private void ApplyZoom()
    {
        if (_fitToWindow)
        {
            var availableWidth = Math.Max(320, ImageScroll.ViewportWidth - 24);
            var availableHeight = Math.Max(320, ImageScroll.ViewportHeight - 24);
            var pageWidth = DoublePageToggle.IsChecked == true ? Math.Max(240, (availableWidth - 12) / 2) : availableWidth;

            _lastFitZoom = ComputeFitZoom(PrimaryPage, pageWidth, availableHeight);
            _zoom = _lastFitZoom;
            ApplyZoomSize(PrimaryPage, _lastFitZoom);
            ApplyZoomSize(SecondaryPage, ComputeFitZoom(SecondaryPage, pageWidth, availableHeight));
            UpdateReadingStatus();
            return;
        }

        ApplyZoomSize(PrimaryPage, _zoom);
        ApplyZoomSize(SecondaryPage, _zoom);
    }

    private void SetFitZoom()
    {
        _fitToWindow = true;
        ApplyZoom();
        ImageScroll.ScrollToHorizontalOffset(0);
        ImageScroll.ScrollToVerticalOffset(0);
        UpdateReadingStatus();
    }

    private void SetManualZoom(double zoom, bool updateStatus = true)
    {
        _fitToWindow = false;
        _zoom = Math.Clamp(zoom, 0.05, 5);
        ApplyZoom();

        if (updateStatus)
        {
            UpdateReadingStatus();
        }
    }

    private double ComputeFitZoom(System.Windows.Controls.Image image, double maxWidth, double maxHeight)
    {
        if (image.Source is not BitmapSource bitmap || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return _lastFitZoom;
        }

        return Math.Clamp(Math.Min(maxWidth / bitmap.Width, maxHeight / bitmap.Height), 0.05, 5);
    }

    private void ApplyZoomSize(System.Windows.Controls.Image image, double zoom)
    {
        image.LayoutTransform = Transform.Identity;
        image.MaxWidth = double.PositiveInfinity;
        image.MaxHeight = double.PositiveInfinity;
        image.Stretch = Stretch.Uniform;

        if (image.Source is BitmapSource bitmap)
        {
            image.Width = Math.Max(1, bitmap.Width * zoom);
            image.Height = Math.Max(1, bitmap.Height * zoom);
        }
        else
        {
            image.Width = double.NaN;
            image.Height = double.NaN;
        }
    }

    private void UpdateReadingStatus()
    {
        if (_document is null)
        {
            StatusText.Text = "Ready";
            return;
        }

        if (_document.Format == ComicBookFormat.Pdf)
        {
            StatusText.Text = "PDF";
            return;
        }

        var zoomText = _fitToWindow ? "Fit" : $"{Math.Round(_zoom * 100)}%";
        StatusText.Text = $"{_pageIndex + 1} / {_document.Pages.Count} - {zoomText}";
    }

    private void ImageScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var shouldZoom = _state.Settings.WheelZooms
            ? Keyboard.Modifiers != ModifierKeys.Control
            : Keyboard.Modifiers == ModifierKeys.Control;

        if (!shouldZoom || _document?.Format == ComicBookFormat.Pdf)
        {
            return;
        }

        var oldZoom = _zoom;
        var factor = e.Delta > 0 ? 1.08 : 0.925;
        SetManualZoom(Math.Clamp(_zoom * factor, 0.05, 5), updateStatus: false);

        var position = e.GetPosition(ImageScroll);
        var ratio = _zoom / oldZoom;
        ImageScroll.ScrollToHorizontalOffset((ImageScroll.HorizontalOffset + position.X) * ratio - position.X);
        ImageScroll.ScrollToVerticalOffset((ImageScroll.VerticalOffset + position.Y) * ratio - position.Y);
        UpdateReadingStatus();
        e.Handled = true;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueReaderReflow();
    }

    private void QueueReaderReflow()
    {
        if (_resizeReflowQueued || _document?.Format == ComicBookFormat.Pdf)
        {
            return;
        }

        _resizeReflowQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _resizeReflowQueued = false;

            if (_document is null || _document.Format == ComicBookFormat.Pdf)
            {
                return;
            }

            ApplyZoom();
            UpdateReadingStatus();
        }), DispatcherPriority.Loaded);
    }

    private void ImageScroll_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = true;
        _lastPanPoint = e.GetPosition(ImageScroll);
        ImageScroll.Cursor = System.Windows.Input.Cursors.SizeAll;
        ImageScroll.CaptureMouse();
        e.Handled = true;
    }

    private void ImageScroll_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var current = e.GetPosition(ImageScroll);
        ImageScroll.ScrollToHorizontalOffset(ImageScroll.HorizontalOffset + (_lastPanPoint.X - current.X));
        ImageScroll.ScrollToVerticalOffset(ImageScroll.VerticalOffset + (_lastPanPoint.Y - current.Y));
        _lastPanPoint = current;
        e.Handled = true;
    }

    private void ImageScroll_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _isPanning = false;
        ImageScroll.Cursor = System.Windows.Input.Cursors.Arrow;
        ImageScroll.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tab)
        {
            SelectTab(tab);
        }
    }

    private void SelectTab(ToggleButton selected)
    {
        var tabs = new[] { LibraryTab, ReaderTab, BookmarksTab, HistoryTab, SettingsTab };
        foreach (var tab in tabs)
        {
            tab.IsChecked = ReferenceEquals(tab, selected);
        }

        LibraryPanel.Visibility = ReferenceEquals(selected, LibraryTab) ? Visibility.Visible : Visibility.Collapsed;
        ReaderPanel.Visibility = ReferenceEquals(selected, ReaderTab) ? Visibility.Visible : Visibility.Collapsed;
        BookmarksPanel.Visibility = ReferenceEquals(selected, BookmarksTab) ? Visibility.Visible : Visibility.Collapsed;
        HistoryPanel.Visibility = ReferenceEquals(selected, HistoryTab) ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = ReferenceEquals(selected, SettingsTab) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _state.Settings.DefaultMode = SelectedComboText(DefaultModeCombo) ?? "Single Page";
        _state.Settings.ResumeLastPage = ResumeLastPageCheck.IsChecked == true;
        _state.Settings.RememberWindowSize = RememberWindowCheck.IsChecked == true;
        _state.Settings.WheelZooms = MouseWheelCombo.SelectedIndex == 1;
        _state.Settings.CacheRadius = Math.Clamp((int)Math.Round(CacheRadiusSlider.Value), 1, 10);
        _state.Settings.PreloadLargeBooks = PreloadLargeBooksCheck.IsChecked == true;
        CacheRadiusText.Text = $"{_state.Settings.CacheRadius} page(s) before and after";
        SaveStateFromUi();
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        SaveStateFromUi();
    }

    private void ClearBookmarks_Click(object sender, RoutedEventArgs e)
    {
        _bookmarks.Clear();
        _bookmarkLabels.Clear();
        _state.Bookmarks.Clear();
        SaveStateFromUi();
    }

    private void ClearLibrary_Click(object sender, RoutedEventArgs e)
    {
        _library.Clear();
        _state.LibraryFolders.Clear();
        SaveStateFromUi();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveStateFromUi();
    }

    private void StartPreload()
    {
        if (_document is null || _document.Format == ComicBookFormat.Pdf)
        {
            return;
        }

        _preloadCts?.Cancel();
        _preloadCts = new CancellationTokenSource();
        var token = _preloadCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                if (_state.Settings.PreloadLargeBooks)
                {
                    await _preloadQueue.PreloadAroundAsync(_document, _pageIndex, _state.Settings.CacheRadius, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private static string BuildTitle(ComicDocument document)
    {
        var title = document.Metadata.Title ?? Path.GetFileNameWithoutExtension(document.Path);
        return document.Metadata.Series is null ? title : $"{document.Metadata.Series} - {title}";
    }

    private void RestoreStateToUi()
    {
        if (_state.Settings.RememberWindowSize)
        {
            Width = Math.Max(MinWidth, _state.Settings.WindowWidth);
            Height = Math.Max(MinHeight, _state.Settings.WindowHeight);
        }

        foreach (var file in _state.LibraryFiles.Where(File.Exists))
        {
            AddUnique(_library, file);
        }

        foreach (var file in _state.RecentFiles.Where(File.Exists))
        {
            AddUnique(_history, file);
        }

        foreach (var bookmark in _state.Bookmarks.Where(b => File.Exists(b.DocumentPath)))
        {
            _bookmarks.Add(new ReadingBookmark(bookmark.DocumentPath, bookmark.PageIndex, string.Empty, bookmark.CreatedAt));
            _bookmarkLabels.Add($"{Path.GetFileName(bookmark.DocumentPath)} - page {bookmark.PageIndex + 1}");
        }

        DefaultModeCombo.SelectedIndex = _state.Settings.DefaultMode switch
        {
            "Double Page" => 1,
            "Manga RTL" => 2,
            _ => 0
        };
        ResumeLastPageCheck.IsChecked = _state.Settings.ResumeLastPage;
        RememberWindowCheck.IsChecked = _state.Settings.RememberWindowSize;
        MouseWheelCombo.SelectedIndex = _state.Settings.WheelZooms ? 1 : 0;
        CacheRadiusSlider.Value = _state.Settings.CacheRadius;
        CacheRadiusText.Text = $"{_state.Settings.CacheRadius} page(s) before and after";
        PreloadLargeBooksCheck.IsChecked = _state.Settings.PreloadLargeBooks;
        StatePathText.Text = $"State file: {AppStateStore.StatePath}";
    }

    private void ApplyDefaultReadingMode()
    {
        DoublePageToggle.IsChecked = _state.Settings.DefaultMode == "Double Page";
        MangaToggle.IsChecked = _state.Settings.DefaultMode == "Manga RTL";
    }

    private void SaveStateFromUi()
    {
        _state.LibraryFiles = _library.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _state.RecentFiles = _history.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();

        if (_document is not null && _document.Format is not ComicBookFormat.Pdf)
        {
            _state.LastPages[_document.Path] = _pageIndex;
        }

        if (_state.Settings.RememberWindowSize)
        {
            _state.Settings.WindowWidth = Width;
            _state.Settings.WindowHeight = Height;
        }

        AppStateStore.Save(_state);
    }

    private void AddRecent(string path)
    {
        var existing = _history.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _history.Remove(existing);
        }

        _history.Insert(0, new DocumentListItem(path));
        while (_history.Count > 30)
        {
            _history.RemoveAt(_history.Count - 1);
        }
    }

    private static void AddUnique(ObservableCollection<DocumentListItem> items, string path)
    {
        if (items.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        items.Add(new DocumentListItem(path));
    }

    private static string? SelectedComboText(System.Windows.Controls.ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
    }
}
