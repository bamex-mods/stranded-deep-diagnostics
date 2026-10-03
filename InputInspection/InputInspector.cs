using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Players;
using StrandedDeepDiagnostics.Reflection;

namespace StrandedDeepDiagnostics.InputInspection
{
    internal sealed class InputInspector
    {
        private float _nextOverlaySampleAt;
        private int _cachedPlayerInstanceId;
        private string[] _cachedOverlay = new string[0];

        public string[] DescribeOverlay(PlayerContext player, float realtimeSinceStartup)
        {
            int playerId = SafeInstanceId(player == null ? null : player.PlayerObject);
            if (playerId == _cachedPlayerInstanceId && realtimeSinceStartup < _nextOverlaySampleAt && _cachedOverlay != null)
            {
                return _cachedOverlay;
            }

            _cachedPlayerInstanceId = playerId;
            _nextOverlaySampleAt = realtimeSinceStartup + 0.10f;
            _cachedOverlay = BuildOverlay(player);
            return _cachedOverlay;
        }

        public string RenderReport(PlayerContext player)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== INPUT / REWIRED INSPECTOR ===");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Player: " + (player == null ? "<unresolved>" : player.DisplayName));
            sb.AppendLine("NOTE: raw joystick indices are not Rewired Element Identifier IDs.");
            sb.AppendLine();

            object rewiredPlayer = ResolveRewiredPlayer(player);
            if (rewiredPlayer == null)
            {
                sb.AppendLine("Rewired.Player: <unresolved from known Beam.Player Input members>");
                return sb.ToString();
            }

            sb.AppendLine("Rewired.Player runtime type: " + SafeTypeName(rewiredPlayer));
            AppendKnownMember(sb, rewiredPlayer, "id");
            AppendKnownMember(sb, rewiredPlayer, "name");
            sb.AppendLine();

            List<object> joysticks = ResolveJoysticks(rewiredPlayer);
            sb.AppendLine("Assigned joysticks: " + joysticks.Count.ToString(CultureInfo.InvariantCulture));
            int i;
            for (i = 0; i < joysticks.Count; i++)
            {
                WriteJoystick(sb, joysticks[i], i, true);
            }

