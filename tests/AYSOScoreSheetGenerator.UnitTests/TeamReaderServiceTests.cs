using System.Collections.Generic;
using System.IO;
using System.Text;
using AYSOScoreSheetGenerator.Objects;
using AYSOScoreSheetGenerator.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace AYSOScoreSheetGenerator.UnitTests
{
	public class TeamReaderServiceTests
	{
		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void TestGetTeams(bool interregionalPlay)
		{
			// test reading the teams from the Team Detail Report; expect:
			// 1. Teams from divisions we're not tracking are excluded
			// 2. Teams from programs that aren't included in this sheet are excluded
			// 3. When interregional play exists, teams from other regions are included
			// 4. Quotation marks have been removed from the values

			const string PROGRAM_NAME = "2021 Core Program";
			const string UNWANTED_PROGRAM_NAME = "2021 Extra";
			const string OTHER_REGION_PROGRAM_NAME = "Other";

			const string DIVISION1 = "10U Boys";
			const string DIVISION2 = "10U Girls";

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("\"Program Name\", \"Division Name\", \"Team Name\", \"Team Code\", \"AllocatedPlayer\", \"UnAllocatedPlayer\", \"AllocatedVolun.\", \"UnAllocatedVolun.\"");
			sb.AppendLine($"\"{PROGRAM_NAME}\", \"5U Boys\", \"Sharks\" ");
			sb.AppendLine($"\"{PROGRAM_NAME}\", \"{DIVISION1}\", \"Super Sharks\" ");
			sb.AppendLine($"\"{PROGRAM_NAME}\", \"{DIVISION2}\", \"Sharknado\" ");
			sb.AppendLine($"\"{UNWANTED_PROGRAM_NAME}\", \"{DIVISION1}\", \"Awesomeness\"");
			sb.AppendLine($"\"{OTHER_REGION_PROGRAM_NAME}\", \"{DIVISION1}\", \"Big Bois\"");
			sb.AppendLine($"\"{OTHER_REGION_PROGRAM_NAME}\", \"{DIVISION2}\", \"Big Girlz\"");

			string teamDetailsList = sb.ToString();
			byte[] teamDetailsListBytes = Encoding.UTF8.GetBytes(teamDetailsList);

			MemoryStream ms = new MemoryStream();
			ms.Write(teamDetailsListBytes, 0, teamDetailsListBytes.Length);
			ms.Position = 0;

			ScoreSheetConfiguration config = new ScoreSheetConfiguration
			{
				Divisions = new[] { DIVISION1, DIVISION2 },
				TeamDetailsReportProvider = () => ms,
				ProgramName = PROGRAM_NAME,
			};
			foreach (string division in config.Divisions)
			{
				config.DivisionConfigurations.Add(new DivisionConfiguration
				{
					DivisionName = division,
					ProgramNameForOtherRegions = interregionalPlay ? OTHER_REGION_PROGRAM_NAME : null,
				});
			}

			TeamReaderService service = new TeamReaderService(Options.Create(config));
			IDictionary<string, IList<Team>>? divisionTeams = service.GetTeams();

			Assert.Equal(2, divisionTeams.Count);

			// confirm quote marks have been removed
			const string quotedStringRegex = "\"[\\w ]+";
			Assert.All(divisionTeams, x => Assert.All(x.Value, t => Assert.DoesNotMatch(quotedStringRegex, t.ProgramName)));
			Assert.All(divisionTeams, x => Assert.All(x.Value, t => Assert.DoesNotMatch(quotedStringRegex, t.DivisionName)));
			Assert.All(divisionTeams, x => Assert.All(x.Value, t => Assert.DoesNotMatch(quotedStringRegex, t.TeamName)));

			Assert.All(divisionTeams, x => Assert.Contains(x.Key, config.Divisions));
			Assert.All(divisionTeams, x => Assert.All(x.Value, t => Assert.NotEqual(UNWANTED_PROGRAM_NAME, t.ProgramName)));
			if (interregionalPlay)
			{
				Assert.All(divisionTeams, x => Assert.Equal(2, x.Value.Count));
				Assert.All(divisionTeams, x => Assert.Contains(x.Value, t => t.ProgramName == PROGRAM_NAME));
				Assert.All(divisionTeams, x => Assert.Contains(x.Value, t => t.ProgramName == OTHER_REGION_PROGRAM_NAME));
			}
			else
			{
				Assert.All(divisionTeams, x => Assert.Single(x.Value));
				Assert.All(divisionTeams, x => Assert.All(x.Value, t => Assert.Equal(PROGRAM_NAME, t.ProgramName)));
			}

		}
	}
}