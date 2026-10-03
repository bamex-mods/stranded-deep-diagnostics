using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace StrandedDeepDiagnostics.Reflection
{
    internal static class SafeReflection
    {
        public static object GetKnownMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            Type type = instance.GetType();
            while (type != null)
            {
                try
                {
                    FieldInfo field = type.GetField(
                        memberName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        return field.GetValue(instance);
                    }
                }
                catch
                {
                }

                try
                {
                    PropertyInfo property = type.GetProperty(
                        memberName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (property != null && property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) != null)
                    {
                        return property.GetValue(instance, null);
                    }
                }
                catch
                {
                }

                type = type.BaseType;
            }

            return null;
        }

        public static string GetHierarchyPath(Transform transform, int maxDepth)
        {
            if (transform == null)
            {
                return "<null>";
            }

            List<string> names = new List<string>();
            Transform current = transform;
            int depth = 0;

            while (current != null && depth < maxDepth)
            {
                string name;
                try
                {
                    name = current.name;
                }
                catch
                {
                    name = "<unavailable>";
                }

                names.Add(name);
                current = current.parent;
                depth++;
            }

            names.Reverse();
            string path = string.Join("/", names.ToArray());
            if (current != null)
            {
                path = ".../" + path;
            }
            return path;
        }

        public static List<string> DescribeComponents(GameObject gameObject, bool deep, int maxFieldsPerComponent)
        {
            List<string> lines = new List<string>();
            if (gameObject == null)
            {
                return lines;
            }

            Component[] components;
            try
            {
                components = gameObject.GetComponents<Component>();
            }
            catch (Exception ex)
            {
                lines.Add("<GetComponents failed: " + ex.GetType().Name + ">");
                return lines;
            }

            int i;
            for (i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    lines.Add("<missing component>");
                    continue;
                }

                Type type = component.GetType();
                lines.Add("COMPONENT " + type.FullName);
                DescribeFields(lines, component, type, deep, maxFieldsPerComponent);
                if (deep)
                {
                    DescribePropertyMetadata(lines, type, 24);
                    DescribeInterfaceMetadata(lines, type, 24);
                }
            }

            return lines;
        }

        private static void DescribeFields(List<string> lines, object instance, Type type, bool deep, int maxFields)
        {
            int emitted = 0;
            Type current = type;

            while (current != null && current != typeof(UnityEngine.Object) && emitted < maxFields)
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
                if (deep)
                {
                    flags |= BindingFlags.NonPublic;
                }

                FieldInfo[] fields;
                try
                {
                    fields = current.GetFields(flags);
                }
                catch
                {
                    fields = new FieldInfo[0];
                }

                int i;
                for (i = 0; i < fields.Length && emitted < maxFields; i++)
                {
                    FieldInfo field = fields[i];
                    string visibility = field.IsPublic ? "public" : "nonpublic";
                    string valueText;
                    try
                    {
                        object value = field.GetValue(instance);
                        valueText = ValueFormatter.FormatSimple(value);
                    }
                    catch (Exception ex)
                    {
                        valueText = "<read failed: " + ex.GetType().Name + ">";
                    }

                    lines.Add(string.Format(
                        "  FIELD [{0}] {1} {2} = {3}",
                        visibility,
                        field.FieldType.FullName,
                        field.Name,
                        valueText));
                    emitted++;
                }

                current = current.BaseType;
            }

            if (emitted >= maxFields)
            {
                lines.Add("  <field output truncated>");
            }
        }

        private static void DescribePropertyMetadata(List<string> lines, Type type, int maxProperties)
        {
            PropertyInfo[] properties;
            try
            {
                properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch
            {
                return;
            }

            int count = Math.Min(properties.Length, maxProperties);
            int i;
            for (i = 0; i < count; i++)
            {
                PropertyInfo property = properties[i];
                MethodInfo getter = property.GetGetMethod(true);
                MethodInfo setter = property.GetSetMethod(true);
                string access = string.Empty;
                if (getter != null)
                {
                    access += "get";
                }
                if (setter != null)
                {
                    if (access.Length > 0)
                    {
                        access += "/";
                    }
                    access += "set";
                }

                lines.Add(string.Format(
                    "  PROPERTY-META {0} {1} [{2}]",
                    property.PropertyType.FullName,
                    property.Name,
                    access));
            }

            if (properties.Length > maxProperties)
            {
                lines.Add("  <property metadata truncated>");
            }
        }

        private static void DescribeInterfaceMetadata(List<string> lines, Type type, int maxInterfaces)
        {
            Type[] interfaces;
            try
            {
                interfaces = type.GetInterfaces();
            }
            catch
            {
                return;
            }

            int count = Math.Min(interfaces.Length, maxInterfaces);
            int i;
            for (i = 0; i < count; i++)
            {
                lines.Add("  INTERFACE " + interfaces[i].FullName);
            }

            if (interfaces.Length > maxInterfaces)
            {
                lines.Add("  <interface metadata truncated>");
            }
        }


        public static GameObject ResolveInspectableAncestor(GameObject raw, int maxParents)
        {
            if (raw == null)
            {
                return null;
            }

            Transform current = raw.transform;
            int depth = 0;
            while (current != null && depth < maxParents)
            {
                GameObject candidate = current.gameObject;
                Component[] components;
                try
                {
                    components = candidate.GetComponents<Component>();
                }
                catch
                {
                    components = new Component[0];
                }

                int i;
                for (i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    Type type = component.GetType();
                    string fullName = type.FullName ?? type.Name;
                    if (fullName.StartsWith("Beam.", StringComparison.Ordinal) ||
                        fullName.StartsWith("Funlabs.", StringComparison.Ordinal) ||
                        fullName.IndexOf("InteractiveObject", StringComparison.Ordinal) >= 0 ||
                        fullName.IndexOf("ConstructionObject", StringComparison.Ordinal) >= 0)
                    {
                        return candidate;
                    }
                }

                current = current.parent;
                depth++;
            }

            return raw;
        }

        public static string GetPrimaryComponentTypeName(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "<none>";
            }

            Component[] components;
            try
            {
                components = gameObject.GetComponents<Component>();
            }
            catch
            {
                return "<unavailable>";
            }

            string firstBehaviour = null;
            int i;
            for (i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                string fullName = type.FullName ?? type.Name;
                if (fullName.StartsWith("Beam.", StringComparison.Ordinal) ||
                    fullName.StartsWith("Funlabs.", StringComparison.Ordinal))
                {
                    return fullName;
                }

                if (firstBehaviour == null && component is MonoBehaviour)
                {
                    firstBehaviour = fullName;
                }
            }

            return firstBehaviour ?? typeof(GameObject).FullName;
        }

        public static string GetComponentTypeSummary(GameObject gameObject, int maxComponents)
        {
            if (gameObject == null)
            {
                return "<none>";
            }

            Component[] components;
            try
            {
                components = gameObject.GetComponents<Component>();
            }
            catch
            {
                return "<unavailable>";
            }

            StringBuilder sb = new StringBuilder();
            int count = Math.Min(components.Length, maxComponents);
            int i;
            for (i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                Component component = components[i];
                sb.Append(component == null ? "<missing>" : component.GetType().Name);
            }

            if (components.Length > maxComponents)
            {
                sb.Append(", ...");
            }

            return sb.ToString();
        }
    }
}
