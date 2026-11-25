using System.IO;
using System.Text.Json;

namespace UeLogViewer;

/// Remembered between runs in UeLogViewer.settings.json next to the exe (portable: nothing is written anywhere else).
public sealed class ViewerSettings
{
	/// Last opened log file. Empty until the user picks one.
	public string LogPath { get; set; } = "";
	/// Visibility of every category seen so far. Unknown categories use NewCategoriesVisible.
	public Dictionary<string, bool> CategoryStates { get; set; } = new();
	public bool NewCategoriesVisible { get; set; } = true;
	public bool ShowErrors { get; set; } = true;
	public bool ShowWarnings { get; set; } = true;
	public bool ShowLogs { get; set; } = true;
	public bool ShowTimestamps { get; set; }
	public string Search { get; set; } = "";
	public bool UseRegex { get; set; }
	public double CategoryPanelWidth { get; set; } = 260;

	/// Environment.ProcessPath is the real exe location, also for a single-file publish.
	static string FilePath => Path.Combine(
		Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "UeLogViewer.settings.json");

	public static ViewerSettings Load()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				var loaded = JsonSerializer.Deserialize<ViewerSettings>(File.ReadAllText(FilePath));
				if (loaded != null)
				{
					return loaded;
				}
			}
		}
		catch (Exception)
		{
			// Corrupt or unreadable settings: start from defaults.
		}
		return new ViewerSettings();
	}

	public void Save()
	{
		try
		{
			// Folder not writable (e.g. a read-only share): settings are simply not kept.
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception)
		{
		}
	}
}
