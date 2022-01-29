using AYSOScoreSheetGenerator.Lib;
using AYSOScoreSheetGenerator.Objects;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using GoogleSheetsHelper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddDebug();
builder.Logging.AddSignalRLogging();

// Add services to the container.
builder.Services
	.AddOptions()
	.AddMemoryCache()
	.AddRazorPages()
	.AddRazorRuntimeCompilation();

builder.Services.AddSignalR(options =>
{
	options.KeepAliveInterval = TimeSpan.FromMinutes(5);
});

if (builder.Environment.IsLocalhost())
{
	builder.Services
		.AddAuthentication(o =>
		{
			// This forces challenge results to be handled by Google OpenID Handler, so there's no
			// need to add an AccountController that emits challenges for Login.
			o.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
			// This forces forbid results to be handled by Google OpenID Handler, which checks if
			// extra scopes are required and does automatic incremental auth.
			o.DefaultForbidScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
			// Default scheme that will handle everything else.
			// Once a user is authenticated, the OAuth2 token info is stored in cookies.
			o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
		})
		.AddCookie()
		.AddGoogleOpenIdConnect(options =>
		{
			// when running on localhost, we have to use your personal Google account via OAuth; when running in Google Cloud Run, this uses the IAM role instead
			string? fileName = Environment.GetEnvironmentVariable("TEST_WEB_CLIENT_SECRET_FILENAME");
			string path = Path.Combine(builder.Environment.ContentRootPath, fileName);
			if (File.Exists(path))
			{
				var secrets = JObject.Parse(File.ReadAllText(path))["web"];
				options.ClientId = secrets["client_id"].Value<string>();
				options.ClientSecret = secrets["client_secret"].Value<string>();
			}
		});
}

// app-specific services
builder.Services
	.AddScoped<ISheetsClient, SheetsClientAdapter>(provider =>
	{
		// This is a hack: getting the GoogleCredential requires an async method which can't be used here, so instead of using a SheetsClient, we use an adapter class.
		// Then on page load of Index.cshtml we grab the credential and set it into cache. The adapter lazy loads the actual client when finally need to use it.
		IMemoryCache cache = provider.GetRequiredService<IMemoryCache>();
		GoogleCredential credential = cache.Get<GoogleCredential>(nameof(GoogleCredential));
		return new SheetsClientAdapter(credential, provider.GetRequiredService<ILogger<SheetsClient>>());
	})
	.AddSingleton(Options.Create(new ScoreSheetConfiguration())) // default; we'll replace this when start generating the file
	 //.Configure<ScoreSheetConfiguration>(builder.Configuration.GetSection("ScoreSheet")) // this would be used to load any config values that the user doesn't set
	;

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();	

app.MapDefaultControllerRoute();
app.MapRazorPages();
app.MapHub<LoggerHub>("/hub");

app.Run();
