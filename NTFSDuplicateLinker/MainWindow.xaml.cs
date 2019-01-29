using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NTFSDuplicateLinker {
	class Dll {
		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		public struct BY_HANDLE_FILE_INFORMATION {
			public uint FileAttributes;
			public FILETIME CreationTime;
			public FILETIME LastAccessTime;
			public FILETIME LastWriteTime;
			public uint VolumeSerialNumber;
			public uint FileSizeHigh;
			public uint FileSizeLow;
			public uint NumberOfLinks;
			public uint FileIndexHigh;
			public uint FileIndexLow;
		}
		[DllImport("kernel32.dll")]
		public static extern uint GetLastError();
		[DllImport("Kernel32.dll", CharSet = CharSet.Auto)]
		public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
		public static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);
	}
	public partial class MainWindow : Window {
		struct WorkFile {
			public byte[] data;
			public string path;
		}
		struct HandoverObject {
			public Dictionary<string, byte[]> results;
			public Queue<WorkFile> queque;
			public List<DuplicateFile> targets;
		}
		class DuplicateFile {
			public readonly string filename;
			public List<string> instances;
			public DuplicateFile(NormalFile f) {
				filename = f.filename;
				instances = new List<string>() { f.fullpath };
			}
			public void Sort() {
				instances.Sort(delegate (string a, string b) {
					return new FileInfo(a).LastWriteTime.CompareTo(new FileInfo(b).LastWriteTime);
				});
			}
		}
		Dictionary<string, byte[]> hashedFiles;
		Queue<WorkFile> files;
		public MainWindow() {
			InitializeComponent();
		}
		struct NormalFile {
			public string fullpath;
			public string filename;
			public NormalFile(string fp) {
				fullpath = fp;
				filename = Path.GetFileName(fp);
			}
		}

		bool stillLoading = false;
		/// <summary>
		/// True if DirectoryInfo is Junction
		/// </summary>
		/// <param name="di"></param>
		/// <returns>True if DirectoryInfo is Junction</returns>
		bool AnalyzeDIForJunctions(DirectoryInfo di) =>
			di.Attributes.HasFlag(FileAttributes.ReparsePoint);
		/// <summary>
		/// True if last Directory is Junction
		/// </summary>
		/// <param name="pathToDirectory">Path to the Directory</param>
		/// <returns>True if last Directory is Junction</returns>
		bool AnalyzeDirectoryForJunctions(string pathToDirectory) =>
				AnalyzeDIForJunctions(new DirectoryInfo(pathToDirectory));
		/// <summary>
		/// 
		/// </summary>
		/// <param name="path"></param>
		/// <returns>True if Path is contained in a Junction</returns>
		bool AnalyzePathForJunctions(string path) {
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
		void Discover(object obj) {
			string currPath;
			List<NormalFile> ret;
			{
				var conv = (Tuple<string, List<NormalFile>>)obj;
				currPath = conv.Item1;
				ret = conv.Item2;
			}
			var dirs = new Queue<string>();
			dirs.Enqueue(currPath);
			while (dirs.Count > 0) {
				string work = dirs.Dequeue();
				try {
					foreach (var d in Directory.EnumerateDirectories(work))
						if (!AnalyzePathForJunctions(d))
							dirs.Enqueue(d);

					foreach (var fp in Directory.EnumerateFiles(work))
						ret.Add(new NormalFile(fp));
				}
				catch {
				}
			}
			running = 0;
		}
		int running = 0;
		int position = 0;
		long usedMemory = 0;
		void MemoryMonitor(object nul) {
			while (running > 0) {
				usedMemory = GC.GetTotalMemory(false);
				Task.Delay(250).Wait();
			}
		}
		void HashAsync(object obj) {
			var access = (HandoverObject)obj;
			using (var hasher = MD5.Create()) {
				bool loadall = false;
				while (stillLoading) {
					if (access.queque.Count > 0) {
						var work = new WorkFile();
						lock (access.queque) {
							if (access.queque.Count > 0)
								work = access.queque.Dequeue();
						}
						if (work.path != null) {
							var dat = hasher.ComputeHash(work.data);
							lock (access.results) {
								if (!access.results.ContainsKey(work.path))
									access.results.Add(work.path, dat);
							}
						}
						if (!loadall && usedMemory > 2000000000L) {
							loadall = true;
						}
						else if (loadall && usedMemory < 400000000L) {//400.000.000 400mb
							loadall = false;
						}
						else if (usedMemory < 1000000000L && stillLoading && !loadall) {//1.000.000.00
							Task.Delay(100).Wait();
						}
						else if (stillLoading && !loadall) {//1.000.000.00
							Task.Delay(10).Wait();
						}

					}
					else {
						GC.Collect();
						Task.Delay(100).Wait();
					}
				}
			}
			running--;
		}
		void HashSync(string file, Dictionary<string, byte[]> lst) {
			using (var hasher = MD5.Create()) {
				using (var fs = File.OpenRead(file)) {
					if (fs.Length == 0)
						return;
					var hash = hasher.ComputeHash(fs);
					lock (lst) {
						if (!lst.ContainsKey(file))
							lst.Add(file, hash);
					}
				}
			}
		}
		void Reader(object obj) {
			position = 0;
			var access = (HandoverObject)obj;
			foreach (var dup in access.targets) {
				position++;
				foreach (var file in dup.instances) {
					//check for huge size
					var fi = new FileInfo(file);
					if (fi.Length > 1000000000L) {
						HashSync(file, access.results);
					}
					else {
						var wf = new WorkFile();
						try {
							wf = new WorkFile() {
								data = File.ReadAllBytes(file),
								path = file
							};
						}
						catch {

						}
						if (wf.data != null && wf.data.LongLength != 0) {
							lock (access.queque) {
								access.queque.Enqueue(wf);
							}
						}
					}
				}
			}
			stillLoading = false;
		}
		void FindDuplicates(object transfer) {
			position = 0;
			var trans = (Tuple<List<DuplicateFile>, List<NormalFile>>)transfer;
			var filePaths = trans.Item2;
			for (var i = 0; i < filePaths.Count; i++, position++) {
				var work = new DuplicateFile(filePaths[i]);
				for (var n = i + 1; n < filePaths.Count; n++)
					if (work.filename == filePaths[n].filename)
						work.instances.Add(filePaths[n].fullpath);
				if (work.instances.Count > 1)
					trans.Item1.Add(work);
			}
			running = 0;
		}
		void Sortall(object obj) {
			var dups = (List<DuplicateFile>)obj;
			position = 0;
			foreach (var dup in dups) {
				dup.Sort();
				position++;
			}
			running = 0;
		}
		bool CompareByteArray(byte[] a, byte[] b) {
			if (a == null || b == null)
				return false;
			if (a.LongLength != b.LongLength)
				return false;
			for (long n = 0; n < a.LongLength; n++)
				if (a[n] != b[n])
					return false;
			return true;
		}
		List<DuplicateFile> FinalDuplicates(List<DuplicateFile> duplicates) {
			var ret = new List<DuplicateFile>();
			foreach (var dup in duplicates) {
				var options = new List<DuplicateFile>() { new DuplicateFile(new NormalFile(dup.instances[0])) };
				hashedFiles.TryGetValue(options[0].instances[0], out var tmp);
				var hashes = new List<byte[]> { tmp };
				for (int n = 1; n < dup.instances.Count; n++) {
					var inst = dup.instances[n];
					hashedFiles.TryGetValue(inst, out var myhash);
					var found = false;
					for (int i = 0; i < options.Count; i++) {
						if (CompareByteArray(hashes[i], myhash)) {
							found = true;
							options[i].instances.Add(inst);
						}
					}
					if (!found) {
						options.Add(new DuplicateFile(new NormalFile(dup.instances[n])));
						hashes.Add(myhash);
					}
				}
				ret.AddRange(options);
			}
			return ret;
		}
		List<DuplicateFile> CleanupDuplicates(List<DuplicateFile> ldf) {
			var ret = new List<DuplicateFile>();
			foreach (var d in ldf)
				if (d.instances.Count > 1)
					ret.Add(d);
			return ret;
		}
		static uint GetLinks(string filePath) {
			//try {
			using (var f = new FileStream(filePath, FileMode.Open, FileAccess.Read)) {
				Dll.GetFileInformationByHandle(f.Handle, out var info);
				return info.NumberOfLinks;
			}
			//}
			//catch {
			//	return uint.MaxValue;
			//}
		}
		static uint GetLinks(DuplicateFile df) => GetLinks(df.instances[0]);
		static bool IsOnNTFS(string path) {
			var drives = DriveInfo.GetDrives();
			var inputDriveName = Path.GetPathRoot(new FileInfo(path).FullName).Substring(0, 3);
			foreach (DriveInfo d in drives)
				if (d.Name == inputDriveName)
					return d.DriveFormat == "NTFS";
			return false;
		}
		static void LinkDuplicates(DuplicateFile file) {
			file.instances.Sort();
			var newExtension = ".DVSLINKER.BAK";
			string prefix = "\\\\?\\";// @"\?";
			var orig = prefix + file.instances[0];
			uint links = GetLinks(file);
			for (int n = 1; n < file.instances.Count; n++, links++) {
				if (links >= 1023) {
					orig = prefix + file.instances[n];
					links = GetLinks(file.instances[n]) - 1;//for loops adds 1 back on
				}
				else {
					var backup = file.instances[n] + newExtension;
					try {
						if (File.Exists(backup))
							File.Delete(backup);
						File.Copy(file.instances[n], backup);//backup
						File.Delete(file.instances[n]);
					}
					catch {
						if (!File.Exists(file.instances[n])) {
							if (File.Exists(backup)) {
								File.Copy(backup, file.instances[n]);
								File.Delete(backup);
							}
						}
						continue;
					}

					var path = prefix + file.instances[n];
					if (!Dll.CreateHardLink(path, orig, IntPtr.Zero)) {
						uint error = Dll.GetLastError();//Marshal.GetLastWin32Error();
						Debug.WriteLine("ERROR: " + error);
						File.Copy(backup, file.instances[n]);//restore backup
						File.Delete(backup);
					}
					else {
						File.Delete(backup);//remove backup because HardLink worked
					}
				}
			}
		}

		async Task LinkAllDuplicates(List<DuplicateFile> duplicates) {
			int last = 0;
			for (long n = 0; n < duplicates.Count; n++) {
				LinkDuplicates(duplicates[(int)n]);
				pbStatus.Value = n;
				if ((n * 100) / duplicates.Count > last * 5) {
					last++;
					await Task.Delay(100);
				}
			}
		}
		/*
		void LinkAllDuplicates(List<DuplicateFile> duplicates) {
			int last = 0;
			for (long n = 0; n < duplicates.Count; n++) {
				LinkDuplicates(duplicates[(int)n]);
				pbStatus.Value = n;
				if ((n * 100) / duplicates.Count > last * 5) {
					last++;
					Task.Delay(100);
				}
			}
		}
		*/
		Expander CreateEntry(DuplicateFile df, SolidColorBrush bg) {
			hashedFiles.TryGetValue(df.instances[0], out var myhash);
			var exp = new Expander() {
				Header = df.filename + "\t(" + (myhash == null ? "NULL" : Convert.ToBase64String(myhash)) + ")",
				Background = bg,
				BorderBrush = null
			};
			var mysp = new StackPanel();
			foreach (var path in df.instances) {
				mysp.Children.Add(
					new TextBox() {
						BorderBrush = null,
						Background = null,
						IsReadOnly = true,
						IsReadOnlyCaretVisible = false,
						Text = String.IsNullOrEmpty(path) ? "NULL" : path
					});
			}
			exp.Content = mysp;
			return exp;
		}
		async Task DisplayDuplicates(List<DuplicateFile> ldf) {
			bool switcher = true;
			position = 0;
			SolidColorBrush light = new SolidColorBrush(Colors.White), dark = new SolidColorBrush(Colors.LightGray);
			int next = 0;
			foreach (var df in ldf) {
				position++;
				switcher = !switcher;
				spItems.Children.Add(CreateEntry(df, switcher ? dark : light));
				if (((position * 100L) / ldf.Count) >= next) {
					next += 10;
					PBManager.UpdateCurrentPosition(position);
					await Task.Delay(100);
				}
			}
		}
		List<DuplicateFile> finalDuplicates;
		private async void Button_Click(object sender, RoutedEventArgs e) {
			btLink.IsEnabled = false;
			var dir = tbPath.Text;
			spItems.Children.Clear();
			if (!IsOnNTFS(dir))
				return;
			files = new Queue<WorkFile>();
			hashedFiles = new Dictionary<string, byte[]>();
			var filePaths = new List<NormalFile>();
			btAnalyze.IsEnabled = false;
			running = 1;
			PBManager.Init(pbStatus, 7, 1);
			ThreadPool.QueueUserWorkItem(Discover, new Tuple<string, List<NormalFile>>(dir, filePaths));
			while (running > 0) {
				await Task.Delay(100);
			}
			PBManager.MoveToNextAction(filePaths.Count);
			var duplicates = new List<DuplicateFile>();
			running = 1;
			ThreadPool.QueueUserWorkItem(
				FindDuplicates,
				new Tuple<List<DuplicateFile>, List<NormalFile>>(duplicates, filePaths));
			while (running > 0) {
				await Task.Delay(100);
				PBManager.UpdateCurrentPosition(position);
			}
			PBManager.MoveToNextAction(duplicates.Count);
			stillLoading = true;
			ThreadPool.QueueUserWorkItem(MemoryMonitor);
			ThreadPool.QueueUserWorkItem(Reader, new HandoverObject() { queque = files, targets = duplicates, results = hashedFiles });
			for (int n = 0; n < 4; n++, running++)
				ThreadPool.QueueUserWorkItem(HashAsync, new HandoverObject() { queque = files, targets = duplicates, results = hashedFiles });
			while (running > 0) {
				PBManager.UpdateCurrentPosition(position);
				await Task.Delay(100);
			}
			await Task.Delay(500);

			running = 1;
			PBManager.MoveToNextAction(duplicates.Count);
			ThreadPool.QueueUserWorkItem(Sortall, duplicates);
			while (running > 0) {
				PBManager.UpdateCurrentPosition(position);
				await Task.Delay(100);
			}
			PBManager.MoveToNextAction(1);
			finalDuplicates = FinalDuplicates(duplicates);
			PBManager.MoveToNextAction(1);
			finalDuplicates = CleanupDuplicates(finalDuplicates);
			PBManager.MoveToNextAction(finalDuplicates.Count);
			await DisplayDuplicates(finalDuplicates);
			btLink.IsEnabled = true;
			btAnalyze.IsEnabled = true;
		}

		private async void BtLink_Click(object sender, RoutedEventArgs e) {
			btLink.IsEnabled = false;
			btAnalyze.IsEnabled = false;
			pbStatus.Value = 0;
			pbStatus.Maximum = finalDuplicates.Count - 1;
			await LinkAllDuplicates(finalDuplicates);
			spItems.Children.Clear();
			btAnalyze.IsEnabled = true;
		}
	}
}
