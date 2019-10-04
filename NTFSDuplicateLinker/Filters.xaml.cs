using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Interaktionslogik für Filters.xaml
	/// </summary>
	partial class Filters : Window {
		private List<Filter> filters;
		public Dictionary<Filter, bool> activatedFilters = new Dictionary<Filter, bool>();
		private void ShowFilters() {
			void createFilterOption(Filter f) {
				var cb = new CheckBox() {
					Content = f.Name
				};
				cb.Click += Cb_Click;
				spFilter.Children.Add(cb);
			}
			void Cb_Click(object sender, RoutedEventArgs e) {
				var snd = (CheckBox)sender;
				var filter = filters[spFilter.Children.IndexOf(snd)];
				if (activatedFilters.ContainsKey(filter)) activatedFilters.Remove(filter);
				activatedFilters.Add(filter, (bool)snd.IsChecked);
			}
			foreach (Filter f in filters) {
				createFilterOption(f);
			}
		}



		public Filters() {
			filters = new List<Filter>() {
				new TestFilter()
			};
			InitializeComponent();
			ShowFilters();
		}
	}
}
