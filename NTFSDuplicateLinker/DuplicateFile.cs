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
		/// <summary>
		/// all known instances of this file
		/// </summary>
		public List<string> instances;
		/// <summary>
		///
		/// </summary>
		/// <param name="f">First file</param>
		public DuplicateFile(NormalFile f) {
			filename = f.filename;
			instances = new List<string>() { f.fullpath };
		}
		/// <summary>
		/// Sorts instances by <see cref="FileInfo.LastWriteTime"/>
		/// </summary>
		public void Sort() {
			instances.Sort(delegate (string a, string b) {
				return new FileInfo(a).LastWriteTime.CompareTo(new FileInfo(b).LastWriteTime);
			});
		}
	}
}
