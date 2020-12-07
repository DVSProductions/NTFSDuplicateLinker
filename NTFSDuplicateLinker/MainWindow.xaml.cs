#define UseThreadPool
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
		long position = 0;
		/// <summary>
		/// Program memory usage in bits
		/// </summary>
		long usedMemory = 0;
		Folder fullstructure;
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
			fullstructure = new Folder(currPath, null);
			var dirs = new Queue<Folder>();
			dirs.Enqueue(fullstructure);
			while(dirs.Count > 0) {
				var work = dirs.Dequeue();
				try {
					foreach(var d in Directory.EnumerateDirectories(work.Path))
						if(!Util.AnalyzePathForJunctions(d)) {
							var f = new Folder(Path.GetFileName(d), work);//seems wrong but is correct
																		  //work.Directories.Add(f);
							dirs.Enqueue(f);
						}

					foreach(var fp in Directory.EnumerateFiles(work.Path)) {
						var nf = new NormalFile(Path.GetFileName(fp), work);
						ret.Add(nf);
						//work.Files.Add(nf);
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
			var p = Process.GetCurrentProcess();
			while(stillLoading) {
				p.Refresh();
				usedMemory = p.WorkingSet64; //GC.GetTotalMemory(false);
				Thread.Sleep(Config.MEMORYMONITORDELAY);
			}
			usedMemory = 0;
		}
		static int hashedFiles;
		
#if UseThreadPool
		void WorkerThreadHasher(object target) {
			try {
				if(target is NormalFile work && work.Hashstate == Hashstate.queued) {
					work.Hashstate = Hashstate.hashing;
					using(var hasher = MD5.Create()) {
						work.hash = hasher.ComputeHash(work.data);
						work.data = null;
					}
					work.Hashstate = Hashstate.done;
				}
			}
			catch { }
			//lock()
			hashedFiles++;
		}
#else
		/// <summary>
		/// Looks for Files in the Queue&lt;<see cref="WorkFile"/>&gt; and starts hashing them
		/// </summary>
		/// <param name="obj"><see cref="HandoverObject"/> containing the required references</param>
		int HashAsync(HandoverObject access) {
			using(var hasher = MD5.Create()) {
				var panic = false;
				while(stillLoading || access.queue.Count > 0) {
					if(access.queue.Count > 0) {
						NormalFile work = null;
						lock(access.queue)
							if(access.queue.Count == 0)
								Thread.Sleep(10);
							else
								work = access.queue.Dequeue();
						if(work != null && work.data != null) {
							work.hash = hasher.ComputeHash(work.data);
							work.data = null;
							hashedFiles++;
						}
						if(!panic) {
							if(usedMemory > Config.MAXMEMORYUSAGE)
								panic = true;
							else if(stillLoading)
								Thread.Sleep(usedMemory < Config.OKMEMORYUSAGE ? Config.HASHASYNCREGDELAY : Config.HASHASYNCNOTOKDELAY);
						}
						else if(usedMemory < Config.MINMEMORYUSAGE)
							panic = false;
					}
					else {
						GC.Collect();
						Thread.Sleep(Config.HASHASYNCREGDELAY);
					}
				}
			}
			running--;
			return 0;
		}
#endif
		/// <summary>
		/// Hashes very large files on the fly.
		/// MUCH SLOWER than async hashing!
		/// </summary>
		/// <param name="file">Path of the file to hash</param>
		/// <param name="lst"><see cref="Dictionary{string,byte[]}"/> reference to all hashes</param>
		static bool HashSync(NormalFile file) {
			if(file.Hashstate == Hashstate.hashing) return false;
			file.Hashstate = Hashstate.hashing;
			using(var hasher = MD5.Create()) {
				using(var fs = File.OpenRead(file.Path)) {
					if(fs.Length == 0) {
						fs.Close();
						file.Hashstate = Hashstate.nonexsistant;
						return false;
					}
					file.hash = hasher.ComputeHash(fs);
					//hashedFiles++;
					file.Hashstate = Hashstate.done;
					return true;
				}
			}
		}
		long queued;
		/// <summary>
		/// Loads all identified duplicates into Memory for hashing by <see cref="HashAsync(object)"/>
		/// </summary>
		/// <param name="obj"><see cref="HandoverObject"/> containing the required references</param>
		int Reader(HandoverObject access) {
			queued = 0;
			foreach(var dup in access.targets) {
				position++;
				foreach(var file in dup.instances) {
					if(file.Hashstate != Hashstate.nonexsistant) continue;
					file.Hashstate = Hashstate.queued;
					var path = file.Path;
					//check for huge size
					if(file.Size > Config.MAXIMUMASYNCFILESIZE) {
						//if(HashSync(file)) queued++;
						HashSync(file);
					}
					else {
						try {
							file.data = File.ReadAllBytes(path);
						}
						catch { }
#if UseThreadPool
						queued++;
						ThreadPool.QueueUserWorkItem(WorkerThreadHasher, file);
#else
						if(file.data != null && file.data.LongLength != 0)
							lock(access.queue)
								access.queue.Enqueue(file);
#endif
					}

				}
			}
			stillLoading = false;
			while(hashedFiles <1113) {
				Thread.Sleep(100);
			}
			running = 0;
			return 0;
		}
		List<DuplicateFile> FindDuplicatesT(int offset, List<NormalFile> files) {
			running++;
			var results = new List<DuplicateFile>();
			for(int i = offset, stepsize = Environment.ProcessorCount; i < files.Count; i += stepsize, position++) {
				var work = new DuplicateFile(files[i]);
				for(var n = i + 1; n < files.Count; n++)
					if(work.filename == files[n].filename)
						work.instances.Add(files[n]);
				if(work.instances.Count > 1)
					results.Add(work);
			}
			return results;
		}
		/// <summary>
		/// Analyze all normal files and find duplicate filenames
		/// </summary>
		/// <param name="transfer">
		///		Tuple&lt;List&lt; <see cref="DuplicateFile"/>&gt;, List&lt; <see cref="NormalFile"/>&gt;&gt;
		///		reference for searching and output
		/// </param>
		void FindDuplicates(object transfer) {
			var trans = (Tuple<List<DuplicateFile>, List<NormalFile>>) transfer;
			var threads = new ThreadWithResult<List<DuplicateFile>, int, List<NormalFile>>[Environment.ProcessorCount];
			for(var i = 0; i < Environment.ProcessorCount; i++, running++)
				threads[i] = new ThreadWithResult<List<DuplicateFile>, int, List<NormalFile>>(FindDuplicatesT).Start(i, trans.Item2);
			foreach(var t in threads) {
				t.SleepTillCompletion();
				trans.Item1.AddRange(t.Result);
				running--;
			}
			running = 0;
		}

		/// <summary>
		/// Sorts all <see cref="DuplicateFile.instances"/> of all <see cref="DuplicateFile"/>s using <see cref="DuplicateFile.Sort()"/>
		/// </summary>
		/// <param name="obj">List&lt;<see cref="DuplicateFile"/>&gt;</param>
		void Sortall(object obj) {
			var a = new int[Environment.ProcessorCount];
			void SortThread(object o) {
				var para = (ValueTuple<List<DuplicateFile>, int>) o;
				var status = 0;
				for(var d = para.Item2; d < para.Item1.Count; d++) {
					a[para.Item2] = status;
					para.Item1[d].Sort();
					status++;
				}
				a[para.Item2] = status;
				lock(this)
					running--;
			}
			var dups = (List<DuplicateFile>) obj;
			position = 0;
			var arr = new Thread[Environment.ProcessorCount];
			for(var n = 0; n < Environment.ProcessorCount; n++) {
				arr[n] = new Thread(SortThread);
				arr[n].Start((dups, n));
				lock(this)
					running++;
			}
			while(running != 1) {
				Thread.Sleep(100);
				long results = 0;
				foreach(var i in a) results += i;
				position = results;
			}
			foreach(var t in arr) t.Join();
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
				var options = new List<DuplicateFile>() { new DuplicateFile(dup.instances[0]) };
				var tmp = options[0].instances[0].hash;
				var hashes = new List<byte[]> { tmp };
				for(var n = 1; n < dup.instances.Count; n++) {
					var inst = dup.instances[n];
					var found = false;
					for(var i = 0; i < options.Count; i++) {
						if(Util.CompareByteArray(hashes[i], inst.hash)) {
							found = true;
							options[i].instances.Add(inst);
						}
					}
					if(!found) {
						options.Add(new DuplicateFile(dup.instances[n]));
						hashes.Add(inst.hash);
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
			var instanceSize = (ulong) file.instances[0].Size;
			UInt64 saved = 0;
			file.instances.Sort();
			var newExtension = ".DVSLINKER.BAK";
			var prefix = @"\\?\";//allow unicode
			var orig = prefix + file.instances[0].Path;
			var links = Util.GetLinks(file);
			for(var n = 1; n < file.instances.Count; n++, links++) {
				var curr = file.instances[n].Path;
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
					if(false) {
						//if(!Syscall.CreateHardLink(prefix + curr, orig, IntPtr.Zero)) {
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
				df.finalhash = df.instances[0].hash;
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
				cp.Number = string.Format("{0:n0}", position); ;
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
			if(Util.isPathOK(path) != Chk.ok)
				return;
			var fileQueue = new Queue<NormalFile>();
			var files = new List<NormalFile>();
			var duplicates = new List<DuplicateFile>();
			async Task whileRunning(Action additional = null) {
				while(running > 0) {
					await Task.Delay(50);
					PBManager.UpdateCurrentPosition(position);
					additional?.Invoke();
				}
			}
			var cp = (CurrentPage as StatusPage);
			void reset(string sub, long v = 1) {
				position = 0;
				running = 1;
				PBManager.Reset(1, (ulong) v);
				cp.Subtitle = sub;
			}
			position = 0;
			running = 1;
			cp.Subtitle = "Files Discovered";
			Debug.WriteLine("Scanning...");
			ThreadPool.QueueUserWorkItem(Discover, new Tuple<string, List<NormalFile>>(path, files));
			PositionWatcher();
			while(running > 0) await Task.Delay(100);

			reset("Files Analyzed", files.Count);
			Debug.WriteLine("Identifying duplicates...");
			ThreadPool.QueueUserWorkItem(
				FindDuplicates,
				new Tuple<List<DuplicateFile>, List<NormalFile>>(duplicates, files));
			await whileRunning();
			files.Clear();
			Debug.WriteLine("Found:" + duplicates.Count + " duplicates in " + files.Count + " Files");

			reset("Files Hashed", duplicates.Count);
			stillLoading = true;
			new Thread(MemoryMonitor).Start();
			new ThreadWithResult<int, HandoverObject>(Reader).Start(new HandoverObject(fileQueue, duplicates));
#if UseThreadPool
#else
				for(short n = 1; n < Environment.ProcessorCount; n++, running++)
				new ThreadWithResult<int, HandoverObject>(HashAsync).Start(new HandoverObject(fileQueue, duplicates));
#endif
			await whileRunning(() => Title = string.Format("{0:n0} vs {1:n0}   Remaining: {2:n0}", hashedFiles, queued,queued-hashedFiles));
			Debug.WriteLine("Computed:" + hashedFiles + " Hashes!");

			await Task.Delay(100);
			reset("Files Sorted", duplicates.Count);
			ThreadPool.QueueUserWorkItem(Sortall, duplicates);
			await whileRunning();
			Debug.WriteLine("Possible Duplicates Sorted!");

			reset("Rough-Matched Duplicates", duplicates.Count);
			finalDuplicates = (await new ThreadWithResult<List<DuplicateFile>, List<DuplicateFile>>(FinalDuplicates).Start(duplicates).WaitTillCompletion()).Result;
			duplicates.Clear();
			Debug.WriteLine("Identified final Duplicates. found: " + finalDuplicates.Count);

			reset("Validated Results", finalDuplicates.Count);
			finalDuplicates = (await new ThreadWithResult<List<DuplicateFile>, List<DuplicateFile>>(CleanupDuplicates).Start(finalDuplicates).WaitTillCompletion()).Result;
			Debug.WriteLine("Removed Lonely Files. final count: " + finalDuplicates.Count);


			//lbSBDuplicates.Content = finalDuplicates.Count;
			watchPosition = false;
			await Task.Delay(33);
			reset("", finalDuplicates.Count);
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
				var result = Util.isPathOK(path);
				switch(result) {
					case Chk.empty:
						MessageBox.Show("Please enter a Path");
						break;
					case Chk.gone:
						MessageBox.Show("Directory not found");
						break;
					case Chk.notNTFS:
						MessageBox.Show("The Directory is not on a NTFS Drive. File linking is only avaliable on NTFS", "Invalid Filesystem");
						break;
					case Chk.ok:
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


