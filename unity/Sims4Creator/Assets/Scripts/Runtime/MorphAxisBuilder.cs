using System;
using System.Collections.Generic;

namespace Sims4Creator
{
    /// <summary>
    /// One slider AXIS built from the flat baked morph names. Antonym directions are paired onto a single
    /// bidirectional axis (+value drives <see cref="positive"/>, -value drives <see cref="negative"/>);
    /// a morph with no antonym is a one-directional 0..1 axis. Each axis carries a body AREA + sub-category
    /// and a head→toes order for the collapsible tree UI.
    /// </summary>
    [Serializable]
    public class MorphAxis
    {
        public string label;       // display, e.g. "Belly Big/Small" or "Eyes Far"
        public string area;        // "Head", "Torso", "Arms", "Legs", "Other"
        public string category;    // sub-area, e.g. "Belly", "Eyes"
        public int order;          // head→toes sort key
        public string baseName;    // axis identity (name minus the direction suffix)
        public int pairIndex = -1; // which antonym pair (so "Eyes Big/Small" ≠ "Eyes Far/Close")
        public string positive;    // morph (blend shape) name for the + side, or null
        public string negative;    // morph name for the - side, or null
        public float value;        // current: -1..1 when bidirectional, else 0..1

        public bool Bidirectional => !string.IsNullOrEmpty(positive) && !string.IsNullOrEmpty(negative);
    }

    /// <summary>
    /// Turns the flat morph-name list into ordered, categorized, paired <see cref="MorphAxis"/> entries.
    /// Pure name parsing — EA modifier names encode region (first token) + direction (suffix).
    /// </summary>
    public static class MorphAxisBuilder
    {
        // (positive suffix, negative suffix). Atomic direction words; Rotate*/Corner* are handled by the
        // trailing Up/Down/Out/In so "Eyes_RotateUp" pairs with "Eyes_RotateDown" (base "Eyes_Rotate").
        private static readonly (string Pos, string Neg)[] Pairs =
        {
            ("WideBone", "NarrowBone"), // before Wide/Narrow so Head_WideBone pairs with Head_NarrowBone
            ("Big", "Small"), ("Wide", "Narrow"), ("Long", "Short"), ("Thick", "Thin"),
            ("Heavy", "Lean"), ("Fit", "Bony"), ("Forward", "Backward"),
            ("High", "Low"), ("Up", "Down"), ("Out", "In"),
            ("Lift", "Droop"), ("Far", "Close"), ("Apart", "Together"),
            ("Extend", "Contract"), ("Square", "Round"),
        };

        // Sub-category (first name token) → (area, head→toes order). Unlisted → "Other" at the end.
        private static readonly (string Cat, string Area, int Order)[] CatTable =
        {
            ("Head", "Head", 0), ("Skull", "Head", 1), ("Forehead", "Head", 2),
            ("Brows", "Head", 3), ("Brow", "Head", 3), ("Eyes", "Head", 4), ("Eye", "Head", 4),
            ("Ears", "Head", 5), ("Ear", "Head", 5), ("Nose", "Head", 6),
            ("Cheeks", "Head", 7), ("Cheek", "Head", 7), ("Mouth", "Head", 8),
            ("Lips", "Head", 9), ("Lip", "Head", 9), ("Teeth", "Head", 10),
            ("Jaw", "Head", 11), ("Chin", "Head", 12),
            ("Neck", "Torso", 20), ("Shoulders", "Torso", 21), ("Shoulder", "Torso", 21),
            ("Chest", "Torso", 22), ("Breast", "Torso", 22), ("Bust", "Torso", 22),
            ("Body", "Torso", 23), ("Belly", "Torso", 24), ("Stomach", "Torso", 24),
            ("Back", "Torso", 25), ("Waist", "Torso", 26),
            ("Hips", "Torso", 27), ("Hip", "Torso", 27), ("Pelvis", "Torso", 28),
            ("Butt", "Torso", 29), ("Rear", "Torso", 29),
            ("UpperArm", "Arms", 40), ("LowerArm", "Arms", 41), ("Forearm", "Arms", 41),
            ("Arm", "Arms", 42), ("Wrist", "Arms", 43), ("Hands", "Arms", 44), ("Hand", "Arms", 44),
            ("Thighs", "Legs", 60), ("Thigh", "Legs", 60), ("Knee", "Legs", 61),
            ("LowerLeg", "Legs", 62), ("Calf", "Legs", 62), ("Leg", "Legs", 63),
            ("Ankle", "Legs", 64), ("Feet", "Legs", 65), ("Foot", "Legs", 65),
        };

