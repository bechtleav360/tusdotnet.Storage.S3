using Amazon.Runtime;
using Amazon.S3;
using AspNetCore_net6._0_TestApp;
using AspNetCore_net6._0_TestApp.Authentication;
using AspNetCore_net6._0_TestApp.Endpoints;
using AspNetCore_net6._0_TestApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Net;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Concatenation;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores.S3;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(
    options =>
    {
        options.ColorBehavior = LoggerColorBehavior.Enabled;
        options.SingleLine = true;
    });
builder.WebHost.ConfigureKestrel(kestrel => { kestrel.Limits.MaxRequestBodySize = null; });
builder.Services.AddOptions<S3Options>().BindConfiguration("S3");
builder.Services.AddSingleton<TusDiskStorageOptionHelper>();
builder.Services.AddSingleton(CreateTusConfiguration);
builder.Services.AddHostedService<ExpiredFilesCleanupService>();

AddAuthorization(builder);

WebApplication app = builder.Build();

app.UseAuthorization();
app.UseAuthentication();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();

// Handle downloads (must be set before MapTus)
app.MapGet("/files/{fileId}", DownloadFileEndpoint.HandleRoute);

// Setup tusdotnet for the /files/ path.
app.MapTus("/files/", TusConfigurationFactory);

app.Run();

static void AddAuthorization(WebApplicationBuilder builder)
{
    builder.Services.AddHttpContextAccessor();

    builder.Services.Configure<OnAuthorizeOption>(
        opt => opt.EnableOnAuthorize = (bool)builder.Configuration.GetValue(typeof(bool), "EnableOnAuthorize"));

    builder.Services.AddAuthorization(
        configure =>
        {
            configure.AddPolicy(
                "BasicAuthentication",
                policyBuilder =>
                {
                    policyBuilder.AddAuthenticationSchemes("BasicAuthentication");
                    policyBuilder.RequireAuthenticatedUser();
                });

            configure.DefaultPolicy = configure.GetPolicy("BasicAuthentication")!;
        });

    builder.Services.AddAuthentication("BasicAuthentication")
        .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
}

static TusS3Store CreateTusS3Store(IServiceProvider services)
{
    ILogger<Program> logger = services.GetRequiredService<ILogger<Program>>();
    IOptions<S3Options> options = services.GetRequiredService<IOptions<S3Options>>();

    var tusS3StoreConfig = new TusS3StoreConfiguration()
    {
        BucketName = options.Value.BucketName
    };

    var awsCredentials = new BasicAWSCredentials(options.Value.AccessKey, options.Value.SecretKey);

    AmazonS3Config config = new AmazonS3Config
    {
        // MUST set this before setting ServiceURL and it should match the `MINIO_REGION` environment variable
        AuthenticationRegion = options.Value.Region,
        // MUST be true to work correctly with MinIO server
        ServiceURL = options.Value.Endpoint,
        ForcePathStyle = options.Value.ForcePathStyle,
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED
    };

    return new TusS3Store(services.GetRequiredService<ILogger<TusS3Store>>(), tusS3StoreConfig, awsCredentials, config);
}

static DefaultTusConfiguration CreateTusConfiguration(IServiceProvider services)
{
    // Simplified configuration just for the ExpiredFilesCleanupService to show load order of configs.
    return new DefaultTusConfiguration
    {
        Store = CreateTusS3Store(services),
        MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
        UsePipelinesIfAvailable = true,
        // Set an expiration time, where incomplete files can no longer be updated.
        // This value can either be absolute or sliding.
        // Absolute expiration will be saved per file on create
        // Sliding expiration will be saved per file on create and updated on each patch/update.
        Expiration = new SlidingExpiration(TimeSpan.FromMinutes(1))
    };
}

