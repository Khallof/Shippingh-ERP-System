using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShippingERP.Domain.Common;

namespace ShippingERP.Infrastructure.Persistence.Configurations.MasterData;

/// <summary>Column settings repeated by many master-data tables.</summary>
public static class MasterDataConfigurationExtensions
{
    /// <summary>
    /// Code (ASCII, unique when <paramref name="uniqueCode"/>), ArabicName (required), EnglishName (optional).
    /// Unique codes include soft-deleted rows on purpose: a code is never reused, so history stays unambiguous.
    /// </summary>
    public static void ConfigureCodeAndNames<T>(
        this EntityTypeBuilder<T> builder,
        System.Linq.Expressions.Expression<Func<T, string>> code,
        System.Linq.Expressions.Expression<Func<T, string>> arabicName,
        System.Linq.Expressions.Expression<Func<T, string?>> englishName,
        int codeLength,
        int nameLength,
        bool uniqueCode = true)
        where T : SoftDeletableEntity
    {
        builder.Property(code).HasMaxLength(codeLength).IsUnicode(false).IsRequired();
        builder.Property(arabicName).HasMaxLength(nameLength).IsRequired();
        builder.Property(englishName).HasMaxLength(nameLength);

        if (uniqueCode)
        {
            builder.HasIndex(PropertyName(code)).IsUnique();
        }

        builder.HasIndex(PropertyName(arabicName));
        builder.HasIndex(x => x.IsActive);
    }

    private static string PropertyName<T, TValue>(System.Linq.Expressions.Expression<Func<T, TValue>> expression)
    {
        return expression.Body is System.Linq.Expressions.MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("The expression must point to a property, e.g. x => x.Code.", nameof(expression));
    }
}
