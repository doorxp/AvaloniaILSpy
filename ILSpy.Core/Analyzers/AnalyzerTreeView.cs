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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.ILSpy.Analyzers.TreeNodes;
using ICSharpCode.TreeView;

namespace ICSharpCode.ILSpy.Analyzers
{
	/// <summary>
	/// Analyzer tree view.
	/// </summary>
	public class AnalyzerTreeView : SharpTreeView, IPane
	{
		static AnalyzerTreeView instance;

		public static AnalyzerTreeView Instance
		{
			get
			{
				if (instance != null) return instance;
				Dispatcher.UIThread.VerifyAccess();
				instance = new AnalyzerTreeView();
				return instance;
			}
		}

		private AnalyzerTreeView()
		{
			this.ShowRoot = false;
			this.Root = new AnalyzerRootNode { Language = MainWindow.Instance.CurrentLanguage };
			this.BorderThickness = new Thickness(0);
			ContextMenuProvider.Add(this);
			MainWindow.Instance.CurrentAssemblyListChanged += MainWindow_Instance_CurrentAssemblyListChanged;
		}

		void MainWindow_Instance_CurrentAssemblyListChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Reset) {
				this.Root.Children.Clear();
			} else {
				var removedAssemblies = new List<LoadedAssembly>();
				if (e.OldItems != null)
					removedAssemblies.AddRange(e.OldItems.Cast<LoadedAssembly>());
				var addedAssemblies = new List<LoadedAssembly>();
				if (e.NewItems != null)
					addedAssemblies.AddRange(e.NewItems.Cast<LoadedAssembly>());
				((AnalyzerRootNode)this.Root).HandleAssemblyListChanged(removedAssemblies, addedAssemblies);
			}
		}

		public void Show()
		{
			if (VisualRoot == null)
				MainWindow.Instance.ShowInBottomPane("Analyzer", this);
		}

		public void Show(AnalyzerTreeNode node)
		{
			Show();

			node.IsExpanded = true;
			this.Root.Children.Add(node);
			this.SelectedItem = node;
			this.FocusNode(node);
		}

		public void ShowOrFocus(AnalyzerTreeNode node)
		{
			if (node is AnalyzerEntityTreeNode an) {
				var found = this.Root.Children.OfType<AnalyzerEntityTreeNode>().FirstOrDefault(n => Equals(n.Member, an.Member));
				if (found != null) {
					Show();
					
					found.IsExpanded = true;
					this.SelectedItem = found;
					this.FocusNode(found);
					return;
				}
			}
			Show(node);
		}

		public void Analyze(IEntity entity)
		{
            ArgumentNullException.ThrowIfNull(entity);

            if (entity.MetadataToken.IsNil)
            {
                MessageBox.Show(Properties.Resources.CannotAnalyzeMissingRef, "ILSpy");
                return;
            }

            switch (entity) {
				case ITypeDefinition td:
					ShowOrFocus(new AnalyzedTypeTreeNode(td));
					break;
				case IField fd:
                    if (!fd.IsConst)
                        ShowOrFocus(new AnalyzedFieldTreeNode(fd));
					break;
				case IMethod md:
					ShowOrFocus(new AnalyzedMethodTreeNode(md));
					break;
				case IProperty pd:
					ShowOrFocus(new AnalyzedPropertyTreeNode(pd));
					break;
				case IEvent ed:
					ShowOrFocus(new AnalyzedEventTreeNode(ed));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(entity), $"Entity {entity.GetType().FullName} is not supported.");
			}
		}

        public void Closed()
		{
			this.Root.Children.Clear();
		}
		
		sealed class AnalyzerRootNode : AnalyzerTreeNode
		{
			public override bool HandleAssemblyListChanged(ICollection<LoadedAssembly> removedAssemblies, ICollection<LoadedAssembly> addedAssemblies)
			{
				this.Children.RemoveAll(
					delegate(SharpTreeNode n) {
						var an = n as AnalyzerTreeNode;
						return an == null || !an.HandleAssemblyListChanged(removedAssemblies, addedAssemblies);
					});
				return true;
			}
		}
	}
}
