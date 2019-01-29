using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NTFSDuplicateLinker {
	public partial class MainWindow : Window {
		/// <summary>
		/// Filepath and hash
		/// </summary>
		Dictionary<string, byte[]> hashedFiles;
		public MainWindow() {
			InitializeComponent();
		}
		
		/// <summary>
		/// true while Reader is running
		/// </summary>
		bool stillLoading = false;
		
		
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
		/// Monitors memory usage while running > 0
		/// </summary>
		/// <param name="nul">noting</param>
		void MemoryMonitor(object nul) {
			while (running > 0) {
				usedMemory = GC.GetTotalMemory(false);
				Task.Delay(250).Wait();
			}
		}
		/// <summary>
		/// Looks for Files in the Queue&lt;<see cref="WorkFile"/>&gt; and starts hashing them
		/// </summary>
		/// <param name="obj"><see cref="HandoverObject"/> containing the required references</param>
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
		///		Create a <see cref="Expander"/> containing this <see cref="DuplicateFile"/>
		/// </summary>
		/// <param name="df"><see cref="DuplicateFile"/> to display</param>
		/// <param name="bg">Background color of the created <see cref="Expander"/></param>
		/// <returns>A expander visualizing the Duplicate</returns>
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
		/// <summary>
		/// Show all Duplicates in <see cref="MainWindow.spItems"/>
		/// </summary>
		/// <param name="ldf">All duplicates</param>
		/// <returns></returns>
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
		/// <summary>
		/// <see cref="List{}"/> containing every identified <see cref="DuplicateFile"/>
		/// </summary>
		List<DuplicateFile> finalDuplicates;
		/// <summary>
		/// Event für <see cref="btAnalyze"/>
		/// </summary>
		/// <param name="sender"><see cref="btAnalyze"/></param>
		private async void Button_Click(object sender, RoutedEventArgs e) {
			btLink.IsEnabled = false;
			var dir = tbPath.Text;
			spItems.Children.Clear();
			if (!Util.IsOnNTFS(dir))
				return;
			var files = new Queue<WorkFile>();
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
			spItems.Children.Clear();
			btAnalyze.IsEnabled = true;
		}
	}
}
