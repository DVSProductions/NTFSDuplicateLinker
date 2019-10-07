using System.IO;

namespace NTFSDuplicateLinker {
	public enum Hashstate { nonexsistant, queued, hashing, done };
	public enum Chk { empty, gone, notNTFS, ok }
	static class Util {
		/// <summary>
		/// True if DirectoryInfo is Junction
		/// </summary>
		/// <param name="di"></param>
		/// <returns>True if DirectoryInfo is Junction</returns>
		public static bool AnalyzeDIForJunctions(DirectoryInfo di) =>
			di.Attributes.HasFlag(FileAttributes.ReparsePoint);
		/// <summary>
		/// True if last Directory is Junction
		/// </summary>
		/// <param name="pathToDirectory">Path to the Directory</param>
		/// <returns>True if last Directory is Junction</returns>
		public static bool AnalyzeDirectoryForJunctions(string pathToDirectory) =>
				AnalyzeDIForJunctions(new DirectoryInfo(pathToDirectory));
		/// <summary>
		/// Finds out whether a path is part of a Junction
		/// </summary>
		/// <param name="path"></param>
		/// <returns>True if Path is contained in a Junction</returns>
		public static bool AnalyzePathForJunctions(string path) {
			if(AnalyzeDirectoryForJunctions(path))
				return true;
			var dir = Directory.GetParent(path);
			do {
				if(AnalyzeDIForJunctions(dir))
					return true;
				dir = Directory.GetParent(dir.FullName);
			} while(dir != null);
			return false;
		}
		/// <summary>
		/// Simply compares two <see cref="byte[]"/> because c# won't do it
		/// </summary>
		/// <returns></returns>
		public static bool CompareByteArray(byte[] a, byte[] b) {
			if(a == null || b == null)
				return false;
			if(a.LongLength != b.LongLength)
				return false;
			for(long n = 0; n < a.LongLength; n++)
				if(a[n] != b[n])
					return false;
			return true;
		}
		/// <summary>
		/// Gets the number of HardLinks of a given <paramref name="filePath"/>
		/// </summary>
		/// <param name="filePath">Path to the file</param>
		/// <returns></returns>
		public static uint GetLinks(string filePath) {
			//try {
			using(var f = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
#pragma warning disable CS0618 // Typ oder Element ist veraltet
				Syscall.GetFileInformationByHandle(f.Handle, out var info);
#pragma warning restore CS0618 // Typ oder Element ist veraltet
				return info.NumberOfLinks;
			}
			//}
			//catch {
			//	return uint.MaxValue;
			//}
		}
		/// <summary>
		/// Gets the number of HardLinks of the first <see cref="DuplicateFile.instances"/> in <paramref name="df"/>
		/// </summary>
		/// <param name="df">DuplicateFile to analyze</param>
		/// <returns></returns>
		public static uint GetLinks(DuplicateFile df) => GetLinks(df.instances[0].Path);
		/// <summary>
		/// Analyzes if a path is on a NTFS formatted drive
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static bool IsOnNTFS(string path) {
			var drives = DriveInfo.GetDrives();
			var inputDriveName = Path.GetPathRoot(new FileInfo(path).FullName).Substring(0, 3);
			foreach(var d in drives)
				if(d.Name == inputDriveName)
					return d.DriveFormat == "NTFS";
			return false;
		}
		public static Chk isPathOK(string path) {
			return string.IsNullOrWhiteSpace(path)
				? Chk.empty
				: !Directory.Exists(path) ? Chk.gone : !Util.IsOnNTFS(path) ? Chk.notNTFS : Chk.ok; ;
		}
	}
}
