using System.Collections.Generic;

namespace NTFSDuplicateLinker {
	 public abstract class Filter {
		public abstract string Name { get; }
		public abstract bool Validate(DuplicateFile df);
	}
	class TestFilter : Filter {
		
		public override string Name => "Testfilter";

		public override bool Validate(DuplicateFile df) {
			
			return true;
		}
	}
}
