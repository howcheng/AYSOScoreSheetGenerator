using System.Collections.Generic;
using AYSOScoreSheetGenerator.Objects;
using AYSOScoreSheetGenerator.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StandingsGoogleSheetsHelper;
using Xunit.Abstractions;

namespace AYSOScoreSheetGenerator.UnitTests
{
	public class BaseTest
	{
		private readonly ITestOutputHelper _outputHelper;

		public BaseTest(ITestOutputHelper outputHelper)
		{
			_outputHelper = outputHelper;
		}

		protected DivisionSheetHelper CreateDivisionSheetHelper()
		{
			string[] standingsHeaders = new[] { Constants.HDR_GAMES_PLAYED, Constants.HDR_NUM_WINS, Constants.HDR_NUM_LOSSES, Constants.HDR_NUM_DRAWS, Constants.HDR_GAME_PTS, Constants.HDR_REF_PTS, Constants.HDR_VOL_PTS, Constants.HDR_TOTAL_PTS };
			List<string> allHeaders = new List<string> { Constants.HDR_HOME_TEAM, Constants.HDR_HOME_GOALS, Constants.HDR_AWAY_GOALS, Constants.HDR_AWAY_TEAM, Constants.HDR_WINNING_TEAM };
			allHeaders.AddRange(standingsHeaders);

			DivisionSheetHelper helper = new DivisionSheetHelper(allHeaders, standingsHeaders);
			return helper;
		}

		protected IOptionsSnapshot<ScoreSheetConfiguration> GetScoreSheetConfigOptions(ScoreSheetConfiguration config)
		{
			Mock<IOptionsSnapshot<ScoreSheetConfiguration>> mock = new Mock<IOptionsSnapshot<ScoreSheetConfiguration>>();
			mock.SetupGet(x => x.Value).Returns(config);
			return mock.Object;
		}

		protected ILogger<T> GetLogger<T>() => _outputHelper.BuildLoggerFor<T>();
	}
}
