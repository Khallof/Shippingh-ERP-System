using Microsoft.AspNetCore.Identity;

namespace ShippingERP.Infrastructure.Identity;

/// <summary>
/// Arabic messages for ASP.NET Core Identity errors (weak password, duplicate user name...).
/// The Code values stay the same as Identity's, so code can still check them.
/// </summary>
public sealed class ArabicIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "حدث خطأ غير معروف.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "تم تعديل هذا السجل من قبل مستخدم آخر. يرجى المحاولة مرة أخرى.");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "كلمة المرور غير صحيحة.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), $"اسم المستخدم '{userName}' غير صالح.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), $"البريد الإلكتروني '{email}' غير صالح.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), $"اسم المستخدم '{userName}' مستخدم من قبل.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), $"البريد الإلكتروني '{email}' مستخدم من قبل.");

    public override IdentityError InvalidRoleName(string? role) =>
        Error(nameof(InvalidRoleName), $"اسم الدور '{role}' غير صالح.");

    public override IdentityError DuplicateRoleName(string role) =>
        Error(nameof(DuplicateRoleName), $"الدور '{role}' موجود من قبل.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Error(nameof(UserAlreadyInRole), $"المستخدم لديه الدور '{role}' مسبقاً.");

    public override IdentityError UserNotInRole(string role) =>
        Error(nameof(UserNotInRole), $"المستخدم ليس لديه الدور '{role}'.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"يجب ألا تقل كلمة المرور عن {length} أحرف.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "يجب أن تحتوي كلمة المرور على رمز مثل ! @ # $.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "يجب أن تحتوي كلمة المرور على رقم (0-9).");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "يجب أن تحتوي كلمة المرور على حرف إنجليزي صغير (a-z).");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "يجب أن تحتوي كلمة المرور على حرف إنجليزي كبير (A-Z).");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"يجب أن تحتوي كلمة المرور على {uniqueChars} أحرف مختلفة على الأقل.");

    private static IdentityError Error(string code, string description) =>
        new() { Code = code, Description = description };
}
