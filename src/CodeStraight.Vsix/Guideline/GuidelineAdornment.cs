using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using System.Windows.Shapes;
using CodeStraight.Core.Configuration;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace CodeStraight.Vsix.Guideline;

/// <summary>Creates a guideline for each code editor whose file belongs to a solution with a .codestraight file.</summary>
[Export(typeof(IWpfTextViewCreationListener))]
[ContentType("code")]
[TextViewRole(PredefinedTextViewRoles.Document)]
internal sealed class GuidelineViewListener : IWpfTextViewCreationListener
{
	public const string LayerName = "CodeStraight.Guideline";

	[Export(typeof(AdornmentLayerDefinition))]
	[Name(LayerName)]
	[Order(After = PredefinedAdornmentLayers.Selection, Before = PredefinedAdornmentLayers.Text)]
	public AdornmentLayerDefinition? Layer { get; set; }

	[Import]
	public ITextDocumentFactoryService DocumentFactory { get; set; } = null!;

	[Import]
	public ConfigurationService Configuration { get; set; } = null!;

	public void TextViewCreated(IWpfTextView textView)
	{
		if (DocumentFactory.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out ITextDocument document))
		{
			_ = new GuidelineAdornment(textView, document, Configuration.Loader);
		}
	}
}

/// <summary>
/// Draws vertical lines at the columns of <c>guidelines</c> in the <c>.codestraight</c> when <c>show_guideline = true</c>,
/// in the color of <c>guideline_color</c>.
/// The configuration is re-read when the view gets focus or the file is saved, not on every repaint.
/// </summary>
internal sealed class GuidelineAdornment
{
	private static readonly Color DefaultColor = Color.FromArgb(0x70, 0x80, 0x80, 0x80);

	private readonly IWpfTextView _view;
	private readonly ITextDocument _document;
	private readonly ConfigurationLoader _loader;
	private readonly IAdornmentLayer _layer;
	private IReadOnlyList<int> _columns = Array.Empty<int>();
	private Brush? _brush;

	public GuidelineAdornment(IWpfTextView view, ITextDocument document, ConfigurationLoader loader)
	{
		_view = view;
		_document = document;
		_loader = loader;
		_layer = view.GetAdornmentLayer(GuidelineViewListener.LayerName);

		Reload();
		_view.LayoutChanged += OnLayoutChanged;
		_view.GotAggregateFocus += OnGotFocus;
		_document.FileActionOccurred += OnFileAction;
		_view.Closed += OnClosed;
	}

	private void Reload()
	{
		FileConfiguration? configuration = _loader.Load(_document.FilePath);
		_columns = configuration is null ? Array.Empty<int>() : configuration.Settings.GuidelineColumns();
		_brush = CreateBrush(configuration?.Settings.GuidelineColor);
		Draw();
	}

	private void Draw()
	{
		_layer.RemoveAllAdornments();
		if (_columns.Count == 0 || _view.IsClosed || _brush is null)
		{
			return;
		}

		foreach (int column in _columns)
		{
			double x = column * _view.FormattedLineSource.ColumnWidth;
			var line = new Line
			{
				X1 = x,
				X2 = x,
				Y1 = _view.ViewportTop,
				Y2 = _view.ViewportBottom,
				Stroke = _brush,
				StrokeThickness = 1,
				IsHitTestVisible = false,
			};

			_layer.AddAdornment(AdornmentPositioningBehavior.OwnerControlled, null, this, line, null);
		}
	}

	private static Brush CreateBrush(string? color)
	{
		Color value = DefaultColor;
		if (!string.IsNullOrWhiteSpace(color))
		{
			try
			{
				value = (Color)ColorConverter.ConvertFromString(color);
			}
			catch (FormatException)
			{
			}
		}

		var brush = new SolidColorBrush(value);
		brush.Freeze();
		return brush;
	}

	private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) => Draw();

	private void OnGotFocus(object sender, EventArgs e) => Reload();

	private void OnFileAction(object sender, TextDocumentFileActionEventArgs e) => Reload();

	private void OnClosed(object sender, EventArgs e)
	{
		_view.LayoutChanged -= OnLayoutChanged;
		_view.GotAggregateFocus -= OnGotFocus;
		_document.FileActionOccurred -= OnFileAction;
		_view.Closed -= OnClosed;
	}
}
