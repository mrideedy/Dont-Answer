using System;
using System.Collections.Generic;
using UnityEngine;

namespace DontAnswer
{
    public enum PropKind { Cap, PoliceCap, PizzaBox, Badge, Blood, SkullMask, Mask, Glasses, Ribs, LostBoy }

    /// <summary>Which rigged model plays a role, how it is re-coloured, and what it carries.</summary>
    public sealed class Role
    {
        public readonly string[] models;   // one per group member; the last one repeats
        public readonly Func<VisitorLook, string, int, Color?> tint;   // (look, material, member) -> colour, or null to keep
        public readonly PropKind[] props;

        public Role(string[] models, Func<VisitorLook, string, int, Color?> tint, params PropKind[] props)
        {
            this.models = models;
            this.tint = tint;
            this.props = props;
        }

        public string Model(int member) => models[Mathf.Clamp(member, 0, models.Length - 1)];
    }

    /// <summary>The cast list: VisitorLook.modelKey -> role. Anything not listed (Leo) is built from primitives.</summary>
    public static class Casting
    {
        static readonly Color Dark = new Color(0.05f, 0.05f, 0.055f);
        static readonly Color NormalSkin = new Color(0.85f, 0.66f, 0.52f);
        public static readonly Color[] MaskColors = { new Color(0.95f, 0.45f, 0.06f), new Color(0.35f, 0.6f, 0.25f), new Color(0.92f, 0.9f, 0.84f) };
        static readonly Color[] KidShirts = { new Color(0.16f, 0.12f, 0.24f), new Color(0.16f, 0.08f, 0.2f), new Color(0.1f, 0.15f, 0.14f) };

        static readonly Dictionary<string, Role> roles = new Dictionary<string, Role>
        {
            ["marco"] = new Role(new[] { "Casual_Character" }, (l, m, i) => m switch
            {
                "LightBrown" => l.shirt, "LightBlue" => l.pants, "Red_Dark" => Dark, "Skin" => l.skin, "Skin_Darker" => l.skin * 0.88f,
                _ => (Color?)null
            }, PropKind.Cap, PropKind.PizzaBox),

            ["ana"] = new Role(new[] { "Hooded_Adventurer" }, (l, m, i) => m switch
            {
                "DarkBrown" => l.shirt, "Metal" => l.shirt * 0.85f, "LightBrown" => Dark, "Black" => l.pants, "Gold" => Dark,
                "White" => new Color(0.3f, 0.19f, 0.1f), "Brown" => new Color(0.14f, 0.09f, 0.06f), "Skin" => l.skin,
                _ => (Color?)null
            }),

            ["officer"] = new Role(new[] { "Business_Man" }, (l, m, i) => m switch
            {
                "Suit" => l.shirt, "White" => new Color(0.5f, 0.56f, 0.68f), "Tie" => Dark, "Black" => Dark,
                "Hair" => new Color(0.1f, 0.08f, 0.06f), "Skin" => l.skin,
                _ => (Color?)null
            }, PropKind.PoliceCap, PropKind.Badge),

            ["tom"] = new Role(new[] { "Punk" }, (l, m, i) => m switch
            {
                "Black" => Dark, "White" => new Color(0.07f, 0.07f, 0.08f), "LightBlue" => l.pants, "Skin" => NormalSkin,
                _ => (Color?)null
            }, PropKind.SkullMask, PropKind.Ribs),

            ["eli"] = new Role(new[] { "Hoodie_Character" }, (l, m, i) => m switch
            {
                "Purple" => l.shirt, "LightBlue" => l.pants, "Skin" => l.skin, "Hair" => l.hatColor,
                _ => (Color?)null
            }, PropKind.Blood),

            ["dad"] = new Role(new[] { "Adventurer" }, (l, m, i) => m switch
            {
                "Green" => l.shirt, "LightGreen" => new Color(0.24f, 0.2f, 0.15f), "Brown" => l.pants, "Brown2" => new Color(0.12f, 0.1f, 0.08f),
                "Hair" => l.hatColor, "Skin" => l.skin, "Gold" => new Color(0.3f, 0.26f, 0.18f), "Grey" => Dark,
                _ => (Color?)null
            }),

            ["voss"] = new Role(new[] { "Farmer" }, (l, m, i) => m switch
            {
                "Brown" => l.shirt, "LightBlue" => new Color(0.16f, 0.19f, 0.26f), "Eyebrows" => new Color(0.62f, 0.62f, 0.6f), "Skin" => l.skin,
                _ => (Color?)null
            }, PropKind.Glasses),

            ["hale"] = new Role(new[] { "Animated_Woman" }, (l, m, i) => m switch
            {
                "Red" => l.hatColor, "LimeGreen" => l.shirt, "Skin" => l.skin, "Brown" => new Color(0.4f, 0.4f, 0.4f), "Gold" => new Color(0.3f, 0.25f, 0.12f),
                _ => (Color?)null
            }, PropKind.Glasses),

            // Leo: a little boy in the tuxedo he went trick-or-treating in a year ago - bow tie, orange sneakers,
            // pumpkin bucket - and a year out in the rain on his face.
            ["leo"] = new Role(new[] { "Business_Man" }, (l, m, i) => m switch
            {
                "Suit" => l.shirt, "White" => new Color(0.88f, 0.88f, 0.85f), "Tie" => new Color(0.88f, 0.88f, 0.85f),   // a bow tie goes on instead
                "Black" => new Color(0.95f, 0.42f, 0.08f),   // his orange sneakers
                "Skin" => l.skin, "Hair" => l.hatColor, "Eyebrows" => new Color(0.12f, 0.08f, 0.05f), "Eye" => new Color(0.04f, 0.03f, 0.035f),
                _ => (Color?)null
            }, PropKind.LostBoy),

            ["masks"] = new Role(new[] { "Casual_Character", "Witch", "Hoodie_Character" }, (l, m, i) => m switch
            {
                "LightBrown" or "Purple" => i == 1 ? new Color(0.16f, 0.08f, 0.2f) : KidShirts[i % KidShirts.Length],
                "LightBlue" => Dark, "Gold" => new Color(0.35f, 0.26f, 0.1f),
                _ => (Color?)null
            }, PropKind.Mask),
        };

        public static Role Find(string key) => !string.IsNullOrEmpty(key) && roles.TryGetValue(key, out var r) ? r : null;
    }
}
