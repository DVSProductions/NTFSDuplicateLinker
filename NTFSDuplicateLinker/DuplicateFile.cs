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
        public string Filename
        {
            get => filename;
        }

        public bool Deduplicate {  get;  set; }

		/// <summary>
		/// all known instances of this file
		/// </summary>
		public List<string> instances;
        public List<string> Instances {
            get {
                return instances;
            }
        }
		public byte[] finalhash;
		public string DisplayText {
			get => filename + "\t(" + (finalhash == null ? "NULL" : System.Convert.ToBase64String(finalhash)) + ")";
		}
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
