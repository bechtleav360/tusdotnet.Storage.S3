﻿using System.Text;
using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace AspNetCore_TestApp.Endpoints;

public static class DownloadFileEndpoint
{
    public static async Task HandleRoute(HttpContext context)
    {
        var config = context.RequestServices.GetRequiredService<DefaultTusConfiguration>();

        if (config.Store is not ITusReadableStore store)
        {
            return;
        }

        var fileId = (string?)context.Request.RouteValues["fileId"];
        var file = await store.GetFileAsync(fileId, context.RequestAborted);

        if (file == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"File with id {fileId} was not found.", context.RequestAborted);

            return;
        }

        var fileStream = await file.GetContentAsync(context.RequestAborted);
        var metadata = await file.GetMetadataAsync(context.RequestAborted);

        context.Response.ContentType = GetContentTypeOrDefault(metadata);

        if (fileStream.CanSeek)
        {
            context.Response.ContentLength = fileStream.Length;
        }

        if (metadata.TryGetValue("name", out Metadata? nameMeta))
        {
            context.Response.Headers.Append(
                "Content-Disposition",
                new[] { $"attachment; filename=\"{nameMeta.GetString(Encoding.UTF8)}\"" });
        }

        await using (fileStream)
        {
            await fileStream.CopyToAsync(context.Response.Body, 81920, context.RequestAborted);
        }
    }

    private static string GetContentTypeOrDefault(Dictionary<string, Metadata> metadata)
    {
        if (metadata.TryGetValue("contentType", out var contentType))
        {
            return contentType.GetString(Encoding.UTF8);
        }

        return "application/octet-stream";
    }
}