            return sb.ToString();
        }

        public void Invalidate()
        {
            _nextOverlaySampleAt = 0f;
            _cachedPlayerInstanceId = 0;
            _cachedOverlay = new string[0];
        }

        private static string[] BuildOverlay(PlayerContext player)
        {
            List<string> lines = new List<string>();
            object rewiredPlayer = ResolveRewiredPlayer(player);
            if (rewiredPlayer == null)
            {
                lines.Add("REWIRED <unresolved>");
                lines.Add("F12 exports input resolution details");
                return lines.ToArray();
            }

            lines.Add("REWIRED " + SafeTypeName(rewiredPlayer) + " id=" + ReadKnownText(rewiredPlayer, "id"));
            List<object> joysticks = ResolveJoysticks(rewiredPlayer);
            lines.Add("JOYSTICKS " + joysticks.Count.ToString(CultureInfo.InvariantCulture));

            int i;
            int max = Math.Min(2, joysticks.Count);
            for (i = 0; i < max; i++)
            {
                object joystick = joysticks[i];
                string id = ReadKnownText(joystick, "id");
                string name = FirstNonEmpty(
                    ReadKnownText(joystick, "name"),
                    ReadKnownText(joystick, "hardwareName"),
                    SafeTypeName(joystick));
                lines.Add("JOY" + i + " id=" + id + " " + Trim(name, 48));

                int axisCount = ClampKnownCount(ReadKnownInt(joystick, "axisCount", 6), 8);
                int buttonCount = ClampKnownCount(ReadKnownInt(joystick, "buttonCount", 16), 24);
                lines.Add("COUNT axis=" + axisCount.ToString(CultureInfo.InvariantCulture) + " button=" + buttonCount.ToString(CultureInfo.InvariantCulture));
                string axes = ReadAxesCompact(joystick, Math.Min(6, axisCount));
                lines.Add("AXIS RAW  " + axes);
                lines.Add("BUTTON RAW pressed=" + ReadPressedButtons(joystick, buttonCount));
            }

            lines.Add("F12 exports raw axes/buttons for assigned joysticks");
            return lines.ToArray();
        }

        private static object ResolveRewiredPlayer(PlayerContext player)
        {
            if (player == null || player.PlayerObject == null) return null;
            object beamPlayer = player.PlayerObject;
            string[] names = new string[] { "Input", "input", "_input" };
            int i;
            for (i = 0; i < names.Length; i++)
            {
                object value = SafeReflection.GetKnownMemberValue(beamPlayer, names[i]);
                if (LooksLikeRewiredPlayer(value)) return value;
            }

            return null;
        }

        private static bool LooksLikeRewiredPlayer(object value)
        {
            if (value == null) return false;
            try
            {
                Type type = value.GetType();
                return type.FullName == "Rewired.Player" ||
                       (type.Namespace != null && type.Namespace.StartsWith("Rewired", StringComparison.Ordinal));
            }
            catch
            {
                return false;
            }
        }

        private static List<object> ResolveJoysticks(object rewiredPlayer)
        {
            List<object> result = new List<object>();
            if (rewiredPlayer == null) return result;

            object controllers = SafeReflection.GetKnownMemberValue(rewiredPlayer, "controllers");
            if (controllers == null) controllers = SafeReflection.GetKnownMemberValue(rewiredPlayer, "Controllers");
            if (controllers == null) return result;

            object joysticks = SafeReflection.GetKnownMemberValue(controllers, "Joysticks");
            if (joysticks == null) joysticks = SafeReflection.GetKnownMemberValue(controllers, "joysticks");
            IEnumerable enumerable = joysticks as IEnumerable;
            if (enumerable == null) return result;

            try
            {
                foreach (object item in enumerable)
                {
                    if (item != null) result.Add(item);
                    if (result.Count >= 8) break;
                }
            }
            catch
            {
            }

            return result;
        }

        private static void WriteJoystick(StringBuilder sb, object joystick, int index, bool extended)
        {
            sb.AppendLine("--- JOYSTICK[" + index.ToString(CultureInfo.InvariantCulture) + "] ---");
            sb.AppendLine("RuntimeType: " + SafeTypeName(joystick));
            AppendKnownMember(sb, joystick, "id");
            AppendKnownMember(sb, joystick, "name");
            AppendKnownMember(sb, joystick, "hardwareName");
            AppendKnownMember(sb, joystick, "hardwareIdentifier");
            AppendKnownMember(sb, joystick, "isConnected");
            AppendKnownMember(sb, joystick, "axisCount");
            AppendKnownMember(sb, joystick, "buttonCount");
            int axisCount = ClampKnownCount(ReadKnownInt(joystick, "axisCount", 8), 32);
            int buttonCount = ClampKnownCount(ReadKnownInt(joystick, "buttonCount", 20), 64);
            sb.AppendLine("RAW AXES (0.." + Math.Max(0, axisCount - 1).ToString(CultureInfo.InvariantCulture) + "):");
            int i;
            for (i = 0; i < axisCount; i++)
            {
                string value;
                if (TryInvokeIndex(joystick, "GetAxis", i, out value))
                    sb.AppendLine("  A" + i.ToString("00", CultureInfo.InvariantCulture) + " = " + value);
                else
                    sb.AppendLine("  A" + i.ToString("00", CultureInfo.InvariantCulture) + " = <unavailable>");
            }

            sb.AppendLine("RAW BUTTONS (0.." + Math.Max(0, buttonCount - 1).ToString(CultureInfo.InvariantCulture) + "):");
            for (i = 0; i < buttonCount; i++)
            {
                string value;
                if (TryInvokeIndex(joystick, "GetButton", i, out value))
                    sb.AppendLine("  B" + i.ToString("00", CultureInfo.InvariantCulture) + " = " + value);
                else
                    sb.AppendLine("  B" + i.ToString("00", CultureInfo.InvariantCulture) + " = <unavailable>");
            }
            sb.AppendLine();
        }

        private static string ReadAxesCompact(object joystick, int count)
        {
            List<string> parts = new List<string>();
            int i;
            for (i = 0; i < count; i++)
            {
                string value;
                if (!TryInvokeIndex(joystick, "GetAxis", i, out value)) value = "?";
                parts.Add("A" + i.ToString("00", CultureInfo.InvariantCulture) + "=" + value);
            }
            return string.Join(" ", parts.ToArray());
        }

        private static string ReadPressedButtons(object joystick, int count)
        {
            List<string> pressed = new List<string>();
            int i;
            for (i = 0; i < count; i++)
            {
                string value;
                if (TryInvokeIndex(joystick, "GetButton", i, out value) && string.Equals(value, "True", StringComparison.OrdinalIgnoreCase))
                    pressed.Add("B" + i.ToString("00", CultureInfo.InvariantCulture));
            }
            return pressed.Count == 0 ? "<none>" : string.Join(",", pressed.ToArray());
        }

        private static bool TryInvokeIndex(object instance, string methodName, int index, out string valueText)
        {
            valueText = null;
            if (instance == null) return false;
            MethodInfo method = FindIndexedMethod(instance.GetType(), methodName);
            if (method == null) return false;
            try
            {
                object value = method.Invoke(instance, new object[] { index });
                valueText = ValueFormatter.FormatSimple(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo FindIndexedMethod(Type type, string methodName)
        {
            if (type == null) return null;
            try
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
                int i;
                for (i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(int)) return method;
                }
            }
            catch
            {
            }
            return null;
        }


        private static int ReadKnownInt(object instance, string memberName, int fallback)
        {
            object value = SafeReflection.GetKnownMemberValue(instance, memberName);
            if (value == null) return fallback;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static int ClampKnownCount(int value, int maximum)
        {
            if (value < 0) return 0;
            if (value > maximum) return maximum;
            return value;
        }

        private static void AppendKnownMember(StringBuilder sb, object instance, string memberName)
        {
            object value = SafeReflection.GetKnownMemberValue(instance, memberName);
            sb.AppendLine(memberName + ": " + (value == null ? "<unavailable>" : ValueFormatter.FormatSimple(value)));
        }

        private static string ReadKnownText(object instance, string memberName)
        {
            object value = SafeReflection.GetKnownMemberValue(instance, memberName);
            return value == null ? "<unavailable>" : ValueFormatter.FormatSimple(value);
        }

        private static string SafeTypeName(object value)
        {
            if (value == null) return "<null>";
            try { return value.GetType().FullName; }
            catch { return "<unavailable>"; }
        }

        private static int SafeInstanceId(UnityEngine.Object value)
        {
            if (value == null) return 0;
            try { return value.GetInstanceID(); }
            catch { return 0; }
        }

        private static string FirstNonEmpty(string a, string b, string c)
        {
            if (!string.IsNullOrEmpty(a) && a != "<unavailable>") return a;
            if (!string.IsNullOrEmpty(b) && b != "<unavailable>") return b;
            return c ?? "<unknown>";
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
            return text.Substring(0, max - 3) + "...";
        }
    }
}
