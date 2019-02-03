using System.Collections.Generic;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Datastructure for theads to share memory
	/// </summary>
	struct HandoverObject {
		public Dictionary<int, byte[]> results;
		public Queue<WorkFile> queque;
		public List<DuplicateFile> targets;
	}
}
