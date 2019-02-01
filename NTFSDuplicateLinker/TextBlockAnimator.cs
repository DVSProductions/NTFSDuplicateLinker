using System.Threading.Tasks;
using System.Windows.Controls;

namespace NTFSDuplicateLinker {
	class TextBlockAnimator {
		private TextBlock tb;
		private readonly string orig;
		bool run;
		private static string Reverse(string inp) {
			var ret = inp.ToCharArray();
			for (int ln = inp.Length - 1, n = ln; n >= 0; n--)
				ret[n - ln] = inp[n];
			return new string(ret);
		}
		private readonly string animationFramesL = "|/-\\";
		private readonly string animationFramesR = "|\\-/";
		public TextBlockAnimator(TextBlock textBlock) {
			tb = textBlock;
			orig = textBlock.Text;
		}
		public TextBlockAnimator(TextBlock textBlock, string customAnimation) {
			animationFramesL = customAnimation;
			animationFramesR = Reverse(customAnimation);
			tb = textBlock;
			orig = textBlock.Text;
		}
		/// <summary>
		/// Creates a Animation with two different animations
		/// </summary>
		/// <param name="textBlock"></param>
		/// <param name="customAnimation">Left Frames</param>
		/// <param name="frames2">Right frames. must have same length as <paramref name="customAnimation"/></param>
		/// <exception cref="System.ArgumentOutOfRangeException">When lengths are unequal</exception>
		public TextBlockAnimator(TextBlock textBlock, string customAnimation, string frames2) {
			animationFramesL = customAnimation;
			if (frames2.Length != customAnimation.Length)
				throw new System.ArgumentOutOfRangeException("frames2", "Animation frame length unequal: " + customAnimation.Length + " != " + frames2.Length);
			animationFramesR = frames2;
			tb = textBlock;
			orig = textBlock.Text;
		}

		public async void Start(string newMessage) {
			run = true;
			int idx = 0;
			int len = animationFramesL.Length;
			while (run) {
				if (++idx >= len)
					idx = 0;
				int ipos = len - idx - 1;
				string print = " " + animationFramesL[idx] + newMessage + animationFramesR[idx];
				tb.Text = print;
				await Task.Delay(1000 / (len * 2));
			}
		}
		public void Stop() {
			run = false;
			tb.Text = orig;
		}
	}
}
