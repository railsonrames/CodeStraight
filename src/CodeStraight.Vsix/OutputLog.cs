using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodeStraight.Vsix;

/// <summary>Writes to a "CodeStraight" pane of the Output window. Errors never interrupt the save.</summary>
internal sealed class OutputLog
{
	private static readonly Guid PaneGuid = new("3b0f6c1e-5a2d-4f8e-9c71-2e4d8a6b5f03");
	private readonly IVsOutputWindowPane? _pane;
	private readonly IVsUIShell? _shell;

	public OutputLog(IVsOutputWindow? outputWindow, IVsUIShell? shell = null)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		_shell = shell;
		if (outputWindow is null)
		{
			return;
		}

		Guid guid = PaneGuid;
		outputWindow.CreatePane(ref guid, "CodeStraight", fInitVisible: 1, fClearWithSolution: 0);
		if (ErrorHandler.Succeeded(outputWindow.GetPane(ref guid, out IVsOutputWindowPane pane)))
		{
			_pane = pane;
		}
	}

	public void Write(string message)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		_pane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
	}

	/// <summary>Opens the Output window on the CodeStraight pane.</summary>
	public void Show()
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		Guid output = VSConstants.StandardToolWindows.Output;
		if (_shell is not null
			&& ErrorHandler.Succeeded(_shell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref output, out IVsWindowFrame frame)))
		{
			frame.Show();
		}

		_pane?.Activate();
	}

	public void Write(Exception exception)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		Write($"Error: {exception}");
	}
}
