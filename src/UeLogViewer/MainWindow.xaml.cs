using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace UeLogViewer;

public partial class MainWindow : Window
{
	/// Above this many new visible lines in one batch, rebuild the list instead of adding one by one.
	const int BulkAddThreshold = 2000;

	readonly ViewerSettings settings;
	readonly List<LogEntry> allEntries = new();
	readonly ObservableCollection<CategoryItem> categories = new();
	readonly Dictionary<string, CategoryItem> categoryMap = new();
	readonly ICollectionView categoryView;
	readonly DispatcherTimer searchTimer;

	ObservableCollection<LogEntry> visibleEntries = new();
	LogTailer? tailer;
	ScrollViewer? logScroll;
	Func<string, bool>? searchMatch;
	bool follow = true;
	bool fileMissing;
	int errorCount;
	int warningCount;
	int logCount;
	string? contextCategory;

	public MainWindow(string? pathOverride)
	{
		InitializeComponent();
		settings = ViewerSettings.Load();

		categoryView = CollectionViewSource.GetDefaultView(categories);
		categoryView.Filter = FilterCategoryRow;
		CategoryList.ItemsSource = categoryView;

		searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
		searchTimer.Tick += OnSearchTimerTick;

		ErrorsCheck.IsChecked = settings.ShowErrors;
		WarningsCheck.IsChecked = settings.ShowWarnings;
		LogsCheck.IsChecked = settings.ShowLogs;
		RegexCheck.IsChecked = settings.UseRegex;
		TimestampsCheck.IsChecked = settings.ShowTimestamps;
		NewCategoriesCheck.IsChecked = settings.NewCategoriesVisible;
		FollowCheck.IsChecked = true;
		CategoryColumn.Width = new GridLength(Math.Max(150, settings.CategoryPanelWidth));
		SearchBox.Text = settings.Search;
		ApplyTimestampTemplate();
		UpdateSearchMatch();

		LogList.ItemsSource = visibleEntries;
		Open(pathOverride ?? settings.LogPath);
	}

	// ---------------------------------------------------------------- File

	/// Starts following a file. An empty path shows the "no file selected" state (first start).
	/// The path is remembered right away so it survives a crash.
	void Open(string path)
	{
		tailer?.Dispose();
		tailer = null;

		path = path.Trim().Trim('"');
		PathBox.Text = path;
		ClearEntries();
		fileMissing = false;

		bool hasPath = path.Length > 0;
		NoFileHint.Visibility = hasPath ? Visibility.Collapsed : Visibility.Visible;
		Title = hasPath ? $"UE Log Viewer - {Path.GetFileName(path)}" : "UE Log Viewer";

		if (path != settings.LogPath)
		{
			settings.LogPath = path;
			settings.Save();
		}
		UpdateStatus();

		if (!hasPath)
		{
			return;
		}

		LogTailer? created = null;
		created = new LogTailer(path, batch => Dispatcher.BeginInvoke(() => OnBatch(created!, batch)));
		tailer = created;
	}

	void OnBatch(LogTailer source, TailBatch batch)
	{
		// Ignore late batches from a tailer that was replaced.
		if (source != tailer)
		{
			return;
		}

		fileMissing = batch.FileMissing;
		if (batch.Reset)
		{
			ClearEntries();
		}

		var toShow = new List<LogEntry>();
		foreach (LogEntry entry in batch.Entries)
		{
			allEntries.Add(entry);
			CategoryItem category = GetOrAddCategory(entry.Category);
			if (!entry.IsContinuation)
			{
				category.Count++;
				CountLevel(entry.Level);
			}

			if (Passes(entry))
			{
				toShow.Add(entry);
			}
		}

		if (toShow.Count > BulkAddThreshold)
		{
			visibleEntries = new ObservableCollection<LogEntry>(visibleEntries.Concat(toShow));
			LogList.ItemsSource = visibleEntries;
		}
		else
		{
			foreach (LogEntry entry in toShow)
			{
				visibleEntries.Add(entry);
			}
		}

		if (toShow.Count > 0 && follow)
		{
			ScrollToEndDeferred();
		}
		UpdateStatus();
	}

