using System.Collections.Generic;

namespace NTFSDuplicateLinker {
	/// <summary>
	/// Datastructure for theads to share memory
	/// </summary>
	struct HandoverObject {
		public Queue<NormalFile> queue;
		public List<DuplicateFile> targets;
		public HandoverObject(Queue<NormalFile> queue, List<DuplicateFile> targets) {
			this.queue = queue;
			this.targets = targets;
		}
	}
}
