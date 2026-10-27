using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DontAnswer
{
    /// <summary>
    /// Every spoken line in the game, loaded from Assets/_Game/Data/Lines.csv.
    /// Voice clips are optional: Resources/Voice/&lt;ID&gt; is used when it exists, otherwise the game
    /// falls back to synthesized mumbles so everything works before the voices are recorded.
    /// </summary>
    public static class LineBank
    {
        public class Line
        {
            public string id, speaker, voice, variant, text;
            public bool IsMimic => variant == "mimic";
        }

        static Dictionary<string, Line> lines;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> missing = new HashSet<string>();

        public static void Load(TextAsset csv)
        {
            lines = new Dictionary<string, Line>();
            if (csv == null) return;
            var rows = ParseCsv(csv.text);
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.Count < 5 || string.IsNullOrEmpty(r[0])) continue;
                lines[r[0]] = new Line { id = r[0], speaker = r[1], voice = r[2], variant = r[3], text = r[4] };
            }
        }

        public static bool Has(string id) => lines != null && id != null && lines.ContainsKey(id);

        public static Line Get(string id)
        {
            if (lines != null && id != null && lines.TryGetValue(id, out var l)) return l;
            return new Line { id = id, speaker = "", voice = "", variant = "human", text = id ?? "" };
        }

        public static string Text(string id) => Get(id).text;

        /// <summary>The recorded clip for a line, or null if it hasn't been generated yet.</summary>
        public static AudioClip Clip(string id)
        {
            if (string.IsNullOrEmpty(id) || missing.Contains(id)) return null;
            if (clips.TryGetValue(id, out var c)) return c;
            c = Resources.Load<AudioClip>("Voice/" + id);
            if (c == null) { missing.Add(id); return null; }
            clips[id] = c;
            return c;
        }

        static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(ch);
                    continue;
                }
                switch (ch)
                {
                    case '"': quoted = true; break;
                    case ',': row.Add(cell.ToString()); cell.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(cell.ToString()); cell.Clear();
                        rows.Add(row); row = new List<string>();
                        break;
                    default: cell.Append(ch); break;
                }
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
