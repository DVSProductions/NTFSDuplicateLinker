using System;
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
		public class Filesize : IComparable {
			private static readonly string[] Sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
			long mysize;
			private string rendered;
			public Filesize(long bytes) {
				mysize = bytes;
			}

			public int CompareTo(object obj) {
				if (!(obj is Filesize))
					return 0;
				var tmp = (Filesize)obj;
				return tmp.mysize > mysize ? -1 : (tmp.mysize < mysize ? 1 : 0);

			}

			public override string ToString() {
				if (rendered != null)
					return rendered;
				int idx = 0;
				while (idx < Sizes.Length) {
					if (mysize < 10000) {
						rendered = mysize + " " + Sizes[idx];
						return rendered;
					}
					mysize /= 1000;
					idx++;
				}
				rendered = mysize + " " + Sizes[idx];
				return rendered;
			}
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
			get => Util.GetLinks(MainWindow.pathStorage[instances[0]]);
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