	void ClearEntries()
	{
		allEntries.Clear();
		visibleEntries = new ObservableCollection<LogEntry>();
		LogList.ItemsSource = visibleEntries;
		errorCount = 0;
		warningCount = 0;
		logCount = 0;
		foreach (CategoryItem category in categories)
		{
			category.Count = 0;
		}
		UpdateStatus();
	}

	void CountLevel(LogLevel level)
	{
		switch (level)
		{
			case LogLevel.Fatal:
			case LogLevel.Error:
				errorCount++;
				break;
			case LogLevel.Warning:
				warningCount++;
				break;
			default:
				logCount++;
				break;
		}
	}

	// ---------------------------------------------------------------- Filtering

	bool Passes(LogEntry entry)
	{
		if (!IsLevelShown(entry.Level))
		{
			return false;
		}

		if (!categoryMap.TryGetValue(entry.Category, out CategoryItem? category))
		{
			return false;
		}

		if (!category.IsVisible)
		{
			return false;
		}

		if (searchMatch != null && !searchMatch(entry.Raw))
		{
			return false;
		}

		return true;
	}

	bool IsLevelShown(LogLevel level)
	{
		switch (level)
		{
			case LogLevel.Fatal:
			case LogLevel.Error:
				return settings.ShowErrors;
			case LogLevel.Warning:
				return settings.ShowWarnings;
			default:
				return settings.ShowLogs;
		}
	}

	/// Rebuilds the visible list from all entries, keeping the selected line in view when not following.
	void Refilter()
	{
		var anchor = LogList.SelectedItem as LogEntry;
		visibleEntries = new ObservableCollection<LogEntry>(allEntries.Where(Passes));
		LogList.ItemsSource = visibleEntries;

		if (follow)
		{
			ScrollToEndDeferred();
		}
		else if (anchor != null && visibleEntries.Contains(anchor))
		{
			LogList.SelectedItem = anchor;
			LogList.ScrollIntoView(anchor);
		}
		UpdateStatus();
	}

