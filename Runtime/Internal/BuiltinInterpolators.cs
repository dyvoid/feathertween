using UnityEngine;

namespace dyvoid.FeatherTween.Internal
{
	internal sealed class FloatInterpolator : IInterpolator<float>
	{
		public float Lerp(float from, float to, float t) => from + (to - from) * t;
		public float Add(float a, float b) => a + b;
		public float Subtract(float a, float b) => a - b;
	}

	internal sealed class IntInterpolator : IInterpolator<int>
	{
		// Round to nearest: truncation steps asymmetrically across 0 (phase 1.15).
		public int Lerp(int from, int to, float t) => Mathf.RoundToInt(from + (to - from) * t);
		public int Add(int a, int b) => a + b;
		public int Subtract(int a, int b) => a - b;
	}

	internal sealed class Vector2Interpolator : IInterpolator<Vector2>
	{
		public Vector2 Lerp(Vector2 from, Vector2 to, float t) => Vector2.LerpUnclamped(from, to, t);
		public Vector2 Add(Vector2 a, Vector2 b) => a + b;
		public Vector2 Subtract(Vector2 a, Vector2 b) => a - b;
	}

	internal sealed class Vector3Interpolator : IInterpolator<Vector3>
	{
		public Vector3 Lerp(Vector3 from, Vector3 to, float t) => Vector3.LerpUnclamped(from, to, t);
		public Vector3 Add(Vector3 a, Vector3 b) => a + b;
		public Vector3 Subtract(Vector3 a, Vector3 b) => a - b;
	}

	internal sealed class Vector4Interpolator : IInterpolator<Vector4>
	{
		public Vector4 Lerp(Vector4 from, Vector4 to, float t) => Vector4.LerpUnclamped(from, to, t);
		public Vector4 Add(Vector4 a, Vector4 b) => a + b;
		public Vector4 Subtract(Vector4 a, Vector4 b) => a - b;
	}

	internal sealed class ColorInterpolator : IInterpolator<Color>
	{
		public Color Lerp(Color from, Color to, float t) => Color.LerpUnclamped(from, to, t);
		public Color Add(Color a, Color b) => a + b;
		public Color Subtract(Color a, Color b) => a - b;
	}

	internal sealed class QuaternionInterpolator : IInterpolator<Quaternion>
	{
		public Quaternion Lerp(Quaternion from, Quaternion to, float t) => Quaternion.SlerpUnclamped(from, to, t);
		public Quaternion Add(Quaternion a, Quaternion b) => a * b;
		public Quaternion Subtract(Quaternion a, Quaternion b) => a * Quaternion.Inverse(b);
	}
}
