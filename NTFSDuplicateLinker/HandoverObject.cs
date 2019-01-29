using System.Collections.Generic;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Datastructure for theads to share memory
	/// </summary>
	struct HandoverObject {
		public Dictionary<string, byte[]> results;
		public Queue<WorkFile> queque;
		public List<DuplicateFile> targets;
	}
}
