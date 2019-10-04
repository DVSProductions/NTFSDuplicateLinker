using System;
using System.Threading;
using System.Threading.Tasks;

namespace NTFSDuplicateLinker {
	class ThreadWithResult<TResult,TPara> {
		Thread t;
		public ThreadWithResult(Func<TPara,TResult> method) => t = new Thread((p) => Result = method((TPara) p));
		public ThreadWithResult<TResult, TPara> Start(TPara parameter) { t.Start(parameter); return this; }
		public async Task<ThreadWithResult<TResult, TPara>> WaitTillCompletion(int sleepDelay = 100) {
			while(t.IsAlive) await Task.Delay(sleepDelay);
			return this;
		}
		public ThreadWithResult<TResult, TPara> Join() {
			t.Join();
			return this;
		}
		public ThreadWithResult<TResult, TPara> Abort() {
			t.Abort();
			return this;
		}
		public bool IsAlive => t.IsAlive;
		public TResult Result { get; private set; }
	}
	class ThreadWithResult : ThreadWithResult<object,object> {
		public ThreadWithResult(Func<object,object> m):base(m) {
			
		}
	}
}
