// Minimal UnityEngine surface so the package Runtime compiles outside Unity.
// Syntax/type check only — never executed.
using System;

namespace UnityEngine
{
	public class Object
	{
		internal bool destroyed;

		// Emulates Unity's fake-null for destroyed objects.
		public static bool operator ==(Object a, Object b)
		{
			var aNull = ReferenceEquals(a, null) || a.destroyed;
			var bNull = ReferenceEquals(b, null) || b.destroyed;
			if (aNull && bNull)
			{
				return true;
			}
			if (aNull || bNull)
			{
				return false;
			}
			return ReferenceEquals(a, b);
		}

		public static bool operator !=(Object a, Object b) => !(a == b);
		public override bool Equals(object o) => base.Equals(o);
		public override int GetHashCode() => base.GetHashCode();
		public static void DestroyImmediate(Object o)
		{
			o.destroyed = true;
			// Destroying a GameObject destroys its components, transform included.
			if (o is GameObject g)
			{
				g.transform.destroyed = true;
			}
		}
	}

	public class GameObject : Object
	{
		public string name;
		private readonly Transform tf = new Transform();
		public GameObject(string name) { this.name = name; }
		public Transform transform => tf;
		public T AddComponent<T>() where T : new() => new T();
		public T GetComponent<T>() => default;
		public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject("p");
	}

	public static class Debug
	{
		public static void Log(object m) { }
		public static void LogWarning(object m) { }
		public static void LogError(object m) { }
	}

	public static class Time
	{
		public static float deltaTime => 0f;
		public static float unscaledDeltaTime => 0f;
		public static float fixedDeltaTime => 0f;
		public static float fixedUnscaledDeltaTime => 0f;
	}

	public static class Mathf
	{
		public const float PI = 3.14159265f;
		public static float Lerp(float a, float b, float t) => a + (b - a) * t;
		public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
		public static float Pow(float a, float b) => (float)Math.Pow(a, b);
		public static float Sin(float f) => (float)Math.Sin(f);
		public static float Cos(float f) => (float)Math.Cos(f);
		public static float Sqrt(float f) => (float)Math.Sqrt(f);
		public static float Abs(float f) => Math.Abs(f);
		public static float Exp(float f) => (float)Math.Exp(f);
		public static float Log(float f, float b) => (float)Math.Log(f, b);
		public static float Asin(float f) => (float)Math.Asin(f);
		public static float Clamp01(float f) => f < 0f ? 0f : (f > 1f ? 1f : f);
		public static float Round(float f) => (float)Math.Round(f);
		public static float Floor(float f) => (float)Math.Floor(f);
		public static float Ceil(float f) => (float)Math.Ceiling(f);
		public static int RoundToInt(float f) => (int)Math.Round(f);
	}

	public struct Vector2
	{
		public float x, y;
		public Vector2(float x, float y) { this.x = x; this.y = y; }
		public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
		public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
		public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
		public static Vector2 zero => default;
		public static Vector2 one => default;
	}

	public struct Vector3
	{
		public float x, y, z;
		public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
		public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
		public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
		public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
		public static Vector3 zero => new Vector3(0f, 0f, 0f);
		public static Vector3 one => new Vector3(1f, 1f, 1f);
		public static Vector3 right => new Vector3(1f, 0f, 0f);
		public static Vector3 up => new Vector3(0f, 1f, 0f);
		public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
	}

