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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Avalonia;
using Avalonia.Controls;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.ILSpy.Properties;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.TreeNodes;
using Microsoft.Win32;

namespace ICSharpCode.ILSpy
{
	[ExportContextMenuEntry(Header = "Generate portable PDB")]
	internal class GeneratePdbContextMenuEntry : IContextMenuEntry
	{
		public void Execute(TextViewContext context)
		{
			var assembly = (context.SelectedTreeNodes?.FirstOrDefault() as AssemblyTreeNode)?.LoadedAssembly;
			if (assembly == null) return;
			GeneratePdbForAssembly(assembly);
		}

		public bool IsEnabled(TextViewContext context) => true;

		public bool IsVisible(TextViewContext context)
		{
			return context.SelectedTreeNodes?.Length == 1
				&& context.SelectedTreeNodes?.FirstOrDefault() is AssemblyTreeNode tn
				&& !tn.LoadedAssembly.HasLoadError;
		}

		internal static async void GeneratePdbForAssembly(LoadedAssembly assembly)
		{
			var file = assembly.GetPEFileOrNull();
			if (!PortablePdbWriter.HasCodeViewDebugDirectoryEntry(file)) {
				await MessageBox.Show($"Cannot create PDB file for {Path.GetFileName(assembly.FileName)}, because it does not contain a PE Debug Directory Entry of type 'CodeView'.");
				return;
			}
			var dlg = new SaveFileDialog
			{
				Title = "Save file",
				InitialFileName = DecompilerTextView.CleanUpName(assembly.ShortName) + ".pdb",
				Filters = new List<FileDialogFilter> { new FileDialogFilter { Name = "Portable PDB", Extensions = { "pdb" } }, new FileDialogFilter { Name = "All files", Extensions = { "*" } } },
				Directory = Path.GetDirectoryName(assembly.FileName)
			};
			var fileName = await dlg.ShowAsync(Application.Current.GetMainWindow());
			if (string.IsNullOrEmpty(fileName)) return;
			var options = new DecompilationOptions();
			MainWindow.Instance.TextView.RunWithCancellation(ct => Task<AvaloniaEditTextOutput>.Factory.StartNew(() => {
				var output = new AvaloniaEditTextOutput();
				var stopwatch = Stopwatch.StartNew();
				using (var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write)) {
					try {
						var decompiler = new CSharpDecompiler(file, assembly.GetAssemblyResolver(), options.DecompilerSettings);
						PortablePdbWriter.WritePdb(file, decompiler, options.DecompilerSettings, stream);
					} catch (OperationCanceledException) {
						output.WriteLine();
						output.WriteLine("Generation was cancelled.");
						throw;
					}
				}
				stopwatch.Stop();
				output.WriteLine("Generation complete in " + stopwatch.Elapsed.TotalSeconds.ToString("F1") + " seconds.");
				output.WriteLine();
				output.AddButton(null, "Open Explorer", delegate { Process.Start("explorer", "/select,\"" + fileName + "\""); });
				output.WriteLine();
				return output;
			}, ct)).Then(output => MainWindow.Instance.TextView.ShowText(output)).HandleExceptions();
		}
	}

    [ExportMainMenuCommand(Menu = nameof(Resources._File), Header = nameof(Resources.GeneratePortable), MenuCategory = "Save")]
    internal class GeneratePdbMainMenuEntry : SimpleCommand
	{
		public override bool CanExecute(object parameter)
		{
			return MainWindow.Instance.SelectedNodes?.Count() == 1
				&& MainWindow.Instance.SelectedNodes?.FirstOrDefault() is AssemblyTreeNode tn
				&& !tn.LoadedAssembly.HasLoadError;
		}

		public override void Execute(object parameter)
		{
			var assembly = (MainWindow.Instance.SelectedNodes?.FirstOrDefault() as AssemblyTreeNode)?.LoadedAssembly;
			if (assembly == null) return;
			GeneratePdbContextMenuEntry.GeneratePdbForAssembly(assembly);
		}
	}
}
