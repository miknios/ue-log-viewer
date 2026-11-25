using System.Windows;

namespace UeLogViewer;

public partial class App : Application
{
	// Optional first argument: path of the log file to open (overrides the remembered one).
	void OnStartup(object sender, StartupEventArgs e)
	{
		string? path = e.Args.Length > 0 ? e.Args[0] : null;
		var window = new MainWindow(path);
		window.Show();
	}
}
