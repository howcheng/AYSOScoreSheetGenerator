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
		protected ScoreSheetConfiguration Configuration { get => _optionsMonitor.CurrentValue; }
		protected ILogger Log { get; }

		private IOptionsMonitor<ScoreSheetConfiguration> _optionsMonitor;

		protected SheetService(ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger log)
		{
			SheetsClient = sheetsClient;
			Log = log;
			_optionsMonitor = configOptions;
		}

		protected string CreateFormulaForTeamName(Team team) => $"={CreateCellReferenceForTeamName(team)}";

		protected string CreateCellReferenceForTeamName(Team team) => $"'{Configuration.TeamsSheetName}'!{team.TeamSheetCell}";
	}
}
