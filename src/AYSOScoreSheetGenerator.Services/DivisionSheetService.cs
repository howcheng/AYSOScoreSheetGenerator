using AYSOScoreSheetGenerator.Objects;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Services
{
	/// <summary>
	/// Interface for service classes that build the sheets to record scores and standings for each division
	/// </summary>
	public interface IDivisionSheetService
	{
		Task BuildSheet(IList<Team> teams);

		bool IsApplicableToDivision(string divisionName);
	}

	/// <summary>
	/// Service class that builds the sheet to record scores and standings for a division. It creates one set of score entry fields and one standings table for every round of games in a season.
	/// In practice, you'll want to have one of these per division.
	/// </summary>
	public class DivisionSheetService : SheetService, IDivisionSheetService
	{
		private string _divisionName;
		private DivisionConfiguration _divisionConfig;
		private readonly DivisionSheetHelper _helper;
		private readonly StandingsRequestCreatorFactory _requestFactory;

		private int? _sheetId;
		private int _numTeamsThisRegion;
		private int _numTeamsOtherRegions;

		public DivisionSheetService(string divisionName, DivisionSheetHelper helper, StandingsRequestCreatorFactory requestFactory
			, ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<DivisionSheetService> log)
			: base(sheetsClient, configOptions, log)
		{
			_divisionName = divisionName;
			_divisionConfig = Configuration.DivisionConfigurations.Single(x => x.DivisionName == _divisionName);
			_helper = helper;
			_requestFactory = requestFactory;
		}

		public async Task BuildSheet(IList<Team> teams)
		{
			Sheet divisionSheet = await SheetsClient.GetOrAddSheet(_divisionName);
			_sheetId = divisionSheet.Properties.SheetId;

			int teamCount = teams.Count;
			if (!_divisionConfig.IncludeOtherRegionsInStandings)
				teamCount = teams.Count(x => x.ProgramName != _divisionConfig.ProgramNameForOtherRegions);

			int NUM_ROUNDS = Configuration.GameDates.Count();
			int startRowIdx = 0;
			Log.LogInformation("Beginning sheet for {0}", _divisionName);

			for (int i = 0; i < NUM_ROUNDS; i++)
			{
				List<GoogleSheetRow> newRows = new List<GoogleSheetRow>();
				List<AppendRequest> appendRequests = new List<AppendRequest>();
				List<Request> updateSheetRequests = new List<Request>();

				string startColumn = _helper.HomeTeamColumnName;
				int endColumnIndex = _helper.HeaderRowColumns.Count - 1;
				string endColumn = _helper.GetColumnNameByHeader(_helper.HeaderRowColumns.Last());

				// round header row
				int roundNum = i + 1;
				GoogleSheetRow roundHeaderRow = new GoogleSheetRow();
				roundHeaderRow.Add(new GoogleSheetCell
				{
					StringValue = $"ROUND {roundNum}: {Configuration.GameDates.ElementAt(i):M/d}",
					BackgroundColor = Configuration.StandingsSheetRoundHeaderColor,
					Bold = true,
				});
				// background color for the rest of the round header row
				for (int j = 1; j < _helper.HeaderRowColumns.Count(); j++)
				{
					roundHeaderRow.Add(new GoogleSheetCell { BackgroundColor = Configuration.StandingsSheetRoundHeaderColor, });
				}
				newRows.Add(roundHeaderRow);

				// column header row
				GoogleSheetRow headerRow = new GoogleSheetRow();
				foreach (string headerRowColumn in _helper.HeaderRowColumns)
				{
					headerRow.Add(new GoogleSheetCell
					{
						StringValue = headerRowColumn,
						Bold = true,
						BackgroundColor = Configuration.StandingsSheetHeaderColor,
					});
				}
				newRows.Add(headerRow);

				AppendRequest appendRequest = new AppendRequest(_divisionName)
				{
					Rows = newRows,
				};

				startRowIdx += appendRequest.Rows.Count; // on 1st round, this should be 2 because of the round row and the header row
				int startGamesRowNum = startRowIdx + 1;
				appendRequests.Add(appendRequest);

				// build the score entry portion of the round

				// team dropdowns
				int numGameRows;
				IEnumerable<Request> dropdownRequests = CreateRequestsForTeamDropDowns(teams, startRowIdx, out numGameRows);
				updateSheetRequests.AddRange(dropdownRequests);

				if (_divisionConfig.HasFriendlyGamesEachRound)
				{
					// set the last row of games to be red to indicate that it's a friendly
					Request colorRq = RequestCreator.CreateRowFormattingRequest(_sheetId, startRowIdx + numGameRows - 1
						, _helper.GetColumnIndexByHeader(Constants.HDR_HOME_TEAM), _helper.GetColumnIndexByHeader(Constants.HDR_WINNING_TEAM)
						, () =>
						{
							var cell = new CellData();
							cell.SetForegroundColor(System.Drawing.Color.Red);
							return cell;
						}, new[] { nameof(TextFormat) }
						);
					updateSheetRequests.Add(colorRq);
				}

				// game winner column
				IStandingsRequestCreator gwRequestCreator = _requestFactory.GetRequestCreator(Constants.HDR_WINNING_TEAM);
				StandingsRequestCreatorConfig gwRequestConfig = SetupRequestCreatorConfig<StandingsRequestCreatorConfig>(teams, startRowIdx, startGamesRowNum, numGameRows);
				updateSheetRequests.Add(gwRequestCreator.CreateRequest(gwRequestConfig));

				// standings table
				int numStandingsRows;
				IEnumerable<Request> standingsTableRequests = CreateStandingsTableRequests(teams, startRowIdx, numGameRows, roundNum, out numStandingsRows);
				updateSheetRequests.AddRange(standingsTableRequests);

				// update the sheet!
				await SheetsClient.Append(appendRequests);
				await SheetsClient.ExecuteRequests(updateSheetRequests);

				Log.LogInformation("Finished round {0} for {1}", roundNum, _divisionName);

				int rowIncrement = numGameRows >= numStandingsRows ? numGameRows : numStandingsRows;
				startRowIdx += rowIncrement;
			}

			// resize the columns
			IEnumerable<Request> resizeRequests = _helper.CreateCellWidthRequests(divisionSheet.Properties.SheetId, Configuration.TeamNameColumnWidth);
			await SheetsClient.ExecuteRequests(resizeRequests);
		}

		private IEnumerable<Request> CreateRequestsForTeamDropDowns(IList<Team> teams, int startRowIdx, out int numGameRows)
		{
			numGameRows = teams.Count / 2; // this is the default
			if (OddNumberOfTeams(teams) && _divisionConfig.HasFriendlyGamesEachRound)
				numGameRows += 1;

			// however, for interregional play, there are other considerations
			if (_divisionConfig.HasInterregionalPlay && !_divisionConfig.IncludeOtherRegionsInStandings) // but if we include the other regions' teams in our sheet, then there's no special handling required
			{
				_numTeamsOtherRegions = teams.Count(x => x.ProgramName == _divisionConfig.ProgramNameForOtherRegions);
				_numTeamsThisRegion = teams.Count - _numTeamsOtherRegions;
				if (_numTeamsOtherRegions > _numTeamsThisRegion)
					numGameRows = _numTeamsThisRegion;  // our region's teams could all potentially be playing teams from other regions
			}

			int gameRows = numGameRows; // can't use out params in anonymous functions
			Func<string, Request> createDataValidationReq = colHdr => RequestCreator.CreateDataValidationRequest(Configuration.TeamsSheetName, teams.First().TeamSheetCell, teams.Last().TeamSheetCell,
				_sheetId, startRowIdx, _helper.GetColumnIndexByHeader(colHdr), gameRows);
			Request[] requests = new Request[]
			{
				createDataValidationReq(Constants.HDR_HOME_TEAM),
				createDataValidationReq(Constants.HDR_AWAY_TEAM),
			};
			return requests;
		}

		private bool OddNumberOfTeams(IList<Team> teams) => (teams.Count % 2) == 1;

		private IEnumerable<Request> CreateStandingsTableRequests(IList<Team> teams, int startRowIdx, int numGameRows, int roundNum, out int numStandingsRows)
		{
			List<Request> requests = new List<Request>();

			if (!_divisionConfig.IncludeOtherRegionsInStandings)
				teams = teams.Where(x => x.ProgramName != _divisionConfig.ProgramNameForOtherRegions).ToList(); // ASSUMPTION: All the teams for this region are grouped together
			numStandingsRows = teams.Count();
			Team startTeam = teams.First();
			string firstTeamSheetFormula = CreateFormulaForTeamName(startTeam); // "Teams!A2"
			int columnIdx = _helper.GetColumnIndexByHeader(Constants.HDR_TEAM_NAME);
			if (columnIdx == -1)
				throw new InvalidOperationException($"Can't find column '{Constants.HDR_TEAM_NAME}' the collection. Did you forget to add it to {nameof(StandingsSheetHelper)}.{nameof(StandingsSheetHelper.HeaderRowColumns)}?");

			Request teamNamesRequest = RequestCreator.CreateRepeatedSheetFormulaRequest(_sheetId, startRowIdx, columnIdx, teams.Count, firstTeamSheetFormula);
			requests.Add(teamNamesRequest);

			int startGamesRowNum = startRowIdx + 1; // first row in first round is 3
			int endGamesRowNum = startRowIdx + numGameRows; // if 8 games/round, then last row should be 10 (not 3+8, because it's cells A3:A10 inclusive)
			if (_divisionConfig.HasFriendlyGamesEachRound)
				endGamesRowNum -= 1; // last row will be for the friendly and won't count for points

			// does the current round count for standings?
			bool roundCountsForStandings = true;
			if (_divisionConfig.RoundsThatCountTowardsStandings < Configuration.GameDates.Count())
			{
				int numRoundsThatDontCount = Configuration.TotalNumberOfRounds - _divisionConfig.RoundsThatCountTowardsStandings;
				roundCountsForStandings = roundNum > numRoundsThatDontCount;
			}

			int lastRoundStartRowNum = roundNum == 1 ? 0 : startGamesRowNum - numStandingsRows - 2; // this is for adding the values from last round; -2 for the round divider row and the column headers

			foreach (string hdr in _helper.StandingsTableColumns)
			{
				IStandingsRequestCreator requestCreator = _requestFactory.GetRequestCreator(hdr);
				if (requestCreator == null)
					continue;

				Request request;
				if (requestCreator is ScoreBasedStandingsRequestCreator)
				{
					ScoreBasedStandingsRequestCreatorConfig config = SetupRequestCreatorConfig<ScoreBasedStandingsRequestCreatorConfig>(teams, startRowIdx, startGamesRowNum, numStandingsRows, endGamesRowNum, lastRoundStartRowNum);
					config.RoundCountsForStandings = roundCountsForStandings;
					request = requestCreator.CreateRequest(config);
				}
				else if (requestCreator is PointsAdjustmentRequestCreator)
				{
					PointsAdjustmentRequestCreatorConfig config = SetupRequestCreatorConfig<PointsAdjustmentRequestCreatorConfig>(teams, startRowIdx, startGamesRowNum, numStandingsRows, endGamesRowNum, lastRoundStartRowNum);
					config.RoundNumber = roundNum;

					PointsAdjustmentSheetConfiguration? sheetConfig = null;
					switch (requestCreator.ColumnHeader)
					{
						case Constants.HDR_REF_PTS:
							sheetConfig = Configuration.RefPointsSheetConfiguration;
							break;
						case Constants.HDR_VOL_PTS:
							sheetConfig = Configuration.VolunteerPointsSheetConfiguration;
							break;
						case Constants.HDR_SPORTSMANSHIP_PTS:
							sheetConfig = Configuration.SportsmanshipPointsSheetConfiguration;
							break;
						case Constants.HDR_PTS_DEDUCTION:
							sheetConfig = Configuration.PointsDeductionSheetConfiguration;
							break;
					}
					if (sheetConfig == null)
						throw new InvalidOperationException($"Could not find the configuration for the specified sheet ({requestCreator.ColumnHeader})");

					config.SheetName = sheetConfig.SheetName;
					config.ValueIsCumulative = sheetConfig.ValueIsCumulative;
					request = requestCreator.CreateRequest(config);
				}
				else
				{
					StandingsRequestCreatorConfig config = SetupRequestCreatorConfig<StandingsRequestCreatorConfig>(teams, startRowIdx, startGamesRowNum, numStandingsRows);
					request = requestCreator.CreateRequest(config);
				}
				requests.Add(request);
			}

			return requests;
		}

		private T SetupRequestCreatorConfig<T>(IList<Team> teams, int startRowIdx, int startGamesRowNum, int rowCount, int endGamesRowNum = 0, int lastRoundStartRowNum = 0) 
			where T : StandingsRequestCreatorConfig
		{
			T config = Activator.CreateInstance<T>();
			config.SheetId = _sheetId;
			config.RowCount = rowCount;
			config.SheetStartRowIndex = startRowIdx;
			config.StartGamesRowNum = startGamesRowNum;

			ScoreBasedStandingsRequestCreatorConfig? config2 = config as ScoreBasedStandingsRequestCreatorConfig;
			if (config2 != null)
			{
				config2.FirstTeamsSheetCell = CreateCellReferenceForTeamName(teams.First());
				config2.EndGamesRowNum = endGamesRowNum;
				config2.LastRoundStartRowNum = lastRoundStartRowNum;
			}

			return config;
		}

		public bool IsApplicableToDivision(string divisionName) => divisionName == _divisionName;
	}
}
