using System.Diagnostics;

namespace NTFSDuplicateLinker {
	public partial class MainWindow {
		/// <summary>
		/// Does calculations for the Progressbar
		/// </summary>
		public static class PBManager {
			/// <summary>
			/// current work object
			/// </summary>
			static System.Windows.Controls.ProgressBar bar;
			static ulong currentTarget = 0;
			static ulong currentState = 0;
			static ulong currentAction = 0;
			static ulong totalActions = 0;
			/// <summary>
			/// 
			/// </summary>
			/// <param name="pb">Current Progressbar</param>
			/// <param name="Actions">Total amount of Actions till completion</param>
			/// <param name="currentMaximum">Amount of steps to complete the current Action</param>
			public static void Init(System.Windows.Controls.ProgressBar pb, ulong Actions, ulong currentMaximum) {
				bar = pb;
				bar.Value = 0;
				bar.Maximum = 1;
				Reset(Actions, currentMaximum);
			}
			/// <summary>
			/// Reset everything to 0
			/// </summary>
			/// <param name="Actions">Total amount of Actions till completion</param>
			/// <param name="currentMaximum">Amount of steps to complete the current Action</param>
			public static void Reset(ulong Actions, ulong currentMaximum) {
				currentTarget = currentMaximum;
				currentState = 0;
				currentAction = 0;
				totalActions = Actions;
				bar.Value = 0;
				Render();
			}
			/// <summary>
			/// Mark this current Action as complete and set an new number of steps
			/// </summary>
			/// <param name="newTarget">Amount of steps to complete the current Action</param>
			public static void MoveToNextAction(long newTarget) =>
				MoveToNextAction((ulong)newTarget);
			/// <summary>
			/// Mark this current Action as complete and set an new number of steps
			/// </summary>
			/// <param name="newTarget">Amount of steps to complete the current Action</param>
			public static void MoveToNextAction(ulong newTarget) {
				currentTarget = newTarget;
				currentState = 0;
				currentAction++;
				Render();
			}
			/// <summary>
			/// Sets the amount of completed steps in this Action
			/// </summary>
			/// <param name="newPosition">Amount of completed steps</param>
			public static void UpdateCurrentPosition(ulong newPosition) {
				currentState = newPosition;
				Render();
			}
			/// <summary>
			/// Sets the amount of completed steps in this Action
			/// </summary>
			/// <param name="newPosition">Amount of completed steps</param>
			public static void UpdateCurrentPosition(long newPosition) => UpdateCurrentPosition((ulong)newPosition);
			/// <summary>
			/// show current state on <see cref="bar"/>
			/// </summary>
			public static void Render() {
				if (currentTarget != 0) {
					var currentValue = ((currentState * 1000) / currentTarget) / 1000.0d;
					//currentValue *= (double)currentAction / (double)totalActions;
					//currentValue += ((currentAction * 100) / totalActions) / 100.0d;
					bar.Value = currentValue;
					//Debug.WriteLine(currentState + " / " + currentTarget + " = " + currentValue);
				}
			}
		}
	}
}
