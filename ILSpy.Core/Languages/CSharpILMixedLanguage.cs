﻿// Copyright (c) 2018 Siegfried Pammer
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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using System.Windows;
using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.ILSpy
{
	using SequencePoint = ICSharpCode.Decompiler.DebugInfo.SequencePoint;

	[Export(typeof(Language))]
	class CSharpILMixedLanguage : ILLanguage
	{
		public override string Name => "IL with C#";

		protected override ReflectionDisassembler CreateDisassembler(ITextOutput output, DecompilationOptions options)
		{
			return new ReflectionDisassembler(output, 
				new MixedMethodBodyDisassembler(output, options) {
					DetectControlStructure = detectControlStructure,
					ShowSequencePoints = options.DecompilerSettings.ShowDebugInfo
				},
				options.CancellationToken);
		}

		static CSharpDecompiler CreateDecompiler(PEFile module, DecompilationOptions options)
		{
			var decompiler = new CSharpDecompiler(module, module.GetAssemblyResolver(), options.DecompilerSettings)
				{
					CancellationToken = options.CancellationToken
				};
			return decompiler;
		}

		static void WriteCode(TextWriter output, DecompilerSettings settings, SyntaxTree syntaxTree, IDecompilerTypeSystem typeSystem)
		{
			syntaxTree.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
            TokenWriter tokenWriter = new TextWriterTokenWriter(output) { IndentationString = settings.CSharpFormattingOptions.IndentationString };
            tokenWriter = TokenWriter.WrapInWriterThatSetsLocationsInAST(tokenWriter);
			syntaxTree.AcceptVisitor(new CSharpOutputVisitor(tokenWriter, settings.CSharpFormattingOptions));
		}
		
		class MixedMethodBodyDisassembler : MethodBodyDisassembler
		{
			readonly DecompilationOptions options;
			// list sorted by IL offset
			IList<SequencePoint> sequencePoints;
			// lines of raw c# source code
			string[] codeLines;

			public MixedMethodBodyDisassembler(ITextOutput output, DecompilationOptions options)
				: base(output, options.CancellationToken)
			{
				this.options = options;
			}

			public override void Disassemble(MetadataFile module, MethodDefinitionHandle handle)
			{
				try {
					var csharpOutput = new StringWriter();
					var decompiler = CreateDecompiler((PEFile)module, options);
					var st = decompiler.Decompile(handle);
					WriteCode(csharpOutput, options.DecompilerSettings, st, decompiler.TypeSystem);
					var mapping = decompiler.CreateSequencePoints(st).FirstOrDefault(kvp => (kvp.Key.MoveNextMethod ?? kvp.Key.Method).MetadataToken == handle);
					this.sequencePoints = mapping.Value ?? (IList<SequencePoint>)EmptyList<SequencePoint>.Instance;
					this.codeLines = csharpOutput.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None);
					base.Disassemble(module, handle);
				} finally {
					this.sequencePoints = null;
					this.codeLines = null;
				}
			}
			protected override void WriteInstruction(ITextOutput output, MetadataFile metadataFile, MethodDefinitionHandle methodHandle,
				ref BlobReader blob, int methodRva)
			{
				var index = sequencePoints.BinarySearch(blob.Offset, seq => seq.Offset);
				if (index >= 0) {
					var info = sequencePoints[index];
					var highlightingOutput = output as ISmartTextOutput;
					if (!info.IsHidden) {
						for (var line = info.StartLine; line <= info.EndLine; line++) {
							if (highlightingOutput != null) {
								var text = codeLines[line - 1];
								var startColumn = 1;
								var endColumn = text.Length + 1;
								if (line == info.StartLine)
									startColumn = info.StartColumn;
								if (line == info.EndLine)
									endColumn = info.EndColumn;
								WriteHighlightedCommentLine(highlightingOutput, text, startColumn - 1, endColumn - 1, info.StartLine == info.EndLine);
							} else
								WriteCommentLine(output, codeLines[line - 1]);
						}
					} else {
						output.Write("// ");
						highlightingOutput?.BeginSpan(gray);
						output.WriteLine("(no C# code)");
						highlightingOutput?.EndSpan();
					}
				}
				base.WriteInstruction(output,metadataFile, methodHandle, ref blob, methodRva);
			}

		
			HighlightingColor gray = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Colors.DarkGray) };

			void WriteHighlightedCommentLine(ISmartTextOutput output, string text, int startColumn, int endColumn, bool isSingleLine)
			{
				if (startColumn > text.Length) {
					Debug.Fail("startColumn is invalid");
					startColumn = text.Length;
				}
				if (endColumn > text.Length) {
					Debug.Fail("endColumn is invalid");
					endColumn = text.Length;
				}
				output.Write("// ");
				output.BeginSpan(gray);
				output.Write(isSingleLine
					? text.Substring(0, startColumn).TrimStart()
					: text.Substring(0, startColumn));
				output.EndSpan();
				output.Write(text.Substring(startColumn, endColumn - startColumn));
				output.BeginSpan(gray);
				output.Write(text.Substring(endColumn));
				output.EndSpan();
				output.WriteLine();
			}

			void WriteCommentLine(ITextOutput output, string text)
			{
				output.WriteLine("// " + text);
			}
		}
	}
}
