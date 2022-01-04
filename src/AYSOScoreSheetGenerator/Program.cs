using Microsoft.Extensions.Configuration;
using GoogleSheetsHelper;
using StandingsGoogleSheetsHelper;
using AYSOScoreSheetGenerator.Lib;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication.Cookies;
using Newtonsoft.Json.Linq;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using AYSOScoreSheetGenerator.Objects;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddDebug();
builder.Logging.AddSignalRLogging();

// Add services to the container.
builder.Services
	.AddOptions()
	.AddRazorPages()
	.AddRazorRuntimeCompilation();

builder.Services.AddSignalR();

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
	.AddScoped<ISheetsClient, SheetsClient>(provider =>
	{
		GoogleCredential credential;
		if (builder.Environment.IsLocalhost())
		{
			IGoogleAuthProvider auth = provider.GetRequiredService<IGoogleAuthProvider>();
			credential = auth.GetCredentialAsync().Result;
		}
		else
			credential = GoogleCredential.GetApplicationDefault();

		return new SheetsClient(credential, provider.GetRequiredService<ILogger<SheetsClient>>());
	})
	.AddSingleton(Options.Create(new ScoreSheetConfiguration())) // default; we'll replace this when start generating the file
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
app.UseEndpoints(endpoints => endpoints.MapHub<LoggerHub>("/hub"));

app.MapDefaultControllerRoute();
app.MapRazorPages();

app.Run();