        public static List<MorphAxis> Build(IReadOnlyList<string> names)
        {
            var byKey = new Dictionary<string, MorphAxis>(StringComparer.Ordinal);
            if (names != null)
            {
                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    Split(name, out var baseName, out var polarity, out var pairIndex);
                    var key = polarity == 0 ? "u:" + name : baseName + "|" + pairIndex;
                    if (!byKey.TryGetValue(key, out var axis))
                    {
                        axis = new MorphAxis { baseName = baseName, pairIndex = pairIndex };
                        var cat = baseName.Split('_')[0];
                        AreaFor(cat, out axis.area, out axis.category, out axis.order);
                        byKey[key] = axis;
                    }
                    if (polarity < 0) axis.negative = name;
                    else axis.positive = name; // +1 and 0 (no antonym) both ride the positive side
                }
            }

            foreach (var axis in byKey.Values)
            {
                if (axis.Bidirectional && axis.pairIndex >= 0)
                {
                    axis.label = Prettify(axis.baseName) + " " + Pairs[axis.pairIndex].Pos + "/" + Pairs[axis.pairIndex].Neg;
                }
                else
                {
                    var only = !string.IsNullOrEmpty(axis.positive) ? axis.positive : axis.negative;
                    axis.label = Prettify(only);
                }
            }

            var list = new List<MorphAxis>(byKey.Values);
            list.Sort(static (a, b) =>
            {
                var c = a.order.CompareTo(b.order);
                if (c != 0) return c;
                c = string.Compare(a.category, b.category, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private static void Split(string name, out string baseName, out int polarity, out int pairIndex)
        {
            for (var p = 0; p < Pairs.Length; p++)
            {
                if (EndsWithWord(name, Pairs[p].Pos)) { baseName = Strip(name, Pairs[p].Pos); polarity = +1; pairIndex = p; return; }
                if (EndsWithWord(name, Pairs[p].Neg)) { baseName = Strip(name, Pairs[p].Neg); polarity = -1; pairIndex = p; return; }
            }
            baseName = name; polarity = 0; pairIndex = -1;
        }

        // Suffix matches only at a CamelCase / underscore word boundary (so "Chin" never matches "In").
        private static bool EndsWithWord(string name, string suffix)
        {
            if (!name.EndsWith(suffix, StringComparison.Ordinal)) return false;
            var i = name.Length - suffix.Length;
            if (i <= 0) return false;
            var prev = name[i - 1];
            return char.IsLower(prev) || char.IsDigit(prev) || prev == '_';
        }

        private static string Strip(string name, string suffix) =>
            name.Substring(0, name.Length - suffix.Length).TrimEnd('_');

        private static void AreaFor(string cat, out string area, out string category, out int order)
        {
            foreach (var (c, a, o) in CatTable)
            {
                if (string.Equals(c, cat, StringComparison.OrdinalIgnoreCase)) { area = a; category = c; order = o; return; }
            }
            area = "Other"; category = string.IsNullOrEmpty(cat) ? "Misc" : cat; order = 900;
        }

        private static string Prettify(string baseName) => string.IsNullOrEmpty(baseName) ? "" : baseName.Replace('_', ' ');
    }
}
