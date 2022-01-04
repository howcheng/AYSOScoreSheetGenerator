namespace AYSOScoreSheetGenerator.Lib
{
	public static class IHostEnvironmentExtensions
	{
		/// <summary>
		/// determines if the application is running on localhost
		/// </summary>
		/// <param name="environment"></param>
		/// <returns></returns>
		public static bool IsLocalhost(this IHostEnvironment environment)
		{
			string? subenv = Environment.GetEnvironmentVariable("ASPNETCORE_SUBENVIRONMENT");
			if (!string.IsNullOrEmpty(subenv)) //https://thomaslevesque.com/2019/12/20/asp-net-core-when-environments-are-not-enough-use-sub-environments/
				return subenv == "localhost";

			return false;
		}
	}
}
