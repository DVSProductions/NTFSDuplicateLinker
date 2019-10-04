using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Interaktionslogik für ResultPage.xaml
	/// </summary>
	public partial class ResultPage : Page {
		ObservableCollection<DuplicateFile> vm;
		public ResultPage(ObservableCollection<DuplicateFile> duplicates_view) {
			vm = duplicates_view;
			InitializeComponent();
			duplicatesListView.ItemsSource = duplicates_view;
			CheckMonitor();
		}
		bool reset = false;
		async void CheckMonitor() {
			short b = 0;
			while(vm.Count > 0) {
				var isNotChecked = false;
				var isChecked = false;
				var tristate = false;
				foreach(var e in vm) {
					if(reset) break;
					if(e.Link) {
						if(isNotChecked) {
							tristate = true;
							break;
						}
						isChecked = true;
					}
					else {
						if(isChecked) {
							tristate = true;
							break;
						}
						isNotChecked = true;
					}
					b++;
					if(b == 0) await Task.Delay(100);
				}
				if(reset) { reset = false; continue; }
				if(tristate) {
					cb.IsThreeState = true;
					cb.IsChecked = null;
				}
				else {
					cb.IsThreeState = false;
					cb.IsChecked = isChecked;
				}
				await Task.Delay(250);
			}
		}
		//bool currentlyChecking = false;

		//private void CbAll_Click(object sender, RoutedEventArgs e) {
		//	if(currentlyChecking)
		//		return;
		//	currentlyChecking = true;
		//	if(cbAll.IsChecked == true || cbAll.IsChecked == false) {
		//		foreach(var df in duplicates_view) {
		//			df.Link = (bool) cbAll.IsChecked;
		//		}
		//	}
		//	if(duplicatesDataGrid.CurrentColumn == null)
		//		duplicatesDataGrid.CurrentColumn = duplicatesDataGrid.Columns[3];
		//	var orig = duplicatesDataGrid.CurrentColumn.SortDirection;
		//	//--------------------------------------------------------------------
		//	//------------------------ TO FIX!------------------------------------
		//	//--------------------------------------------------------------------
		//	 			duplicatesDataGrid.CurrentColumn.SortDirection = orig == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

		//	duplicatesDataGrid.CurrentColumn.SortDirection = orig;

		//	currentlyChecking = false;

		//}

		//private void DFChecked(bool state) {
		//	if(currentlyChecking)
		//		return;
		//	currentlyChecking = true;
		//	foreach(var df in duplicates_view) {
		//		if(df.Link != state) {
		//			cbAll.IsChecked = null;
		//		}
		//	}
		//	currentlyChecking = false;
		//}

		private void Cb_Click(object sender, System.Windows.RoutedEventArgs args) {
			bool state = cb.IsChecked == true;
			foreach(var e in vm)
				e.Link = state;
			reset = true;
		}
	}
}
