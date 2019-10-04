using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
