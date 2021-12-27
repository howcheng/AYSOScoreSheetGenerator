using System.Collections.Generic;
using AYSOScoreSheetGenerator.Services;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.UnitTests
{
	public class BaseTest
	{
		protected DivisionSheetHelper CreateDivisionSheetHelper()
		{
			string[] standingsHeaders = new[] { Constants.HDR_GAMES_PLAYED, Constants.HDR_NUM_WINS, Constants.HDR_NUM_LOSSES, Constants.HDR_NUM_DRAWS, Constants.HDR_GAME_PTS, Constants.HDR_REF_PTS, Constants.HDR_VOL_PTS, Constants.HDR_TOTAL_PTS };
			List<string> allHeaders = new List<string> { Constants.HDR_HOME_TEAM, Constants.HDR_HOME_GOALS, Constants.HDR_AWAY_GOALS, Constants.HDR_AWAY_TEAM };
			allHeaders.AddRange(standingsHeaders);

			DivisionSheetHelper helper = new DivisionSheetHelper(allHeaders, standingsHeaders);
			return helper;
		}
	}
}
