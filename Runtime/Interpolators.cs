using System;
using System.Collections.Generic;
using UnityEngine;
using PATween.Internal;

namespace PATween
{
	public static class Interpolators
	{
		private static readonly Dictionary<Type, object> registry = new Dictionary<Type, object>();

		static Interpolators()
		{
			RegisterBuiltins();
		}

		public static void Register<T>(IInterpolator<T> interpolator)
		{
			if (interpolator == null)
			{
				throw new ArgumentNullException(nameof(interpolator));
			}
			if (TweenStore.HasLiveOfType<T>())
			{
				throw new InvalidOperationException(
					$"[PATween] Cannot re-register IInterpolator<{typeof(T).Name}> while a tween of that type is live.");
			}
			registry[typeof(T)] = interpolator;
		}

		public static IInterpolator<T> Get<T>()
		{
			if (registry.TryGetValue(typeof(T), out var interp))
			{
				return (IInterpolator<T>)interp;
			}
			throw new InvalidOperationException(
				$"[PATween] No IInterpolator<{typeof(T).Name}> registered.");
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
