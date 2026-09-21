using System;
using System.Runtime.CompilerServices;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween
{
	/// <summary>
	/// Makes <c>await tween</c> and <c>await sequence</c> work. Obtained through
	/// <c>Tween.GetAwaiter()</c> / <c>Sequence.GetAwaiter()</c>, which the compiler calls for you —
	/// you never write this type. Resumes when the animation ends, whether it completed or was
	/// killed; a handle that is already dead resumes immediately rather than hanging.
	/// </summary>
	/// <remarks>
	/// FeatherTween depends on neither UniTask nor <c>UnityEngine.Awaitable</c> for this:
	/// <c>await</c> binds to any type exposing <c>GetAwaiter()</c>, so <c>await tween</c> compiles
	/// the same in a project that uses UniTask and one that does not
	/// (<c>Documentation~/adr/0013-awaitables-without-dependencies.md</c>). The awaiter itself is a
	/// struct and allocates nothing; the C# async state machine around it allocates once per call,
	/// as it does for every <c>await</c> in the language.
	/// </remarks>
	public readonly struct TweenAwaiter : INotifyCompletion
	{
		private readonly int id;
		private readonly uint generation;

		internal TweenAwaiter(int id, uint generation)
		{
			this.id = id;
			this.generation = generation;
		}

		/// <summary>True when there is nothing left to wait for, so <c>await</c> continues synchronously.</summary>
		public bool IsCompleted => AwaitOps.IsFinished(id, generation);

		/// <summary>Called by the compiler; registers the continuation to run when the animation ends.</summary>
		public void OnCompleted(Action continuation)
		{
			if (continuation == null)
			{
				return;
			}

			if (!AwaitOps.TryRegisterResume(id, generation, continuation))
			{
				// Died between the IsCompleted check and here. Nothing will fire
				// a callback now, so resume rather than hang forever.
				continuation();
			}
		}

		/// <summary>Called by the compiler when the await resumes. Awaiting an animation produces no value.</summary>
		public void GetResult()
		{
		}
	}
}
