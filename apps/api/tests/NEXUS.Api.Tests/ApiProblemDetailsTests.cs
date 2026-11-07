using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Api.Middleware;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests;

public sealed class ApiProblemDetailsTests
{
    [Fact]
    public void Domain_validation_error_preserves_code_field_and_safe_details()
    {
        var error = Error.BusinessValidation(
            "HANDOVER_DATE_AFTER_PROJECT_END",
            "The handover date must be on or before the project end date.",
            "EffectiveHandoverDate",
            new Dictionary<string, object?>
            {
                ["handoverDate"] = new DateTime(2026, 11, 5),
                ["projectEndDate"] = new DateTime(2026, 11, 4)
            });

        var problem = ApiProblemDetails.FromResponse(
            error,
            StatusCodes.Status400BadRequest,
            "/api/projects/1/handover-requests",
            "trace-123");

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("HANDOVER_DATE_AFTER_PROJECT_END", problem.Extensions["code"]);
        Assert.Equal("trace-123", problem.Extensions["traceId"]);
        Assert.Equal(
            "The handover date must be on or before the project end date.",
            problem.Detail);
        var fields = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal(
            "The handover date must be on or before the project end date.",
            Assert.Single(fields["EffectiveHandoverDate"]));
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(problem.Extensions["details"]);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, "VALIDATION_ERROR")]
    [InlineData(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED")]
    [InlineData(StatusCodes.Status403Forbidden, "FORBIDDEN")]
    [InlineData(StatusCodes.Status404NotFound, "NOT_FOUND")]
    [InlineData(StatusCodes.Status409Conflict, "CONFLICT")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, "BUSINESS_VALIDATION_ERROR")]
    [InlineData(StatusCodes.Status429TooManyRequests, "RATE_LIMITED")]
    [InlineData(StatusCodes.Status500InternalServerError, "INTERNAL_ERROR")]
    public void Status_class_is_exposed_as_a_stable_error_code(int statusCode, string expectedCode)
    {
        var problem = ApiProblemDetails.FromResponse(
            new { error = "SqlException: confidential database detail" },
            statusCode,
            "/api/resource",
            "trace-456");

        Assert.Equal(expectedCode, problem.Extensions["code"]);
        Assert.DoesNotContain("SqlException", problem.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidential database detail", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_problem_keeps_field_errors_and_uses_the_validation_code()
    {
        var source = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["name"] = ["A name is required."]
        });

        var problem = ApiProblemDetails.FromResponse(
            source,
            StatusCodes.Status400BadRequest,
            "/api/projects",
            "trace-789");

        Assert.Equal("VALIDATION_ERROR", problem.Extensions["code"]);
        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal("A name is required.", Assert.Single(errors["name"]));
    }

    [Fact]
    public void Technical_field_errors_are_replaced_with_safe_validation_guidance()
    {
        var source = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["budget"] = ["SqlException: private diagnostic"]
        });

        var problem = ApiProblemDetails.FromResponse(
            source,
            StatusCodes.Status400BadRequest,
            "/api/projects",
            "trace-field-diagnostics");

        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal(
            "Review the indicated fields and correct the values before submitting.",
            Assert.Single(errors["budget"]));
        Assert.DoesNotContain("private diagnostic", Assert.Single(errors["budget"]));
    }

    [Fact]
    public void Legacy_error_payload_preserves_field_name_and_safe_business_details()
    {
        var problem = ApiProblemDetails.FromResponse(
            new
            {
                error = "The budget must be greater than zero.",
                code = "BUDGET_MUST_BE_POSITIVE",
                fieldName = "budget",
                details = new { minimum = 0 }
            },
            StatusCodes.Status400BadRequest,
            "/api/projects",
            "trace-budget");

        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(problem.Extensions["errors"]);
        Assert.Equal("The budget must be greater than zero.", Assert.Single(errors["budget"]));
        var details = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(problem.Extensions["details"]);
        Assert.Equal(0, Assert.IsType<JsonElement>(details["minimum"]).GetInt32());
    }

    [Fact]
    public void Database_diagnostics_in_legacy_client_errors_are_replaced_with_safe_detail()
    {
        var problem = ApiProblemDetails.FromResponse(
            new { error = "The INSERT statement conflicted with the FOREIGN KEY constraint 'FK_Project_User'." },
            StatusCodes.Status400BadRequest,
            "/api/projects",
            "trace-diagnostics");

        Assert.Equal("Review the indicated fields and correct the values before submitting.", problem.Detail);
        Assert.DoesNotContain("FOREIGN KEY", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sensitive_and_technical_details_are_not_returned_to_clients()
    {
        var problem = ApiProblemDetails.FromResponse(
            new
            {
                code = "PROJECT_VALIDATION_FAILED",
                message = "Review the indicated project values.",
                details = new
                {
                    projectEndDate = "2026-11-04",
                    connectionString = "Server=private;Database=Secret",
                    diagnostic = "Microsoft.EntityFrameworkCore.DbUpdateException"
                }
            },
            StatusCodes.Status400BadRequest,
            "/api/projects",
            "trace-private");

        var details = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(problem.Extensions["details"]);
        Assert.Equal("2026-11-04", Assert.IsType<JsonElement>(details["projectEndDate"]).GetString());
        Assert.DoesNotContain("connectionString", details.Keys);
        Assert.DoesNotContain("diagnostic", details.Keys);
    }
}
