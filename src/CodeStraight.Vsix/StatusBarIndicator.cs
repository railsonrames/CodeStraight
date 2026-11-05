using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CodeStraight.Core.Configuration;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodeStraight.Vsix;

/// <summary>Result of running CodeStraight on one saved C# file, shown by the status bar indicator.</summary>
internal sealed class SaveOutcome
{
	public SaveOutcome(string fileName, int changed, int errors, bool timedOut)
	{
		FileName = fileName;
		Changed = changed;
		Errors = errors;
		TimedOut = timedOut;
	}

	public string FileName { get; }

	public int Changed { get; }

	public int Errors { get; }

	public bool TimedOut { get; }
}

/// <summary>
/// A small set square in Visual Studio's status bar that tells whether CodeStraight is active for the current document:
/// green dot when its repository has a <c>.codestraight</c> with <c>format_on_save = true</c>, gray when not, orange
/// when the last save reported an error. The tooltip lists the enabled switches; a click opens the Output pane.
/// <para>
/// Visual Studio has no public API to add items to the status bar, so the indicator is inserted into the status bar's
/// WPF tree (as other extensions do). If that tree is not found, the indicator is simply not shown.
/// </para>
/// </summary>
internal sealed class StatusBarIndicator : IVsSelectionEvents
{
	private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(4);
	private static readonly Brush ActiveBrush = Frozen(Color.FromRgb(0x4C, 0xC2, 0x5A));
	private static readonly Brush InactiveBrush = Frozen(Color.FromRgb(0x9A, 0x9A, 0x9A));
	private static readonly Brush ErrorBrush = Frozen(Color.FromRgb(0xF0, 0x9A, 0x2E));

	private readonly ConfigurationLoader _loader;
	private readonly IVsMonitorSelection? _selection;
	private readonly IVsSolution? _solution;
	private readonly Action _onClick;
	private readonly OutputLog _log;

	private readonly Ellipse _dot;
	private readonly TextBlock _flash;
	private readonly FrameworkElement _root;
	private readonly DispatcherTimer _flashTimer;

	private string? _activePath;
	private SaveOutcome? _lastSave;
	private uint _selectionCookie;

	public StatusBarIndicator(
		ConfigurationLoader loader,
		IVsMonitorSelection? selection,
		IVsSolution? solution,
		OutputLog log,
		Action onClick)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		_loader = loader;
		_selection = selection;
		_solution = solution;
		_log = log;
		_onClick = onClick;

		_dot = new Ellipse { Width = 6, Height = 6, Fill = InactiveBrush, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
		_flash = new TextBlock { Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
		_flash.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.StatusBarDefaultTextBrushKey);
		_root = CreateRoot();
		_flashTimer = new DispatcherTimer { Interval = FlashDuration };
		_flashTimer.Tick += (_, _) =>
		{
			_flashTimer.Stop();
			_flash.Visibility = Visibility.Collapsed;
		};
	}

