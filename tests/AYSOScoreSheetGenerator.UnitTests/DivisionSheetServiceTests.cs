using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AYSOScoreSheetGenerator.Objects;
using AYSOScoreSheetGenerator.Services;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using StandingsGoogleSheetsHelper;
using Xunit;

namespace AYSOScoreSheetGenerator.UnitTests
{
	public class DivisionSheetServiceTests : BaseTest
	{
		private const string DIVISION = "10U Boys";
		private const string PROGRAM = "Core season";
		private const string OTHER_REGION_PROGRAM = "Other region";

		[Flags]
		public enum TestFlags
		{
			None = 0,
			EvenNumberOfTeams = 1,
			HasInterregionalPlay = 2,
			AllRoundsCountForStandings = 4,
			HasFriendlyGames = 8,
			IncludeOtherRegions = 16,
		}

		private ScoreSheetConfiguration BuildConfiguration(TestFlags flags)
		{
			ScoreSheetConfiguration configuration = new ScoreSheetConfiguration
			{
				ProgramName = PROGRAM,
				GameDates = new[] { DateTime.Today.AddDays(7), DateTime.Today.AddDays(14), },
				Divisions = new[] { DIVISION },
				DivisionConfigurations = new[]
				{
					new DivisionConfiguration
					{
						DivisionName = DIVISION,
						HasFriendlyGamesEachRound = (flags & TestFlags.HasFriendlyGames) == TestFlags.HasFriendlyGames,
						IncludeOtherRegionsInStandings = (flags & TestFlags.IncludeOtherRegions) == TestFlags.IncludeOtherRegions,
						ProgramNameForOtherRegions = (flags & TestFlags.HasInterregionalPlay) == TestFlags.HasInterregionalPlay ? OTHER_REGION_PROGRAM : null,
						RoundsThatCountTowardsStandings = (flags & TestFlags.AllRoundsCountForStandings) == TestFlags.AllRoundsCountForStandings ? 2 : 1,
					}
				},
				RefPointsSheetConfiguration = new RefPointsSheetConfiguration(),
				VolunteerPointsSheetConfiguration =	new VolunteerPointsSheetConfiguration(),
			};
			return configuration;
		}

		private Sheet GetDivisionSheet()
		{
			return new Sheet
			{
				Properties = new SheetProperties
				{
					SheetId = 12345,
					Title = DIVISION,
				},
			};
		}

		private StandingsRequestCreatorFactory GetRequestCreatorFactory(FormulaGenerator fg)
		{
			IServiceCollection services = new ServiceCollection()
				.AddSingleton(fg)
				.AddScoped<IStandingsRequestCreator, GameWinnerRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GamesPlayedRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GamesWonRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GamesLostRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GamesDrawnRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GamePointsRequestCreator>()
				.AddScoped<IStandingsRequestCreator, RefPointsRequestCreator>()
				.AddScoped<IStandingsRequestCreator, VolunteerPointsRequestCreator>()
				.AddScoped<IStandingsRequestCreator, TotalPointsRequestCreator>()
				.AddScoped<IStandingsRequestCreator, TeamRankRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GoalsScoredRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GoalsAgainstRequestCreator>()
				.AddScoped<IStandingsRequestCreator, GoalDifferentialRequestCreator>();

			ServiceProvider provider = services.BuildServiceProvider();
			return new StandingsRequestCreatorFactory(provider.GetServices<IStandingsRequestCreator>());
		}

		private IList<Team> CreateTeams(TestFlags flags)
		{
			bool hasOtherRegions = (flags & TestFlags.IncludeOtherRegions) == TestFlags.IncludeOtherRegions;
			bool evenNumberOfTeams = (flags & TestFlags.EvenNumberOfTeams) == TestFlags.EvenNumberOfTeams;

			int counter = 0, counter2 = 0;
			Fixture f = new Fixture();
			IList<Team> teams = f.Build<Team>()
				.With(x => x.DivisionName, DIVISION)
				.With(x => x.ProgramName, () => hasOtherRegions ? (counter++ < 2 ? OTHER_REGION_PROGRAM : PROGRAM) : PROGRAM)
				.With(x => x.TeamSheetCell, () => $"A{++counter2}")
				.CreateMany(evenNumberOfTeams ? 4 : 5)
				.ToList();
			return teams;
		}

