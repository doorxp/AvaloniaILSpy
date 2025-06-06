﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
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
using System.Threading;
using ICSharpCode.TreeView;
using System.Linq;
using ICSharpCode.ILSpy.Controls;

namespace ICSharpCode.ILSpy
{
	class ILSpyTraceListener : DefaultTraceListener
	{
		[Conditional("DEBUG")]
		public static void Install()
		{
			//TODO: This was debug listeners
			Trace.Listeners.Clear();
			Trace.Listeners.Add(new ILSpyTraceListener());
		}

		public ILSpyTraceListener()
		{
			base.AssertUiEnabled = false;
		}

		HashSet<string> ignoredStacks = new HashSet<string>();
		bool dialogIsOpen;

		public override void Fail(string message)
		{
			this.Fail(message, null);
		}

		public override void Fail(string message, string detailMessage)
		{
			base.Fail(message, detailMessage); // let base class write the assert to the debug console
			var topFrame = "";
			var stackTrace = "";
			try {
				stackTrace = new StackTrace(true).ToString();
				var frames = stackTrace.Split('\r', '\n')
					.Where(f => f.Length > 0)
					.SkipWhile(f => f.Contains("ILSpyTraceListener") || f.Contains("System.Diagnostics"))
					.ToList();
				topFrame = frames[0];
				stackTrace = string.Join(Environment.NewLine, frames);
			}
			catch
			{
				// ignored
			}

			lock (ignoredStacks) {
				if (ignoredStacks.Contains(topFrame))
					return;
				if (dialogIsOpen)
					return;
				dialogIsOpen = true;
			}
			// We might be unable to display a dialog here, e.g. because
			// we're on the UI thread but dispatcher processing is disabled.
			// In any case, we don't want to pump messages while the dialog is displaying,
			// so we create a separate UI thread for the dialog:
			var result = 0;
			var thread = new Thread(() => result = ShowAssertionDialog(message, detailMessage, stackTrace));
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			switch (result)
			{
				case 0: // throw
					throw new AssertionFailedException(message);
				case 1: // debug
					Debugger.Break();
					break;
				case 2: // ignore
					break;
				case 3:
				{
					lock (ignoredStacks) {
						ignoredStacks.Add(topFrame);
					}

					break;
				}
			}
		}

		int ShowAssertionDialog(string message, string detailMessage, string stackTrace)
		{
			message = message + Environment.NewLine + detailMessage + Environment.NewLine + stackTrace;
			string[] buttonTexts = { "Throw", "Debug", "Ignore", "Ignore All" };
			var inputBox = new CustomDialog("Assertion Failed", message.TakeStartEllipsis(750), -1, 2, buttonTexts)
				{
					//inputBox.StartPosition = Avalonia.Forms.FormStartPosition.CenterScreen;
					ShowInTaskbar = true // make this window more visible, because it effectively interrupts the decompilation process.
				};
			try {
				inputBox.ShowDialog(MainWindow.Instance);
				return inputBox.Result;
			} finally {
				dialogIsOpen = false;
				//inputBox.Dispose();
			}
		}
	}

	class AssertionFailedException : Exception
	{
		public AssertionFailedException(string message) : base(message) { }
	}
}
