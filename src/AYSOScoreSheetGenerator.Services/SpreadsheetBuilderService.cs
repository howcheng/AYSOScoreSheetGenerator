using AYSOScoreSheetGenerator.Objects;
using GoogleSheetsHelper;
using Microsoft.Extensions.Options;

namespace AYSOScoreSheetGenerator.Services
{
	public interface ISpreadsheetBuilderService
	{
		Task BuildSpreadsheet();
	}

	public class SpreadsheetBuilderService : SheetService, ISpreadsheetBuilderService
	{
		ITeamReaderService _readerSvc;
		IEnumerable<ITeamListSheetService> _teamSheetServices;
		IEnumerable<IDivisionSheetService> _divisionSheetServices;

		public SpreadsheetBuilderService(ITeamReaderService teamReader, IEnumerable<ITeamListSheetService> teamSheetSvcs, IEnumerable<IDivisionSheetService> divSheetSvcs
			, ISheetsClient sheetsClient, IOptions<ScoreSheetConfiguration> configOptions) : base(sheetsClient, configOptions)
		{
			_readerSvc = teamReader;
			_teamSheetServices = teamSheetSvcs;
			_divisionSheetServices = divSheetSvcs;
		}

		public async Task BuildSpreadsheet()
		{
			IDictionary<string, IList<Team>> divisions = _readerSvc.GetTeams();

			foreach (ITeamListSheetService teamSheetSvc in _teamSheetServices)
			{
				await teamSheetSvc.BuildSheet(divisions);
			}

			foreach (KeyValuePair<string, IList<Team>> division in divisions)
			{
				IDivisionSheetService? divSheetSvc = _divisionSheetServices.SingleOrDefault(x => x.IsApplicableToDivision(division.Key));
				if (divSheetSvc == null)
					throw new InvalidOperationException($"No {nameof(IDivisionSheetService)} instance set up for division {division.Key}");

				await divSheetSvc.BuildSheet(division.Value);
			}
		}
	}
}
