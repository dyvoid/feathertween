using System;
using System.Collections.Generic;
using UnityEngine;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween
{
	/// <summary>
	/// Registry mapping value types to their <see cref="IInterpolator{T}"/>.
	/// Built-ins for <c>float</c>, <c>int</c>, <c>Vector2/3/4</c>, <c>Color</c>
	/// and <c>Quaternion</c> are registered automatically.
	/// </summary>
	public static class Interpolators
	{
		private static readonly Dictionary<Type, object> registry = new Dictionary<Type, object>();

		// Get<T> runs on every Build(). A static generic field resolves without
		// hashing the Type; the dictionary stays as the non-generic view, and the
		// clearers let Reset() empty every closed Cache<T> it cannot name.
		private static readonly List<Action> cacheClearers = new List<Action>();

		private static class Cache<T>
		{
			public static IInterpolator<T> Value;
			public static bool Tracked;
		}

		static Interpolators()
		{
			RegisterBuiltins();
		}

		/// <summary>Registers (or replaces) the interpolator for <typeparamref name="T"/>. Throws while any tween of that type is live.</summary>
		public static void Register<T>(IInterpolator<T> interpolator)
		{
			if (interpolator == null)
			{
				throw new ArgumentNullException(nameof(interpolator));
			}
			if (TweenStore.HasLiveOfType<T>())
			{
				throw new InvalidOperationException(
					$"[FeatherTween] Cannot re-register IInterpolator<{typeof(T).Name}> while a tween of that type is live.");
			}
			Store(interpolator);
		}

		/// <summary>Returns the registered interpolator for <typeparamref name="T"/>; throws if none is registered.</summary>
		public static IInterpolator<T> Get<T>()
		{
			var cached = Cache<T>.Value;
			if (cached != null)
			{
				return cached;
			}
			throw new InvalidOperationException(
				$"[FeatherTween] No IInterpolator<{typeof(T).Name}> registered.");
		}

		internal static bool IsRegistered<T>()
		{
			return registry.ContainsKey(typeof(T));
		}

		internal static void Reset()
		{
			registry.Clear();
			for (var i = 0; i < cacheClearers.Count; i++)
			{
				cacheClearers[i]();
			}
			RegisterBuiltins();
		}

		private static void Store<T>(IInterpolator<T> interpolator)
		{
			registry[typeof(T)] = interpolator;
			Cache<T>.Value = interpolator;
			if (!Cache<T>.Tracked)
			{
				Cache<T>.Tracked = true;
				cacheClearers.Add(() => Cache<T>.Value = null);
			}
		}

		private static void RegisterBuiltins()
		{
			Store<float>(new FloatInterpolator());
			Store<int>(new IntInterpolator());
			Store<Vector2>(new Vector2Interpolator());
			Store<Vector3>(new Vector3Interpolator());
			Store<Vector4>(new Vector4Interpolator());
			Store<Color>(new ColorInterpolator());
			Store<Quaternion>(new QuaternionInterpolator());
		}
	}
}
