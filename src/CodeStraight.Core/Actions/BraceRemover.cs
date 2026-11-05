using System.Collections.Generic;
using System.Linq;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Removes the braces of <c>if</c>/<c>else</c>, <c>for</c>, <c>foreach</c> and <c>while</c> blocks that hold a single
/// simple statement, following <c>csharp_prefer_braces</c> of the <c>.editorconfig</c>: <c>false</c> removes them always,
/// <c>when_multiline</c> only when the header and the statement fit on one line each.
/// To keep the meaning and the readability of the code, the braces stay when:
/// the statement is not a simple one (declarations, nested <c>if</c> or loops, blocks: avoids the dangling else),
/// there are comments or directives around the braces, or any branch of an <c>if</c>/<c>else</c> chain needs them
/// (the whole chain keeps the same style).
/// </summary>
public static class BraceRemover
{
	public static SyntaxNode Remove(SyntaxNode root, StyleOptions style)
	{
		if (style.PreferBraces is not (PreferBracesStyle.False or PreferBracesStyle.WhenMultiline))
		{
			return root;
		}

		SourceText text = root.SyntaxTree.GetText();
		return new Rewriter(text, style, NewLine(root, style)).Visit(root);
	}

	private static string NewLine(SyntaxNode root, StyleOptions style)
	{
		if (style.NewLine is string configured)
		{
			return configured;
		}

		SyntaxTrivia endOfLine = root.DescendantTrivia().FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));
		return endOfLine.IsKind(SyntaxKind.EndOfLineTrivia) ? endOfLine.ToString() : "\n";
	}

	private sealed class Rewriter : CSharpSyntaxRewriter
	{
		private readonly SourceText _text;
		private readonly StyleOptions _style;
		private readonly string _newLine;

		public Rewriter(SourceText text, StyleOptions style, string newLine)
		{
			_text = text;
			_style = style;
			_newLine = newLine;
		}

		public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
		{
			var visited = (IfStatementSyntax)base.VisitIfStatement(node)!;

			// The chain is handled from its first if; an else if is rewritten together with it.
			if (node.Parent is ElseClauseSyntax || !ChainCanLoseBraces(node))
			{
				return visited;
			}

			string indent = Indentation(node.SpanStart);
			return RewriteChain(visited, indent);
		}

		public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
		{
			var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
			if (!CanLoseBraces(node.Statement, node.ForKeyword, node.CloseParenToken))
			{
				return visited;
			}

			string indent = Indentation(node.SpanStart);
			return visited
				.WithCloseParenToken(EndWithNewLine(visited.CloseParenToken))
				.WithStatement(Unwrap((BlockSyntax)visited.Statement, indent, forceNewLine: false));
		}

		public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
		{
			var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
			if (!CanLoseBraces(node.Statement, node.ForEachKeyword, node.CloseParenToken))
			{
				return visited;
			}

			string indent = Indentation(node.SpanStart);
			return visited
				.WithCloseParenToken(EndWithNewLine(visited.CloseParenToken))
				.WithStatement(Unwrap((BlockSyntax)visited.Statement, indent, forceNewLine: false));
		}

		public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
		{
			var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
			if (!CanLoseBraces(node.Statement, node.WhileKeyword, node.CloseParenToken))
			{
				return visited;
			}

			string indent = Indentation(node.SpanStart);
			return visited
				.WithCloseParenToken(EndWithNewLine(visited.CloseParenToken))
				.WithStatement(Unwrap((BlockSyntax)visited.Statement, indent, forceNewLine: false));
		}

		/// <summary>Every branch of the chain is a block that can lose its braces or already has none.</summary>
		private bool ChainCanLoseBraces(IfStatementSyntax first)
		{
			bool anyBlock = false;
			IfStatementSyntax? current = first;
			while (current is not null)
			{
				if (!BranchAllows(current.Statement, current.IfKeyword, current.CloseParenToken, ref anyBlock))
				{
					return false;
				}

				ElseClauseSyntax? elseClause = current.Else;
				if (elseClause is null)
				{
					break;
				}

				if (!OnlyWhitespace(elseClause.ElseKeyword.LeadingTrivia) || !OnlyWhitespace(elseClause.ElseKeyword.TrailingTrivia))
				{
					return false;
				}

				if (elseClause.Statement is IfStatementSyntax elseIf)
				{
					current = elseIf;
					continue;
				}

				if (!BranchAllows(elseClause.Statement, elseClause.ElseKeyword, elseClause.ElseKeyword, ref anyBlock))
				{
					return false;
				}

				break;
			}

			return anyBlock;
		}

		private bool BranchAllows(StatementSyntax statement, SyntaxToken headerStart, SyntaxToken headerEnd, ref bool anyBlock)
		{
			if (statement is not BlockSyntax)
			{
				return true;
			}

			anyBlock = true;
			return CanLoseBraces(statement, headerStart, headerEnd);
		}

		private bool CanLoseBraces(StatementSyntax statement, SyntaxToken headerStart, SyntaxToken headerEnd)
		{
			if (statement is not BlockSyntax { Statements.Count: 1 } block || block.ContainsDirectives)
			{
				return false;
			}

			StatementSyntax inner = block.Statements[0];
			if (!IsSimple(inner)
				|| !OnlyWhitespace(block.OpenBraceToken.LeadingTrivia)
				|| !OnlyWhitespace(block.OpenBraceToken.TrailingTrivia)
				|| !OnlyWhitespace(block.CloseBraceToken.LeadingTrivia)
				|| !OnlyWhitespace(inner.GetLeadingTrivia()))
			{
				return false;
			}

			return _style.PreferBraces != PreferBracesStyle.WhenMultiline
				|| (IsSingleLine(TextSpan.FromBounds(headerStart.SpanStart, headerEnd.Span.End)) && IsSingleLine(inner.Span));
		}

		/// <summary>
		/// Statements that can stand without braces and cannot capture an <c>else</c>. Declarations are not valid as
		/// embedded statements and nested <c>if</c>/loops could change which <c>if</c> an <c>else</c> belongs to.
		/// </summary>
		private static bool IsSimple(StatementSyntax statement) => statement is ExpressionStatementSyntax
			or ReturnStatementSyntax
			or ThrowStatementSyntax
			or BreakStatementSyntax
			or ContinueStatementSyntax
			or GotoStatementSyntax
			or YieldStatementSyntax;

		private IfStatementSyntax RewriteChain(IfStatementSyntax node, string indent)
		{
			IfStatementSyntax result = node;
			bool hasElse = node.Else is not null;

			if (node.Statement is BlockSyntax block)
			{
				result = result
					.WithCloseParenToken(EndWithNewLine(node.CloseParenToken))
					.WithStatement(Unwrap(block, indent, forceNewLine: hasElse));
			}
			else if (hasElse)
			{
				// A branch that already had no braces must end its line too, so the else starts on the next one.
				result = result.WithStatement(node.Statement.WithTrailingTrivia(
					TrimEnd(node.Statement.GetTrailingTrivia()).Add(SyntaxFactory.EndOfLine(_newLine))));
			}

			if (node.Else is not ElseClauseSyntax elseClause)
			{
				return result;
			}

			SyntaxToken elseKeyword = elseClause.ElseKeyword.WithLeadingTrivia(SyntaxFactory.Whitespace(indent));
			StatementSyntax elseStatement = elseClause.Statement;
			if (elseStatement is IfStatementSyntax elseIf)
			{
				elseStatement = RewriteChain(elseIf, indent);
			}
			else if (elseStatement is BlockSyntax elseBlock)
			{
				elseKeyword = EndWithNewLine(elseKeyword);
				elseStatement = Unwrap(elseBlock, indent, forceNewLine: false);
			}

			return result.WithElse(elseClause.WithElseKeyword(elseKeyword).WithStatement(elseStatement));
		}

		/// <summary>
		/// Replaces the block with its statement. A statement on its own line keeps its indentation, already one level
		/// deeper than the header (this does not mix tabs and spaces in files not yet migrated to the <c>.editorconfig</c>);
		/// one written after the <c>{</c> gets the header indentation plus one level. The statement keeps its own trailing
		/// comment and takes the trivia after the closing brace, unless an <c>else</c> follows (then it ends the line).
		/// </summary>
		private StatementSyntax Unwrap(BlockSyntax block, string indent, bool forceNewLine)
		{
			StatementSyntax inner = block.Statements[0];
			SyntaxTriviaList trailing = TrimEnd(inner.GetTrailingTrivia());
			trailing = forceNewLine
				? trailing.Add(SyntaxFactory.EndOfLine(_newLine))
				: trailing.AddRange(block.CloseBraceToken.TrailingTrivia);

			bool ownLine = block.OpenBraceToken.TrailingTrivia.Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));
			SyntaxTriviaList leading = ownLine
				? inner.GetLeadingTrivia()
				: SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(indent + _style.IndentUnit));

			return inner.WithLeadingTrivia(leading).WithTrailingTrivia(trailing);
		}

		/// <summary>Keeps a comment after the header and makes the header end the line.</summary>
		private SyntaxToken EndWithNewLine(SyntaxToken token) =>
			token.WithTrailingTrivia(TrimEnd(token.TrailingTrivia).Add(SyntaxFactory.EndOfLine(_newLine)));

		/// <summary>Leading whitespace of the header line, as written (not converted to the configured style).</summary>
		private string Indentation(int position)
		{
			TextLine line = _text.Lines.GetLineFromPosition(position);
			int start = line.Start;
			int end = start;
			while (end < line.End && (_text[end] == ' ' || _text[end] == '\t'))
			{
				end++;
			}

			return _text.ToString(TextSpan.FromBounds(start, end));
		}

		private bool IsSingleLine(TextSpan span) =>
			_text.Lines.GetLineFromPosition(span.Start).LineNumber == _text.Lines.GetLineFromPosition(span.End).LineNumber;

		private static bool OnlyWhitespace(SyntaxTriviaList trivia) =>
			trivia.All(item => item.IsKind(SyntaxKind.WhitespaceTrivia) || item.IsKind(SyntaxKind.EndOfLineTrivia));

		private static SyntaxTriviaList TrimEnd(SyntaxTriviaList trivia)
		{
			List<SyntaxTrivia> items = trivia.ToList();
			while (items.Count > 0 && OnlyWhitespace(SyntaxFactory.TriviaList(items[items.Count - 1])))
			{
				items.RemoveAt(items.Count - 1);
			}

			return SyntaxFactory.TriviaList(items);
		}
	}
}