		private Mock<ISheetsClient> GetMockClient(Action<IList<AppendRequest>, CancellationToken> appendCallback, Action<IList<UpdateRequest>, CancellationToken> updateDataCallback,
			Action<IEnumerable<Request>, CancellationToken> updateSheetCallback)
		{
			Mock<ISheetsClient> mockClient = new Mock<ISheetsClient>();
			mockClient.Setup(x => x.Append(It.IsAny<IList<AppendRequest>>(), It.IsAny<CancellationToken>())).Callback(appendCallback);
			mockClient.Setup(x => x.Update(It.IsAny<IList<UpdateRequest>>(), It.IsAny<CancellationToken>())).Callback(updateDataCallback);
			mockClient.Setup(x => x.ExecuteRequests(It.IsAny<IEnumerable<Request>>(), It.IsAny<CancellationToken>())).Callback(updateSheetCallback);
			return mockClient;
		}

		[Fact]
		public async Task TestHeaderRowRequests()
		{
			// test to make sure that the header rows are done correctly; here, we don't care about the dropdowns or the standings table
			TestFlags flags = TestFlags.EvenNumberOfTeams;

			DivisionSheetHelper helper = CreateDivisionSheetHelper();
			FormulaGenerator fg = new FormulaGenerator(helper);
			ScoreSheetConfiguration config = BuildConfiguration(flags);
			DivisionConfiguration divisionConfig = config.DivisionConfigurations.Single();

			List<IList<AppendRequest>> appendRequests = new List<IList<AppendRequest>>();
			Action<IList<AppendRequest>, CancellationToken> appendCallback = (rqs, ct) => appendRequests.Add(rqs);
			Action<IList<UpdateRequest>, CancellationToken> updateDataCallback = (rqs, ct) => { };
			Action<IEnumerable<Request>, CancellationToken> updateSheetCallback = (rqs, ct) => { };
			Mock<ISheetsClient> mockClient = GetMockClient(appendCallback, updateDataCallback, updateSheetCallback);

			DivisionSheetService service = new DivisionSheetService(DIVISION, GetDivisionSheet(), fg, GetRequestCreatorFactory(fg), mockClient.Object, Options.Create(config));
			IList<Team> teams = CreateTeams(flags);
			await service.BuildSheet(teams);

			// should be one AppendRequest per round
			Assert.Equal(config.GameDates.Count(), appendRequests.Count);
			Assert.All(appendRequests, ar => Assert.Single(ar)); // each AppendRequest contains a collection of rows
			Assert.All(appendRequests, ar => Assert.All(ar, rq => Assert.Equal(2, rq.Rows.Count))); // two rows: one for the round header, and one for the column headers
			Assert.All(appendRequests, ar => Assert.All(ar, rq => Assert.Collection(rq.Rows,
				row => // first row is the round header
				{
					int roundIdx = appendRequests.IndexOf(ar);
					int roundNum = roundIdx + 1;
					Assert.Equal(helper.HeaderRowColumns.Count, row.Count);
					Assert.Equal($"ROUND {roundNum}: {config.GameDates.ElementAt(roundIdx):M/d}", row.First().StringValue); // first cell has the round name/date
					Assert.All(row, cell => Assert.Equal(config.StandingsSheetRoundHeaderColor, cell.BackgroundColor));
				},
				row => // second row is the column headers
				{
					Assert.Equal(helper.HeaderRowColumns.Count, row.Count);
					Assert.Equal(helper.HeaderRowColumns, row.Select(cell => cell.StringValue).ToList());
					Assert.All(row, cell => Assert.Equal(config.StandingsSheetHeaderColor, cell.BackgroundColor));
				}))
			);
		}

