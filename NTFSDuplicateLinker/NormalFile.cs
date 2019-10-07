using System;
using System.IO;

namespace NTFSDuplicateLinker {
	public class NormalFile : IComparable<NormalFile> {
		/// <summary>
		/// Path to the file
		/// </summary>
		public readonly Folder parent;
		/// <summary>
		/// Just the name of the file with extension
		/// </summary>
		public readonly string filename;
		public long Size => fi?.Length ?? 0;
		public byte[] data;
		public byte[] hash;
		public FileInfo fi;
		public Hashstate Hashstate=Hashstate.nonexsistant;
		/// <summary>
		///
		/// </summary>
		/// <param name="fp">Path to the file</param>
		public NormalFile(string filename, Folder parent) {
			this.parent = parent;
			this.filename = filename;
			try {
				fi = new FileInfo(Path);
			}
			catch { }
		}
		public string Path => parent.Path + filename;
		public int CompareTo(NormalFile other) => ((fi?.LastWriteTime) ?? new DateTime(0)).CompareTo((other?.fi?.LastWriteTime) ?? new DateTime(0));
	}
}