	void UpdateSearchMatch()
	{
		string text = SearchBox.Text;
		SearchBox.ClearValue(Border.BorderBrushProperty);
		SearchBox.ToolTip = "Text to find in the line (case-insensitive). Esc clears.";

		if (string.IsNullOrEmpty(text))
		{
			searchMatch = null;
			return;
		}

		if (settings.UseRegex)
		{
			try
			{
				var regex = new Regex(text, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				searchMatch = regex.IsMatch;
			}
			catch (ArgumentException ex)
			{
				searchMatch = null;
				SearchBox.BorderBrush = Brushes.Red;
				SearchBox.ToolTip = ex.Message;
			}
			return;
		}

		searchMatch = line => line.Contains(text, StringComparison.OrdinalIgnoreCase);
	}

	void OnFilterToggled(object sender, RoutedEventArgs e)
	{
		settings.ShowErrors = ErrorsCheck.IsChecked == true;
		settings.ShowWarnings = WarningsCheck.IsChecked == true;
		settings.ShowLogs = LogsCheck.IsChecked == true;

		bool useRegex = RegexCheck.IsChecked == true;
		if (useRegex != settings.UseRegex)
		{
			settings.UseRegex = useRegex;
			UpdateSearchMatch();
		}
		Refilter();
	}

	void OnSearchChanged(object sender, TextChangedEventArgs e)
	{
		searchTimer.Stop();
		searchTimer.Start();
	}

	void OnSearchTimerTick(object? sender, EventArgs e)
	{
		searchTimer.Stop();
		settings.Search = SearchBox.Text;
		UpdateSearchMatch();
		Refilter();
	}

	void OnSearchKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Escape)
		{
			return;
		}
		SearchBox.Text = "";
	}

	// ---------------------------------------------------------------- Categories

	CategoryItem GetOrAddCategory(string name)
	{
		if (categoryMap.TryGetValue(name, out CategoryItem? existing))
		{
			return existing;
		}

		bool visible = settings.CategoryStates.TryGetValue(name, out bool remembered) ? remembered : settings.NewCategoriesVisible;
		var item = new CategoryItem(name, visible, OnCategoryVisibilityChanged);
		categoryMap.Add(name, item);

		int index = 0;
		while (index < categories.Count && string.Compare(categories[index].Name, name, StringComparison.OrdinalIgnoreCase) < 0)
		{
			index++;
		}
		categories.Insert(index, item);
		return item;
	}

	void OnCategoryVisibilityChanged(CategoryItem item)
	{
		settings.CategoryStates[item.Name] = item.IsVisible;
		Refilter();
	}

	void SetAllCategories(bool visible)
	{
		settings.NewCategoriesVisible = visible;
		NewCategoriesCheck.IsChecked = visible;
		foreach (CategoryItem item in categories)
		{
			item.SetVisibleSilently(visible);
			settings.CategoryStates[item.Name] = visible;
		}
		Refilter();
	}

	/// Shows only one category; categories that appear later stay hidden.
	void ShowOnlyCategory(string name)
	{
		settings.NewCategoriesVisible = false;
		NewCategoriesCheck.IsChecked = false;
		foreach (CategoryItem item in categories)
		{
			bool visible = item.Name == name;
			item.SetVisibleSilently(visible);
			settings.CategoryStates[item.Name] = visible;
		}
		Refilter();
	}

	void HideCategory(string name)
	{
		if (!categoryMap.TryGetValue(name, out CategoryItem? item))
		{
			return;
		}
		item.IsVisible = false;
	}

	bool FilterCategoryRow(object row)
	{
		string text = CategorySearchBox.Text;
		if (string.IsNullOrEmpty(text))
		{
			return true;
		}
		return ((CategoryItem)row).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
	}

	void OnCategorySearchChanged(object sender, TextChangedEventArgs e)
	{
		categoryView.Refresh();
	}

	void OnShowAllCategories(object sender, RoutedEventArgs e)
	{
		SetAllCategories(true);
	}

	void OnHideAllCategories(object sender, RoutedEventArgs e)
	{
		SetAllCategories(false);
	}

	void OnNewCategoriesToggled(object sender, RoutedEventArgs e)
	{
		settings.NewCategoriesVisible = NewCategoriesCheck.IsChecked == true;
	}

	void OnCategoryDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (sender is not ListBoxItem { DataContext: CategoryItem item })
		{
			return;
		}
		ShowOnlyCategory(item.Name);
		e.Handled = true;
	}

	void OnCategoryShowOnly(object sender, RoutedEventArgs e)
	{
		if (CategoryList.SelectedItem is not CategoryItem item)
		{
			return;
		}
		ShowOnlyCategory(item.Name);
	}

	// ---------------------------------------------------------------- Log list: scrolling, copy, context menu

	void OnLogListLoaded(object sender, RoutedEventArgs e)
	{
		logScroll = FindChild<ScrollViewer>(LogList);
		if (logScroll == null)
		{
			return;
		}
		logScroll.ScrollChanged += OnLogScrollChanged;
		ScrollToEndDeferred();
	}

	/// Follow turns off when the user moves away from the bottom and back on when they reach it.
	/// Changes caused by new lines (extent or viewport change) are ignored.
	void OnLogScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (logScroll == null)
		{
			return;
		}

		if (e.ExtentHeightChange != 0)
		{
			return;
		}

		if (e.ViewportHeightChange != 0)
		{
			return;
		}

		if (e.VerticalChange == 0)
		{
			return;
		}

		bool atBottom = logScroll.VerticalOffset >= logScroll.ScrollableHeight - 0.5;
		if (atBottom != follow)
		{
			FollowCheck.IsChecked = atBottom;
		}
	}

	void OnFollowChecked(object sender, RoutedEventArgs e)
	{
		follow = true;
		ScrollToEndDeferred();
	}

	void OnFollowUnchecked(object sender, RoutedEventArgs e)
	{
		follow = false;
	}

	void ScrollToEndDeferred()
	{
		Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
		{
			if (!follow)
			{
				return;
			}
			logScroll?.ScrollToEnd();
		});
	}

	void OnCopy(object sender, ExecutedRoutedEventArgs e)
	{
		var selected = LogList.SelectedItems.Cast<LogEntry>().OrderBy(entry => entry.LineNo).ToList();
		if (selected.Count == 0)
		{
			return;
		}

		var text = new StringBuilder();
		foreach (LogEntry entry in selected)
		{
			text.AppendLine(entry.Raw);
		}

		try
		{
			Clipboard.SetText(text.ToString());
		}
		catch (System.Runtime.InteropServices.COMException)
		{
			// Clipboard held by another app; the user can retry.
		}
	}

	void OnLogContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		contextCategory = (LogList.SelectedItem as LogEntry)?.Category;
		bool hasCategory = contextCategory != null;
		ShowOnlyMenuItem.IsEnabled = hasCategory;
		HideCategoryMenuItem.IsEnabled = hasCategory;
		ShowOnlyMenuItem.Header = hasCategory ? $"Show only {contextCategory}" : "Show only this category";
		HideCategoryMenuItem.Header = hasCategory ? $"Hide {contextCategory}" : "Hide this category";
	}

	void OnLineShowOnlyCategory(object sender, RoutedEventArgs e)
	{
		if (contextCategory == null)
		{
			return;
		}
		ShowOnlyCategory(contextCategory);
	}

	void OnLineHideCategory(object sender, RoutedEventArgs e)
	{
		if (contextCategory == null)
		{
			return;
		}
		HideCategory(contextCategory);
	}

	// ---------------------------------------------------------------- Toolbar

	void OnPathKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
		{
			return;
		}
		Open(PathBox.Text);
	}

	void OnOpenPath(object sender, RoutedEventArgs e)
	{
		Open(PathBox.Text);
	}

	void OnBrowse(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*",
		};

		string? folder = Path.GetDirectoryName(PathBox.Text);
		if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
		{
			dialog.InitialDirectory = folder;
		}

		if (dialog.ShowDialog(this) != true)
		{
			return;
		}
		Open(dialog.FileName);
	}

	void OnClear(object sender, RoutedEventArgs e)
	{
		ClearEntries();
	}

	void OnTimestampsToggled(object sender, RoutedEventArgs e)
	{
		settings.ShowTimestamps = TimestampsCheck.IsChecked == true;
		ApplyTimestampTemplate();
	}

	void ApplyTimestampTemplate()
	{
		string key = settings.ShowTimestamps ? "FullLineTemplate" : "ShortLineTemplate";
		LogList.ItemTemplate = (DataTemplate)FindResource(key);
	}

	void OnSplitterDragged(object sender, DragCompletedEventArgs e)
	{
		settings.CategoryPanelWidth = CategoryColumn.ActualWidth;
	}

	void UpdateStatus()
	{
		ErrorsCheck.Content = $"Errors ({errorCount:N0})";
		WarningsCheck.Content = $"Warnings ({warningCount:N0})";
		LogsCheck.Content = $"Logs ({logCount:N0})";

		if (settings.LogPath.Length == 0)
		{
			StatusText.Text = "No log file selected";
			return;
		}

		string state = fileMissing ? "File not found, waiting for it to appear" : "Watching";
		StatusText.Text = $"{visibleEntries.Count:N0} of {allEntries.Count:N0} lines shown    |    {categories.Count} categories    |    {state}: {settings.LogPath}";
	}

	void OnWindowClosing(object? sender, CancelEventArgs e)
	{
		tailer?.Dispose();
		settings.Save();
	}

	static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
	{
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T match)
			{
				return match;
			}

			T? nested = FindChild<T>(child);
			if (nested != null)
			{
				return nested;
			}
		}
		return null;
	}
}
