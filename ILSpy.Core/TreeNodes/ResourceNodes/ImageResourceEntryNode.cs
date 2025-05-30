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

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.ILSpy.TextView;
using ICSharpCode.ILSpy.Properties;

namespace ICSharpCode.ILSpy.TreeNodes
{
	[Export(typeof(IResourceNodeFactory))]
	sealed class ImageResourceNodeFactory : IResourceNodeFactory
	{
		private static readonly string[] imageFileExtensions = { ".png", ".gif", ".bmp", ".jpg" };

		public ILSpyTreeNode CreateNode(Resource resource)
		{
			var stream = resource.TryOpenStream();
			return stream == null ? null : CreateNode(resource.Name, stream);
		}

		public ILSpyTreeNode CreateNode(string key, object data)
		{
			if (!(data is Stream stream))
			    return null;
			return imageFileExtensions.Any(fileExt => key.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase)) ? new ImageResourceEntryNode(key, stream) : null;
		}
	}

	sealed class ImageResourceEntryNode : ResourceEntryNode
	{
		public ImageResourceEntryNode(string key, Stream data)
			: base(key, data)
		{
		}

		public override object Icon => Images.ResourceImage;

		public override bool View(DecompilerTextView textView)
		{
			try {
				var output = new AvaloniaEditTextOutput();
				Data.Position = 0;
                IBitmap image = new Bitmap(Data);
                output.AddUIElement(() => new Image { Source = image });
				output.WriteLine();
                output.AddButton(Images.Save, Resources.Save, async delegate {
                    await Save(null);
                });
				textView.ShowNode(output, this);
				return true;
			}
			catch (Exception) {
				return false;
			}
		}
	}
}
