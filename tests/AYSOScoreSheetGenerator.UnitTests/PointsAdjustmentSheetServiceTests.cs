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
using Microsoft.Extensions.Options;
using Moq;
using StandingsGoogleSheetsHelper;
using Xunit;

namespace AYSOScoreSheetGenerator.UnitTests
{
	public class PointsAdjustmentSheetServiceTests
	{
		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public async Task CanAddHeadersForAllRounds(bool interregionalPlay)
		{
			// test that there is a header column created for each round of play
			// this test is applicable for all derivatives of the PointsAdjustmentSheetService

			Fixture f = new Fixture();

			ScoreSheetConfiguration config = new ScoreSheetConfiguration
			{
				Divisions = new[] { "10U Boys", "10U Girls" },
				RefPointsSheetName = f.Create<string>(),
			};
			const string PROGRAM_NAME = "Core season";
			const string OTHER_PROGRAM_NAME = "Other";
			foreach (string division in config.Divisions)
			{
				config.DivisionConfigurations.Add(new DivisionConfiguration
				{
					DivisionName = division,
					ProgramNameForOtherRegions = interregionalPlay ? OTHER_PROGRAM_NAME : null,
				});
			}
			List<DateTime> gameDates = new List<DateTime>();
			for (int i = 0; i < 10; i++)
			{
				gameDates.Add(DateTime.Today.AddDays(1 + i * 7));
			}
			config.GameDates = gameDates;

			Dictionary<string, IList<Team>> divisionTeams = new Dictionary<string, IList<Team>>();
			const int QUANTITY = 5;
			foreach (string division in config.Divisions)
			{
				int i = 0;
				List<Team> teams = f.Build<Team>()
					.With(x => x.ProgramName, () => interregionalPlay ? (i++ == 0 ? OTHER_PROGRAM_NAME : PROGRAM_NAME) : PROGRAM_NAME)
					.With(x => x.DivisionName, division)
					.CreateMany(QUANTITY)
					.ToList();
				divisionTeams.Add(division, teams);
			}

			Mock<ISheetsClient> mockClient = new Mock<ISheetsClient>();
			string? sheetName = null;
			mockClient.Setup(x => x.AddSheet(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
				.Callback((string name, int? rows, int? cols, CancellationToken ct) => sheetName = name)
				.ReturnsAsync((string name, int? rows, int? cols, CancellationToken ct) => new Sheet { Properties = new SheetProperties { Title = name } });

			IList<AppendRequest>? appendRequests = null;
			mockClient.Setup(x => x.Append(It.IsAny<IList<AppendRequest>>(), It.IsAny<CancellationToken>()))
				.Callback((IList<AppendRequest> rqs, CancellationToken ct) => appendRequests = rqs);

			mockClient.Setup(x => x.AutoResizeColumn(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(0);

			RefPointsSheetService service = new RefPointsSheetService(mockClient.Object, Options.Create(config));
			await service.BuildSheet(divisionTeams);

			Assert.Equal(config.RefPointsSheetName, sheetName); // confirm that name of sheet was changed
			Assert.NotNull(appendRequests);
			Assert.Equal(config.Divisions.Count(), appendRequests.Count); // there should be one AppendRequest per division

			GoogleSheetRow headerRow = appendRequests.First().Rows[0];
			Assert.Equal(gameDates.Count + 1, headerRow.Count); // there should be a header for each game date plus the one for the team itself
			for (int i = 1; i < headerRow.Count; i++) // skip the first cell
			{
				string expected = $"{Constants.HDR_REF_PTS} R{i} {gameDates[i - 1].ToString("M/d")}";
				Assert.Equal(expected, headerRow[i].StringValue);
			}
			Assert.All(appendRequests, x => Assert.Equal((interregionalPlay ? QUANTITY - 1 : QUANTITY) + 2, x.Rows.Count)); // including header and blank value

			Func<AppendRequest, IEnumerable<string?>> getTeamFormulas = x => x.Rows.Skip(1).Take(QUANTITY).SelectMany(r => r.Select(c => c.FormulaValue));
			Func<Team, string> teamSheetCellToFormula = t => $"='{config.TeamsSheetName}'!{t.TeamSheetCell}";
			if (interregionalPlay)
			{
				Action<IList<Team>, IEnumerable<string?>> assertTeamFormulaSet = (teams, formulas) => Assert.Equal(teams.Where(t => t.ProgramName == PROGRAM_NAME).Select(t => teamSheetCellToFormula(t)), formulas);
				// each team from our program should have a row
				Assert.Collection(appendRequests,
					x =>
					{
						IEnumerable<string?> teamFormulas = getTeamFormulas(x);
						Assert.Null(teamFormulas.Last()); // the blank row
						assertTeamFormulaSet(divisionTeams.First().Value, teamFormulas.Take(QUANTITY - 1));
					},
					x =>
					{
						IEnumerable<string?> teamFormulas = getTeamFormulas(x);
						Assert.Null(teamFormulas.Last()); // the blank row
						assertTeamFormulaSet(divisionTeams.Last().Value, teamFormulas.Take(QUANTITY - 1));
					}
					);
			}
			else
			{
				// each team in the division should have a row
				Action<IList<Team>, IEnumerable<string?>> assertTeamFormulaSet = (teams, formulas) => Assert.Equal(teams.Select(t => teamSheetCellToFormula(t)), formulas);
				Assert.Collection(appendRequests,
					x => assertTeamFormulaSet(divisionTeams.First().Value, getTeamFormulas(x)),
					x => assertTeamFormulaSet(divisionTeams.Last().Value, getTeamFormulas(x))
					);
			}
		}
	}
}
