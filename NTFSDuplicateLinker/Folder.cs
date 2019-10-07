using System.Collections.Generic;

namespace NTFSDuplicateLinker {
	public class Folder {
		public Folder parent;
		public string name;
		//public List<Folder> Directories;
		//public List<NormalFile> Files;
		public Folder(string name, Folder parent) {
			this.parent = parent;
			this.name = name;
		//	Directories = new List<Folder>();
		//	Files = new List<NormalFile>();
		}
		public string Path => parent?.Path + name+"\\";
	}
}
