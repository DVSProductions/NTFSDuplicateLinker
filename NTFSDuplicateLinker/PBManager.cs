namespace NTFSDuplicateLinker {
	public partial class MainWindow {
		static class PBManager {
			static System.Windows.Controls.ProgressBar bar;
			static ulong currentTarget = 0;
			static ulong currentState = 0;
			static ulong currentAction = 0;
			static ulong totalActions = 0;
			public static void Init(System.Windows.Controls.ProgressBar pb, ulong Actions, ulong currentMaximum) {
				bar = pb;
				bar.Value = 0;
				bar.Maximum = 1;
				Reset(Actions, currentMaximum);
			}
			public static void Reset(ulong Actions, ulong currentMaximum) {
				currentTarget = currentMaximum;
				currentState = 0;
				currentAction = 0;
				totalActions = Actions;
				bar.Value = 0;
				Render();
			}
			public static void MoveToNextAction(long newTarget) =>
				MoveToNextAction((ulong)newTarget);
			public static void MoveToNextAction(ulong newTarget) {
				currentTarget = newTarget;
				currentState = 0;
				currentAction++;
			}
			public static void UpdateCurrentPosition(ulong newPosition) {
				currentState = newPosition;
				Render();
			}
			public static void UpdateCurrentPosition(long newPosition) => UpdateCurrentPosition((ulong)newPosition);
			public static void Render() {
				if (currentTarget != 0) {
					var currentValue = ((currentState * 1000) / currentTarget) / 1000.0d;
					currentValue *= (double)currentAction / (double)totalActions;
					currentValue += ((currentAction * 100) / totalActions) / 100.0d;
					bar.Value = currentValue;
					Debug.WriteLine(currentState + " / " + currentTarget + " = " + currentValue);
				}
			}
		}
}
