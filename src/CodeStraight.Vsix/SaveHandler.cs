using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeStraight.Core;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CodeStraight.Vsix;

/// <summary>
/// Runs CodeStraight right before Visual Studio writes a document to disk, so the saved file already contains the
/// changes: the C# actions on <c>.cs</c> files and, on any text file of the solution, the line breaks and the encoding
/// of the <c>.editorconfig</c>. Any failure is logged and the save goes on with the original text.
/// </summary>
internal sealed class SaveHandler : IVsRunningDocTableEvents3
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	/// <summary>U+FFFD, inserted by the decoder for bytes that are not valid in the encoding used to open the file.</summary>
	private const char ReplacementCharacter = (char)0xFFFD;

	private readonly IVsRunningDocumentTable _documentTable;
	private readonly VisualStudioWorkspace _workspace;
	private readonly ConfigurationLoader _loader;
	private readonly OutputLog _log;
	private readonly IVsEditorAdaptersFactoryService _adapters;
	private readonly ITextDocumentFactoryService _documents;
	private readonly StatusBarIndicator? _indicator;

	/// <summary>Files whose encoding was set before saving, checked on disk right after the save.</summary>
	private readonly Dictionary<string, Charset> _pendingCharsetChecks = new(StringComparer.OrdinalIgnoreCase);

	public SaveHandler(
		IVsRunningDocumentTable documentTable,
		VisualStudioWorkspace workspace,
		ConfigurationLoader loader,
		OutputLog log,
		IVsEditorAdaptersFactoryService adapters,
		ITextDocumentFactoryService documents,
		StatusBarIndicator? indicator)
	{
		_documentTable = documentTable;
		_workspace = workspace;
		_loader = loader;
		_log = log;
		_adapters = adapters;
		_documents = documents;
		_indicator = indicator;
	}

	public int OnBeforeSave(uint docCookie)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		try
		{
			(string? path, ITextDocument? textDocument, IVsTextBuffer? vsBuffer) = GetDocument(docCookie);
			if (path is null)
			{
				return VSConstants.S_OK;
			}

			FileConfiguration? configuration = _loader.Load(path);
			if (configuration is null || !configuration.Settings.FormatOnSave)
			{
				return VSConstants.S_OK;
			}

			if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
			{
				// The C# engine normalizes the line breaks itself, after the rest of the actions.
				DocumentId? id = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(path).FirstOrDefault();
				if (id is not null)
				{
					ThreadHelper.JoinableTaskFactory.Run(() => ApplyAsync(id, configuration, path));
				}
			}
			else if (textDocument is not null && configuration.Settings.NormalizeLineEndings && configuration.Style.NewLine is string newLine)
			{
				NormalizeLineEndings(textDocument.TextBuffer, newLine, path, configuration.Settings.VerboseLog);
			}

			if (textDocument is not null && configuration.Settings.NormalizeCharset)
			{
				ApplyCharset(textDocument, vsBuffer, configuration.Style.Charset, path, configuration.Settings.VerboseLog);
			}
		}
		catch (Exception exception)
		{
			_log.Write(exception);
		}

		return VSConstants.S_OK;
	}

	private async Task ApplyAsync(DocumentId id, FileConfiguration configuration, string path)
	{
		Solution original = _workspace.CurrentSolution;
		Document? document = original.GetDocument(id);
		if (document is null)
		{
			return;
		}

		var stopwatch = Stopwatch.StartNew();
		using var cancellation = new CancellationTokenSource(Timeout);
		var report = new List<string>();
		Solution updated;
		try
		{
			updated = await Task.Run(
				() => CodeStraightEngine.ApplyAsync(document, configuration, cancellation.Token, report),
				cancellation.Token);
		}
		catch (OperationCanceledException)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			_log.Write($"Skipped {System.IO.Path.GetFileName(path)}: took longer than {Timeout.TotalSeconds:0} s.");
			_indicator?.ReportSave(new SaveOutcome(System.IO.Path.GetFileName(path), 0, 0, timedOut: true));
			return;
		}

		await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
		string fileName = System.IO.Path.GetFileName(path);
		foreach (string line in report.Where(line => configuration.Settings.VerboseLog || line.Contains("ERROR")))
		{
			_log.Write($"{fileName}: {line}");
		}

		int errors = report.Count(line => line.Contains("ERROR"));
		if (ReferenceEquals(updated, original))
		{
			_indicator?.ReportSave(new SaveOutcome(fileName, 0, errors, timedOut: false));
			return;
		}

		bool applied = _workspace.TryApplyChanges(updated);
		_indicator?.ReportSave(new SaveOutcome(
			fileName,
			applied ? report.Count(line => line.EndsWith(": changed", StringComparison.Ordinal)) : 0,
			applied ? errors : errors + 1,
			timedOut: false));
		_log.Write(applied
			? $"Applied to {fileName} in {stopwatch.ElapsedMilliseconds} ms."
			: $"Could not apply to {fileName}: the document changed while it was being processed.");
	}

	/// <summary>Path of the document being saved and, when it is a text document, its editor document and buffer.</summary>
	private (string? Path, ITextDocument? Document, IVsTextBuffer? Buffer) GetDocument(uint docCookie)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		int result = _documentTable.GetDocumentInfo(
			docCookie,
			out _,
			out _,
			out _,
			out string moniker,
			out _,
			out _,
			out IntPtr docData);

		ITextDocument? textDocument = null;
		IVsTextBuffer? textBuffer = null;
		if (docData != IntPtr.Zero)
		{
			try
			{
				if (Marshal.GetObjectForIUnknown(docData) is IVsTextBuffer vsBuffer
					&& _adapters.GetDocumentBuffer(vsBuffer) is { } buffer
					&& _documents.TryGetTextDocument(buffer, out ITextDocument document))
				{
					textDocument = document;
					textBuffer = vsBuffer;
				}
			}
			finally
			{
				Marshal.Release(docData);
			}
		}

		return (ErrorHandler.Succeeded(result) ? moniker : null, textDocument, textBuffer);
	}

	/// <summary>Replaces every line break that is not the one of <c>end_of_line</c> (files other than C#).</summary>
	private void NormalizeLineEndings(ITextBuffer buffer, string newLine, string path, bool verbose)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		int changed = 0;
		using (ITextEdit edit = buffer.CreateEdit())
		{
			foreach (ITextSnapshotLine line in buffer.CurrentSnapshot.Lines)
			{
				if (line.LineBreakLength > 0 && line.GetLineBreakText() != newLine)
				{
					edit.Replace(line.End, line.LineBreakLength, newLine);
					changed++;
				}
			}

			if (changed > 0)
			{
				edit.Apply();
			}
		}

		if (verbose || changed > 0)
		{
			_log.Write($"{System.IO.Path.GetFileName(path)}: normalize_line_endings: {(changed > 0 ? $"{changed} line breaks changed" : "no changes")}");
		}
	}

	/// <summary>
	/// Makes Visual Studio save the file with the encoding of <c>charset</c>. The encoding is set both on the editor
	/// document and on the buffer setting that Visual Studio actually uses when writing the file (the one changed by
	/// "Save with Encoding"); the result is checked on disk after the save.
	/// </summary>
	private void ApplyCharset(ITextDocument document, IVsTextBuffer? vsBuffer, Charset charset, string path, bool verbose)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		Encoding? target = TargetEncoding(charset);
		if (target is null)
		{
			return;
		}

		string fileName = System.IO.Path.GetFileName(path);

		// U+FFFD (the replacement character) means some bytes could not be decoded: the file was opened with the wrong
		// encoding, for example a Windows-1252 file read as UTF-8. Saving it makes the damage permanent (the "é" of
		// "Método" is lost), so the encoding is left alone and the developer is warned.
		if (document.TextBuffer.CurrentSnapshot.GetText().IndexOf(ReplacementCharacter) >= 0)
		{
			_log.Write($"{fileName}: normalize_charset: WARNING, the text contains '{ReplacementCharacter}' (characters that could "
				+ $"not be read as {document.Encoding.WebName}). The encoding was not changed; close without saving and reopen "
				+ "the file with the right encoding.");
			return;
		}

		string before = DescribeFileOnDisk(path);
		document.Encoding = target;

		uint flags = (uint)target.CodePage | (target.GetPreamble().Length > 0 ? (uint)__VSTFF.VSTFF_SIGNATURE : 0u);
		if (vsBuffer is IVsUserData userData)
		{
			Guid encodingKey = VSConstants.VsTextBufferUserDataGuid.VsBufferEncodingVSTFF_guid;
			userData.SetData(ref encodingKey, flags);
		}

		_pendingCharsetChecks[path] = charset;
		if (verbose)
		{
			_log.Write($"{fileName}: normalize_charset: on disk before saving: {before}; saving as {charset}");
		}
	}

	private static Encoding? TargetEncoding(Charset charset) => charset switch
	{
		Charset.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
		Charset.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
		Charset.Latin1 => Encoding.GetEncoding(28591),
		Charset.Utf16Le => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
		Charset.Utf16Be => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
		_ => null,
	};

	/// <summary>How the file is encoded on disk: UTF-8 with or without BOM, or not valid UTF-8 (a legacy code page).</summary>
	private static string DescribeFileOnDisk(string path)
	{
		try
		{
			byte[] bytes = System.IO.File.ReadAllBytes(path);
			bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
			try
			{
				new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
				return bom ? "UTF-8 with BOM" : "UTF-8 without BOM";
			}
			catch (DecoderFallbackException)
			{
				return "not valid UTF-8 (legacy code page, e.g. Windows-1252)";
			}
		}
		catch (System.IO.IOException)
		{
			return "unreadable";
		}
	}

	/// <summary>Reads the saved file back and reports whether it really has the configured encoding.</summary>
	public int OnAfterSave(uint docCookie)
	{
		ThreadHelper.ThrowIfNotOnUIThread();
		try
		{
			(string? path, _, _) = GetDocument(docCookie);
			if (path is null || !_pendingCharsetChecks.TryGetValue(path, out Charset charset))
			{
				return VSConstants.S_OK;
			}

			_pendingCharsetChecks.Remove(path);
			string actual = DescribeFileOnDisk(path);
			string expected = charset switch
			{
				Charset.Utf8 => "UTF-8 without BOM",
				Charset.Utf8Bom => "UTF-8 with BOM",
				_ => actual,
			};

			string fileName = System.IO.Path.GetFileName(path);
			_log.Write(actual == expected
				? $"{fileName}: normalize_charset: saved as {actual}"
				: $"{fileName}: normalize_charset: WARNING, expected {expected} but the file on disk is {actual}");
		}
		catch (Exception exception)
		{
			_log.Write(exception);
		}

		return VSConstants.S_OK;
	}

	public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

	public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

	public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

	public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) => VSConstants.S_OK;

	public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;

	public int OnAfterAttributeChangeEx(
		uint docCookie,
		uint grfAttribs,
		IVsHierarchy pHierOld,
		uint itemidOld,
		string pszMkDocumentOld,
		IVsHierarchy pHierNew,
		uint itemidNew,
		string pszMkDocumentNew) => VSConstants.S_OK;
}
