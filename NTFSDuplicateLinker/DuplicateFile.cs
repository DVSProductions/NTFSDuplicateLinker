using System.Collections.Generic;
using System.IO;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Stores duplicates
	/// </summary>
	public partial class DuplicateFile {
		/// <summary>
		/// Just the name and extension
		/// </summary>
		public readonly string filename;
		private bool doLink;
		public bool Link { get => doLink; set { doLink = value; /*MainWindow.checkedCallback(value);*/} }
		public string Filename => filename;
		public string DisplayText => filename + "\t(" + (finalhash == null ? "NULL" : System.Convert.ToBase64String(finalhash)) + ")";
		/// <summary>
		/// all known instances of this file
		/// </summary>
		public List<NormalFile> instances;
		/// <summary>
		/// test
		/// </summary>
		public string[] Instances {
			get {
				var ret = new string[instances.Count];
				for(int n = 0, ln = instances.Count; n < ln; n++)
					ret[n] = instances[n].Path;
				return ret;
			}
		}

		public int DetectedCopies => instances.Count;
		public byte[] finalhash;
		public string Hash => finalhash == null ? "NULL" : System.Convert.ToBase64String(finalhash);


		private Filesize FZ;
		public Filesize Size {
			get {
				if(FZ == null)
					FZ = new Filesize(instances[0].Size);
				return FZ;
			}
		}
		public uint Links => Util.GetLinks(instances[0].Path) - 1;


		/// <param name="f">First file</param>
		public DuplicateFile(NormalFile f) {
			filename = f.filename;
			instances = new List<NormalFile>() { f };
		}
		/// <summary>
		/// Sorts instances by <see cref="FileInfo.LastWriteTime"/>
		/// </summary>
		public void Sort() { }// => instances.Sort();
	}
}
