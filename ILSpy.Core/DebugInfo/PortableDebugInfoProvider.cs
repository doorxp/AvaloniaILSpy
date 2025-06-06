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
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;

namespace ICSharpCode.ILSpy.DebugInfo
{
	class PortableDebugInfoProvider : IDebugInfoProvider
	{
		string pdbFileName;
		MetadataReaderProvider provider;

		public PortableDebugInfoProvider(string pdbFileName, MetadataReaderProvider provider)
		{
			this.pdbFileName = pdbFileName;
			this.provider = provider;
		}

		public string Description => pdbFileName == null ? "Embedded in this assembly" : $"Loaded from portable PDB: {pdbFileName}";

		public string SourceFileName => pdbFileName;

		public IList<Decompiler.DebugInfo.SequencePoint> GetSequencePoints(MethodDefinitionHandle method)
		{
			var metadata = provider.GetMetadataReader();
			var debugInfo = metadata.GetMethodDebugInformation(method);
			var sequencePoints = new List<Decompiler.DebugInfo.SequencePoint>();

			foreach (var point in debugInfo.GetSequencePoints()) {
				string documentFileName;

				if (!point.Document.IsNil) {
					var document = metadata.GetDocument(point.Document);
					documentFileName = metadata.GetString(document.Name);
				} else {
					documentFileName = "";
				}

				sequencePoints.Add(new Decompiler.DebugInfo.SequencePoint() {
					Offset = point.Offset,
					StartLine = point.StartLine,
					StartColumn = point.StartColumn,
					EndLine = point.EndLine,
					EndColumn = point.EndColumn,
					DocumentUrl = documentFileName
				});
			}

			return sequencePoints;
		}

		public IList<Variable> GetVariables(MethodDefinitionHandle method)
		{
			var metadata = provider.GetMetadataReader();
			var variables = new List<Variable>();

			foreach (var scope in metadata.GetLocalScopes(method).Select(h => metadata.GetLocalScope(h)))
			{
				variables.AddRange(scope.GetLocalVariables().Select(v => metadata.GetLocalVariable(v)).Select(var => new Variable(var.Index, metadata.GetString(var.Name))));
			}

			return variables;
		}

		public bool TryGetName(MethodDefinitionHandle method, int index, out string name)
		{
			var metadata = provider.GetMetadataReader();
			name = null;

			foreach (var var in metadata.GetLocalScopes(method).Select(h => metadata.GetLocalScope(h)).SelectMany(scope => scope.GetLocalVariables().Select(v => metadata.GetLocalVariable(v)).Where(var => var.Index == index)))
			{
				name = metadata.GetString(var.Name);
				return true;
			}
			return false;
		}
		
		public bool TryGetExtraTypeInfo(MethodDefinitionHandle method, int index, out PdbExtraTypeInfo extraTypeInfo)
		{
			var metadata = provider.GetMetadataReader();
			extraTypeInfo = new PdbExtraTypeInfo();

			foreach (var var in metadata.GetLocalScopes(method).Select(h => metadata.GetLocalScope(h)).SelectMany(scope => scope.GetLocalVariables().Select(v => metadata.GetLocalVariable(v)).Where(var => var.Index == index)))
			{
				extraTypeInfo.TupleElementNames = new[] { metadata.GetString(var.Name) };
				return true;
			}

			return false;
		}
	}
}
