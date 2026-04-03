using System.Web;
using Asp.Versioning.ApiExplorer;
using AspNetCore.Swagger.Themes;
using Microsoft.AspNetCore.HttpOverrides;
using NoMercyBot.Api.Middleware;
using NoMercyBot.Globals.Information;
using NoMercyBot.Globals.SystemCalls;
using NoMercyBot.Services.Widgets;

namespace NoMercyBot.Server.AppConfig;

public static class ApplicationConfiguration
{
    public static void ConfigureApp(
        IApplicationBuilder app,
        IApiVersionDescriptionProvider provider
    )
    {
        ConfigureLocalization(app);
        ConfigureMiddleware(app);
        ConfigureSwaggerUi(app, provider);
        ConfigureWebSockets(app);
        ConfigureStaticFiles(app);

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<WidgetHub>("/hubs/widgets");
        });
    }

    private static void ConfigureLocalization(IApplicationBuilder app)
    {
        string[] supportedCultures = ["en-US", "nl-NL"]; // Add other supported locales
        RequestLocalizationOptions localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        localizationOptions.FallBackToParentCultures = true;
        localizationOptions.FallBackToParentUICultures = true;

        app.UseRequestLocalization(localizationOptions);
    }

    private static void ConfigureMiddleware(IApplicationBuilder app)
    {
        app.UseForwardedHeaders(
            new()
            {
                ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            }
        );

        app.UseRateLimiter();

        app.UseRouting();
        app.UseCors("VueAppPolicy");

        app.UseHsts();
        app.UseHttpsRedirection();
        app.UseResponseCompression();
        app.UseRequestLocalization();

        app.UseMiddleware<LocalizationMiddleware>();
        app.UseMiddleware<TokenParamAuthMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        app.Use(
            async (context, next) =>
            {
                string path = HttpUtility.UrlDecode(context.Request.Path);
                Logger.Http($"Request: {context.Request.Method} {path}");

                if (
                    !Config.Swagger
                    && (
                        context.Request.Path.StartsWithSegments("/swagger")
                        || context.Request.Path.StartsWithSegments("/index.html")
                    )
                )
                {
                    context.Response.StatusCode = StatusCodes.Status410Gone;
                    await context.Response.WriteAsync("Swagger is disabled.");
                    return;
                }

                await next();
            }
        );
        IWebHostEnvironment env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        if (env.IsDevelopment())
            app.UseDeveloperExceptionPage();
    }

    private static void ConfigureSwaggerUi(
        IApplicationBuilder app,
        IApiVersionDescriptionProvider provider
    )
    {
        app.UseSwagger();
        app.UseSwaggerUI(
            ModernStyle.Dark,
            options =>
            {
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "NoMercyBot API";
                options.EnableTryItOutByDefault();

                IReadOnlyList<ApiVersionDescription> descriptions = provider.ApiVersionDescriptions;
                foreach (ApiVersionDescription description in descriptions)
                {
                    string url = $"/swagger/{description.GroupName}/swagger.json";
                    string name = description.GroupName.ToUpperInvariant();
                    options.SwaggerEndpoint(url, name);
                }
            }
        );

        app.UseMvcWithDefaultRoute();
    }

    private static void ConfigureWebSockets(IApplicationBuilder app)
    {
        app.UseWebSockets()
            .UseEndpoints(endpoints =>
            {
                // endpoints.MapHub<ChatHub>("/dashboardHub", options =>
                // {
                //     options.Transports = HttpTransportType.WebSockets;
                //     options.TransportSendTimeout = TimeSpan.FromSeconds(40);
                //     options.CloseOnAuthenticationExpiration = true;
                // });
            });
    }

    private static void ConfigureStaticFiles(IApplicationBuilder app)
    {
        // app.UseStaticFiles(new StaticFileOptions
        // {
        //     FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
        //     RequestPath = new("/transcode"),
        //     ServeUnknownFileTypes = true,
        //     HttpsCompression = HttpsCompressionMode.Compress
        // });
        //
        // app.UseDirectoryBrowser(new DirectoryBrowserOptions
        // {
        //     FileProvider = new PhysicalFileProvider(AppFiles.TranscodePath),
        //     RequestPath = new("/transcode")
        // });
    }
}
