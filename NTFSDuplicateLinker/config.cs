
using System;
namespace NTFSDuplicateLinker
{
   static class Config
    {
		/// <summary>
		/// Maximum memory usage in bytes the program will try to sustain
		/// <para>
		/// (May be exceeded for &lt;<see cref="MEMORYMONITORDELAY"/>)
		/// </para>
		/// Causes <see cref="MainWindow.HashAsync(object)"/> to panic when hit
		/// <para>
		/// default: 2GB (2.000.000.000L)
		/// </para>
		/// Higher memory -> higher loading speed
		/// </summary>
		public static readonly long MAXMEMORYUSAGE=2000000000L;
		/// <summary>
		///		Memory usage must be under this value to stop <see cref="MainWindow.HashAsync(object)"/> from panicking
		/// <para>
		///		default: 400MB (400.000.000L)
		/// </para>
		/// </summary>
		public static readonly long MINMEMORYUSAGE = 400000000L;
		/// <summary>
		/// If this value is exceeded <see cref="MainWindow.HashAsync(object)"/> will decrease Hashing delays
		/// <para>
		///		default: 1GB (1.000.000.000L)
		/// </para>
		/// Allows for a more responsive UI and fewer lock Instructions while loading and memory is below <see cref="OKMEMORYUSAGE"/>
		/// </summary>
		public static readonly long OKMEMORYUSAGE = 1000000000L;		
		/// <summary>
		/// Polling frequency of <see cref="MainWindow.MemoryMonitor(object)"/>
		/// <para>
		///		Lower values might slow down the <see cref="GC"/>
		///		Which might hurt performance significantly
		/// </para>
		/// default: 250ms (250)
		/// </summary>
		public static readonly int MEMORYMONITORDELAY = 250;
		/// <summary>
		/// Hashing interval when <see cref="OKMEMORYUSAGE"/> is not exceeded 
		/// nor <see cref="MainWindow.HashAsync(object)"/> is panicking
		/// <para>
		///		default: 100ms (100)
		/// </para>
		/// </summary>
		public static readonly int HASHASYNCREGDELAY = 100;
		/// <summary>
		/// Hashing interval when <see cref="OKMEMORYUSAGE"/> is exceeded 
		/// but <see cref="MainWindow.HashAsync(object)"/> isn't panicking yet
		/// <para>
		///		default: 10ms (10)
		/// </para>
		/// </summary>
		public static readonly int HASHASYNCNOTOKDELAY = 10;
		/// <summary>
		/// Largest size for a file to still be hashed Async.
		/// Larger files will be hashed on the fly (VERY SLOW)
		/// <para>
		///		default: 1GB (1.000.000.000L)
		/// </para>
		/// </summary>
		public static readonly long MAXIMUMASYNCFILESIZE = 1000000000L;
		/// <summary>
		/// Number of hashing threads
		/// <para>
		///		The increased amount of lock instructions may slow down <see cref="MainWindow.Reader(object)"/>
		/// </para>
		///	default: 4 threads (4)
		/// </summary>
		public static readonly short HASHTHREADS = 4;
	}
}
