using AYSOScoreSheetGenerator.Objects;
using GoogleSheetsHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AYSOScoreSheetGenerator.Services
{
	/// <summary>
	/// Base service class to build sheets within the scores and standings spreadsheet
	/// </summary>
	public abstract class SheetService
	{
		protected ISheetsClient SheetsClient { get; }
		protected ScoreSheetConfiguration Configuration { get; }
		protected ILogger Log { get; }

		protected SheetService(ISheetsClient sheetsClient, IOptionsSnapshot<ScoreSheetConfiguration> configOptions, ILogger log)
		{
			SheetsClient = sheetsClient;
			Configuration = configOptions.Value;
			Log = log;
		}

		protected string CreateFormulaForTeamName(Team team) => $"='{Configuration.TeamsSheetName}'!{team.TeamSheetCell}";
	}
}