	public struct Vector4
	{
		public float x, y, z, w;
		public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
		public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t) => new Vector4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
		public static Vector4 operator +(Vector4 a, Vector4 b) => new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
		public static Vector4 operator -(Vector4 a, Vector4 b) => new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
	}

	public struct Color
	{
		public float r, g, b, a;
		public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
		public static Color LerpUnclamped(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
		public static Color cyan => default;
		public static Color green => default;
		public static Color magenta => default;
		public static Color yellow => default;
		public static Color red => default;
		public static Color blue => default;
		public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
		public static Color operator -(Color a, Color b) => new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
	}

	public struct Quaternion
	{
		public float x, y, z, w;
		public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
		public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

		public static Quaternion Euler(float x, float y, float z)
		{
			const float deg2Rad = (float)(Math.PI / 180.0);
			var rx = x * deg2Rad * 0.5f;
			var ry = y * deg2Rad * 0.5f;
			var rz = z * deg2Rad * 0.5f;
			var qx = new Quaternion((float)Math.Sin(rx), 0f, 0f, (float)Math.Cos(rx));
			var qy = new Quaternion(0f, (float)Math.Sin(ry), 0f, (float)Math.Cos(ry));
			var qz = new Quaternion(0f, 0f, (float)Math.Sin(rz), (float)Math.Cos(rz));
			return qy * qx * qz; // Unity ZXY rotation order
		}

		public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
			a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
			a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
			a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
			a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

		public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);

		public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

		public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
		{
			var dot = Dot(a, b);
			if (dot < 0f)
			{
				b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
				dot = -dot;
			}
			if (dot > 0.9995f)
			{
				var r = new Quaternion(
					a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t,
					a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
				var m = (float)Math.Sqrt(Dot(r, r));
				return m > 0f ? new Quaternion(r.x / m, r.y / m, r.z / m, r.w / m) : identity;
			}
			var theta0 = (float)Math.Acos(dot);
			var theta = theta0 * t;
			var sin0 = (float)Math.Sin(theta0);
			var s0 = (float)Math.Sin(theta0 - theta) / sin0;
			var s1 = (float)Math.Sin(theta) / sin0;
			return new Quaternion(
				a.x * s0 + b.x * s1, a.y * s0 + b.y * s1,
				a.z * s0 + b.z * s1, a.w * s0 + b.w * s1);
		}
	}

	public struct Keyframe
	{
		public Keyframe(float time, float value) { }
	}

	public class AnimationCurve
	{
		public AnimationCurve(params Keyframe[] keys) { }
		public float Evaluate(float t) => t;
		public static AnimationCurve EaseInOut(float ts, float vs, float te, float ve) => new AnimationCurve();
		public static AnimationCurve Linear(float ts, float vs, float te, float ve) => new AnimationCurve();
	}

	public enum RuntimeInitializeLoadType
	{
		SubsystemRegistration = 4,
	}

	public class RuntimeInitializeOnLoadMethodAttribute : Attribute
	{
		public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
	}
}

namespace UnityEngine.LowLevel
{
	public struct PlayerLoopSystem
	{
		public delegate void UpdateFunction();
		public Type type;
		public UpdateFunction updateDelegate;
		public PlayerLoopSystem[] subSystemList;
	}

	public static class PlayerLoop
	{
		public static PlayerLoopSystem GetCurrentPlayerLoop() => default;
		public static void SetPlayerLoop(PlayerLoopSystem loop) { }
	}
}

namespace UnityEngine.PlayerLoop
{
	public struct Update { public struct ScriptRunBehaviourUpdate { } }
	public struct PreLateUpdate { public struct ScriptRunBehaviourLateUpdate { } }
	public struct FixedUpdate { public struct ScriptRunBehaviourFixedUpdate { } }
}

namespace UnityEngine
{
	public enum LogType { Error, Assert, Warning, Log, Exception }
}

namespace UnityEngine.TestTools
{
	public static class LogAssert
	{
		public static void Expect(UnityEngine.LogType type, System.Text.RegularExpressions.Regex re) { }
		public static void Expect(UnityEngine.LogType type, string message) { }
	}
}

namespace UnityEngine
{
	public class Component : Object
	{
		public Transform transform => null;
		public GameObject gameObject => null;
		public T GetComponent<T>() => default;
	}

	public class Behaviour : Component { }

	public class MonoBehaviour : Behaviour
	{
		public void Invoke(string method, float time) { }
		public static void Destroy(Object o) { }
	}

	public class Transform : Component
	{
		public Vector3 position { get; set; }
		public Vector3 localPosition { get; set; }
		public Vector3 localScale { get; set; }
		public Quaternion rotation { get; set; }
		public Quaternion localRotation { get; set; }
		public void SetParent(Transform parent, bool worldPositionStays) { }
	}

	public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

	public class Material
	{
		public Color color { get; set; }
	}

	public class Renderer : Component
	{
		public Material material { get; set; }
	}

	public struct Rect
	{
		public Rect(float x, float y, float w, float h) { }
	}

	public static class GUI
	{
		public static void Label(Rect r, string text) { }
	}

	public class HeaderAttribute : System.Attribute
	{
		public HeaderAttribute(string h) { }
	}

	public class TooltipAttribute : System.Attribute
	{
		public TooltipAttribute(string t) { }
	}

	public class SerializeFieldAttribute : System.Attribute { }
}

namespace UnityEngine
{
	public class CanvasGroup : Behaviour
	{
		public float alpha { get; set; } = 1f;
	}

	public static class GameObjectExtensionsForStub { }
}

namespace UnityEngine.UI
{
	public class Image : Behaviour
	{
		public Color color { get; set; } = new Color(1f, 1f, 1f, 1f);
		public float fillAmount { get; set; } = 1f;
	}
}
