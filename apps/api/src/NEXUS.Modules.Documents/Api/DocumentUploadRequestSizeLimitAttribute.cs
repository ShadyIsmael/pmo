using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NEXUS.Modules.Documents.Application;

namespace NEXUS.Modules.Documents.Api;

/// <summary>Aligns the request body ceiling with the configured file limit plus multipart framing.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DocumentUploadRequestSizeLimitAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var requestLimit = checked(DocumentUploadLimits.ResolveMaximumBytes(configuration) + DocumentUploadLimits.MultipartOverheadBytes);
        var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = requestLimit;

        await next();
    }
}
