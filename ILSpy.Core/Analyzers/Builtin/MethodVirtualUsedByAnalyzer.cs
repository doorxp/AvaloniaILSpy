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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy.Analyzers.Builtin
{
	/// <summary>
	/// Shows entities that are used by a method.
	/// </summary>
	[ExportAnalyzer(Header = "Used By", Order = 20)]
	class MethodVirtualUsedByAnalyzer : IAnalyzer
	{
		const GetMemberOptions Options = GetMemberOptions.IgnoreInheritedMembers | GetMemberOptions.ReturnMemberDefinitions;

		public bool Show(ISymbol symbol) => symbol is IMethod { IsVirtual: true };

		public IEnumerable<ISymbol> Analyze(ISymbol analyzedSymbol, AnalyzerContext context)
		{
			Debug.Assert(analyzedSymbol is IMethod);
			var scope = context.GetScopeOf((IEntity)analyzedSymbol);
			foreach (var type in scope.GetTypesInScope(context.CancellationToken)) {
				var mappingInfo = context.Language.GetCodeMappingInfo((PEFile)type.ParentModule?.MetadataFile , type.MetadataToken);
				var methods = type.GetMembers(m => m is IMethod, Options).OfType<IMethod>();
				foreach (var method in methods) {
					if (IsUsedInMethod((IMethod)analyzedSymbol, method, mappingInfo, context))
						yield return method;
				}

				foreach (var property in type.Properties)
				{
					if ((!property.CanGet ||
					     !IsUsedInMethod((IMethod)analyzedSymbol, property.Getter, mappingInfo, context)) &&
					    (!property.CanSet || !IsUsedInMethod((IMethod)analyzedSymbol, property.Setter, mappingInfo,
						    context))) continue;
					yield return property;
				}

				foreach (var @event in type.Events)
				{
					if ((!@event.CanAdd ||
					     !IsUsedInMethod((IMethod)analyzedSymbol, @event.AddAccessor, mappingInfo, context)) &&
					    (!@event.CanRemove || !IsUsedInMethod((IMethod)analyzedSymbol, @event.RemoveAccessor,
						    mappingInfo, context)) && (!@event.CanInvoke || !IsUsedInMethod((IMethod)analyzedSymbol,
						    @event.InvokeAccessor, mappingInfo, context))) continue;
					yield return @event;
				}

			}
		}

		bool IsUsedInMethod(IMethod analyzedEntity, IMethod method, CodeMappingInfo mappingInfo, AnalyzerContext context)
		{
			return ScanMethodBody(analyzedEntity, method, context.GetMethodBody(method));
		}

		static bool ScanMethodBody(IMethod analyzedMethod, IMethod method, MethodBodyBlock methodBody)
		{
			if (methodBody == null)
				return false;
			var mainModule = (MetadataModule)method.ParentModule;
			var blob = methodBody.GetILReader();
			var genericContext = new Decompiler.TypeSystem.GenericContext();

			while (blob.RemainingBytes > 0)
			{
				var opCode = blob.DecodeOpCode();
				if (opCode.GetOperandType() == OperandType.Field || opCode.GetOperandType() == OperandType.Method ||
				    opCode.GetOperandType() == OperandType.Sig || opCode.GetOperandType() == OperandType.Tok)
				{
					var member = MetadataTokenHelpers.EntityHandleOrNil(blob.ReadInt32());
					if (member.IsNil) continue;

					if (member.Kind != HandleKind.MethodDefinition &&
					    member.Kind != HandleKind.MethodSpecification &&
					    member.Kind != HandleKind.MemberReference) continue;
					var m = (mainModule?.ResolveEntity(member, genericContext) as IMember)?.MemberDefinition;
					if (m?.MetadataToken == analyzedMethod.MetadataToken && m.ParentModule?.MetadataFile ==
					    analyzedMethod.ParentModule?.MetadataFile)
					{
						return true;
					}
				}
				else
				{
					blob.SkipOperand(opCode);
				}
			}

			return false;
		}
	}
}
