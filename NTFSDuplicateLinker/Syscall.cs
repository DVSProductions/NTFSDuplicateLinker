using System;
using System.Runtime.InteropServices;

namespace NTFSDuplicateLinker {
	static class Syscall {
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct BY_HANDLE_FILE_INFORMATION {
			public uint FileAttributes;
#pragma warning disable CS0618 // Type or member is obsolete
			public FILETIME CreationTime;
			public FILETIME LastAccessTime;
			public FILETIME LastWriteTime;
#pragma warning restore CS0618 // Type or member is obsolete
			public uint VolumeSerialNumber;
			public uint FileSizeHigh;
			public uint FileSizeLow;
			public uint NumberOfLinks;
			public uint FileIndexHigh;
			public uint FileIndexLow;
		}
		[DllImport("kernel32.dll")]
		public static extern uint GetLastError();
		//[DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
		//public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

		//[DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
		//public static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
	}
}
