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
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy.Analyzers.Builtin
{
	/// <summary>
	/// Shows entities that are used by a method.
	/// </summary>
	[ExportAnalyzer(Header = "Uses", Order = 10)]
	class MethodUsesAnalyzer : IAnalyzer
	{
		public bool Show(ISymbol symbol) => symbol is IMethod method && method.HasBody;

		public IEnumerable<ISymbol> Analyze(ISymbol symbol, AnalyzerContext context)
		{
			if (symbol is IMethod method) {
				return context.Language.GetCodeMappingInfo((PEFile)method.ParentModule.MetadataFile , method.MetadataToken)
					.GetMethodParts((MethodDefinitionHandle)method.MetadataToken)
					.SelectMany(h => ScanMethod(method, h, context)).Distinct();
			}
			throw new InvalidOperationException("Should never happen.");
		}

		IEnumerable<IEntity> ScanMethod(IMethod analyzedMethod, MethodDefinitionHandle handle, AnalyzerContext context)
		{
			var module = (MetadataModule)analyzedMethod.ParentModule;
			var md = module.MetadataFile .Metadata.GetMethodDefinition(handle);
			if (!md.HasBody()) yield break;

			BlobReader blob;
			try {
				blob = ((PEFile)module.MetadataFile).Reader.GetMethodBody(md.RelativeVirtualAddress).GetILReader();
			} catch (BadImageFormatException) {
				yield break;
			}
			var visitor = new TypeDefinitionCollector();
			var genericContext = new GenericContext(); // type parameters don't matter for this analyzer

			while (blob.RemainingBytes > 0) {
				ILOpCode opCode;
				try {
					opCode = blob.DecodeOpCode();
				} catch (BadImageFormatException) {
					yield break;
				}

				if (opCode.GetOperandType() == OperandType.Field || opCode.GetOperandType() == OperandType.Method ||
				    opCode.GetOperandType() == OperandType.Sig || opCode.GetOperandType() == OperandType.Tok)
				{
					var member = MetadataTokenHelpers.EntityHandleOrNil(blob.ReadInt32());
					if (member.IsNil) continue;

					switch (member.Kind)
					{
						case HandleKind.StandaloneSignature:
							break;
						case HandleKind.TypeDefinition:
						case HandleKind.TypeReference:
						case HandleKind.TypeSpecification:
						{
							IType ty;
							try
							{
								ty = module.ResolveType(member, genericContext);
							}
							catch (BadImageFormatException)
							{
								ty = null;
							}

							ty?.AcceptVisitor(visitor);
							break;
						}
						case HandleKind.MethodDefinition:
						case HandleKind.MethodSpecification:
						case HandleKind.MemberReference:
						case HandleKind.FieldDefinition:
						{
							IEntity m;
							try
							{
								m = module.ResolveEntity(member, genericContext);
							}
							catch (BadImageFormatException)
							{
								m = null;
							}

							if (m != null)
								yield return m;
							break;
						}
					}
				}
				else
				{
					try
					{
						blob.SkipOperand(opCode);
					}
					catch (BadImageFormatException)
					{
						yield break;
					}
				}
			}

			foreach (var type in visitor.UsedTypes) {
				yield return type;
			}
		}

		class TypeDefinitionCollector : TypeVisitor
		{
			public readonly List<ITypeDefinition> UsedTypes = new List<ITypeDefinition>(); 

			public override IType VisitTypeDefinition(ITypeDefinition type)
			{
				UsedTypes.Add(type);
				return base.VisitTypeDefinition(type);
			}
		}
	}
}
