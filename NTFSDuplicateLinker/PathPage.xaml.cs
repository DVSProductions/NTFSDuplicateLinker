using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Interaktionslogik für PathPage.xaml
	/// </summary>
	public partial class PathPage : Page {
		private Action onEnter;
		public PathPage(Action onEnter) {
			this.onEnter = onEnter;
			InitializeComponent();
			SearchForPath();
		}
		public void DisplayError(bool error) {
			tbPath.BorderBrush = error ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);
			tbPath.BorderThickness = error ? new Thickness(2) : new Thickness(1);
		}
		private void TbPath_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
			if(e.Key == System.Windows.Input.Key.Enter)
				onEnter();
		}
		public enum Chk { empty, gone, notNTFS, ok }
		Chk result;
		public string Path => tbPath.Text;
		public Chk Validate(string path) {
			return string.IsNullOrWhiteSpace(path)
				? Chk.empty
				: !Directory.Exists(path) ? Chk.gone : !Util.IsOnNTFS(path) ? Chk.notNTFS : Chk.ok; ;
		}
		void ValidateThread(object o) => result = Validate(o as string);

		async void SearchForPath() {

			while(true) {
				if(tbPath.Text != "") {
					var validator = new Thread(ValidateThread);
					validator.Start(Path);
					while(validator.IsAlive) await Task.Delay(100);
					validator.Join();
					if(result == Chk.empty || result == Chk.gone) {
						DisplayError(true);
					}
					else if(result == Chk.notNTFS) {
						Error.Visibility = Visibility.Visible;
						Error.Content = "This tool only works on NTFS drives";
					}
					else {
						DisplayError(false);
						Error.Visibility = Visibility.Hidden;
					}
				}
				await Task.Delay(250);
			}
		}

		private void Button_Click(object sender, RoutedEventArgs e) {
			using(var dialog = new System.Windows.Forms.FolderBrowserDialog())
				if(dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
					tbPath.Text = dialog.SelectedPath;
		}
	}
}