		[Theory]
		[InlineData(TestFlags.EvenNumberOfTeams)]
		[InlineData(TestFlags.HasFriendlyGames)]
		public async Task TestScoreEntryRequests(TestFlags flags)
		{
			// test to make sure that the team dropdowns and game winner column are done correctly

			bool hasEvenNumberOfTeams = (flags & TestFlags.EvenNumberOfTeams) == TestFlags.EvenNumberOfTeams;
			bool hasFriendlies = (flags & TestFlags.HasFriendlyGames) == TestFlags.HasFriendlyGames;

			DivisionSheetHelper helper = CreateDivisionSheetHelper();
			FormulaGenerator fg = new FormulaGenerator(helper);
			ScoreSheetConfiguration config = BuildConfiguration(flags);
			DivisionConfiguration divisionConfig = config.DivisionConfigurations.Single();

			List<IList<UpdateRequest>> updateDataRequests = new List<IList<UpdateRequest>>();
			List<IEnumerable<Request>> updateSheetRequests = new List<IEnumerable<Request>>();
			Action<IList<AppendRequest>, CancellationToken> appendCallback = (rqs, ct) => { };
			Action<IList<UpdateRequest>, CancellationToken> updateDataCallback = (rqs, ct) => updateDataRequests.Add(rqs);
			Action<IEnumerable<Request>, CancellationToken> updateSheetCallback = (rqs, ct) => updateSheetRequests.Add(rqs);
			Mock<ISheetsClient> mockClient = GetMockClient(appendCallback, updateDataCallback, updateSheetCallback);

			DivisionSheetService service = new DivisionSheetService(DIVISION, GetDivisionSheet(), fg, GetRequestCreatorFactory(fg), mockClient.Object, Options.Create(config));
			IList<Team> teams = CreateTeams(flags);
			await service.BuildSheet(teams);

			// should be one set of Requests per round
			Assert.Equal(config.GameDates.Count(), updateSheetRequests.Count);
			// verify the team dropdowns
			int startRowIdx = 0;
			int counter = 0;
			Func<int, int> calculateNextStartRowIdx = start => (counter++ * (teams.Count + 2)) + 2; // the 2nd +2 is to account for the first set of headers
			int numGameRows = teams.Count / 2;
			if (!hasEvenNumberOfTeams && hasFriendlies)
				numGameRows += 1;
			Func<int, int> calculateNextEndRowIdx = start => start + numGameRows;
			Assert.All(updateSheetRequests, usr =>
			{
				IEnumerable<Request> ddlRequests = usr.Where(rq => rq.SetDataValidation != null);
				Assert.Equal(2, ddlRequests.Count()); // expect requests for home team and away team columns
				Assert.All(ddlRequests, ddlr =>
				{
					ConditionValue cv = ddlr.SetDataValidation.Rule.Condition.Values.Single();
					Assert.Equal($"='{config.TeamsSheetName}'!{teams.First().TeamSheetCell}:{teams.Last().TeamSheetCell}", cv.UserEnteredValue);
				});
			});
			Assert.All(updateSheetRequests, usr =>
			{
				IEnumerable<Request> ddlRequests = usr.Where(rq => rq.SetDataValidation != null);
				Assert.Collection(ddlRequests,
					ddlr => Assert.Equal(helper.GetColumnIndexByHeader(Constants.HDR_HOME_TEAM), ddlr.SetDataValidation.Range.StartColumnIndex),
					ddlr => Assert.Equal(helper.GetColumnIndexByHeader(Constants.HDR_AWAY_TEAM), ddlr.SetDataValidation.Range.StartColumnIndex)
				);
			});
			Assert.All(updateSheetRequests, usr =>
			{
				IEnumerable<Request> ddlRequests = usr.Where(rq => rq.SetDataValidation != null);
				int startIdx = calculateNextStartRowIdx(startRowIdx);
				Assert.All(ddlRequests, ddlr => Assert.Equal(startIdx, ddlr.SetDataValidation.Range.StartRowIndex));
				Assert.All(ddlRequests, ddlr => Assert.Equal(startIdx, ddlr.SetDataValidation.Range.StartRowIndex));
				int endIdx = calculateNextEndRowIdx(startIdx);
				Assert.All(ddlRequests, ddlr => Assert.Equal(endIdx, ddlr.SetDataValidation.Range.EndRowIndex));
				Assert.All(ddlRequests, ddlr => Assert.Equal(endIdx, ddlr.SetDataValidation.Range.EndRowIndex));
			});

			counter = 0;
			Assert.Equal(config.GameDates.Count(), updateDataRequests.Count);
			if (hasFriendlies)
			{
				Assert.All(updateDataRequests, ur => Assert.Single(ur));
				Assert.All(updateDataRequests, ur => Assert.All(ur, r => Assert.Single(r.Rows)));
				// the friendly games will be in the last row
				Assert.All(updateDataRequests, ur =>
				{
					Assert.All(ur, r =>
					{
						Assert.Equal(calculateNextStartRowIdx(startRowIdx) + numGameRows - 1, r.RowStart);

						GoogleSheetRow row = r.Rows.Single();
						Assert.Equal(5, row.Count); // home/away teams + home/away scores + game winner
						Assert.All(row, cell => Assert.Equal(System.Drawing.Color.Red, cell.ForegroundColor));
					});
				});
			}
			else
				Assert.All(updateDataRequests, ur => Assert.Empty(ur)); // no friendlies means no update requests to change the background color of the last games

			// verify the formula columns
			Assert.All(updateSheetRequests, usr => AssertFormulaRequestExists(helper, usr, Constants.HDR_WINNING_TEAM));
		}

