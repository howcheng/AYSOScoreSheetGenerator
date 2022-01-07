using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using AYSOScoreSheetGenerator.Lib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4;

namespace AYSOScoreSheetGenerator.Pages
{
	[GoogleScopedAuthorize(SheetsService.ScopeConstants.DriveFile)]
	public class IndexModel : PageModel
	{
		private readonly IGoogleAuthProvider _authProvider;
		private readonly IHostEnvironment _host;
		private readonly IMemoryCache _memoryCache;

		public IndexModel(IGoogleAuthProvider authProvider, IHostEnvironment host, IMemoryCache cache)
		{
			_authProvider = authProvider;
			_host = host;
			_memoryCache = cache;
		}

		public async Task OnGet()
		{
			_ = await _memoryCache.GetOrCreateAsync(nameof(GoogleCredential), async (cacheEntry) =>
			{
				cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
				var credential = _host.IsLocalhost() ? await _authProvider.GetCredentialAsync() : await GoogleCredential.GetApplicationDefaultAsync();
				return credential;
			});
		}
	}
}