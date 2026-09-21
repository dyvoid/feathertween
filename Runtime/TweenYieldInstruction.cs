using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween
{
	/// <summary>
	/// Coroutine equivalent of <c>await tween</c>: <c>yield return tween.ToYieldInstruction();</c>.
	/// Resumes the coroutine when the animation ends, whether it completed or was killed, and
	/// resumes immediately for a handle that is already dead.
	/// </summary>
	/// <remarks>
	/// Not pooled, unlike the rest of FeatherTween's internals. Unity holds a yield instruction
	/// across frames with no signal for when it is finished with it, so recycling one risks handing
	/// a live coroutine an instruction that now belongs to a different tween. One allocation per
	/// coroutine wait is the honest price; <c>await</c> is the allocation-lighter path.
	/// </remarks>
	public sealed class TweenYieldInstruction : UnityEngine.CustomYieldInstruction
	{
		private readonly int id;
		private readonly uint generation;

		internal TweenYieldInstruction(int id, uint generation)
		{
			this.id = id;
			this.generation = generation;
		}

		/// <summary>Polled by Unity each frame; false once the animation has ended.</summary>
		public override bool keepWaiting => AwaitOps.KeepWaiting(id, generation);
	}
}
