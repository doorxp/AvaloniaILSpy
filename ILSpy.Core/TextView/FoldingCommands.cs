﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System.Linq;

using AvaloniaEdit;
using AvaloniaEdit.Folding;
using ICSharpCode.ILSpy.Properties;

namespace ICSharpCode.ILSpy.TextView
{
	[ExportContextMenuEntryAttribute(Header = nameof(Resources.ToggleFolding), Category = nameof(Resources.Folding))]
	internal sealed class ToggleAllContextMenuEntry : IContextMenuEntry
	{
		public bool IsVisible(TextViewContext context)
		{
			return context.TextView != null;
		}

		public bool IsEnabled(TextViewContext context)
		{
			return context.TextView != null && context.TextView.FoldingManager != null;
		}

		public void Execute(TextViewContext context)
		{
			var foldingManager = context.TextView?.FoldingManager;
			if (foldingManager == null)
				return;
			var doFold = foldingManager.AllFoldings.All(fm => !fm.IsFolded);
			foreach (var fm in foldingManager.AllFoldings) {
				fm.IsFolded = doFold;
			}
		}
	}

	[ExportContextMenuEntryAttribute(Header = nameof(Resources._ToggleFolding), Category = nameof(Resources.Folding))]
	internal sealed class ToggleContextMenuEntry : IContextMenuEntry
	{
		public bool IsVisible(TextViewContext context)
		{
			return context.TextView != null;
		}

		public bool IsEnabled(TextViewContext context)
		{
			return context.TextView != null && context.TextView.FoldingManager != null;
		}

		public void Execute(TextViewContext context)
		{
			var textView = context.TextView;
			if (null == textView)
				return;
			var editor = textView.textEditor;
			var foldingManager = context.TextView.FoldingManager;
			if (null == foldingManager)
				return;
			// TODO: or use Caret if position is not given?
			var posBox = context.Position;
			if (null == posBox)
				return;
			var pos = posBox.Value;
			// look for folding on this line:
			var folding = foldingManager.GetNextFolding(editor.Document.GetOffset(pos.Line, 1));
			if (folding == null || editor.Document.GetLineByOffset(folding.StartOffset).LineNumber != pos.Line) {
				// no folding found on current line: find innermost folding containing the mouse position
				folding = foldingManager.GetFoldingsContaining(editor.Document.GetOffset(pos.Line, pos.Column)).LastOrDefault();
			}
			if (folding != null) {
				folding.IsFolded = !folding.IsFolded;
			}
		}
	}

}
