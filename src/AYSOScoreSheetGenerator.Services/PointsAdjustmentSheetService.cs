using AYSOScoreSheetGenerator.Objects;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Services
{
	/// <summary>
	/// Base service class to build sheets where users can enter values that affect points totals
	/// </summary>
	public abstract class PointsAdjustmentSheetService : SheetService
	{
		private string _headerName;

		protected PointsAdjustmentSheetService(string headerName, ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<PointsAdjustmentSheetService> log)
			: base(sheetsClient, configOptions, log)
		{
			_headerName = headerName;
		}

		public async Task BuildSheet(IDictionary<string, IList<Team>> divisions)
		{
			Sheet teamSheet = await SetUpSheet();

			int rowIndex = 0;

			// create requests for each division
			List<AppendRequest> requests = new List<AppendRequest>();
			foreach (KeyValuePair<string, IList<Team>> division in divisions)
			{
				List<GoogleSheetRow> rows = new List<GoogleSheetRow>(division.Value.Count + 1);
				// header row
				GoogleSheetRow headerRow = new GoogleSheetRow
				{
					new GoogleSheetCell // first column is the team name
					{
						StringValue = division.Key,
						Bold = true,
						BackgroundColor = Configuration.TeamsSheetHeaderColor,
					}
				};
				headerRow.AddRange(CreatePointsAdjustmentColumnHeaders());
				rows.Add(headerRow);
				rowIndex++;

				// team names
				IEnumerable<Team> teams = GetTeamsForDivision(division.Value);
				foreach (Team team in teams)
				{
					GoogleSheetCell cell = CreateGoogleSheetCellForTeam(team, rowIndex);
					rows.Add(new GoogleSheetRow { cell });
					rowIndex++;
				}
				rows.Add(new GoogleSheetRow { new GoogleSheetCell { StringValue = string.Empty } }); // blank row
				rowIndex++;

				AppendRequest request = new AppendRequest(teamSheet.Properties.Title)
				{
					Rows = rows,
				};
				requests.Add(request);
			}

			await SheetsClient.Append(requests);

			// resize the team name column
			Request resizeRequest = RequestCreator.CreateCellWidthRequest(teamSheet.Properties.SheetId, Configuration.TeamNameColumnWidth, 0);
			await SheetsClient.ExecuteRequests(new[] { resizeRequest });
		}

		protected IEnumerable<GoogleSheetCell> CreatePointsAdjustmentColumnHeaders()
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

		protected abstract Task<Sheet> SetUpSheet();

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

		private IEnumerable<Team> GetTeamsForDivision(IList<Team> teams)
		{
			// if there is interregional play, remove teams from the other regions
			string divisionName = teams.First().DivisionName!;
			DivisionConfiguration config = Configuration.DivisionConfigurations.Single(x => x.DivisionName == divisionName);
			if (!config.HasInterregionalPlay)
				return teams;
			return teams.Where(x => x.ProgramName != config.ProgramNameForOtherRegions);
		}

		private GoogleSheetCell CreateGoogleSheetCellForTeam(Team team, int rowIndex)
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
	public class RefPointsSheetService : PointsAdjustmentSheetService, ITeamListSheetService
	{
		public int Ordinal { get => 2; }

		public RefPointsSheetService(ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<RefPointsSheetService> log) 
			: base(Constants.HDR_REF_PTS, sheetsClient, configOptions, log)
		{
		}

		protected override async Task<Sheet> SetUpSheet()
		{
			if (Configuration.RefPointsSheetConfiguration == null)
				throw new InvalidOperationException("No configuration object found for the referee points sheet");

			Log.LogInformation("Beginning the referee points sheet");
			return await SetUpSheet(Configuration.RefPointsSheetConfiguration.SheetName!, Configuration.RefPointsSheetConfiguration.ValueIsCumulative);
		}
	}

	/// <summary>
	/// Service class to build the sheet for entering volunteer points (e.g., snack shack duty, field monitors)
	/// </summary>
	public class VolunteerPointsSheetService : PointsAdjustmentSheetService, ITeamListSheetService
	{
		public int Ordinal { get => 3; }

		public VolunteerPointsSheetService(ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<VolunteerPointsSheetService> log) 
			: base(Constants.HDR_VOL_PTS, sheetsClient, configOptions, log)
		{
		}

		protected override async Task<Sheet> SetUpSheet()
		{
			if (Configuration.VolunteerPointsSheetConfiguration == null)
				throw new InvalidOperationException("No configuration object found for the volunteer points sheet");

			Log.LogInformation("Beginning the volunteer points sheet");
			return await SetUpSheet(Configuration.VolunteerPointsSheetConfiguration.SheetName!, Configuration.VolunteerPointsSheetConfiguration.ValueIsCumulative);
		}
	}

	/// <summary>
	/// Service class to build the sheet for entering sportsmanship points
	/// </summary>
	public class SportsmanshipPointsSheetService : PointsAdjustmentSheetService, ITeamListSheetService
	{
		public int Ordinal { get => 4; }

		public SportsmanshipPointsSheetService(ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<SportsmanshipPointsSheetService> log) 
			: base(Constants.HDR_SPORTSMANSHIP_PTS, sheetsClient, configOptions, log)
		{
		}

		protected override async Task<Sheet> SetUpSheet()
		{
			if (Configuration.SportsmanshipPointsSheetConfiguration == null)
				throw new InvalidOperationException("No configuration object found for the sportsmanship points sheet");

			Log.LogInformation("Beginning the sportsmanship points sheet");
			return await SetUpSheet(Configuration.SportsmanshipPointsSheetConfiguration.SheetName!, Configuration.SportsmanshipPointsSheetConfiguration.ValueIsCumulative);
		}
	}

	/// <summary>
	/// Service class to build the sheet for entering points deductions (e.g., yellow and red cards)
	/// </summary>
	public class PointsDeductionSheetService : PointsAdjustmentSheetService, ITeamListSheetService
	{
		public int Ordinal { get => 5; }

		public PointsDeductionSheetService(ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<PointsDeductionSheetService> log) 
			: base(Constants.HDR_PTS_DEDUCTION, sheetsClient, configOptions, log)
		{
		}

		protected override async Task<Sheet> SetUpSheet()
		{
			if (Configuration.PointsDeductionSheetConfiguration == null)
				throw new InvalidOperationException("No configuration object found for the points deduction sheet");

			Log.LogInformation("Beginning the points deduction sheet");
			return await SetUpSheet(Configuration.PointsDeductionSheetConfiguration.SheetName!, Configuration.PointsDeductionSheetConfiguration.ValueIsCumulative);
		}
	}
}
