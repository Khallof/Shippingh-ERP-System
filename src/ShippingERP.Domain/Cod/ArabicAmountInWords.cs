using System.Globalization;

namespace ShippingERP.Domain.Cod;

/// <summary>
/// Writes an amount in Arabic words for printed vouchers (التفقيط), e.g.
/// 125500 SYP -> "فقط مائة وخمسة وعشرون ألفاً وخمسمائة ليرة سورية لا غير".
///
/// Numbers are written in the masculine counting form commonly used on Syrian cash vouchers; the fraction of
/// currencies with decimals is written as digits ("و 50/100"). Supports up to 999,999,999,999.
/// </summary>
public static class ArabicAmountInWords
{
    private static readonly string[] Units =
    {
        string.Empty, "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة",
        "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"
    };

    private static readonly string[] Tens =
    {
        string.Empty, string.Empty, "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"
    };

    private static readonly string[] Hundreds =
    {
        string.Empty, "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"
    };

    /// <summary>Scale words: singular, dual, plural (3-10), accusative singular (11-99).</summary>
    private static readonly (string One, string Two, string Plural, string Accusative)[] Scales =
    {
        ("ألف", "ألفان", "آلاف", "ألفاً"),
        ("مليون", "مليونان", "ملايين", "مليوناً"),
        ("مليار", "ملياران", "مليارات", "ملياراً")
    };

    /// <summary>The full printed sentence with the currency name.</summary>
    public static string Format(decimal amount, string currencyName, int decimals)
    {
        var value = Math.Abs(Math.Round(amount, Math.Clamp(decimals, 0, 4), MidpointRounding.AwayFromZero));
        var integer = (long)Math.Truncate(value);
        var words = ToWords(integer);

        var fraction = value - integer;
        var fractionText = string.Empty;
        if (decimals > 0 && fraction > 0m)
        {
            var scale = (long)Math.Pow(10, decimals);
            var digits = (long)Math.Round(fraction * scale, MidpointRounding.AwayFromZero);
            fractionText = string.Create(CultureInfo.InvariantCulture, $" و {digits}/{scale}");
        }

        var sign = amount < 0m ? "سالب " : string.Empty;
        return $"فقط {sign}{words}{fractionText} {currencyName} لا غير";
    }

    /// <summary>The number in words (0 -> "صفر").</summary>
    public static string ToWords(long number)
    {
        if (number == 0)
        {
            return "صفر";
        }

        if (number < 0 || number > 999_999_999_999)
        {
            return number.ToString("N0", CultureInfo.InvariantCulture);
        }

        var parts = new List<string>();
        var groups = new[]
        {
            (int)(number / 1_000_000_000 % 1000),
            (int)(number / 1_000_000 % 1000),
            (int)(number / 1000 % 1000),
            (int)(number % 1000)
        };

        for (var i = 0; i < 3; i++)
        {
            var group = groups[i];
            if (group == 0)
            {
                continue;
            }

            parts.Add(ScaledGroup(group, Scales[2 - i]));
        }

        if (groups[3] > 0)
        {
            parts.Add(Group(groups[3]));
        }

        return string.Join(" و", parts);
    }

    private static string ScaledGroup(int group, (string One, string Two, string Plural, string Accusative) scale)
    {
        if (group == 1)
        {
            return scale.One;
        }

        if (group == 2)
        {
            return scale.Two;
        }

        var lastTwo = group % 100;
        if (group < 100)
        {
            return group <= 10 ? $"{Group(group)} {scale.Plural}" : $"{Group(group)} {scale.Accusative}";
        }

        // 100 and above: the noun follows the last two digits.
        var noun = lastTwo switch
        {
            0 => scale.One,
            <= 2 => scale.One,
            <= 10 => scale.Plural,
            _ => scale.Accusative
        };

        return $"{Group(group)} {noun}";
    }

    /// <summary>1..999 in words.</summary>
    private static string Group(int value)
    {
        var hundreds = value / 100;
        var rest = value % 100;
        var parts = new List<string>();

        if (hundreds > 0)
        {
            parts.Add(Hundreds[hundreds]);
        }

        if (rest > 0)
        {
            if (rest < 20)
            {
                parts.Add(Units[rest]);
            }
            else
            {
                var unit = rest % 10;
                var ten = rest / 10;
                parts.Add(unit == 0 ? Tens[ten] : $"{Units[unit]} و{Tens[ten]}");
            }
        }

        return string.Join(" و", parts);
    }
}
