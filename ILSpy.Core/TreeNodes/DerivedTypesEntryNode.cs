// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
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
using System.Linq;
using System.Threading;
using Avalonia.Interactivity;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy.TreeNodes
{
	class DerivedTypesEntryNode : ILSpyTreeNode, IMemberTreeNode
	{
		readonly AssemblyList list;
		readonly ITypeDefinition type;
		readonly ThreadingSupport threading;

		public DerivedTypesEntryNode(AssemblyList list, ITypeDefinition type)
		{
			this.list = list;
			this.type = type;
			this.LazyLoading = true;
			threading = new ThreadingSupport();
		}

		public override bool ShowExpander => !type.IsSealed && base.ShowExpander;

		public override object Text => Language.TypeToString(type, includeNamespace: true) + type.MetadataToken.ToSuffixString();

		public override object Icon => TypeTreeNode.GetIcon(type);

		public override FilterResult Filter(FilterSettings settings)
		{
            if (settings.ShowApiLevel == ApiVisibility.PublicOnly && !IsPublicAPI)
                    return FilterResult.Hidden;
            if (!settings.SearchTermMatches(type.Name)) return FilterResult.Recurse;
            if (type.DeclaringType != null && (settings.ShowApiLevel != ApiVisibility.All || !settings.Language.ShowMember(type)))
				return FilterResult.Hidden;
			return FilterResult.Match;

		}
		
		public override bool IsPublicAPI {
			get
			{
				return type.Accessibility switch
				{
					Accessibility.Public => true,
					Accessibility.Internal => true,
					Accessibility.ProtectedOrInternal => true,
					_ => false
				};
			}
		}

		protected override void LoadChildren()
		{
			threading.LoadChildren(this, FetchChildren);
		}

		IEnumerable<ILSpyTreeNode> FetchChildren(CancellationToken ct)
		{
			// FetchChildren() runs on the main thread; but the enumerator will be consumed on a background thread
			var assemblies = list.GetAssemblies().Select(node => node.GetPEFileOrNull()).Where(asm => asm != null).ToArray();
			return DerivedTypesTreeNode.FindDerivedTypes(list, type, assemblies, ct);
		}

		public override void ActivateItem(RoutedEventArgs e)
		{
			e.Handled = BaseTypesEntryNode.ActivateItem(this, type);
		}

		public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
		{
			language.WriteCommentLine(output, language.TypeToString(type, includeNamespace: true));
		}

		IEntity IMemberTreeNode.Member => type;
	}
}
