using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Stores duplicates
	/// </summary>
	partial class DuplicateFile {
		/// <summary>
		/// Just the name and extension
		/// </summary>
		public readonly string filename;
		public bool Link { get; set; }
		public string Filename {
			get => filename;
		}
		/// <summary>
		/// all known instances of this file
		/// </summary>
		public List<int> instances;
		/*
		public string[] Instances {
			get {
				
				var ret = new string[instances.Count];
				for (int n = 0, ln = instances.Count; n < ln; n++)
					ret[n] = MainWindow.pathStorage[instances[n]];
				return ret;
			}
		}
		*/
		public int DetectedCopies {
			get => instances.Count;
		}
		public byte[] finalhash;
		public string Hash {
			get => finalhash == null ? "NULL" : System.Convert.ToBase64String(finalhash);
		}
		

	private Filesize FZ;
	public Filesize Size {
		get {
			if (FZ == null) {
				FZ = new Filesize(MainWindow.sizeStorage[instances[0]]);
			}
			return FZ;
		}
	}
	public uint Links {
		get => Util.GetLinks(MainWindow.pathStorage[instances[0]])-1;
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
