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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using System.Xml.Linq;
using Avalonia.Controls.Primitives;
using AvaloniaEdit.Rendering;
using ICSharpCode.Decompiler;
using ICSharpCode.ILSpy.TextView;
using Avalonia.Layout;
using ICSharpCode.ILSpy.Properties;

namespace ICSharpCode.ILSpy
{
	[ExportMainMenuCommand(Menu = nameof(Resources._Help), Header = nameof(Resources._About), MenuOrder = 99999)]
	sealed class AboutPage : SimpleCommand
	{
		[Import]
		DecompilerTextView decompilerTextView = null;
		
		public override void Execute(object parameter)
		{
			MainWindow.Instance.UnselectAll();
			Display(decompilerTextView);
		}
		
		static readonly Uri UpdateUrl = new Uri("https://github.com/icsharpcode/AvaloniaILSpy/raw/master/updates.xml");
		const string band = "stable";
		
		static AvailableVersionInfo latestAvailableVersion;
		
		public static void Display(DecompilerTextView textView)
		{
			var output = new AvaloniaEditTextOutput();
			output.WriteLine(Resources.ILSpyVersion + RevisionClass.FullVersion);
			var stackPanel = new StackPanel();
			CheckBox checkBox = new CheckBox();
			UpdateSettings settings = new UpdateSettings(ILSpySettings.Load());
			output.AddUIElement(
				delegate {
					stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
					stackPanel.Orientation = Orientation.Horizontal;
					if (latestAvailableVersion == null) {
						AddUpdateCheckButton(stackPanel, textView);
					} else {
						// we already retrieved the latest version sometime earlier
						ShowAvailableVersion(latestAvailableVersion, stackPanel);
					}

					checkBox.Margin = new Thickness(4);
					checkBox.Content = Resources.AutomaticallyCheckUpdatesEveryWeek;
					checkBox.Bind(ToggleButton.IsCheckedProperty, new Binding("AutomaticUpdateCheckEnabled") { Source = settings });
					return new StackPanel {
						Margin = new Thickness(0, 4, 0, 0),
						Cursor = Cursor.Default,
						Children = { stackPanel, checkBox }
					};
				});
			output.WriteLine();
			foreach (var plugin in App.ExportProvider.GetExportedValues<IAboutPageAddition>())
				plugin.Write(output);
			output.WriteLine();
			var asm = typeof(AboutPage).Assembly;
			using (var s = typeof(AboutPage).Assembly.GetManifestResourceStream(typeof(AboutPage), "README.txt")) {
				using (var r = new StreamReader(s)) {
					while (r.ReadLine() is { } line) {
						output.WriteLine(line);
					}
				}
			}
			output.AddVisualLineElementGenerator(new MyLinkElementGenerator("SharpDevelop", "http://www.icsharpcode.net/opensource/sd/"));
			output.AddVisualLineElementGenerator(new MyLinkElementGenerator("MIT License", "resource:license.txt"));
			output.AddVisualLineElementGenerator(new MyLinkElementGenerator("LGPL", "resource:LGPL.txt"));
			output.AddVisualLineElementGenerator(new MyLinkElementGenerator("MS-PL", "resource:MS-PL.txt"));
			textView.ShowText(output);
		}
		
		sealed class MyLinkElementGenerator : LinkElementGenerator
		{
			readonly Uri uri;
			
			public MyLinkElementGenerator(string matchText, string url) : base(new Regex(Regex.Escape(matchText)))
			{
				this.uri = new Uri(url);
				this.RequireControlModifierForClick = false;
			}
			
			protected override Uri GetUriFromMatch(Match match)
			{
				return uri;
			}
		}
		
