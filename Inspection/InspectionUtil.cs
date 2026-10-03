using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using StrandedDeepDiagnostics.Reflection;
using StrandedDeepDiagnostics.Targets;

namespace StrandedDeepDiagnostics.Inspection
{
    internal static class InspectionUtil
    {
        public static List<Component> GetComponentsOnAncestorChain(DiagnosticTarget target, int maxParents)
        {
            List<Component> result = new List<Component>();
            GameObject start = target == null ? null : target.GameObject;
            if (start == null) return result;

            Transform current = start.transform;
            int depth = 0;
            while (current != null && depth < maxParents)
            {
                Component[] components;
                try { components = current.gameObject.GetComponents<Component>(); }
                catch { components = new Component[0]; }

                int i;
                for (i = 0; i < components.Length; i++)
                {
                    if (components[i] != null) result.Add(components[i]);
                }
                current = current.parent;
                depth++;
            }
            return result;
        }

        public static List<Component> FindComponentsByTokens(DiagnosticTarget target, string[] tokens, int maxParents)
        {
            List<Component> all = GetComponentsOnAncestorChain(target, maxParents);
            List<Component> result = new List<Component>();
            int i;
            for (i = 0; i < all.Count; i++)
            {
                Component c = all[i];
                if (TypeMatches(c.GetType(), tokens)) result.Add(c);
            }
            return result;
        }

        public static List<Component> FindComponentsInPlayer(Component player, string[] tokens, int maxComponents)
        {
            List<Component> result = new List<Component>();
            if (player == null) return result;

            Component[] components;
            try { components = player.gameObject.GetComponentsInChildren<Component>(true); }
            catch { components = new Component[0]; }

            int i;
            for (i = 0; i < components.Length && result.Count < maxComponents; i++)
            {
                Component c = components[i];
                if (c != null && TypeMatches(c.GetType(), tokens)) result.Add(c);
            }
            return result;
        }

        public static bool TypeMatches(Type type, string[] tokens)
        {
            if (type == null || tokens == null) return false;
            string name = type.FullName ?? type.Name;
            int i;
            for (i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            Type[] interfaces;
            try { interfaces = type.GetInterfaces(); }
            catch { interfaces = new Type[0]; }
            int j;
            for (j = 0; j < interfaces.Length; j++)
            {
                string iname = interfaces[j].FullName ?? interfaces[j].Name;
                for (i = 0; i < tokens.Length; i++)
                {
                    if (!string.IsNullOrEmpty(tokens[i]) && iname.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        public static string ReadKnownMembers(object instance, string[] memberNames, int maxCollectionItems)
        {
            StringBuilder sb = new StringBuilder();
            if (instance == null) return "<null>";
            int i;
            for (i = 0; i < memberNames.Length; i++)
            {
                string member = memberNames[i];
                object value = SafeReflection.GetKnownMemberValue(instance, member);
                if (value == null) continue;
                sb.Append("  ");
                sb.Append(member);
                sb.Append(" = ");
                sb.AppendLine(FormatBoundedValue(value, maxCollectionItems));
            }
            return sb.ToString();
        }

        public static string ReadMatchingFields(object instance, string[] nameTokens, int maxFields, int maxCollectionItems)
        {
            StringBuilder sb = new StringBuilder();
            if (instance == null) return string.Empty;
            Type current = instance.GetType();
            int emitted = 0;
            while (current != null && emitted < maxFields)
            {
                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { fields = new FieldInfo[0]; }
                int i;
                for (i = 0; i < fields.Length && emitted < maxFields; i++)
                {
                    FieldInfo field = fields[i];
                    if (!NameMatches(field.Name, nameTokens)) continue;
                    string value;
                    try { value = FormatBoundedValue(field.GetValue(instance), maxCollectionItems); }
                    catch (Exception ex) { value = "<read failed: " + ex.GetType().Name + ">"; }
                    sb.Append("  FIELD ");
                    sb.Append(field.FieldType.FullName);
                    sb.Append(" ");
                    sb.Append(field.Name);
                    sb.Append(" = ");
                    sb.AppendLine(value);
                    emitted++;
                }
                current = current.BaseType;
            }
            if (emitted >= maxFields) sb.AppendLine("  <matching field output truncated>");
            return sb.ToString();
        }

        public static string ReadUnityReferenceFields(object instance, int maxFields)
        {
            StringBuilder sb = new StringBuilder();
            if (instance == null) return string.Empty;
            Type current = instance.GetType();
            int emitted = 0;
            while (current != null && emitted < maxFields)
            {
                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { fields = new FieldInfo[0]; }
                int i;
                for (i = 0; i < fields.Length && emitted < maxFields; i++)
                {
                    FieldInfo field = fields[i];
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue;
                    string value;
                    try { value = ValueFormatter.FormatSimple(field.GetValue(instance)); }
                    catch (Exception ex) { value = "<read failed: " + ex.GetType().Name + ">"; }
                    sb.Append("  REF ");
                    sb.Append(field.FieldType.FullName);
                    sb.Append(" ");
                    sb.Append(field.Name);
                    sb.Append(" = ");
                    sb.AppendLine(value);
                    emitted++;
                }
                current = current.BaseType;
            }
            return sb.ToString();
        }

        public static string FormatBoundedValue(object value, int maxCollectionItems)
        {
            if (value == null) return "null";
            Array array = value as Array;
            if (array != null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<"); sb.Append(value.GetType().FullName); sb.Append(" length="); sb.Append(array.Length); sb.Append(">");
                int count = Math.Min(array.Length, Math.Max(0, maxCollectionItems));
                int i;
                for (i = 0; i < count; i++)
                {
                    object item = null;
                    try { item = array.GetValue(i); } catch { }
                    sb.Append(" ["); sb.Append(i); sb.Append("]="); sb.Append(ValueFormatter.FormatSimple(item));
                }
                if (array.Length > count) sb.Append(" ...");
                return sb.ToString();
            }

            IList list = value as IList;
            if (list != null)
            {
                int count = 0;
                try { count = list.Count; } catch { return "<IList count unavailable>"; }
                StringBuilder sb = new StringBuilder();
                sb.Append("<"); sb.Append(value.GetType().FullName); sb.Append(" count="); sb.Append(count); sb.Append(">");
                int max = Math.Min(count, Math.Max(0, maxCollectionItems));
                int i;
                for (i = 0; i < max; i++)
                {
                    object item = null;
                    try { item = list[i]; } catch { }
                    sb.Append(" ["); sb.Append(i); sb.Append("]="); sb.Append(ValueFormatter.FormatSimple(item));
                }
                if (count > max) sb.Append(" ...");
                return sb.ToString();
            }

            return ValueFormatter.FormatSimple(value);
        }

        public static string SafePath(Component component)
        {
            if (component == null) return "<null>";
            try { return SafeReflection.GetHierarchyPath(component.transform, 48); }
            catch { return "<unavailable>"; }
        }

        private static bool NameMatches(string name, string[] tokens)
        {
            if (string.IsNullOrEmpty(name) || tokens == null) return false;
            int i;
            for (i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && name.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
