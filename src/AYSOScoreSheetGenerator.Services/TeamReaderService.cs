using AYSOScoreSheetGenerator.Objects;
using Microsoft.Extensions.Options;

namespace AYSOScoreSheetGenerator.Services
{
	public interface ITeamReaderService
	{
		IDictionary<string, IList<Team>> GetTeams();
	}

	public class TeamReaderService : ITeamReaderService
	{
		private readonly ScoreSheetConfiguration _configuration;

		public TeamReaderService(IOptions<ScoreSheetConfiguration> configOptions)
		{
			_configuration = configOptions.Value;
		}

		public IDictionary<string, IList<Team>> GetTeams()
		{
			SortedDictionary<string, IList<Team>> teams = new SortedDictionary<string, IList<Team>>();

			if (_configuration.TeamDetailsReportProvider == null)
				throw new InvalidOperationException("No method set to get the SportsConnect Team Details Report");

			Stream stream = _configuration.TeamDetailsReportProvider();
			using (stream)
			using (StreamReader teamsReader = new StreamReader(stream))
			{
				bool first = true;
				while (!teamsReader.EndOfStream)
				{
					string? line = teamsReader.ReadLine();
					if (first)
					{
						// header row
						first = false;
						continue;
					}
					if (string.IsNullOrEmpty(line))
						continue;

					/* ASSUMPTION: The Team Details Report follows the pattern:
					 *		"Program Name","Division Name","Team Name" (with a bunch of other things we don't care about)
					 */
					string[] lineArr = line.Split(',');
					string division = TrimQuoteMarks(lineArr[1]);

					// skip the divisions that are not included the configuration
					if (!_configuration.Divisions.Any(x => x == division))
						continue;

					// skip the programs that are not included in this spreadsheet (keep teams that have the primary program name or the other regions' program name in case of interregional play)
					string programName = TrimQuoteMarks(lineArr[0]);
					DivisionConfiguration? divisionConfig = _configuration.DivisionConfigurations.SingleOrDefault(x => x.DivisionName == division);
					bool isMainProgram = programName == _configuration.ProgramName;
					bool isOtherRegionProgram = divisionConfig != null && programName == divisionConfig.ProgramNameForOtherRegions;
					if (!isMainProgram && !isOtherRegionProgram) 
						continue;

					string teamName = TrimQuoteMarks(lineArr[2]);
					if (_configuration.TeamNameTransformer != null)
						teamName = _configuration.TeamNameTransformer(teamName);
					Team team = new Team
					{
						ProgramName = programName,
						DivisionName = division,
						TeamName = teamName,
					};

					IList<Team> divisionTeams;
					if (teams.ContainsKey(division))
						divisionTeams = teams[division];
					else
					{
						divisionTeams = new List<Team>();
						teams.Add(division, divisionTeams);
					}
					divisionTeams.Add(team);
				}
			}

			return teams;
		}

		private static string TrimQuoteMarks(string s) => s.Trim().Replace("\"", string.Empty);
	}
}