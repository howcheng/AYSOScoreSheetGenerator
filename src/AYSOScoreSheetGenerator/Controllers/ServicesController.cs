using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AYSOScoreSheetGenerator.Objects;
using AYSOScoreSheetGenerator.Services;
using AYSOScoreSheetGenerator.Models;
using GoogleSheetsHelper;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class ServicesController : ControllerBase
	{
		private readonly ISheetsClient _sheetsClient;
		private readonly IOptionsMonitorCache<ScoreSheetConfiguration> _optionsMonitorCache;

		public ServicesController(ISheetsClient sheetsClient, IOptionsMonitorCache<ScoreSheetConfiguration> optsMonitorCache)
		{
			_sheetsClient = sheetsClient;
			_optionsMonitorCache = optsMonitorCache;
		}

		[HttpPost]
		public async Task<IActionResult> Post([FromBody] UploadModel model)
		{
			// replace the score sheet config with the posted value
			_optionsMonitorCache.TryRemove(string.Empty);
			_optionsMonitorCache.TryAdd(string.Empty, model.SpreadsheetConfiguration);

			// figure out what services we are going to need
			bool hasRefPts = model.SpreadsheetConfiguration.RefPointsSheetConfiguration != null;
			bool hasVolPts = model.SpreadsheetConfiguration.VolunteerPointsSheetConfiguration != null;
			bool hasSptsPts = model.SpreadsheetConfiguration.SportsmanshipPointsSheetConfiguration != null;
			bool hasPtsDed = model.SpreadsheetConfiguration.PointsDeductionSheetConfiguration != null;

			IServiceCollection services = new ServiceCollection();
			services.AddLogging();
			services.AddSignalR();
			services.AddSingleton(_sheetsClient);
			services.AddSingleton(new PostedTeamReaderService(model.Divisions));

			List<string> standingsHeaders = new List<string>
			{
				Constants.HDR_GAMES_PLAYED, 
				Constants.HDR_NUM_WINS, 
				Constants.HDR_NUM_LOSSES, 
				Constants.HDR_NUM_DRAWS, 
				Constants.HDR_GAME_PTS, // insert ref/volunteer etc columns here if needed
				Constants.HDR_TOTAL_PTS,
				Constants.HDR_RANK,
				Constants.HDR_GOALS_FOR,
				Constants.HDR_GOALS_AGAINST,
				Constants.HDR_GOAL_DIFF,
			};
			int insertIdx = standingsHeaders.IndexOf(Constants.HDR_TOTAL_PTS);
			if (hasRefPts)
			{
				services.AddSingleton<ITeamListSheetService, RefPointsSheetService>();
				if (model.SpreadsheetConfiguration.RefPointsSheetConfiguration.AffectsStandings)
				{
					standingsHeaders.Insert(insertIdx++, Constants.HDR_REF_PTS);
					services.AddSingleton<IStandingsRequestCreator, RefPointsRequestCreator>();
				}
			}
			if (hasVolPts)
			{
				services.AddSingleton<ITeamListSheetService, VolunteerPointsSheetService>();
				if (model.SpreadsheetConfiguration.VolunteerPointsSheetConfiguration.AffectsStandings)
				{
					standingsHeaders.Insert(insertIdx++, Constants.HDR_VOL_PTS);
					services.AddSingleton<IStandingsRequestCreator, VolunteerPointsRequestCreator>();
				}
			}
			if (hasSptsPts && model.SpreadsheetConfiguration.SportsmanshipPointsSheetConfiguration.AffectsStandings)
			{
				services.AddSingleton<ITeamListSheetService, SportsmanshipPointsSheetService>();
				if (model.SpreadsheetConfiguration.SportsmanshipPointsSheetConfiguration.AffectsStandings)
				{
					standingsHeaders.Insert(insertIdx++, Constants.HDR_SPORTSMANSHIP_PTS);
					services.AddSingleton<IStandingsRequestCreator, SportsmanshipPointsRequestCreator>();
				}
			}
			if (hasPtsDed)
			{
				services.AddSingleton<ITeamListSheetService, PointsDeductionSheetService>();
				standingsHeaders.Insert(insertIdx++, Constants.HDR_PTS_DEDUCTION);
				services.AddSingleton<IStandingsRequestCreator, PointsDeductionPointsRequestCreator>();
			}

			List<string> allHeaders = new List<string> { Constants.HDR_HOME_TEAM, Constants.HDR_HOME_GOALS, Constants.HDR_AWAY_GOALS, Constants.HDR_AWAY_TEAM, Constants.HDR_WINNING_TEAM };
			allHeaders.AddRange(standingsHeaders);

			DivisionSheetHelper helper = new DivisionSheetHelper(allHeaders, standingsHeaders);
			services.AddSingleton<StandingsSheetHelper>(helper);
			services.AddSingleton<FormulaGenerator>();
			services.AddSingleton<StandingsRequestCreatorFactory>();
			services.AddSingleton<ITeamListSheetService, TeamListSheetService>();

			// the other request creators
			services.AddSingleton<IStandingsRequestCreator, GameWinnerRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GamesPlayedRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GamesWonRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GamesLostRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GamesDrawnRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GamePointsRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, TotalPointsRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, TeamRankRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GoalsScoredRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GoalsAgainstRequestCreator>();
			services.AddSingleton<IStandingsRequestCreator, GoalDifferentialRequestCreator>();

			// division sheet services
			string[] divisionNames = model.Divisions.Keys.ToArray();
			foreach (string divisionName in divisionNames)
			{
				services.AddSingleton<IDivisionSheetService>(provider => new DivisionSheetService(divisionName,
					helper,
					provider.GetRequiredService<StandingsRequestCreatorFactory>(),
					provider.GetRequiredService<ISheetsClient>(),
					provider.GetRequiredService<IOptionsSnapshot<ScoreSheetConfiguration>>(),
					provider.GetRequiredService<ILogger<DivisionSheetService>>())
				);
			}

			services.AddSingleton<ISpreadsheetBuilderService, SpreadsheetBuilderService>();

			IServiceProvider provider = services.BuildServiceProvider();
			ISpreadsheetBuilderService builder = provider.GetRequiredService<ISpreadsheetBuilderService>();
			builder.BuildSpreadsheet();

			return Ok();
		}

		/// <summary>
		/// In this sample site, we created the team list on the client side, so instad of reading the file on the server, we can just use the values sent by the browser
		/// </summary>
		private class PostedTeamReaderService : ITeamReaderService
		{
			Dictionary<string, IList<Team>> _teams;
			public PostedTeamReaderService(Dictionary<string, IList<Team>> teams)
			{
				_teams = teams;
			}

			public IDictionary<string, IList<Team>> GetTeams() => _teams;
		}
	}
}
