namespace Max35050.Helper;

public static class UnicodeSanitizer
{
    /*
    Number    Name                        HTML Code    Appearance
    ------    ------------------------    ---------    ----------
    U+2000    En Quad                     &#8192;      " "
    U+2001    Em Quad                     &#8193;      " "
    U+2002    En Space                    &#8194;      " "
    U+2003    Em Space                    &#8195;      " "
    U+2004    Three-Per-Em Space          &#8196;      " "
    U+2005    Four-Per-Em Space           &#8197;      " "
    U+2006    Six-Per-Em Space            &#8198;      " "
    U+2007    Figure Space                &#8199;      " "
    U+2008    Punctuation Space           &#8200;      " "
    U+2009    Thin Space                  &#8201;      " "
    U+200A    Hair Space                  &#8202;      " "
    U+200B    Zero-Width Space            &#8203;      "​"
    U+200C    Zero Width Non-Joiner       &#8204;      "‌"
    U+200D    Zero Width Joiner           &#8205;      "‍"
    U+200E    Left-To-Right Mark          &#8206;      "‎"
    U+200F    Right-To-Left Mark          &#8207;      "‏"
    U+202F    Narrow No-Break Space       &#8239;      " "
    U+00AD    Soft Hyphen                 &#173;       "­"
    U+034F    Combining Grapheme Joiner   &#847;       "͏"
    U+00A0    Non-Breaking Space          &#160;       " "
    U+061C    Arabic Letter Mark          &#1644;      "؜"
    U+070F    Syriac Abbreviation Mark    &#1807;      "܏"
    U+115F    Hangul Choseong Filler      &#4447;      "ᅟ"
    U+1160    Hangul Jungseong Filler     &#4448;      "ᅠ"
    U+1680    Ogham Space Mark            &#5760;      " "
    U+17B4    Khmer Vowel Inherent AQ     &#6068;      "឴"
    U+17B5    Khmer Vowel Inherent AA     &#6069;      "឵"
    U+180E    Mongolian Vowel Separator   &#6158;      "᠎"
    U+205F    Medium Mathematical Space   &#8287;      " "
    U+2060    Word Joiner                 &#8288;      "⁠"
    U+2061    Function Application        &#8289;      "⁡"
    U+2062    Invisible Times             &#8290;      "⁢"
    U+2063    Invisible Separator         &#8291;      "⁣"
    U+2064    Invisible Plus              &#8292;      "⁤"
    U+206A    Inhibit Symmetric Swapping  &#8298;      "⁦"
    U+206B    Activate Symmetric Swapping &#8299;      "⁧"
    U+206C    Inhibit Arabic Form Shaping &#8300;      "
    U+206D    Activate Arabic Form Shaping &#8301;      "⁭"
    U+206E    National Digit Shapes       &#8302;      "⁮"
    U+206F    Nominal Digit Shapes        &#8303;      "⁯"
    U+3000    Ideographic Space           &#12288;     "　"
    U+2800    Braille Pattern Blank       &#10240;     "⠀"
    U+3164    Hangul Filler               &#12644;     "ㅤ"
    U+FEFF    Zero Width No-Break Space   &#65279;     "﻿"
    U+FFA0    Halfwidth Hangul Filler     &#65440;     "ﾠ"
    */
    public static (string, bool) SanitizeString(string input)
    {
        // Remove common invisible Unicode characters
        string[] invisibleChars = new string[]
        {
            "\u2000", // En Quad
            "\u2001", // Em Quad
            "\u2002", // En Space
            "\u2003", // Em Space
            "\u2004", // Three-Per-Em Space
            "\u2005", // Four-Per-Em Space
            "\u2006", // Six-Per-Em Space
            "\u2007", // Figure Space
            "\u2008", // Punctuation Space
            "\u2009", // Thin Space
            "\u200A", // Hair Space
            "\u200B", // Zero-Width Space
            "\u200C", // Zero Width Non-Joiner
            "\u200D", // Zero Width Joiner
            "\u200E", // Left-To-Right Mark
            "\u200F", // Right-To-Left Mark
            "\u202F",  // Narrow No-Break Space
            "\u00AD", // Soft Hyphen
            "\u034F", // Combining Grapheme Joiner
            "\u00A0", // Non-Breaking Space
            "\u061C", // Arabic Letter Mark
            "\u070F", // Syriac Abbreviation Mark
            "\u115F", // Hangul Choseong Filler
            "\u1160",  // Hangul Jungseong Filler
            "\u1680", // Ogham Space Mark
            "\u17B4", // Khmer Vowel Inherent AQ
            "\u17B5", // Khmer Vowel Inherent AA
            "\u180E",  // Mongolian Vowel Separator
            "\u205F", // Medium Mathematical Space
            "\u2060", // Word Joiner
            "\u2061", // Function Application
            "\u2062", // Invisible Times
            "\u2063", // Invisible Separator
            "\u2064", // Invisible Plus
            "\u206A", // Inhibit Symmetric Swapping
            "\u206B", // Activate Symmetric Swapping
            "\u206C", // Inhibit Arabic Form Shaping
            "\u206D", // Activate Arabic Form Shaping
            "\u206E", // National Digit Shapes
            "\u206F", // Nominal Digit Shapes
            "\u3000", // Ideographic Space
            "\u2800", // Braille Pattern Blank
            "\u3164", // Hangul Filler
            "\uFEFF", // Zero Width No-Break Space
            "\uFFA0",  // Halfwidth Hangul Filler
        };
        bool foundInvisible = false;
        foreach (var ch in invisibleChars)
        {
            if (input.Contains(ch))
            {
                foundInvisible = true;
                input = input.Replace(ch, "");
            }
        }

        return (input, foundInvisible);
    }
}