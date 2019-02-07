using System.IO;

namespace NTFSDuplicateLinker {
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
			if (AnalyzeDirectoryForJunctions(path))
				return true;
			var dir = Directory.GetParent(path);
			do {
				if (AnalyzeDIForJunctions(dir))
					return true;
				dir = Directory.GetParent(dir.FullName);
			} while (dir != null);
			return false;
		}
		/// <summary>
		/// Simply compares two <see cref="byte[]"/> because c# won't do it
		/// </summary>
		/// <returns></returns>
		public static bool CompareByteArray(byte[] a, byte[] b) {
			if (a == null || b == null)
				return false;
			if (a.LongLength != b.LongLength)
				return false;
			for (long n = 0; n < a.LongLength; n++)
				if (a[n] != b[n])
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
			using (var f = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
				Syscall.GetFileInformationByHandle(f.Handle, out var info);
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
		public static uint GetLinks(DuplicateFile df) => GetLinks(MainWindow.pathStorage[df.instances[0]]);
		/// <summary>
		/// Analyzes if a path is on a NTFS formatted drive
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static bool IsOnNTFS(string path) {
			var drives = DriveInfo.GetDrives();
			var inputDriveName = Path.GetPathRoot(new FileInfo(path).FullName).Substring(0, 3);
			foreach (DriveInfo d in drives)
				if (d.Name == inputDriveName)
					return d.DriveFormat == "NTFS";
			return false;
		}
		/// <summary>
		/// Ensures that a path is safe to use and on NTFS
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static bool IsPathOK(string path) =>
			!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && IsOnNTFS(path);
	}
}
