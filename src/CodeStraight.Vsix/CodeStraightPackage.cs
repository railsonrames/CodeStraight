using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace CodeStraight.Vsix;

/// <summary>
/// Loads in the background when a solution is opened and subscribes to document saves.
/// The guideline adornment is a MEF component and does not depend on this package. The package also shows the
/// <see cref="StatusBarIndicator"/>.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideBindingPath]
[InstalledProductRegistration("CodeStraight", "Keeps C# code straight on every save.", "0.9.1")]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageGuidString)]
public sealed class CodeStraightPackage : AsyncPackage
{
	public const string PackageGuidString = "c2f5a8d4-7e1b-4a63-9f02-5b8e3d6c1a97";

	private IVsRunningDocumentTable? _documentTable;
	private uint _eventsCookie;
	private StatusBarIndicator? _indicator;

	protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
	{
		await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

		var componentModel = (IComponentModel?)await GetServiceAsync(typeof(SComponentModel));
		_documentTable = (IVsRunningDocumentTable?)await GetServiceAsync(typeof(SVsRunningDocumentTable));
		var outputWindow = (IVsOutputWindow?)await GetServiceAsync(typeof(SVsOutputWindow));
		var shell = (IVsUIShell?)await GetServiceAsync(typeof(SVsUIShell));
		var selection = (IVsMonitorSelection?)await GetServiceAsync(typeof(SVsShellMonitorSelection));
		var vsSolution = (IVsSolution?)await GetServiceAsync(typeof(SVsSolution));
		if (componentModel is null || _documentTable is null)
		{
			return;
		}

		var log = new OutputLog(outputWindow, shell);
		var loader = componentModel.GetService<ConfigurationService>().Loader;

		// The indicator is a convenience: if it fails, saving keeps working.
		try
		{
			_indicator = new StatusBarIndicator(loader, selection, vsSolution, log, log.Show);
			_indicator.Start();
		}
		catch (Exception exception)
		{
			log.Write(exception);
			_indicator = null;
		}

		var handler = new SaveHandler(
			_documentTable,
			componentModel.GetService<VisualStudioWorkspace>(),
			loader,
			log,
			componentModel.GetService<Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService>(),
			componentModel.GetService<Microsoft.VisualStudio.Text.ITextDocumentFactoryService>(),
			_indicator);

		_documentTable.AdviseRunningDocTableEvents(handler, out _eventsCookie);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _documentTable is not null && _eventsCookie != 0)
		{
			ThreadHelper.JoinableTaskFactory.Run(async () =>
			{
				await JoinableTaskFactory.SwitchToMainThreadAsync();
				_documentTable.UnadviseRunningDocTableEvents(_eventsCookie);
				_eventsCookie = 0;
				_indicator?.Stop();
			});
		}

		base.Dispose(disposing);
	}
}
