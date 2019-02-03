namespace NTFSDuplicateLinker {
	/// <summary>
	/// Structure to contain a path and the file contents
	/// </summary>
	struct WorkFile {
		/// <summary>
		/// File content
		/// </summary>
		public byte[] data;
		/// <summary>
		/// ID to path in <see cref="MainWindow.pathStorage"/>
		/// </summary>
		public int pathID;
	}
}
