using System.Collections.Generic;

namespace Kyalio.VRKeyboard
{
    /// <summary>Which keyboard a field gets.</summary>
    public enum KeyboardLayoutKind
    {
        /// <summary>Letters + symbols page (passwords, names, free text).</summary>
        Text,
        /// <summary>As Text, with @ and .com keys on the letters page.</summary>
        Email,
        /// <summary>Digits only, larger keys (codes, PINs).</summary>
        Numeric,
    }

    internal enum KeyAction
    {
        /// <summary>Types <see cref="KeyDef.Value"/> (a letter follows Shift).</summary>
        Insert,
        Backspace,
        Shift,
        /// <summary>Switches to the page named by <see cref="KeyDef.Value"/>.</summary>
        Page,
        /// <summary>Closes the keyboard (deselects the field).</summary>
        Done,
    }

    internal readonly struct KeyDef
    {
        public readonly KeyAction Action;
        public readonly string Value;   // text to insert, or target page
        public readonly string Label;   // null: show Value
        public readonly float Width;    // in key units (1 = a letter key)

        public KeyDef(KeyAction action, string value, string label, float width)
        {
            Action = action;
            Value = value;
            Label = label;
            Width = width;
        }

        public bool IsLetter => Action == KeyAction.Insert && Value != null && Value.Length == 1 &&
                                char.IsLetter(Value[0]);
    }

    internal sealed class KeyboardPage
    {
        public readonly string Name;
        public readonly List<KeyDef[]> Rows;
        /// <summary>Widest a key unit may get relative to its height (stops a 3-key pad stretching).</summary>
        public readonly float MaxUnitAspect;

        public KeyboardPage(string name, float maxUnitAspect, params KeyDef[][] rows)
        {
            Name = name;
            MaxUnitAspect = maxUnitAspect;
            Rows = new List<KeyDef[]>(rows);
        }
    }

    /// <summary>
    /// The built-in layouts. Each kind has a start page; Text and Email switch between a
    /// letters page and a symbols page with the 123 / ABC key.
    /// </summary>
    internal static class KeyboardLayouts
    {
        public const string LettersPage = "letters";
        public const string SymbolsPage = "symbols";
        public const string MorePage = "more";
        public const string NumericPage = "numeric";

        private static KeyDef K(string value, float width = 1f) =>
            new KeyDef(KeyAction.Insert, value, null, width);

        private static KeyDef[] Chars(string chars)
        {
            var row = new KeyDef[chars.Length];
            for (int i = 0; i < chars.Length; i++) row[i] = K(chars[i].ToString());
            return row;
        }

        private static KeyDef[] Row(params object[] parts)
        {
            var row = new List<KeyDef>();
            foreach (var part in parts)
            {
                if (part is string s) row.AddRange(Chars(s));
                else if (part is KeyDef k) row.Add(k);
            }
            return row.ToArray();
        }

        private static readonly KeyDef Backspace = new(KeyAction.Backspace, null, null, 1.5f);
        private static readonly KeyDef Shift     = new(KeyAction.Shift, null, null, 1.5f);
        private static readonly KeyDef Done      = new(KeyAction.Done, null, null, 2f);
        private static readonly KeyDef Space     = new(KeyAction.Insert, " ", null, 5f);
        private static readonly KeyDef ToSymbols = new(KeyAction.Page, SymbolsPage, "123", 1.5f);
        private static readonly KeyDef ToLetters = new(KeyAction.Page, LettersPage, "ABC", 1.5f);
        private static readonly KeyDef ToMore    = new(KeyAction.Page, MorePage, "#+=", 1.5f);
        private static readonly KeyDef BackTo123 = new(KeyAction.Page, SymbolsPage, "123", 1.5f);

        private static KeyboardPage Letters(bool email) => new(LettersPage, 1.15f,
            Row("qwertyuiop", Backspace),
            Row("asdfghjkl"),
            Row(Shift, "zxcvbnm-_"),
            // No space key for email (addresses never contain one, and a wide space bar
            // next to @ and . is easy to hit with a ray).
            email
                ? Row(ToSymbols, new KeyDef(KeyAction.Insert, "@", null, 1.5f),
                      new KeyDef(KeyAction.Insert, ".", null, 1.5f),
                      new KeyDef(KeyAction.Insert, ".com", null, 2f), Done)
                : Row(ToSymbols, Space, K("."), Done));

        private static KeyboardPage Symbols() => new(SymbolsPage, 1.15f,
            Row("1234567890", Backspace),
            Row("!@#$%&*()?"),
            Row(ToMore, "-_+=/:;,'\""),
            Row(ToLetters, Space, K("."), Done));

        // The remaining printable ASCII symbols, so any password can be typed.
        private static KeyboardPage More() => new(MorePage, 1.15f,
            Row("[]{}<>^~`|", Backspace),
            Row("\\\"'.,?!:;"),
            Row(BackTo123, "-_@#$%&*"),
            Row(ToLetters, Space, K("."), Done));

        private static KeyboardPage Numeric() => new(NumericPage, 2.2f,
            Row("123"),
            Row("456"),
            Row("789"),
            Row(new KeyDef(KeyAction.Done, null, null, 1f), K("0"),
                new KeyDef(KeyAction.Backspace, null, null, 1f)));

        /// <summary>The pages of a layout kind; the first is the start page.</summary>
        public static Dictionary<string, KeyboardPage> PagesFor(KeyboardLayoutKind kind, out string startPage)
        {
            var pages = new Dictionary<string, KeyboardPage>();
            switch (kind)
            {
                case KeyboardLayoutKind.Numeric:
                    pages[NumericPage] = Numeric();
                    startPage = NumericPage;
                    break;
                default:
                    pages[LettersPage] = Letters(kind == KeyboardLayoutKind.Email);
                    pages[SymbolsPage] = Symbols();
                    pages[MorePage] = More();
                    startPage = LettersPage;
                    break;
            }
            return pages;
        }
    }
}
