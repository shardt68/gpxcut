using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace GpxCut.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		var startupFilePath = ResolveStartupFilePath(e.Args);
		var mainWindow = new MainWindow(startupFilePath);
		MainWindow = mainWindow;
		mainWindow.Show();
	}

	private static string? ResolveStartupFilePath(string[] args)
	{
		if (args.Length == 0)
		{
			return null;
		}

		var firstArg = args[0];
		if (string.IsNullOrWhiteSpace(firstArg))
		{
			return null;
		}

		try
		{
			return Path.GetFullPath(firstArg);
		}
		catch (Exception)
		{
			return null;
		}
	}
}

