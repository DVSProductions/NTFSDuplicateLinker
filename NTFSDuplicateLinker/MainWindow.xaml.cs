using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NTFSDuplicateLinker {
	public partial class MainWindow : Window {
		/// <summary>
		/// true while Reader is running
		/// </summary>
		bool stillLoading = false;
		/// <summary>
		/// indicates how many Tasks are running
		/// </summary>
		int running = 0;
		/// <summary>
		/// Used for Tracking progress across Tasks
		/// </summary>
		int position = 0;
		/// <summary>
		/// Program memory usage in bits
		/// </summary>
		long usedMemory = 0;
		/// <summary>
		/// Filepath and hash
		/// </summary>
		Dictionary<string, byte[]> hashedFiles;
		/// <summary>
		/// <see cref="List{}"/> containing every identified <see cref="DuplicateFile"/>
		/// </summary>
		List<DuplicateFile> finalDuplicates;

		//view model

		ObservableCollection<DuplicateFile> duplicates_view = new ObservableCollection<DuplicateFile>();


		/// <summary>
		/// Independend Thread
		/// Searches for files in a given folderstructure
		/// </summary>
		/// <param name="obj">Tuple<string, List<NormalFile>> Object containing </param>
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
						if (!Util.AnalyzePathForJunctions(d))
							dirs.Enqueue(d);

					foreach (var fp in Directory.EnumerateFiles(work))
						ret.Add(new NormalFile(fp));
				}
				catch {
				}
			}
			running = 0;
		}

		/// <summary>
		/// Monitors memory usage while running > 0
		/// </summary>
		/// <param name="nul">noting</param>
		void MemoryMonitor(object nul) {
			while (running > 1) {
				usedMemory = GC.GetTotalMemory(false);
				Task.Delay(Config.MEMORYMONITORDELAY).Wait();
			}
			running--;
		}
		/// <summary>
		/// Looks for Files in the Queue&lt;<see cref="WorkFile"/>&gt; and starts hashing them
		/// </summary>
		/// <param name="obj"><see cref="HandoverObject"/> containing the required references</param>
		void HashAsync(object obj) {
			var access = (HandoverObject)obj;
			using (var hasher = MD5.Create()) {
				bool panic = false;
				while (stillLoading || access.queque.Count > 0) {
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
						if (!panic && usedMemory > Config.MAXMEMORYUSAGE) {
							panic = true;
						}
						else if (panic && usedMemory < Config.MINMEMORYUSAGE) {//400.000.000 400mb
							panic = false;
						}
						else if (!panic && usedMemory < Config.OKMEMORYUSAGE && stillLoading) {//1.000.000.00
							Task.Delay(Config.HASHASYNCREGDELAY).Wait();
						}
						else if (!panic && stillLoading) {
							Task.Delay(Config.HASHASYNCNOTOKDELAY).Wait();
						}

					}
					else {
						GC.Collect();
						Task.Delay(Config.HASHASYNCREGDELAY).Wait();
					}
				}
			}
			running--;
		}
		/// <summary>
		/// Hashes very large files on the fly
		/// MUCH SLOWER than async hashing.
		/// </summary>
		/// <param name="file">Path of the file to hash</param>
		/// <param name="lst"><see cref="Dictionary{string,byte[]}"/> reference to all hashes</param>
		static void HashSync(string file, Dictionary<string, byte[]> lst) {
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
		/// <summary>
		/// Loads all identified duplicates into Memory for hashing by <see cref="HashAsync(object)"/>
		/// </summary>
		/// <param name="obj"><see cref="HandoverObject"/> containing the required references</param>
		void Reader(object obj) {
			position = 0;
			var access = (HandoverObject)obj;
			foreach (var dup in access.targets) {
				position++;
				foreach (var file in dup.instances) {
					//check for huge size
					var fi = new FileInfo(file);
					if (fi.Length > Config.MAXIMUMASYNCFILESIZE) {
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
		/// <summary>
		/// Analyze all normal files and find duplicate filenames
		/// </summary>
		/// <param name="transfer">
		///		Tuple&lt;List&lt; <see cref="DuplicateFile"/>&gt;, List&lt; <see cref="NormalFile"/>&gt;&gt;
		///		reference for searching and output
		/// </param>
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
		/// <summary>
		/// Sorts all <see cref="DuplicateFile.instances"/> of all <see cref="DuplicateFile"/>s using <see cref="DuplicateFile.Sort()"/>
		/// </summary>
		/// <param name="obj">List&lt;<see cref="DuplicateFile"/>&gt;</param>
		void Sortall(object obj) {
			var dups = (List<DuplicateFile>)obj;
			position = 0;
			foreach (var dup in dups) {
				dup.Sort();
				position++;
			}
			running = 0;
		}
		/// <summary>
		/// Checks hashes of duplicates whether they are acutal duplicates
		/// </summary>
		/// <param name="duplicates"></param>
		/// <returns></returns>
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
						if (Util.CompareByteArray(hashes[i], myhash)) {
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
		/// <summary>
		/// Remove duplicates that have no more reverences
		/// </summary>
		/// <param name="ldf"></param>
		/// <returns></returns>
		static List<DuplicateFile> CleanupDuplicates(List<DuplicateFile> ldf) {
			var ret = new List<DuplicateFile>();
			foreach (var d in ldf)
				if (d.instances.Count > 1)
					ret.Add(d);
			return ret;
		}
		/// <summary>
		/// Do the linking of one file
		/// Ensures that duplicate link limits are not exceeded
		/// </summary>
		/// <param name="file">The DuplicateFile to link into one</param>
		static void LinkDuplicates(DuplicateFile file) {
			file.instances.Sort();
			var newExtension = ".DVSLINKER.BAK";
			string prefix = "\\\\?\\";// @"\?";
			var orig = prefix + file.instances[0];
			uint links = Util.GetLinks(file);
			for (int n = 1; n < file.instances.Count; n++, links++) {
				if (links >= 1023) {
					orig = prefix + file.instances[n];
					links = Util.GetLinks(file.instances[n]) - 1;//for loops adds 1 back on
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
					if (!Syscall.CreateHardLink(path, orig, IntPtr.Zero)) {
						uint error = Syscall.GetLastError();//Marshal.GetLastWin32Error();
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
		/// <summary>
		/// Wrapper to link everything up
		/// </summary>
		/// <param name="duplicates">List of all duplicates to link</param>
		/// <returns></returns>
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
		/// <summary>
		/// Show all Duplicates in <see cref="MainWindow.duplicatesListView"/>
		/// </summary>
		/// <param name="ldf">All duplicates</param>
		/// <returns></returns>
		async Task DisplayDuplicates(List<DuplicateFile> ldf) {
			bool switcher = true;
			position = 0;
			long next = 0;
			var s = Stopwatch.StartNew();
			foreach (var df in ldf) {
				position++;
				switcher = !switcher;
				hashedFiles.TryGetValue(df.instances[0], out var myhash);
				df.finalhash = myhash;
				duplicates_view.Add(df);
				if (s.ElapsedMilliseconds / 1000 >= next) {
					next = (s.ElapsedMilliseconds / 1000) + 1;
					PBManager.UpdateCurrentPosition(position);
					await Task.Delay(33);
				}
			}
			PBManager.UpdateCurrentPosition(position);
		}
		void DisplayError(bool error) {
			tbPath.BorderBrush = error ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);
			tbPath.BorderThickness = error ? new Thickness(2) : new Thickness(1);
		}
		/// <summary>
		/// Launches all hashing and analyzing logic
		/// </summary>
		/// <param name="path">Path to analyze. Does not have to be null checked</param>
		/// <returns></returns>
		async Task Analyzer(string path) {
			if (
				string.IsNullOrWhiteSpace(path) ||
				!Directory.Exists(path) ||
				!Util.IsOnNTFS(path)
				) {
				DisplayError(true);
				return;
			}
			DisplayError(false);
			var files = new Queue<WorkFile>();
			var filePaths = new List<NormalFile>();
			var duplicates = new List<DuplicateFile>();
			hashedFiles = new Dictionary<string, byte[]>();

			running = 1;
			PBManager.Init(pbStatus, 7, 1);
			Debug.WriteLine("Scanning...");
			ThreadPool.QueueUserWorkItem(Discover, new Tuple<string, List<NormalFile>>(path, filePaths));
			while (running > 0) {
				await Task.Delay(100);
			}
			PBManager.MoveToNextAction(filePaths.Count);

			running = 1;
			Debug.WriteLine("Identifying duplicates...");
			ThreadPool.QueueUserWorkItem(
				FindDuplicates,
				new Tuple<List<DuplicateFile>, List<NormalFile>>(duplicates, filePaths));
			while (running > 0) {
				await Task.Delay(100);
				PBManager.UpdateCurrentPosition(position);
			}
			Debug.WriteLine("Found:" + duplicates.Count + " duplicates in " + filePaths.Count + " Files");
			PBManager.MoveToNextAction(duplicates.Count);

			stillLoading = true;
			running = 1;
			ThreadPool.QueueUserWorkItem(MemoryMonitor);
			ThreadPool.QueueUserWorkItem(
				Reader,
				new HandoverObject() {
					queque = files,
					targets = duplicates,
					results = hashedFiles
				}
			);
			for (short n = 0; n < Config.HASHTHREADS; n++, running++)
				ThreadPool.QueueUserWorkItem(
					HashAsync,
					new HandoverObject() {
						queque = files,
						targets = duplicates,
						results = hashedFiles
					}
				);
			while (running > 0) {
				PBManager.UpdateCurrentPosition(position);
				await Task.Delay(100);
			}
			Debug.WriteLine("Computed:" + hashedFiles.Count + " Hashes!");
			running = 1;
			PBManager.MoveToNextAction(duplicates.Count);

			ThreadPool.QueueUserWorkItem(Sortall, duplicates);
			while (running > 0) {
				PBManager.UpdateCurrentPosition(position);
				await Task.Delay(100);
			}
			Debug.WriteLine("Sorted!");
			PBManager.MoveToNextAction(1);

			finalDuplicates = FinalDuplicates(duplicates);
			Debug.WriteLine("Identified final Duplicates. found: " + finalDuplicates.Count);
			PBManager.MoveToNextAction(1);

			finalDuplicates = CleanupDuplicates(finalDuplicates);
			Debug.WriteLine("Removed non-duplicates. final count: " + finalDuplicates.Count);
			PBManager.MoveToNextAction(finalDuplicates.Count);
			await DisplayDuplicates(finalDuplicates);
			btLink.IsEnabled = true;
		}

		//---------- LOGIC FOR UI ----------
		public MainWindow() {
			InitializeComponent();
			duplicatesListView.ItemsSource = duplicates_view;
		}
		/// <summary>
		/// Event für <see cref="btAnalyze"/>
		/// </summary>
		/// <param name="sender"><see cref="btAnalyze"/></param>
		private async void BtAnalyze_Click(object sender, RoutedEventArgs e) {
			btLink.IsEnabled = false;
			tbPath.IsReadOnly = true;
			btAnalyze.IsEnabled = false;
			duplicates_view.Clear();
			await Analyzer(tbPath.Text);
			btAnalyze.IsEnabled = true;
			tbPath.IsReadOnly = false;
		}
		/// <summary>
		/// Event für <see cref="btLink"/>
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void BtLink_Click(object sender, RoutedEventArgs e) {
			btLink.IsEnabled = false;
			btAnalyze.IsEnabled = false;
			pbStatus.Value = 0;
			pbStatus.Maximum = finalDuplicates.Count - 1;
			await LinkAllDuplicates(finalDuplicates);
			duplicates_view.Clear();
			btAnalyze.IsEnabled = true;
		}
	}
}
