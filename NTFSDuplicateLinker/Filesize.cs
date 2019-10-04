﻿using System;

namespace NTFSDuplicateLinker {

	public class Filesize : IComparable {
		private static readonly string[] Sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
		private string rendered;
		public long Mysize { get; }
		public Filesize(long bytes) {
			Mysize = bytes;
		}
		public int CompareTo(object obj) {
			if (!(obj is Filesize))
				return obj is IComparable ? (obj as IComparable).CompareTo(this) : 0;
			var tmp = (Filesize)obj;
			return tmp.Mysize > Mysize ? -1 : (tmp.Mysize < Mysize ? 1 : 0);
		}
		public override string ToString() {
			if (rendered != null)
				return rendered;
			int idx = 0;
			long tmp = Mysize;
			while (idx < Sizes.Length) {
				if (tmp < 10000) {
					rendered = tmp + " " + Sizes[idx];
					return rendered;
				}
				tmp /= 1000;
				idx++;
			}
			rendered = tmp + " " + Sizes[idx];
			return rendered;
		}
	}
	public class SortFZ : System.Collections.Generic.IComparer<Filesize> {
		private readonly bool _direction;
		public SortFZ(bool direction) {
			_direction = direction;
		}
		public int Compare(Filesize x, Filesize y) {
			return (_direction ? 1 : -1) * x.CompareTo(y);
		}
	}
}