using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NTFSDuplicateLinker {
	public partial class MainWindow : Window {
		public static Action<bool> checkedCallback;
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
		/// stores all identified paths
		/// </summary>
		public static List<string> pathStorage;
		/// <summary>
		/// stores filesizes
		/// </summary>
		public static List<long> sizeStorage;
		/// <summary>
		/// FilepathID and hash
		/// </summary>
		Dictionary<int, byte[]> hashedFiles;
		/// <summary>
		/// <see cref="List{}"/> containing every identified <see cref="DuplicateFile"/>
		/// </summary>
		List<DuplicateFile> finalDuplicates;

		//view model

		ObservableCollection<DuplicateFile> duplicates_view = new ObservableCollection<DuplicateFile>();


		/// <summary>
		/// Independent Thread
		/// Searches for files in a given folder structure
		/// </summary>
		/// <param name="obj">Tuple<string, List<NormalFile>> Object containing </param>
		void Discover(object obj) {
			string currPath;
			List<NormalFile> ret;
			{
				var conv = (Tuple<string, List<NormalFile>>) obj;
				currPath = conv.Item1;
				ret = conv.Item2;
			}
			var dirs = new Queue<string>();
			dirs.Enqueue(currPath);
			var idx = 0;
			while(dirs.Count > 0) {
				var work = dirs.Dequeue();
				try {
					foreach(var d in Directory.EnumerateDirectories(work))
						if(!Util.AnalyzePathForJunctions(d))
							dirs.Enqueue(d);

					foreach(var fp in Directory.EnumerateFiles(work)) {
						pathStorage.Add(fp);
						ret.Add(new NormalFile(fp, idx++));
						position++;
					}
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
			while(running > 1) {
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
			var access = (HandoverObject) obj;
			using(var hasher = MD5.Create()) {
				var panic = false;
				while(stillLoading || access.queque.Count > 0) {
					if(access.queque.Count > 0) {
						var work = new WorkFile() { pathID = -1 };
						lock(access.queque) {
							if(access.queque.Count > 0)
								work = access.queque.Dequeue();
						}
						if(work.pathID != -1) {
							var dat = hasher.ComputeHash(work.data);
							lock(access.results)
								if(!access.results.ContainsKey(work.pathID))
									access.results.Add(work.pathID, dat);
						}
						if(!panic) {
							if(usedMemory > Config.MAXMEMORYUSAGE)
								panic = true;
							else if(usedMemory < Config.OKMEMORYUSAGE && stillLoading)
								Task.Delay(Config.HASHASYNCREGDELAY).Wait();
							else if(stillLoading)
								Task.Delay(Config.HASHASYNCNOTOKDELAY).Wait();
						}
						else if(usedMemory < Config.MINMEMORYUSAGE)
							panic = false;
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
		static void HashSync(string file, int id, Dictionary<int, byte[]> lst) {
			using(var hasher = MD5.Create()) {
				using(var fs = File.OpenRead(file)) {
					if(fs.Length == 0) {
						fs.Close();
						return;
					}
					var hash = hasher.ComputeHash(fs);
					lock(lst) {
						if(!lst.ContainsKey(id))
							lst.Add(id, hash);
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
			var access = (HandoverObject) obj;
			foreach(var dup in access.targets) {
				position++;
				foreach(var file in dup.instances) {
					var path = pathStorage[file];
					//check for huge size
					var fi = new FileInfo(path);
					sizeStorage.Add(fi.Length);
					if(fi.Length > Config.MAXIMUMASYNCFILESIZE) {
						HashSync(path, file, access.results);
					}
					else {
						WorkFile wf;
						try {
							wf = new WorkFile() {
								data = File.ReadAllBytes(path),
								pathID = file
							};
						}
						catch {
							wf = new WorkFile();
						}
						if(wf.data != null && wf.data.LongLength != 0) {
							lock(access.queque) {
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
			var trans = (Tuple<List<DuplicateFile>, List<NormalFile>>) transfer;
			var filePaths = trans.Item2;
			for(var i = 0; i < filePaths.Count; i++, position++) {
				var work = new DuplicateFile(filePaths[i]);
				for(var n = i + 1; n < filePaths.Count; n++)
					if(work.filename == filePaths[n].filename)
						work.instances.Add(filePaths[n].fullpathID);
				if(work.instances.Count > 1)
					trans.Item1.Add(work);
			}
			running = 0;
		}
		/// <summary>
		/// Sorts all <see cref="DuplicateFile.instances"/> of all <see cref="DuplicateFile"/>s using <see cref="DuplicateFile.Sort()"/>
		/// </summary>
		/// <param name="obj">List&lt;<see cref="DuplicateFile"/>&gt;</param>
		void Sortall(object obj) {
			var dups = (List<DuplicateFile>) obj;
			position = 0;
			foreach(var dup in dups) {
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
			position = 0;
			var ret = new List<DuplicateFile>();
			foreach(var dup in duplicates) {
				var options = new List<DuplicateFile>() {
					new DuplicateFile(
						new NormalFile(
							pathStorage[dup.instances[0]],
							dup.instances[0]
						)
					)
				};
				hashedFiles.TryGetValue(options[0].instances[0], out var tmp);
				var hashes = new List<byte[]> { tmp };
				for(var n = 1; n < dup.instances.Count; n++) {
					var inst = dup.instances[n];
					hashedFiles.TryGetValue(inst, out var myhash);
					var found = false;
					for(var i = 0; i < options.Count; i++) {
						if(Util.CompareByteArray(hashes[i], myhash)) {
							found = true;
							options[i].instances.Add(inst);
						}
					}
					if(!found) {
						options.Add(
							new DuplicateFile(
								new NormalFile(
									pathStorage[dup.instances[n]],
									dup.instances[n]
								)
							)
						);
						hashes.Add(myhash);
						position++;
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
		List<DuplicateFile> CleanupDuplicates(List<DuplicateFile> ldf) {
			var ret = new List<DuplicateFile>();
			position = 0;
			foreach(var d in ldf)
				if(d.instances.Count > 1) {
					ret.Add(d);
					position++;
				}
			return ret;
		}
		/// <summary>
		/// Do the linking of one file
		/// Ensures that duplicate link limits are not exceeded
		/// </summary>
		/// <param name="file">The DuplicateFile to link into one</param>
		static UInt64 LinkDuplicates(DuplicateFile file) {
			UInt64 instanceSize;
			try {
				instanceSize = (ulong) new FileInfo(pathStorage[file.instances[0]]).Length;
			}
			catch {
				return 0;
			}
			UInt64 saved = 0;
			file.instances.Sort();
			var newExtension = ".DVSLINKER.BAK";
			var prefix = @"\\?\";//allow unicode
			var orig = prefix + pathStorage[file.instances[0]];
			var links = Util.GetLinks(file);
			for(var n = 1; n < file.instances.Count; n++, links++) {
				var curr = pathStorage[file.instances[n]];
				if(links >= 1023) {
					orig = prefix + curr;
					links = Util.GetLinks(curr) - 1;//for loops adds 1 back on
				}
				else {
					var backup = curr + newExtension;
					try {
						if(File.Exists(backup))
							File.Delete(backup);
						File.Copy(curr, backup);//backup
						File.Delete(curr);
					}
					catch {
						if(!File.Exists(curr)) {
							if(File.Exists(backup))
								File.Copy(backup, curr);
						}
						if(File.Exists(backup)) {
							File.SetAttributes(backup, FileAttributes.Normal);
							File.Delete(backup);
						}
						continue;
					}
					saved++;
					if(!Syscall.CreateHardLink(prefix + curr, orig, IntPtr.Zero)) {
						var error = Syscall.GetLastError();//Marshal.GetLastWin32Error();
						Debug.WriteLine("ERROR: " + error);
						File.Copy(backup, curr);//restore backup
						File.Delete(backup);
					}
					else {
						File.Delete(backup);//remove backup because HardLink worked
					}
				}
			}
			return instanceSize * saved;
		}
		/// <summary>
		/// Wrapper to link everything up
		/// </summary>
		/// <param name="duplicates">List of all duplicates to link</param>
		/// <returns></returns>
		UInt64 LinkAllDuplicates(List<DuplicateFile> duplicates) {
			UInt64 totalSavings = 0;
			for(long n = 0; n < duplicates.Count; n++) {
				if(duplicates[(int) n].Link == false)
					continue;
				totalSavings += LinkDuplicates(duplicates[(int) n]);
				position++;
			}
			return totalSavings;
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
		/// Show all Duplicates in <see cref="MainWindow.duplicatesDataGrid"/>
		/// </summary>
		/// <param name="ldf">All duplicates</param>
		/// <returns></returns>
		async Task DisplayDuplicates(List<DuplicateFile> ldf) {
			var switcher = true;
			position = 0;
			var s = Stopwatch.StartNew();
			foreach(var df in ldf) {
				position++;
				switcher = !switcher;
				hashedFiles.TryGetValue(df.instances[0], out var myhash);
				df.finalhash = myhash;
				duplicates_view.Add(df);
				if(s.ElapsedMilliseconds / 1000 >= 1) {
					s.Restart();
					PBManager.UpdateCurrentPosition(position);
					await Task.Delay(100);
				}
			}
			PBManager.UpdateCurrentPosition(position);
		}
		bool watchPosition = false;
		async void PositionWatcher(StatusPage cp) {
			watchPosition = true;
			while(watchPosition) {
				cp.Number = position.ToString();
				await Task.Delay(66);
			}
		}
		void PositionWatcher() => PositionWatcher(CurrentPage as StatusPage);
		//void PaintTB(bool error) {
		//	tbPath.BorderBrush = error ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);
		//}

		/// <summary>
		/// Launches all hashing and analyzing logic
		/// </summary>
		/// <param name="path">Path to analyze. Does not have to be null checked</param>
		/// <returns></returns>
		async Task Analyzer(string path) {
			if(
				string.IsNullOrWhiteSpace(path) ||
				!Directory.Exists(path) ||
				!Util.IsOnNTFS(path)
				) {
				return;
			}
			pathStorage = new List<string>();
			sizeStorage = new List<long>();
			var files = new Queue<WorkFile>();
			var filePaths = new List<NormalFile>();
			var duplicates = new List<DuplicateFile>();
			hashedFiles = new Dictionary<int, byte[]>();
			async Task whileRunning(Action additional = null) {
				while(running > 0) {
					await Task.Delay(100);
					PBManager.UpdateCurrentPosition(position);
					additional?.Invoke();
				}
			}


			running = 1;
			Debug.WriteLine("Scanning...");
			ThreadPool.QueueUserWorkItem(Discover, new Tuple<string, List<NormalFile>>(path, filePaths));
			var cp = (CurrentPage as StatusPage);
			position = 0;
			cp.Subtitle = "Files Discovered";
			PositionWatcher();
			await whileRunning();

			PBManager.Reset(1, (ulong) filePaths.Count);
			running = 1;
			Debug.WriteLine("Identifying duplicates...");
			ThreadPool.QueueUserWorkItem(
				FindDuplicates,
				new Tuple<List<DuplicateFile>, List<NormalFile>>(duplicates, filePaths));
			position = 0;
			cp.Subtitle = "Files Analyzed";
			await whileRunning();
			filePaths.Clear();
			Debug.WriteLine("Found:" + duplicates.Count + " duplicates in " + filePaths.Count + " Files");
			PBManager.Reset(1, (ulong) duplicates.Count);

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
			for(short n = 0; n < Config.HASHTHREADS; n++, running++)
				ThreadPool.QueueUserWorkItem(
					HashAsync,
					new HandoverObject() {
						queque = files,
						targets = duplicates,
						results = hashedFiles
					}
				);
			position = 0;
			cp.Subtitle = "Files Hashed";
			await whileRunning();
			Debug.WriteLine("Computed:" + hashedFiles.Count + " Hashes!");
			running = 1;
			PBManager.Reset(1, (ulong) duplicates.Count);
			position = 0;
			cp.Subtitle = "Files Sorted";
			ThreadPool.QueueUserWorkItem(Sortall, duplicates);
			await whileRunning();
			Debug.WriteLine("Possible Duplicates Sorted!");
			PBManager.Reset(1, 1);
			position = 0;
			cp.Subtitle = "Rough-Matched Duplicates";
			finalDuplicates = (await new ThreadWithResult<List<DuplicateFile>, List<DuplicateFile>>(FinalDuplicates).Start(duplicates).WaitTillCompletion()).Result;
			duplicates.Clear();
			Debug.WriteLine("Identified final Duplicates. found: " + finalDuplicates.Count);
			PBManager.Reset(1, 1);
			position = 0;
			cp.Subtitle = "Validated Results";
			finalDuplicates = (await new ThreadWithResult<List<DuplicateFile>, List<DuplicateFile>>(CleanupDuplicates).Start(finalDuplicates).WaitTillCompletion()).Result;
			Debug.WriteLine("Removed Lonely Files. final count: " + finalDuplicates.Count);
			PBManager.Reset(1, (ulong) finalDuplicates.Count);
			//lbSBDuplicates.Content = finalDuplicates.Count;
			watchPosition = false;
			cp.Subtitle = "";
			cp.Number = "Rendering Results";
			await DisplayDuplicates(finalDuplicates);
			//btLink.IsEnabled = true;
			CurrentPage = new ResultPage(duplicates_view);
			btNext.Content = "Link";
			btBack.Content = "Start Over";
			btNext.Visibility = Visibility.Visible;
			btBack.Visibility = Visibility.Visible;
			GC.Collect();
		}

		//---------- LOGIC FOR UI ----------
		Page CurrentPage {
			get => PageViewer.Content as Page;
			set => PageViewer.Content = value;
		}

		public MainWindow() {
			InitializeComponent();
			CurrentPage = new PathPage(() => BtNext_Click(null, null));
		}
		async Task LinkInterface() {
			var sp = new StatusPage();
			CurrentPage = sp;
			sp.Number = "0";
			sp.Subtitle = "Duplicates Linked";
			position = 0;

			PositionWatcher(sp);
			PBManager.Init(sp.pb, 1, (ulong) finalDuplicates.Count);
			var twr = new ThreadWithResult<UInt64, List<DuplicateFile>>(LinkAllDuplicates).Start(finalDuplicates);
			while(twr.IsAlive) {
				await Task.Delay(100);
				PBManager.UpdateCurrentPosition(position);
			}
			twr.Join();
			watchPosition = false;
			CurrentPage = new FinalPage(twr.Result, (ulong) finalDuplicates.Count);
		}
		private async void BtNext_Click(object sender, RoutedEventArgs e) {
			if(CurrentPage is PathPage pp) {
				var path = pp.Path;
				var result = pp.Validate(path);
				switch(result) {
					case PathPage.Chk.empty:
						MessageBox.Show("Please enter a Path");
						break;
					case PathPage.Chk.gone:
						MessageBox.Show("Directory not found");
						break;
					case PathPage.Chk.notNTFS:
						MessageBox.Show("The Directory is not on a NTFS Drive. File linking is only avaliable on NTFS", "Invalid Filesystem");
						break;
					case PathPage.Chk.ok:
						CurrentPage = new StatusPage();
						btNext.Visibility = Visibility.Hidden;
						await Task.Delay(100);
						await Analyzer(path);
						break;
					default:
						break;
				}
			}
			else if(CurrentPage is ResultPage rp) {
				btNext.Visibility = Visibility.Hidden;
				btBack.Visibility = Visibility.Hidden;
				await LinkInterface();
				btNext.Content = "Exit";
				btBack.Content = "Start Over";
				btNext.Visibility = Visibility.Visible;
				btBack.Visibility = Visibility.Visible;
			}
			else if(CurrentPage is FinalPage) {
				this.Close();
				Environment.Exit(0);
			}
		}

		private void BtBack_Click(object sender, RoutedEventArgs e) {
			pathStorage.Clear();
			hashedFiles.Clear();
			finalDuplicates.Clear();
			duplicates_view.Clear();
			GC.Collect();
			while(true) {
				try {
					PageViewer.GoBack();
				}
				catch {
					break;
				}
			}
			btBack.Visibility = Visibility.Hidden;
			btNext.Content = "Next";
		}
	}
}


