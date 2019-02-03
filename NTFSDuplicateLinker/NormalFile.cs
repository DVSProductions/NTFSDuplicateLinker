using System.IO;

namespace NTFSDuplicateLinker {
	struct NormalFile {
		/// <summary>
		/// Path to the file
		/// </summary>
		public readonly int fullpathID;
		/// <summary>
		/// Just the name of the file with extension
		/// </summary>
		public readonly string filename;
		/// <summary>
		///
		/// </summary>
		/// <param name="fp">Path to the file</param>
		public NormalFile(string fp,int ID) {
			filename = Path.GetFileName(fp);
			fullpathID = ID;			
		}
	}
}
