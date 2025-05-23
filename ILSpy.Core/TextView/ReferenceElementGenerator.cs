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

using System;
using System.Linq;
using Avalonia.Input;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace ICSharpCode.ILSpy.TextView
{
	/// <summary>
	/// Creates hyperlinks in the text view.
	/// </summary>
	sealed class ReferenceElementGenerator : VisualLineElementGenerator
	{
		readonly Action<ReferenceSegment> referenceClicked;
		readonly Predicate<ReferenceSegment> isLink;
		
		/// <summary>
		/// The collection of references (hyperlinks).
		/// </summary>
		public TextSegmentCollection<ReferenceSegment> References { get; set; }
		
		public ReferenceElementGenerator(Action<ReferenceSegment> referenceClicked, Predicate<ReferenceSegment> isLink)
		{
			this.referenceClicked = referenceClicked ?? throw new ArgumentNullException(nameof(referenceClicked));
			this.isLink = isLink ?? throw new ArgumentNullException(nameof(isLink));
		}
		
		public override int GetFirstInterestedOffset(int startOffset)
		{
			if (this.References == null)
				return -1;
			// inform AvalonEdit about the next position where we want to build a hyperlink
			var segment = this.References.FindFirstSegmentWithStartAfter(startOffset);
			return segment?.StartOffset ?? -1;
		}
		
		public override VisualLineElement ConstructElement(int offset)
		{
			return this.References == null ? null : (from segment in this.References.FindSegmentsContaining(offset) where isLink(segment) let endOffset = Math.Min(segment.EndOffset, CurrentContext.VisualLine.LastDocumentLine.EndOffset) where offset < endOffset select new VisualLineReferenceText(CurrentContext.VisualLine, endOffset - offset, this, segment)).FirstOrDefault();
		}
		
		internal void JumpToReference(ReferenceSegment referenceSegment)
		{
			referenceClicked(referenceSegment);
		}
	}
	
	/// <summary>
	/// VisualLineElement that represents a piece of text and is a clickable link.
	/// </summary>
	sealed class VisualLineReferenceText : VisualLineText
	{
        private static readonly Cursor HandCursor = new Cursor(StandardCursorType.Hand);

        readonly ReferenceElementGenerator parent;
		readonly ReferenceSegment referenceSegment;
		
		/// <summary>
		/// Creates a visual line text element with the specified length.
		/// It uses the <see cref="ITextRunConstructionContext.VisualLine"/> and its
		/// <see cref="VisualLineElement.RelativeTextOffset"/> to find the actual text string.
		/// </summary>
		public VisualLineReferenceText(VisualLine parentVisualLine, int length, ReferenceElementGenerator parent, ReferenceSegment referenceSegment) : base(parentVisualLine, length)
		{
			this.parent = parent;
			this.referenceSegment = referenceSegment;
		}
		
		/// <inheritdoc/>
		protected override void OnQueryCursor(PointerEventArgs e)
		{
			e.Handled = true;

            if (e.Source is InputElement inputElement)
            {
                inputElement.Cursor = referenceSegment.IsLocal ? Cursor.Default : HandCursor;
            }
        }
		
		/// <inheritdoc/>
		protected override VisualLineText CreateInstance(int length)
		{
			return new VisualLineReferenceText(ParentVisualLine, length, parent, referenceSegment);
		}
	}
}
