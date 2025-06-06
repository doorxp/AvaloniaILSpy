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


using ICSharpCode.ILSpy.Properties;

namespace ICSharpCode.ILSpy
{
    [ExportMainMenuCommand(Menu = nameof(Resources._File), Header = nameof(Resources.Open_List), MenuIcon = "Images/AssemblyList.png", MenuCategory = nameof(Resources.Open), MenuOrder = 1.7)]
    internal sealed class OpenListCommand : SimpleCommand
	{
		public override async void Execute(object parameter)
		{
			var dlg = new OpenListDialog
			{
				Title = "Open List"
			};
			if (await dlg.ShowDialog<bool>(MainWindow.Instance))
				MainWindow.Instance.ShowAssemblyList(dlg.SelectedListName);
		}
	}
}