		[Theory]
		[InlineData(TestFlags.None)]
		[InlineData(TestFlags.IncludeOtherRegions)]
		public async Task TestStandingsTableRequests(TestFlags flags)
		{
			// test to make sure that the standings table is done correctly

			bool includeOtherRegions = (flags & TestFlags.IncludeOtherRegions) == TestFlags.IncludeOtherRegions;

			DivisionSheetHelper helper = CreateDivisionSheetHelper();
			FormulaGenerator fg = new FormulaGenerator(helper);
			ScoreSheetConfiguration config = BuildConfiguration(flags);
			DivisionConfiguration divisionConfig = config.DivisionConfigurations.Single();

			List<IEnumerable<Request>> updateSheetRequests = new List<IEnumerable<Request>>();
			Action<IList<AppendRequest>, CancellationToken> appendCallback = (rqs, ct) => { };
			Action<IList<UpdateRequest>, CancellationToken> updateDataCallback = (rqs, ct) => { };
			Action<IEnumerable<Request>, CancellationToken> updateSheetCallback = (rqs, ct) => updateSheetRequests.Add(rqs);
			Mock<ISheetsClient> mockClient = GetMockClient(appendCallback, updateDataCallback, updateSheetCallback);

			DivisionSheetService service = new DivisionSheetService(DIVISION, GetDivisionSheet(), fg, GetRequestCreatorFactory(fg), mockClient.Object, Options.Create(config));
			IList<Team> teams = CreateTeams(flags);
			await service.BuildSheet(teams);

			// should be one set of Requests per round
			Assert.Equal(config.GameDates.Count(), updateSheetRequests.Count);

			int numTeams = includeOtherRegions ? teams.Count : teams.Count(t => t.ProgramName != divisionConfig.ProgramNameForOtherRegions);

			foreach (string columnHeader in helper.StandingsTableColumns)
			{
				Assert.All(updateSheetRequests, usr => AssertFormulaRequestExists(helper, usr, columnHeader, numTeams));
			}
		}

		private void AssertFormulaRequestExists(DivisionSheetHelper helper, IEnumerable<Request> requests, string columnHeader, int? numTeams = null)
		{
			// don't need to verify the actual formula because it's done in the StandingsRequestCreator tests
			// the start row index of the request is set by the Creator class so if the request exists, it was the correct one
			IEnumerable<Request> formulaReq = requests.Where(rq => rq.RepeatCell != null && rq.RepeatCell.Range.StartColumnIndex == helper.GetColumnIndexByHeader(columnHeader));
			Assert.Single(formulaReq);

			if (numTeams.HasValue)
			{
				Request req = formulaReq.Single();
				Assert.Equal(numTeams, req.RepeatCell.Range.EndRowIndex - req.RepeatCell.Range.StartRowIndex);
			}
		}
	}
}
