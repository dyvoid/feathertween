using System;
using System.Collections.Generic;
using UnityEngine;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// Registry mapping value types to their <see cref="IInterpolator{T}"/>.
	/// Built-ins for <c>float</c>, <c>int</c>, <c>Vector2/3/4</c>, <c>Color</c>
	/// and <c>Quaternion</c> are registered automatically.
	/// </summary>
	public static class Interpolators
	{
		private static readonly Dictionary<Type, object> registry = new Dictionary<Type, object>();

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
			registry[typeof(T)] = interpolator;
		}

		/// <summary>Returns the registered interpolator for <typeparamref name="T"/>; throws if none is registered.</summary>
		public static IInterpolator<T> Get<T>()
		{
			if (registry.TryGetValue(typeof(T), out var interp))
			{
				return (IInterpolator<T>)interp;
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
			RegisterBuiltins();
		}

		private static void RegisterBuiltins()
		{
			registry[typeof(float)] = new FloatInterpolator();
			registry[typeof(int)] = new IntInterpolator();
			registry[typeof(Vector2)] = new Vector2Interpolator();
			registry[typeof(Vector3)] = new Vector3Interpolator();
			registry[typeof(Vector4)] = new Vector4Interpolator();
			registry[typeof(Color)] = new ColorInterpolator();
			registry[typeof(Quaternion)] = new QuaternionInterpolator();
		}
	}
}
