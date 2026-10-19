using ShippingERP.Application.Common.Models;

namespace ShippingERP.Tests.Unit;

public sealed class ApiResponseTests
{
    [Fact]
    public void Ok_SetsSuccessAndData_AndHasNoError()
    {
        var response = ApiResponse<int>.Ok(42, traceId: "trace-1");

        Assert.True(response.Success);
        Assert.Equal(42, response.Data);
        Assert.Null(response.Error);
        Assert.Equal("trace-1", response.TraceId);
        Assert.Equal(ApiResponse<int>.DefaultSuccessMessage, response.Message);
        Assert.Equal(DateTimeKind.Utc, response.TimestampUtc.Kind);
    }

    [Fact]
    public void Fail_SetsErrorCodeAndDetails()
    {
        var details = new Dictionary<string, string[]>
        {
            ["phoneNumber"] = new[] { "رقم الهاتف مطلوب." }
        };

        var response = ApiResponse<object>.Fail(ErrorCodes.ValidationError, "البيانات المدخلة غير صحيحة.", details, "trace-2");

        Assert.False(response.Success);
        Assert.Null(response.Data);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.ValidationError, response.Error!.Code);
        Assert.Equal("رقم الهاتف مطلوب.", response.Error.Details!["phoneNumber"][0]);
        Assert.Equal("trace-2", response.TraceId);
    }
}
