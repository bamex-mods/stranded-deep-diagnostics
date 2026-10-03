using System;
using System.Text;
using UnityEngine;

namespace StrandedDeepDiagnostics.Targets
{
    internal sealed class SemanticTargetResolver
    {
        public void Resolve(DiagnosticTarget target, int maxParents)
        {
            if (target == null || target.GameObject == null)
            {
                return;
            }

            target.RelatedTargets.Clear();
            target.PrimaryGameObject = null;

            Transform current = target.GameObject.transform;
            int depth = 0;

            while (current != null && depth < maxParents)
            {
                GameObject gameObject = current.gameObject;
                string matchedTypes;
                string role = Classify(gameObject, out matchedTypes);

                if (depth == 0)
                {
                    AddRelation(target, "SELECTED", gameObject, matchedTypes);
                }

                if (!string.IsNullOrEmpty(role))
                {
                    if (target.PrimaryGameObject == null)
                    {
                        target.PrimaryGameObject = gameObject;
                    }

                    if (depth != 0 || role != "SELECTED")
                    {
                        AddRelation(target, role, gameObject, matchedTypes);
                    }
                }

                current = current.parent;
                depth++;
            }

            if (target.PrimaryGameObject == null)
            {
                target.PrimaryGameObject = target.GameObject;
            }
        }

        private static void AddRelation(DiagnosticTarget target, string role, GameObject gameObject, string matchedTypes)
        {
            if (gameObject == null)
            {
                return;
            }

            int i;
            for (i = 0; i < target.RelatedTargets.Count; i++)
            {
                RelatedTarget existing = target.RelatedTargets[i];
                if (existing.GameObject == gameObject && existing.Role == role)
                {
                    return;
                }
            }

            RelatedTarget related = new RelatedTarget();
            related.Role = role;
            related.GameObject = gameObject;
            related.MatchedComponentTypes = matchedTypes;
            target.RelatedTargets.Add(related);
        }

        private static string Classify(GameObject gameObject, out string matchedTypes)
        {
            matchedTypes = string.Empty;
            if (gameObject == null)
            {
                return null;
            }

            Component[] components;
            try
            {
                components = gameObject.GetComponents<Component>();
            }
            catch
            {
                return null;
            }

            bool player = false;
            bool interactive = false;
            bool construction = false;
            bool storage = false;
            bool saveable = false;
            bool structure = false;
            bool world = false;
            bool raft = false;

            StringBuilder matches = new StringBuilder();
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
                bool interesting = false;

                if (fullName == "Beam.Player")
                {
                    player = true;
                    interesting = true;
                }
                if (fullName.IndexOf("InteractiveObject", StringComparison.Ordinal) >= 0)
                {
                    interactive = true;
                    interesting = true;
                }
                if (fullName.IndexOf("ConstructionObject", StringComparison.Ordinal) >= 0 ||
                    fullName.IndexOf("Constructing", StringComparison.Ordinal) >= 0 ||
                    fullName.IndexOf("Construction_", StringComparison.Ordinal) >= 0)
                {
                    construction = true;
                    interesting = true;
                }
                if (fullName.IndexOf("SlotStorage", StringComparison.Ordinal) >= 0 ||
                    fullName.IndexOf("Interactive_STORAGE", StringComparison.Ordinal) >= 0)
                {
                    storage = true;
                    interesting = true;
                }
                if (fullName.IndexOf("SaveablePrefab", StringComparison.Ordinal) >= 0 ||
                    fullName.IndexOf("SaveableReference", StringComparison.Ordinal) >= 0)
                {
                    saveable = true;
                    interesting = true;
                }
                if (fullName.IndexOf("Structure", StringComparison.Ordinal) >= 0)
                {
                    structure = true;
                    interesting = true;
                }
                if (fullName.IndexOf("Raft", StringComparison.Ordinal) >= 0)
                {
                    raft = true;
                    interesting = true;
                }
                if (fullName.IndexOf("Zone", StringComparison.Ordinal) >= 0 ||
                    fullName.IndexOf("SaveContainer", StringComparison.Ordinal) >= 0)
                {
                    world = true;
                    interesting = true;
                }

                if (interesting)
                {
                    if (matches.Length > 0)
                    {
                        matches.Append(", ");
                    }
                    matches.Append(fullName);
                }
            }

            matchedTypes = matches.ToString();

            if (player) return "PLAYER";
            if (interactive) return "INTERACTIVE";
            if (storage) return "STORAGE";
            if (construction) return "CONSTRUCTION";
            if (raft) return "RAFT";
            if (structure) return "STRUCTURE";
            if (saveable) return "SAVEABLE";
            if (world) return "WORLD";
            return null;
        }
    }
}
