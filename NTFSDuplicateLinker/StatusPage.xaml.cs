using System.Windows.Controls;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Interaktionslogik für StatusPage.xaml
	/// </summary>
	public partial class StatusPage : Page {
		public StatusPage() {
			InitializeComponent();
			MainWindow.PBManager.Init(pb, 1, 1);
		}
		public string Number {
			get => lbnum.Content as string;
			set => lbnum.Content = value;
		}
		public string Subtitle {
			get => lb2.Content as string;
			set => lb2.Content = value;
		}
	}
}
