using System;
using System.Globalization;
using UnityEngine;

namespace StrandedDeepDiagnostics.Reflection
{
    internal static class ValueFormatter
    {
        public static string FormatSimple(object value)
        {
            if (value == null)
            {
                return "null";
            }

            Type type = value.GetType();

            if (value is UnityEngine.Object)
            {
                UnityEngine.Object unityObject = (UnityEngine.Object)value;
                if (unityObject == null)
                {
                    return "<destroyed UnityObject>";
                }

                string name = string.Empty;
                try
                {
                    name = unityObject.name;
                }
                catch
                {
                }

                int instanceId = 0;
                try
                {
                    instanceId = unityObject.GetInstanceID();
                }
                catch
                {
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "<{0} name='{1}' instance={2}>",
                    type.FullName,
                    name,
                    instanceId);
            }

            if (type.IsEnum)
            {
                return Enum.GetName(type, value) ?? Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is string)
            {
                string text = (string)value;
                if (text.Length > 240)
                {
                    text = text.Substring(0, 240) + "...";
                }
                return "\"" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
            }

            if (value is bool || value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong || value is float ||
                value is double || value is decimal || value is char)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is Vector2)
            {
                Vector2 v = (Vector2)value;
                return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", v.x, v.y);
            }

            if (value is Vector3)
            {
                Vector3 v = (Vector3)value;
                return FormatVector3(v);
            }

            if (value is Vector4)
            {
                Vector4 v = (Vector4)value;
                return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", v.x, v.y, v.z, v.w);
            }

            if (value is Quaternion)
            {
                Quaternion q = (Quaternion)value;
                return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", q.x, q.y, q.z, q.w);
            }

            if (value is Color)
            {
                Color c = (Color)value;
                return string.Format(CultureInfo.InvariantCulture, "rgba({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", c.r, c.g, c.b, c.a);
            }

            if (value is Rect)
            {
                Rect r = (Rect)value;
                return string.Format(CultureInfo.InvariantCulture, "Rect({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", r.x, r.y, r.width, r.height);
            }

            if (value is Bounds)
            {
                Bounds b = (Bounds)value;
                return "Bounds(center=" + FormatVector3(b.center) + ", size=" + FormatVector3(b.size) + ")";
            }

            if (value is LayerMask)
            {
                LayerMask mask = (LayerMask)value;
                return mask.value.ToString(CultureInfo.InvariantCulture);
            }

            Array array = value as Array;
            if (array != null)
            {
                return string.Format(CultureInfo.InvariantCulture, "<{0} length={1}>", type.FullName, array.Length);
            }

            return "<" + type.FullName + ">";
        }

        public static string FormatVector3(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", v.x, v.y, v.z);
        }

        public static string FormatQuaternionEuler(Quaternion q)
        {
            Vector3 euler = q.eulerAngles;
            return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", euler.x, euler.y, euler.z);
        }
    }
}
