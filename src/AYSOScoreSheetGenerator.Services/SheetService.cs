using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AYSOScoreSheetGenerator.Objects;
using GoogleSheetsHelper;
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

		protected SheetService(ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions)
		{
			SheetsClient = sheetsClient;
			Configuration = configOptions.Value;
		}

		protected string CreateFormulaForTeamName(Team team) => $"='{Configuration.TeamsSheetName}'!{team.TeamSheetCell}";
	}
}
