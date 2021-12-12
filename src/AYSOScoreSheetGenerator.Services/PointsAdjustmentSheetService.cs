using AYSOScoreSheetGenerator.Objects;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using Microsoft.Extensions.Options;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Services
{
	/// <summary>
	/// Base service class to build sheets where users can enter values that affect points totals
	/// </summary>
	public abstract class PointsAdjustmentSheetService : TeamListSheetService
	{
		private string _headerName;

		protected PointsAdjustmentSheetService(string headerName, ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions) : base(sheetsClient, configOptions)
		{
			_headerName = headerName;
		}

		protected override IEnumerable<GoogleSheetCell> CreatePointsAdjustmentColumnHeaders()
		{
			List<GoogleSheetCell> cells = new List<GoogleSheetCell>();
			foreach (string headerValue in GetHeaderRowValues())
			{
				cells.Add(new GoogleSheetCell
				{
					StringValue = headerValue,
					Bold = true,
					BackgroundColor = Configuration.TeamsSheetHeaderColor,
				});
			}
			return cells;
		}

		private IEnumerable<string> GetHeaderRowValues()
		{
			int roundNum = 1;
			foreach (DateTime gameDate in Configuration.GameDates)
			{
				yield return $"{_headerName} R{roundNum++} {gameDate:M/d}";
			}
		}

		protected async Task<Sheet> SetUpSheet(string sheetName, bool valueIsCumulative)
		{
			Sheet sheet = await SheetsClient.AddSheet(sheetName);

			// add intro statement about values being cumulative or not
			GoogleSheetCell cell = new GoogleSheetCell
			{
				StringValue = $"ATTENTION! Values entered for each round {(valueIsCumulative ? "must be cumulative totals" : "are for that round only")}!",
				Bold = true,
				ForegroundColor = System.Drawing.Color.DarkRed,
			};
			AppendRequest request = new AppendRequest(sheetName)
			{
				Rows = new[] { new GoogleSheetRow { cell } },
			};
			await SheetsClient.Append(new List<AppendRequest> { request });
			return sheet;
		}

		protected override IEnumerable<Team> GetTeamsForDivision(IList<Team> teams)
		{
			// if there is interregional play, remove teams from the other regions
			string divisionName = teams.First().DivisionName;
			DivisionConfiguration config = Configuration.DivisionConfigurations.Single(x => x.DivisionName == divisionName);
			if (!config.HasInterregionalPlay)
				return teams;
			return teams.Where(x => x.ProgramName != config.ProgramNameForOtherRegions);
		}

		protected override GoogleSheetCell CreateGoogleSheetCellForTeam(Team team, int rowIndex)
		{
			GoogleSheetCell cell = new GoogleSheetCell
			{
				FormulaValue = CreateFormulaForTeamName(team),
			};
			return cell;
		}
	}

	/// <summary>
	/// Service class to build the sheet for entering referee points
	/// </summary>
	public class RefPointsSheetService : PointsAdjustmentSheetService
	{
		public RefPointsSheetService(ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions) : base(Constants.HDR_REF_PTS, sheetsClient, configOptions)
		{
		}

		protected override Task<Sheet> SetUpSheet() => SetUpSheet(Configuration.RefPointsSheetName, Configuration.RefPointsValueIsCumulative);
	}

	/// <summary>
	/// Service class to build the sheet for entering volunteer points (e.g., snack shack duty, field monitors)
	/// </summary>
	public class VolunteerPointsSheetService : PointsAdjustmentSheetService
	{
		public VolunteerPointsSheetService(ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions) : base(Constants.HDR_VOL_PTS, sheetsClient, configOptions)
		{
		}

		protected override Task<Sheet> SetUpSheet() => SetUpSheet(Configuration.VolunteerPointsSheetName, Configuration.VolunteerPointsValueIsCumulative);
	}

	/// <summary>
	/// Service class to build the sheet for entering sportsmanship points
	/// </summary>
	public class SportsmanshipPointsSheetService : PointsAdjustmentSheetService
	{
		public SportsmanshipPointsSheetService(ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions) : base(Constants.HDR_SPORTSMANSHIP_PTS, sheetsClient, configOptions)
		{
		}

		protected override Task<Sheet> SetUpSheet() => SetUpSheet(Configuration.SportsmanshipPointsSheetName, Configuration.SportsmanshipPointsValueIsCumulative);
	}

	/// <summary>
	/// Service class to build the sheet for entering points deductions (e.g., yellow and red cards)
	/// </summary>
	public class PointsDeductionSheetService : PointsAdjustmentSheetService
	{
		public PointsDeductionSheetService(ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions) : base(Constants.HDR_PTS_DEDUCTION, sheetsClient, configOptions)
		{
		}

		protected override Task<Sheet> SetUpSheet() => SetUpSheet(Configuration.PointsDeductionSheetName, Configuration.PointsDeductionValueIsCumulative);
	}
}