	/// <summary>Inserts the indicator into the status bar, retrying for a while if the main window is not ready yet.</summary>
	public void Start()
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		if (_selection is not null)
		{
			_selection.AdviseSelectionEvents(this, out _selectionCookie);
			if (ErrorHandler.Succeeded(_selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out object frame)))
			{
				_activePath = DocumentPath(frame);
			}
		}

		int attempts = 0;
		var retry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		retry.Tick += (_, _) =>
		{
			if (TryInsert() || ++attempts >= 30)
			{
				retry.Stop();
				if (_root.Parent is null)
				{
					_log.Write("Status bar indicator not shown: the status bar of this Visual Studio version was not found.");
				}
			}
		};

		if (!TryInsert())
		{
			retry.Start();
		}

		Refresh();
	}

	public void Stop()
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		if (_selection is not null && _selectionCookie != 0)
		{
			_selection.UnadviseSelectionEvents(_selectionCookie);
			_selectionCookie = 0;
		}

		(_root.Parent as Panel)?.Children.Remove(_root);
	}

	/// <summary>Called by the save handler after CodeStraight ran on a C# file.</summary>
	public void ReportSave(SaveOutcome outcome)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		_lastSave = outcome;
		_flash.Text = outcome.TimedOut ? "timeout"
			: outcome.Errors > 0 ? $"{outcome.Errors} error(s)"
			: outcome.Changed > 0 ? $"✓ {outcome.Changed}"
			: "✓";
		_flash.Visibility = Visibility.Visible;
		_flashTimer.Stop();
		_flashTimer.Start();
		Refresh();
	}

	/// <summary>Re-reads the configuration for the current document (or the solution) and updates dot and tooltip.</summary>
	public void Refresh()
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		string? path = _activePath ?? SolutionPath();
		FileConfiguration? configuration = path is null ? null : _loader.Load(path);
		bool active = configuration?.Settings.FormatOnSave == true;
		bool failed = _lastSave is { } last && (last.Errors > 0 || last.TimedOut);

		_dot.Fill = !active ? InactiveBrush : failed ? ErrorBrush : ActiveBrush;
		_root.Opacity = active ? 1.0 : 0.6;
		_root.ToolTip = Tooltip(configuration, active);
	}

	private string Tooltip(FileConfiguration? configuration, bool active)
	{
		var text = new StringBuilder();
		if (configuration is null)
		{
			text.AppendLine("CodeStraight: inactive");
			text.AppendLine($"No {CodeStraightSettings.FileName} file above {(_activePath is null ? "the solution" : System.IO.Path.GetFileName(_activePath))}.");
		}
		else
		{
			text.AppendLine(active ? "CodeStraight: active" : "CodeStraight: inactive (format_on_save is not true)");
			text.AppendLine(System.IO.Path.Combine(configuration.RootDirectory, CodeStraightSettings.FileName));
			var switches = configuration.Settings.EnabledSwitches();
			text.AppendLine(switches.Count == 0 ? "No actions enabled." : "Actions: " + string.Join(", ", switches));
		}

		if (_lastSave is { } last)
		{
			string result = last.TimedOut ? "skipped, took too long"
				: last.Errors > 0 ? $"{last.Errors} error(s)"
				: last.Changed > 0 ? $"{last.Changed} action(s) changed the file"
				: "no changes";
			text.AppendLine($"Last save: {last.FileName}: {result}");
		}

		text.Append("Click to open Output → CodeStraight.");
		return text.ToString();
	}

	private FrameworkElement CreateRoot()
	{
		// Set square, like the extension icon, drawn in the status bar text color so it works with every theme.
		var square = new Path
		{
			Data = Geometry.Parse("M1.5,1.5 L1.5,14.5 L14.5,14.5 Z M4.5,8.5 L4.5,11.5 L7.5,11.5 Z"),
			StrokeThickness = 1.2,
			StrokeLineJoin = PenLineJoin.Round,
			Width = 16,
			Height = 16,
		};
		square.SetResourceReference(Shape.StrokeProperty, EnvironmentColors.StatusBarDefaultTextBrushKey);

		var icon = new Grid { Width = 16, Height = 16 };
		icon.Children.Add(square);
		icon.Children.Add(_dot);

		var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
		content.Children.Add(icon);
		content.Children.Add(_flash);

		var root = new Border
		{
			Child = content,
			Padding = new Thickness(6, 0, 6, 0),
			Background = Brushes.Transparent,
			Cursor = Cursors.Hand,
			VerticalAlignment = VerticalAlignment.Stretch,
		};
		ToolTipService.SetShowDuration(root, 30000);
		root.MouseEnter += (_, _) => root.Background = Frozen(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
		root.MouseLeave += (_, _) => root.Background = Brushes.Transparent;
		root.MouseLeftButtonUp += (_, _) => _onClick();
		root.ToolTipOpening += (_, _) =>
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			Refresh();
		};
		return root;
	}

	private bool TryInsert()
	{
		if (_root.Parent is not null)
		{
			return true;
		}

		if (Application.Current?.MainWindow is not Window window)
		{
			return false;
		}

		// "StatusBarPanel" is the DockPanel that holds the items on the right of the status bar (Git, notifications…).
		Panel? panel = FindByName(window, "StatusBarPanel") as Panel
			?? (FindByName(window, "StatusBarContainer") is DependencyObject container ? FindDescendant<DockPanel>(container) : null);
		if (panel is null)
		{
			return false;
		}

		DockPanel.SetDock(_root, Dock.Right);
		panel.Children.Insert(Math.Min(1, panel.Children.Count), _root);
		return true;
	}

	private static FrameworkElement? FindByName(DependencyObject parent, string name)
	{
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is FrameworkElement element && element.Name == name)
			{
				return element;
			}

			if (FindByName(child, name) is { } found)
			{
				return found;
			}
		}

		return null;
	}

	private static T? FindDescendant<T>(DependencyObject parent)
		where T : DependencyObject
	{
		int count = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < count; i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T match)
			{
				return match;
			}

			if (FindDescendant<T>(child) is { } found)
			{
				return found;
			}
		}

		return null;
	}

	private string? SolutionPath()
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		if (_solution is null || ErrorHandler.Failed(_solution.GetSolutionInfo(out string directory, out _, out _))
			|| string.IsNullOrEmpty(directory))
		{
			return null;
		}

		// The loader walks up from the folder of a file; any name inside the solution folder will do.
		return System.IO.Path.Combine(directory, "_");
	}

	private static string? DocumentPath(object? frame)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		return frame is IVsWindowFrame windowFrame
			&& ErrorHandler.Succeeded(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object moniker))
			&& moniker is string path && path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0 && System.IO.Path.IsPathRooted(path)
				? path
				: null;
	}

	private static Brush Frozen(Color color)
	{
		var brush = new SolidColorBrush(color);
		brush.Freeze();
		return brush;
	}

	public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		if (elementid == (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame)
		{
			_activePath = DocumentPath(varValueNew);
			try
			{
				Refresh();
			}
			catch (Exception exception)
			{
				_log.Write(exception);
			}
		}

		return VSConstants.S_OK;
	}

	public int OnSelectionChanged(
		IVsHierarchy pHierOld,
		uint itemidOld,
		IVsMultiItemSelect pMISOld,
		ISelectionContainer pSCOld,
		IVsHierarchy pHierNew,
		uint itemidNew,
		IVsMultiItemSelect pMISNew,
		ISelectionContainer pSCNew) => VSConstants.S_OK;

	public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) => VSConstants.S_OK;
}
