using System.Text.RegularExpressions;
using AYSOScoreSheetGenerator.Lib;
using AYSOScoreSheetGenerator.Models;
using AYSOScoreSheetGenerator.Objects;
using AYSOScoreSheetGenerator.Services;
using GoogleSheetsHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class ServicesController : ControllerBase
	{
		private static Regex s_reTeamName = new Regex(@"\d{2}U(B|G)(\.+)"); // 10UB01 - Smith

		private readonly ISheetsClient _sheetsClient;
		private readonly IOptionsMonitorCache<ScoreSheetConfiguration> _optionsMonitorCache;
		private readonly IHubContext<LoggerHub, IScoreSheetLogger> _hub;

		public ServicesController(ISheetsClient sheetsClient, IOptionsMonitorCache<ScoreSheetConfiguration> optionsCache, IHubContext<LoggerHub, IScoreSheetLogger> hub)
		{
			_sheetsClient = sheetsClient;
			_optionsMonitorCache = optionsCache;
			_hub = hub;
		}

		[HttpPost]
		public Task<IActionResult> Post([FromBody] UploadModel model)
		{
			// figure out what services we are going to need
			bool hasRefPts = model.SpreadsheetConfiguration.RefPointsSheetConfiguration != null;
			bool hasVolPts = model.SpreadsheetConfiguration.VolunteerPointsSheetConfiguration != null;
			bool hasSptsPts = model.SpreadsheetConfiguration.SportsmanshipPointsSheetConfiguration != null;
			bool hasPtsDed = model.SpreadsheetConfiguration.PointsDeductionSheetConfiguration != null;

			_optionsMonitorCache.Clear();
			model.SpreadsheetConfiguration.TeamNameTransformer = name =>
			{
				// 10UB01 - Smith
				if (s_reTeamName.IsMatch(name))
				{
					GroupCollection groups = s_reTeamName.Match(name).Groups;
					return groups[groups.Count - 1].Value;
				}
				return name;

				// 10UB - 01 Smith
				//string[] arr = name.Split('-'); // the team names have the division name in them but that's redundant
				//return arr.Skip(1).Aggregate((s1, s2) => $"{s1.Trim()}-{s2.Trim()}"); // in case of a hyphenated last name
			};
			_optionsMonitorCache.TryAdd(string.Empty, model.SpreadsheetConfiguration);

			IServiceCollection services = new ServiceCollection();
			services.AddLogging(builder => builder.ClearProviders().AddDebug().AddSignalRLogging()); // we have to reregister the loggers because this is a separate container
			services.AddSignalR();
			services.AddSingleton(_hub); // we have to use pre-existing hub otherwise it won't know about any active connections
			services.AddSingleton(_sheetsClient);
			services.AddSingleton(_optionsMonitorCache);
			services.AddSingleton<ITeamReaderService>(new PostedTeamReaderService(model.Divisions));

			List<string> standingsHeaders = new List<string>
			{
				Constants.HDR_TEAM_NAME,
				Constants.HDR_GAMES_PLAYED, 
				Constants.HDR_NUM_WINS, 
				Constants.HDR_NUM_LOSSES, 
				Constants.HDR_NUM_DRAWS, 
				Constants.HDR_GAME_PTS,
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
				if (model.SpreadsheetConfiguration.RefPointsSheetConfiguration!.AffectsStandings)
				{
					standingsHeaders.Insert(insertIdx++, Constants.HDR_REF_PTS);
					services.AddSingleton<IStandingsRequestCreator, RefPointsRequestCreator>();
				}
			}
			if (hasVolPts)
			{
				services.AddSingleton<ITeamListSheetService, VolunteerPointsSheetService>();
				if (model.SpreadsheetConfiguration.VolunteerPointsSheetConfiguration!.AffectsStandings)
				{
					standingsHeaders.Insert(insertIdx++, Constants.HDR_VOL_PTS);
					services.AddSingleton<IStandingsRequestCreator, VolunteerPointsRequestCreator>();
				}
			}
			if (hasSptsPts)
			{
				services.AddSingleton<ITeamListSheetService, SportsmanshipPointsSheetService>();
				if (model.SpreadsheetConfiguration.SportsmanshipPointsSheetConfiguration!.AffectsStandings)
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
				services.AddSingleton<IDivisionSheetService>(provider => ActivatorUtilities.CreateInstance<DivisionSheetService>(provider, divisionName, helper));
			}

			services.AddSingleton<ISpreadsheetBuilderService, SpreadsheetBuilderService>();

			IServiceProvider provider = services.BuildServiceProvider();
			ISpreadsheetBuilderService builder = provider.GetRequiredService<ISpreadsheetBuilderService>();
			_ = builder.BuildSpreadsheet();

			return Task.FromResult((IActionResult)Ok());
		}

		/// <summary>
		/// In this sample site, we created the team list on the client side, so instad of reading the file on the server, we can just use the values sent by the browser
		/// </summary>
		private class PostedTeamReaderService : ITeamReaderService
		{
			IDictionary<string, IList<Team>> _teams;
			public PostedTeamReaderService(Dictionary<string, IList<Team>> teams)
			{
				_teams = teams.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
			}

			public IDictionary<string, IList<Team>> GetTeams() => _teams;
		}
	}
}
