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
using Xunit;

namespace AYSOScoreSheetGenerator.UnitTests
{
	public class TeamListSheetServiceTests
	{
		[Fact]
		public async Task CanBuildSheetFromScratch()
		{
			// test for when starting from a brand new spreadsheet

			Fixture f = new Fixture();

			ScoreSheetConfiguration config = new ScoreSheetConfiguration
			{
				Divisions = new[] { "10U Boys", "10U Girls" },
				TeamsSheetName = f.Create<string>(),
			};

			Dictionary<string, IList<Team>> divisionTeams = new Dictionary<string, IList<Team>>();
			const int QUANTITY = 4;
			foreach (string division in config.Divisions)
			{
				List<Team> teams = f.Build<Team>()
					.With(x => x.DivisionName, division)
					.CreateMany(QUANTITY)
					.ToList();
				divisionTeams.Add(division, teams);
			}

			Mock<ISheetsClient> mockClient = new Mock<ISheetsClient>();
			mockClient.Setup(x => x.GetSheetNames(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "Sheet 1" });
			string? newName = null;
			mockClient.Setup(x => x.RenameSheet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.Callback((string n1, string n2, CancellationToken ct) => newName = n2)
				.ReturnsAsync((string n1, string n2, CancellationToken ct) => new Sheet { Properties = new SheetProperties { Title = n2 } });

			IList<AppendRequest>? appendRequests = null;
			mockClient.Setup(x => x.Append(It.IsAny<IList<AppendRequest>>(), It.IsAny<CancellationToken>()))
				.Callback((IList<AppendRequest> rqs, CancellationToken ct) => appendRequests = rqs);

			mockClient.Setup(x => x.AutoResizeColumn(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(0);

			TeamListSheetService service = new TeamListSheetService(mockClient.Object, Options.Create(config));
			await service.BuildSheet(divisionTeams);

			Assert.Equal(config.TeamsSheetName, newName); // confirm that name of first sheet was changed
			Assert.NotNull(appendRequests);
			Assert.Equal(config.Divisions.Count(), appendRequests.Count); // there should be one AppendRequest per division
			Assert.All(appendRequests, x => Assert.All(x.Rows, r => Assert.Single(r))); // each row here should only be a single cell
			Assert.All(appendRequests, x => Assert.Equal(QUANTITY + 2, x.Rows.Count)); // including header and blank value
			// each team in the division should have a row
			Assert.Collection(appendRequests,
				x => Assert.Equal(divisionTeams.First().Value.Select(t => t.TeamName), x.Rows.Skip(1).Take(QUANTITY).SelectMany(r => r.Select(c => c.StringValue))), 
				x => Assert.Equal(divisionTeams.Last().Value.Select(t => t.TeamName), x.Rows.Skip(1).Take(QUANTITY).SelectMany(r => r.Select(c => c.StringValue))));
		}

		[Fact]
		public async Task CanRebuildExistingSheet()
		{
			// test for when reusing an existing spreadsheet -- all the other sheets should get deleted first

			Fixture f = new Fixture();

			ScoreSheetConfiguration config = new ScoreSheetConfiguration
			{
				Divisions = new[] { "10U Boys", "10U Girls" },
				TeamsSheetName = f.Create<string>(),
			};

			Dictionary<string, IList<Team>> divisionTeams = new Dictionary<string, IList<Team>>();
			const int QUANTITY = 4;
			foreach (string division in config.Divisions)
			{
				List<Team> teams = f.Build<Team>()
					.With(x => x.DivisionName, division)
					.Without(x => x.TeamSheetCell)
					.CreateMany(QUANTITY)
					.ToList();
				divisionTeams.Add(division, teams);
			}

			Mock<ISheetsClient> mockClient = new Mock<ISheetsClient>();
			List<string> existingSheetNames = f.CreateMany<string>(QUANTITY).ToList();
			mockClient.Setup(x => x.GetSheetNames(It.IsAny<CancellationToken>())).ReturnsAsync(existingSheetNames);
			string? newName = null;
			mockClient.Setup(x => x.RenameSheet(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.Callback((string n1, string n2, CancellationToken ct) => newName = n2)
				.ReturnsAsync((string n1, string n2, CancellationToken ct) => new Sheet { Properties = new SheetProperties { Title = n2 } });

			List<string> deletedSheets = new List<string>();
			mockClient.Setup(x => x.DeleteSheet(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.Callback((string delSheet, CancellationToken ct) => deletedSheets.Add(delSheet));

			mockClient.Setup(x => x.Append(It.IsAny<IList<AppendRequest>>(), It.IsAny<CancellationToken>()));
			mockClient.Setup(x => x.AutoResizeColumn(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(0);

			TeamListSheetService service = new TeamListSheetService(mockClient.Object, Options.Create(config));
			await service.BuildSheet(divisionTeams);

			Assert.Equal(config.TeamsSheetName, newName); // confirm that name of first sheet was changed
			Assert.Equal(existingSheetNames.Skip(1), deletedSheets); // all but the first sheet should have been deleted

			// teams should have their team sheet cell values filled
			Assert.All(divisionTeams, x => Assert.All(x.Value, t => Assert.NotNull(t.TeamSheetCell)));

			// don't need to verify the AppendRequests because that's the same as when we start a new spreadsheet
		}
	}
}