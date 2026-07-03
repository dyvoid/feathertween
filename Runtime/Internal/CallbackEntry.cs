using System;

namespace PATween.Internal
{
	// One subscription slot: either a plain Action or a target-capture pair
	// (state + Action<TTarget>) invoked through a cached per-type invoker so
	// dispatch never boxes or allocates.
	internal struct CallbackEntry
	{
		public Action Plain;
		public object State;
		public Delegate Stateful;
		public Action<object, Delegate> Invoker;

		public static CallbackEntry FromAction(Action cb)
		{
			return new CallbackEntry { Plain = cb };
		}

		public static CallbackEntry FromTargetCapture<TTarget>(TTarget state, Action<TTarget> cb)
			where TTarget : class
		{
			return new CallbackEntry
			{
				State = state,
				Stateful = cb,
				Invoker = CallbackInvoker<TTarget>.Instance,
			};
		}

		public void Invoke()
		{
			if (Plain != null)
			{
				Plain();
			}
			else
			{
				Invoker?.Invoke(State, Stateful);
			}
		}
	}

	// One cached invoker delegate per closed target type; created once, reused
	// by every target-capture subscription of that type.
	internal static class CallbackInvoker<TTarget> where TTarget : class
	{
		public static readonly Action<object, Delegate> Instance =
			(state, del) => ((Action<TTarget>)del).Invoke((TTarget)state);
	}
}