static Task<DefaultTusConfiguration> TusConfigurationFactory(HttpContext httpContext)
{
    ILogger<Program> logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

    // Change the value of EnableOnAuthorize in appsettings.json to enable or disable
    // the new authorization event.
    bool enableAuthorize = httpContext.RequestServices.GetRequiredService<IOptions<OnAuthorizeOption>>()
        .Value.EnableOnAuthorize;

    var config = new DefaultTusConfiguration
    {
        Store = CreateTusS3Store(httpContext.RequestServices),
        MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
        UsePipelinesIfAvailable = true,
        Events = new Events
        {
            OnAuthorizeAsync = ctx =>
            {
                // Note: This event is called even if RequireAuthorization is called on the endpoint.
                // In that case this event is not required but can be used as fine-grained authorization control.
                // This event can also be used as a "on request started" event to prefetch data or similar.

                if (!enableAuthorize)
                    return Task.CompletedTask;

                if (ctx.HttpContext.User.Identity?.IsAuthenticated != true)
                {
                    ctx.HttpContext.Response.Headers.Add(
                        "WWW-Authenticate",
                        new StringValues("Basic realm=tusdotnet-test-net6.0"));

                    ctx.FailRequest(HttpStatusCode.Unauthorized);

                    return Task.CompletedTask;
                }

                if (ctx.HttpContext.User.Identity.Name != "test")
                {
                    ctx.FailRequest(HttpStatusCode.Forbidden, "'test' is the only allowed user");

                    return Task.CompletedTask;
                }

                // Do other verification on the user; claims, roles, etc.

                // Verify different things depending on the intent of the request.
                // E.g.:
                //   Does the file about to be written belong to this user?
                //   Is the current user allowed to create new files or have they reached their quota?
                //   etc etc
                switch (ctx.Intent)
                {
                    case IntentType.CreateFile:
                        break;
                    case IntentType.ConcatenateFiles:
                        break;
                    case IntentType.WriteFile:
                        break;
                    case IntentType.DeleteFile:
                        break;
                    case IntentType.GetFileInfo:
                        break;
                    case IntentType.GetOptions:
                        break;
                    default:
                        break;
                }

                return Task.CompletedTask;
            },

            OnBeforeCreateAsync = ctx =>
            {
                // Partial files are not complete so we do not need to validate
                // the metadata in our example.
                if (ctx.FileConcatenation is FileConcatPartial)
                {
                    return Task.CompletedTask;
                }

                if (!ctx.Metadata.ContainsKey("name") || ctx.Metadata["name"].HasEmptyValue)
                {
                    ctx.FailRequest("name metadata must be specified. ");
                }

                if (!ctx.Metadata.ContainsKey("contentType") || ctx.Metadata["contentType"].HasEmptyValue)
                {
                    ctx.FailRequest("contentType metadata must be specified. ");
                }

                return Task.CompletedTask;
            },
            OnCreateCompleteAsync = ctx =>
            {
                logger.LogInformation($"Created file {ctx.FileId} using {ctx.Store.GetType().FullName}");

                return Task.CompletedTask;
            },
            OnBeforeDeleteAsync = _ => Task.CompletedTask,
            OnDeleteCompleteAsync = ctx =>
            {
                logger.LogInformation($"Deleted file {ctx.FileId} using {ctx.Store.GetType().FullName}");

                return Task.CompletedTask;
            },
            OnFileCompleteAsync = ctx =>
            {
                logger.LogInformation($"Upload of {ctx.FileId} completed using {ctx.Store.GetType().FullName}");

                // If the store implements ITusReadableStore one could access the completed file here.
                // The default TusDiskStore implements this interface:
                //var file = await ctx.GetFileAsync();
                return Task.CompletedTask;
            }
        },
        // Set an expiration time where incomplete files can no longer be updated.
        // This value can either be absolute or sliding.
        // Absolute expiration will be saved per file on create
        // Sliding expiration will be saved per file on create and updated on each patch/update.
        Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(1))
    };

    return Task.FromResult(config);
}
