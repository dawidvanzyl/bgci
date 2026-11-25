namespace Api.Hosting;

public static class FrontendApplicationBuilderExtensions
{
	public static IApplicationBuilder UseFrontend(this IApplicationBuilder app)
	{
		app.Use((context, next) =>
		{
			if (ServesIndexPage(context))
				context.Request.Path = "/index.html";

			return next();
		});

		app.UseStaticFiles();

		return app;
	}

	private static bool ServesIndexPage(HttpContext context) =>
		context.GetEndpoint() is null
		&& (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
		&& !context.Request.Path.StartsWithSegments("/api")
		&& !Path.HasExtension(context.Request.Path.Value);
}