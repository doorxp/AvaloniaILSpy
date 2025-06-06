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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

namespace ICSharpCode.ILSpy.Analyzers
{
	public class AnalyzerScope
	{
		readonly ITypeDefinition typeScope;

		/// <summary>
		/// Returns whether this scope is local, i.e., AnalyzedSymbol is only reachable
		/// from the current module or containing type.
		/// </summary>
		public bool IsLocal { get; }

		public AssemblyList AssemblyList { get; }
		public ISymbol AnalyzedSymbol { get; }

		public ITypeDefinition TypeScope => typeScope;

		Accessibility memberAccessibility, typeAccessibility;

		public AnalyzerScope(AssemblyList assemblyList, IEntity entity)
		{
			AssemblyList = assemblyList;
			AnalyzedSymbol = entity;
			if (entity is ITypeDefinition type) {
				typeScope = type;
				memberAccessibility = Accessibility.None;
			} else {
				typeScope = entity.DeclaringTypeDefinition;
				memberAccessibility = entity.Accessibility;
			}
			typeAccessibility = DetermineTypeAccessibility(ref typeScope);
			IsLocal = memberAccessibility == Accessibility.Private || typeAccessibility == Accessibility.Private;
		}

		public IEnumerable<PEFile> GetModulesInScope(CancellationToken ct)
		{
			if (IsLocal)
				return new[] { (PEFile)TypeScope.ParentModule.MetadataFile  };

			if (memberAccessibility == Accessibility.Internal ||
				memberAccessibility == Accessibility.ProtectedOrInternal ||
				typeAccessibility == Accessibility.Internal ||
				typeAccessibility == Accessibility.ProtectedAndInternal)
				return GetModuleAndAnyFriends(TypeScope, ct);

			return GetReferencingModules((PEFile)TypeScope.ParentModule.MetadataFile , ct);
		}

		public IEnumerable<PEFile> GetAllModules()
		{
			return AssemblyList.GetAssemblies().Select(module => module.GetPEFileOrNull()).Where(file => file != null);
		}

		public IEnumerable<ITypeDefinition> GetTypesInScope(CancellationToken ct)
		{
			if (IsLocal) {
				var typeSystem = new DecompilerTypeSystem(TypeScope.ParentModule?.MetadataFile , ((PEFile)TypeScope.ParentModule?.MetadataFile).GetAssemblyResolver());
                var scope = typeScope;
                if (memberAccessibility != Accessibility.Private && typeScope.DeclaringTypeDefinition != null)
                {
                    scope = typeScope.DeclaringTypeDefinition;
                }
                foreach (var type in TreeTraversal.PreOrder(scope, t => t.NestedTypes))
                {
                    yield return type;
                }
            }
            else {
				foreach (var module in GetModulesInScope(ct)) {
					var typeSystem = new DecompilerTypeSystem(module, module.GetAssemblyResolver());
					foreach (var type in typeSystem.MainModule.TypeDefinitions) {
						yield return type;
					}
				}
			}
		}

		Accessibility DetermineTypeAccessibility(ref ITypeDefinition typeScope)
		{
			var typeAccessibility = typeScope.Accessibility;
			while (typeScope?.DeclaringType != null) {
				var accessibility = typeScope.Accessibility;
				if ((int)typeAccessibility > (int)accessibility) {
					typeAccessibility = accessibility;
					if (typeAccessibility == Accessibility.Private)
						break;
				}
				typeScope = typeScope.DeclaringTypeDefinition;
			}

			if ((int)typeAccessibility > (int)Accessibility.Internal) {
				typeAccessibility = Accessibility.Internal;
			}
			return typeAccessibility;
		}

		#region Find modules
		IEnumerable<PEFile> GetReferencingModules(PEFile self, CancellationToken ct)
		{
			yield return self;

            var reflectionTypeScopeName = typeScope.Name;
            if (typeScope.TypeParameterCount > 0)
                reflectionTypeScopeName += "`" + typeScope.TypeParameterCount;

            foreach (var assembly in AssemblyList.GetAssemblies()) {
				ct.ThrowIfCancellationRequested();
				var found = false;
				var module = assembly.GetPEFileOrNull();
				if (!(module is { IsAssembly: true }))
					continue;
				var resolver = assembly.GetAssemblyResolver();
				foreach (var reference in module.AssemblyReferences) {
					using (LoadedAssembly.DisableAssemblyLoad())
					{
						if (resolver.Resolve(reference) != self) continue;
						found = true;
						break;
					}
				}
				if (found && ModuleReferencesScopeType(module.Metadata, reflectionTypeScopeName, typeScope.Namespace))
					yield return module;
			}
		}

		IEnumerable<PEFile> GetModuleAndAnyFriends(ITypeDefinition typeScope, CancellationToken ct)
		{
			var self =(PEFile) typeScope.ParentModule?.MetadataFile ;

			yield return self;

			var typeProvider = MetadataExtensions.MinimalAttributeTypeProvider;
			var attributes = self?.Metadata.CustomAttributes.Select(h => self.Metadata.GetCustomAttribute(h))
				.Where(ca => ca.GetAttributeType(self.Metadata).GetFullTypeName(self.Metadata).ToString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute");
			var friendAssemblies = new HashSet<string>();
			if (attributes != null)
				foreach (var attribute in attributes)
				{
					var assemblyName = attribute.DecodeValue(typeProvider).FixedArguments[0].Value as string;
					assemblyName = assemblyName?.Split(',')[0]; // strip off any public key info
					friendAssemblies.Add(assemblyName);
				}

			if (friendAssemblies.Count <= 0) yield break;
			IEnumerable<LoadedAssembly> assemblies = AssemblyList.GetAssemblies();

			foreach (var assembly in assemblies) {
				ct.ThrowIfCancellationRequested();
				if (!friendAssemblies.Contains(assembly.ShortName)) continue;
				var module = assembly.GetPEFileOrNull();
				if (module == null)
					continue;
				if (ModuleReferencesScopeType(module.Metadata, typeScope.Name, typeScope.Namespace))
					yield return module;
			}
		}

		bool ModuleReferencesScopeType(MetadataReader metadata, string typeScopeName, string typeScopeNamespace)
		{
			return metadata.TypeReferences.Select(metadata.GetTypeReference).Any(typeRef => metadata.StringComparer.Equals(typeRef.Name, typeScopeName) && metadata.StringComparer.Equals(typeRef.Namespace, typeScopeNamespace));
		}
		#endregion
	}
}
