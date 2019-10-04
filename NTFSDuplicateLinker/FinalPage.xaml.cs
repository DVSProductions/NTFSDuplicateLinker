using System;
using System.Windows.Controls;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Interaktionslogik für FinalPage.xaml
	/// </summary>
	public partial class FinalPage : Page {
		public FinalPage(UInt64 saved, ulong removedFiles) {
			InitializeComponent();
			lbcount.Content = removedFiles;
			string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };
			var tmp = saved;
			var suffixIDX = 0;
			while(tmp > 9999 && suffixIDX != suffixes.Length - 1) {
				tmp /= 1024;
				suffixIDX++;
			}
			lbstore.Content = tmp + suffixes[suffixIDX];
		}
	}
}