		static void AddUpdateCheckButton(StackPanel stackPanel, DecompilerTextView textView)
		{
			var button = new Button
			{
				Content = Resources.CheckUpdates,
				Cursor = Cursor.Default
			};
			stackPanel.Children.Add(button);
			
			button.Click += delegate {
				button.Content = Resources.Checking;
				button.IsEnabled = false;
				GetLatestVersionAsync().ContinueWith(
					delegate (Task<AvailableVersionInfo> task) {
						try {
							stackPanel.Children.Clear();
							ShowAvailableVersion(task.Result, stackPanel);
						} catch (Exception ex) {
							var exceptionOutput = new AvaloniaEditTextOutput();
							exceptionOutput.WriteLine(ex.ToString());
							textView.ShowText(exceptionOutput);
						}
					}, TaskScheduler.FromCurrentSynchronizationContext());
			};
		}
		
		static readonly Version currentVersion = new Version(RevisionClass.Major + "." + RevisionClass.Minor + "." + RevisionClass.Build + "." + RevisionClass.Revision);
		
		static void ShowAvailableVersion(AvailableVersionInfo availableVersion, StackPanel stackPanel)
		{
			if (currentVersion == availableVersion.Version) {
				stackPanel.Children.Add(
					new Image {
						Width = 16, Height = 16,
						Source = Images.OK,
						Margin = new Thickness(4,0,4,0)
					});
				stackPanel.Children.Add(
					new TextBlock {
						Text = Resources.UsingLatestRelease,
						VerticalAlignment = VerticalAlignment.Bottom
					});
			} else if (currentVersion < availableVersion.Version) {
				stackPanel.Children.Add(
					new TextBlock {
						Text = string.Format(Resources.VersionAvailable, availableVersion.Version),
						Margin = new Thickness(0,0,8,0),
						VerticalAlignment = VerticalAlignment.Bottom
					});
				if (availableVersion.DownloadUrl == null) return;
				var button = new Button
				{
					Content = Resources.Download,
					Cursor = Cursor.Default
				};
				button.Click += delegate {
					MainWindow.OpenLink(availableVersion.DownloadUrl);
				};
				stackPanel.Children.Add(button);
			} else {
				stackPanel.Children.Add(new TextBlock { Text = Resources.UsingNightlyBuildNewerThanLatestRelease });
			}
		}
		
		static Task<AvailableVersionInfo> GetLatestVersionAsync()
		{
			var tcs = new TaskCompletionSource<AvailableVersionInfo>();
			Task.Run(() =>
			{
				var wc = new WebClient();
				var systemWebProxy = WebRequest.GetSystemWebProxy();
				systemWebProxy.Credentials = CredentialCache.DefaultCredentials;
				wc.Proxy = systemWebProxy;
				wc.DownloadDataCompleted += delegate (object sender, DownloadDataCompletedEventArgs e)
				{
					if (e.Error != null)
					{
						tcs.SetException(e.Error);
					}
					else
					{
						try
						{
							var doc = XDocument.Load(new MemoryStream(e.Result));
							var bands = doc.Root.Elements("band");
							var currentBand = bands.FirstOrDefault(b => (string)b.Attribute("id") == band) ?? bands.First();
							var version = new Version((string)currentBand.Element("latestVersion"));
							var url = (string)currentBand.Element("downloadUrl");
							if (url != null && !(url.StartsWith("http://", StringComparison.Ordinal) || url.StartsWith("https://", StringComparison.Ordinal)))
								url = null; // don't accept non-urls
							latestAvailableVersion = new AvailableVersionInfo { Version = version, DownloadUrl = url };
							tcs.SetResult(latestAvailableVersion);
						}
						catch (Exception ex)
						{
							tcs.SetException(ex);
						}
					}
				};
				wc.DownloadDataAsync(UpdateUrl);
			});
			return tcs.Task;
		}
		
		sealed class AvailableVersionInfo
		{
			public Version Version;
			public string DownloadUrl;
		}
		
