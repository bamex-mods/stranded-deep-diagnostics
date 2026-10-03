using System;
using System.Collections.Generic;
using System.Text;

namespace StrandedDeepDiagnostics.Snapshots
{
    internal static class TextReportDiff
    {
        public static string Compare(string before, string after)
        {
            string[] a = Split(before);
            string[] b = Split(after);
            int max = Math.Max(a.Length, b.Length);
            int changes = 0;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== TEXT SNAPSHOT DIFF ===");
            int i;
            for (i = 0; i < max; i++)
            {
                string av = i < a.Length ? a[i] : "<missing>";
                string bv = i < b.Length ? b[i] : "<missing>";
                if (string.Equals(av, bv, StringComparison.Ordinal)) continue;
                changes++;
                sb.AppendLine("LINE " + (i + 1));
                sb.AppendLine("- " + av);
                sb.AppendLine("+ " + bv);
            }
            sb.Insert(0, "Changes: " + changes + Environment.NewLine);
            if (changes == 0) sb.AppendLine("NO CHANGES detected");
            return sb.ToString();
        }

        private static string[] Split(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Split(new char[] { '\n' });
        }
    }
}
