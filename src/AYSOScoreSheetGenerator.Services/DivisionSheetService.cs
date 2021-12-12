using AYSOScoreSheetGenerator.Objects;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
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
		private readonly FormulaGenerator _formulaGenerator;
		private readonly StandingsRequestCreatorFactory _requestFactory;

		private int? _sheetId;
		private int _numTeamsThisRegion;
		private int _numTeamsOtherRegions;

		public DivisionSheetService(string divisionName, Sheet divisionSheet, FormulaGenerator formGen, StandingsRequestCreatorFactory requestFactory, ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions)
			: base(sheetsClient, configOptions)
		{
			_divisionName = divisionName;
			_divisionConfig = configOptions.Value.DivisionConfigurations.Single(x => x.DivisionName == _divisionName);
			_helper = (DivisionSheetHelper)formGen.SheetHelper;
			_formulaGenerator = formGen;
			_requestFactory = requestFactory;

			_sheetId = divisionSheet.Properties.SheetId;
		}

		public async Task BuildSheet(IList<Team> teams)
		{
			int teamCount = teams.Count;
			if (!_divisionConfig.IncludeOtherRegionsInStandings)
				teamCount = teams.Count(x => x.ProgramName != _divisionConfig.ProgramNameForOtherRegions);

			int NUM_ROUNDS = Configuration.GameDates.Count();
			int startRowIdx = 0;
			for (int i = 0; i < NUM_ROUNDS; i++)
			{
				List<GoogleSheetRow> newRows = new List<GoogleSheetRow>();
				List<Request> updateSheetRequests = new List<Request>();
				List<UpdateRequest> updateDataRequests = new List<UpdateRequest>();

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

				startRowIdx += appendRequest.Rows.Count;
				await SheetsClient.Append(new List<AppendRequest> { appendRequest });

				// build the score entry portion of the round

				// team dropdowns
				int numGameRows;
				IEnumerable<Request> dropdownRequests = CreateRequestsForTeamDropDowns(teams, startRowIdx, out numGameRows);
				updateSheetRequests.AddRange(dropdownRequests);

				if (_divisionConfig.HasFriendlyGamesEachRound)
				{
					// set the last row of games to be red to indicate that it's a friendly
					GoogleSheetCell friendlyCell = new GoogleSheetCell
					{
						ForegroundColor = System.Drawing.Color.Red,
					};
					int howMany = _helper.GetColumnIndexByHeader(Constants.HDR_AWAY_TEAM) - _helper.GetColumnIndexByHeader(Constants.HDR_HOME_TEAM) + 1;
					GoogleSheetRow friendlyRow = new GoogleSheetRow();
					friendlyRow.AddRange(Enumerable.Repeat(friendlyCell, howMany));

					updateDataRequests.Add(new UpdateRequest(_divisionName)
					{
						RowStart = startRowIdx + numGameRows,
						ColumnStart = _helper.GetColumnIndexByHeader(Constants.HDR_HOME_TEAM),
						Rows = new List<GoogleSheetRow> { friendlyRow },
					});
				}

				// game winner column
				updateSheetRequests.Add(CreateDetermineGameWinnerRequest(startRowIdx, numGameRows));

				// standings table
				IEnumerable<Request> standingsTableRequests = CreateStandingsTableRequests(teams, startRowIdx, numGameRows, i);
				updateSheetRequests.AddRange(standingsTableRequests);

				await SheetsClient.ExecuteRequests(updateSheetRequests);

				startRowIdx += numGameRows + 2; // 1 for round header, 1 for column headers
			}
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
				if (_numTeamsOtherRegions >= _numTeamsThisRegion)
					numGameRows = _numTeamsOtherRegions;  // our region's teams could all potentially be playing teams from other regions
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

		private Request CreateDetermineGameWinnerRequest(int startRowIdx, int numGameRows)
		{
			int rowNum = startRowIdx + 1;
			return RequestCreator.CreateRepeatedSheetFormulaRequest(_sheetId, startRowIdx, _helper.GetColumnIndexByHeader(Constants.HDR_WINNING_TEAM), numGameRows,
				_formulaGenerator.GetGameWinnerFormula(rowNum));
		}

		private bool OddNumberOfTeams(IList<Team> teams) => (teams.Count % 2) == 1;

		private IEnumerable<Request> CreateStandingsTableRequests(IList<Team> teams, int startRowIdx, int numGameRows, int roundNum)
		{
			List<Request> requests = new List<Request>();

			if (!_divisionConfig.IncludeOtherRegionsInStandings)
				teams = teams.Where(x => x.ProgramName != _divisionConfig.ProgramNameForOtherRegions).ToList(); // ASSUMPTION: All the teams for this region are grouped together
			Team startTeam = teams.First();
			string firstTeamSheetCell = $"{Configuration.TeamsSheetName}!{startTeam.TeamSheetCell}"; // "Teams!A2"
			Request teamNamesRequest = RequestCreator.CreateRepeatedSheetFormulaRequest(_sheetId, startRowIdx, _helper.GetColumnIndexByHeader(Constants.HDR_TEAM_NAME), teams.Count
				, $"={firstTeamSheetCell}");
			requests.Add(teamNamesRequest);

			int startGamesRowNum = startRowIdx + 1; // first row in first round is 3
			int endGamesRowNum = startRowIdx + numGameRows; // if 8 games/round, then last row should be 10 (not 3+8, because it's cells A3:A10 inclusive)
			if (_divisionConfig.HasFriendlyGamesEachRound)
				endGamesRowNum -= 1; // last row will be for the friendly and won't count for points

			int lastRoundStartRowNum = roundNum == 1 ? 0 : startGamesRowNum - numGameRows - 2; // this is for adding the values from last round; -2 for the round divider row and the column headers

			foreach (string hdr in _helper.StandingsTableColumns)
			{
				IStandingsRequestCreator requestCreator = _requestFactory.GetRequestCreator(hdr);
				if (requestCreator == null)
					continue;

				Request request;
				if (requestCreator is ScoreBasedStandingsRequestCreator)
				{
					ScoreBasedStandingsRequestCreatorConfig config = new ScoreBasedStandingsRequestCreatorConfig
					{
						SheetId = _sheetId,
						FirstTeamsSheetCell = teams.First().TeamSheetCell,
						NumTeams = teams.Count,
						SheetStartRowIndex = startRowIdx,
						StartGamesRowNum = startGamesRowNum,
						EndGamesRowNum = endGamesRowNum,
						LastRoundStartRowNum = lastRoundStartRowNum,
					};
					request = requestCreator.CreateRequest(config);
				}
				else if (requestCreator is PointsAdjustmentRequestCreator)
				{
					PointsAdjustmentRequestCreatorConfig config = new PointsAdjustmentRequestCreatorConfig
					{
						SheetId = _sheetId,
						FirstTeamsSheetCell = teams.First().TeamSheetCell,
						NumTeams = teams.Count,
						SheetStartRowIndex = startRowIdx,
						StartGamesRowNum = startGamesRowNum,
						EndGamesRowNum = endGamesRowNum,
						LastRoundStartRowNum = lastRoundStartRowNum,
						RoundNumber = roundNum,
						SheetName = _divisionName,
					};
					switch (requestCreator.ColumnHeader)
					{
						case Constants.HDR_REF_PTS:
							config.ValueIsCumulative = Configuration.RefPointsValueIsCumulative;
							break;
						case Constants.HDR_VOL_PTS:
							config.ValueIsCumulative = Configuration.VolunteerPointsValueIsCumulative;
							break;
						case Constants.HDR_SPORTSMANSHIP_PTS:
							config.ValueIsCumulative = Configuration.SportsmanshipPointsValueIsCumulative;
							break;
						case Constants.HDR_PTS_DEDUCTION:
							config.ValueIsCumulative = Configuration.PointsDeductionValueIsCumulative;
							break;
					}
					request = requestCreator.CreateRequest(config);
				}
				else
				{
					StandingsRequestCreatorConfig config = new StandingsRequestCreatorConfig
					{
						SheetId = _sheetId,
						NumTeams = teams.Count,
						SheetStartRowIndex = startRowIdx,
						StartGamesRowNum = startGamesRowNum,
					};
					request = requestCreator.CreateRequest(config);
				}
				requests.Add(request);
			}

			return requests;
		}

		public bool IsApplicableToDivision(string divisionName) => divisionName == _divisionName;
	}
}
