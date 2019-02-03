using System.Collections.Generic;
using System.IO;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Stores duplicates
	/// </summary>
	class DuplicateFile {
		/// <summary>
		/// Just the name and extension
		/// </summary>
		public readonly string filename;
		public string Filename {
			get => filename;
		}

		public bool Deduplicate { get; set; }

		/// <summary>
		/// all known instances of this file
		/// </summary>
		public List<int> instances;
		public class ReferenceViewer{
			readonly int myref;
			public ReferenceViewer(int nref) {
				myref = nref;
			}
			public new string ToString() {
				return MainWindow.pathStorage[myref];
			}
		}
		public ReferenceViewer[] Instances {
			get {
				var ret = new ReferenceViewer[instances.Count];
				for (int n = 0, ln = instances.Count; n < ln; n++)
					ret[n] =new ReferenceViewer(instances[n]);
				return ret;
			}
		}
		public byte[] finalhash;
		public string DisplayText {
			get => filename + "\t(" + (finalhash == null ? "NULL" : System.Convert.ToBase64String(finalhash)) + ")";
		}
		/// <param name="f">First file</param>
		public DuplicateFile(NormalFile f) {
			filename = f.filename;
			instances = new List<int>() { f.fullpathID };
		}
		/// <summary>
		/// Sorts instances by <see cref="FileInfo.LastWriteTime"/>
		/// </summary>
		public void Sort() {
			instances.Sort(delegate (int a, int b) {
				return new FileInfo(MainWindow.pathStorage[a]).LastWriteTime.CompareTo(new FileInfo(MainWindow.pathStorage[b]).LastWriteTime);
			});
		}
	}
}
