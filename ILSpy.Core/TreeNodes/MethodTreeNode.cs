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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.ILSpy.TreeNodes
{
	/// <summary>
	/// Tree Node representing a field, method, property, or event.
	/// </summary>
	public sealed class MethodTreeNode : ILSpyTreeNode, IMemberTreeNode
	{
		public IMethod MethodDefinition { get; }

		public MethodTreeNode(IMethod method)
		{
			this.MethodDefinition = method ?? throw new ArgumentNullException(nameof(method));
		}

		public override object Text => GetText(MethodDefinition, Language) + MethodDefinition.MetadataToken.ToSuffixString();

		public static object GetText(IMethod method, Language language)
		{
			return language.MethodToString(method, false, false, false);
		}

		public override object Icon => GetIcon(MethodDefinition);

		public static IBitmap GetIcon(IMethod method)
		{
			if (method.IsOperator)
				return Images.GetIcon(MemberIcon.Operator, GetOverlayIcon(method.Accessibility), false);

			if (method.IsExtensionMethod)
				return Images.GetIcon(MemberIcon.ExtensionMethod, GetOverlayIcon(method.Accessibility), false);

			if (method.IsConstructor)
				return Images.GetIcon(MemberIcon.Constructor, GetOverlayIcon(method.Accessibility), method.IsStatic);

			if (!method.HasBody && method.HasAttribute(KnownAttribute.DllImport))
				return Images.GetIcon(MemberIcon.PInvokeMethod, GetOverlayIcon(method.Accessibility), true);

			return Images.GetIcon(method.IsVirtual ? MemberIcon.VirtualMethod : MemberIcon.Method,
				GetOverlayIcon(method.Accessibility), method.IsStatic);
		}

		internal static AccessOverlayIcon GetOverlayIcon(Accessibility accessibility)
		{
			return accessibility switch
			{
				Accessibility.Public => AccessOverlayIcon.Public,
				Accessibility.Internal => AccessOverlayIcon.Internal,
				Accessibility.ProtectedAndInternal => AccessOverlayIcon.PrivateProtected,
				Accessibility.Protected => AccessOverlayIcon.Protected,
				Accessibility.ProtectedOrInternal => AccessOverlayIcon.ProtectedInternal,
				Accessibility.Private => AccessOverlayIcon.Private,
				_ => AccessOverlayIcon.CompilerControlled
			};
		}

		public override void Decompile(Language language, ITextOutput output, DecompilationOptions options)
		{
			language.DecompileMethod(MethodDefinition, output, options);
		}

		public override FilterResult Filter(FilterSettings settings)
		{
            if (settings.ShowApiLevel == ApiVisibility.PublicOnly && !IsPublicAPI)
                return FilterResult.Hidden;
            if (settings.SearchTermMatches(MethodDefinition.Name) && (settings.ShowApiLevel == ApiVisibility.All || settings.Language.ShowMember(MethodDefinition)))
                return FilterResult.Match;
            return FilterResult.Hidden;
		}

		public override bool IsPublicAPI {
			get
			{
				return MethodDefinition.Accessibility switch
				{
					Accessibility.Public => true,
					Accessibility.Protected => true,
					Accessibility.ProtectedOrInternal => true,
					_ => false
				};
			}
		}

		IEntity IMemberTreeNode.Member => MethodDefinition;
	}
}
