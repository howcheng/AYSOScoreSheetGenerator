using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
	public class DivisionSheetServiceTests
	{
		[Flags]
		private enum TestFlags
		{
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
				ProgramName = "Core season",
				GameDates = new[] { DateTime.Today.AddDays(7), DateTime.Today.AddDays(14), },
				Divisions = new[] { "10U Boys" },
				DivisionConfigurations = new[]
				{
					new DivisionConfiguration
					{
						DivisionName = "10U Boys",
						HasFriendlyGamesEachRound = (flags & TestFlags.HasFriendlyGames) == TestFlags.HasFriendlyGames,
						IncludeOtherRegionsInStandings = (flags & TestFlags.IncludeOtherRegions) == TestFlags.IncludeOtherRegions,
						ProgramNameForOtherRegions = (flags & TestFlags.HasInterregionalPlay) == TestFlags.HasInterregionalPlay ? "Other region" : null,
						RoundsThatCountTowardsStandings = (flags & TestFlags.AllRoundsCountForStandings) == TestFlags.AllRoundsCountForStandings ? 2 : 1,
						StandingsTableColumnHeaders = new[]
						{
							Constants.HDR_GAMES_PLAYED,
							Constants.HDR_NUM_WINS,
							Constants.HDR_NUM_LOSSES,
							Constants.HDR_NUM_DRAWS,
							Constants.HDR_GAME_PTS,
							Constants.HDR_REF_PTS,
							Constants.HDR_TOTAL_PTS,
							Constants.HDR_GOALS_FOR,
							Constants.HDR_GOALS_AGAINST,
							Constants.HDR_GOAL_DIFF,
						}
					}
				},
				
			};
			return configuration;
		}

		[Fact]
		public async Task TestBasicUseCase()
		{
			// the most basic scenario: an even number of teams, no interregional play, all rounds count towards standings
		}
	}
}