		sealed class UpdateSettings : INotifyPropertyChanged
		{
			public UpdateSettings(ILSpySettings spySettings)
			{
				XElement s = spySettings["UpdateSettings"];
				this.automaticUpdateCheckEnabled = (bool?)s.Element("AutomaticUpdateCheckEnabled") ?? true;
				try {
					this.lastSuccessfulUpdateCheck = (DateTime?)s.Element("LastSuccessfulUpdateCheck");
				} catch (FormatException) {
					// avoid crashing on settings files invalid due to
					// https://github.com/icsharpcode/ILSpy/issues/closed/#issue/2
				}
			}
			
			bool automaticUpdateCheckEnabled;
			
			public bool AutomaticUpdateCheckEnabled {
				get => automaticUpdateCheckEnabled;
				set
				{
					if (automaticUpdateCheckEnabled == value) return;
					automaticUpdateCheckEnabled = value;
					Save();
					OnPropertyChanged(nameof(AutomaticUpdateCheckEnabled));
				}
			}
			
			DateTime? lastSuccessfulUpdateCheck;
			
			public DateTime? LastSuccessfulUpdateCheck {
				get => lastSuccessfulUpdateCheck;
				set
				{
					if (lastSuccessfulUpdateCheck == value) return;
					lastSuccessfulUpdateCheck = value;
					Save();
					OnPropertyChanged(nameof(LastSuccessfulUpdateCheck));
				}
			}
			
			public void Save()
			{
				var updateSettings = new XElement("UpdateSettings");
				updateSettings.Add(new XElement("AutomaticUpdateCheckEnabled", automaticUpdateCheckEnabled));
				if (lastSuccessfulUpdateCheck != null)
					updateSettings.Add(new XElement("LastSuccessfulUpdateCheck", lastSuccessfulUpdateCheck));
				ILSpySettings.SaveSettings(updateSettings);
			}
			
			public event PropertyChangedEventHandler PropertyChanged;
			
			void OnPropertyChanged(string propertyName)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		
		/// <summary>
		/// If automatic update checking is enabled, checks if there are any updates available.
		/// Returns the download URL if an update is available.
		/// Returns null if no update is available, or if no check was performed.
		/// </summary>
		public static Task<string> CheckForUpdatesIfEnabledAsync(ILSpySettings spySettings)
		{
			var tcs = new TaskCompletionSource<string>();
			var s = new UpdateSettings(spySettings);
			if (s.AutomaticUpdateCheckEnabled) {
				// perform update check if we never did one before;
				// or if the last check wasn't in the past 7 days
				if (s.LastSuccessfulUpdateCheck == null
					|| s.LastSuccessfulUpdateCheck < DateTime.UtcNow.AddDays(-7)
					|| s.LastSuccessfulUpdateCheck > DateTime.UtcNow)
				{
					CheckForUpdateInternal(tcs, s);
				} else {
					tcs.SetResult(null);
				}
			} else {
				tcs.SetResult(null);
			}
			return tcs.Task;
		}

		public static Task<string> CheckForUpdatesAsync(ILSpySettings spySettings)
		{
			var tcs = new TaskCompletionSource<string>();
			var s = new UpdateSettings(spySettings);
			CheckForUpdateInternal(tcs, s);
			return tcs.Task;
		}

		static void CheckForUpdateInternal(TaskCompletionSource<string> tcs, UpdateSettings s)
		{
			GetLatestVersionAsync().ContinueWith(
				delegate (Task<AvailableVersionInfo> task) {
					try {
						s.LastSuccessfulUpdateCheck = DateTime.UtcNow;
						var v = task.Result;
						tcs.SetResult(v.Version > currentVersion ? v.DownloadUrl : null);
					} catch (AggregateException) {
						// ignore errors getting the version info
						tcs.SetResult(null);
					}
				});
		}
	}
	
	/// <summary>
	/// Interface that allows plugins to extend the about page.
	/// </summary>
	public interface IAboutPageAddition
	{
		void Write(ISmartTextOutput textOutput);
	}
}
