using AYSOScoreSheetGenerator.Objects;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using Microsoft.Extensions.Logging;
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
			, ISheetsClient sheetsClient, IOptionsSnapshot<ScoreSheetConfiguration> configOptions, ILogger<SpreadsheetBuilderService> log) 
			: base(sheetsClient, configOptions, log)
		{
			_readerSvc = teamReader;
			_teamSheetServices = teamSheetSvcs;
			_divisionSheetServices = divSheetSvcs;
		}

		public async Task BuildSpreadsheet()
		{
			try
			{
				Log.LogInformation("Beginning spreadsheet setup");

				if (Configuration.SpreadsheetId == null)
				{
					await SheetsClient.CreateSpreadsheet(Configuration.SpreadsheetTitle);
					Log.LogInformation("Created a new spreadsheet with ID {0}", SheetsClient.SpreadsheetId);
				}
				else
				{
					Spreadsheet spreadsheet = await SheetsClient.LoadSpreadsheet(Configuration.SpreadsheetId);
					Log.LogInformation("Using the existing spreadsheet with ID {0}", Configuration.SpreadsheetId);
					if (spreadsheet.Properties.Title != Configuration.SpreadsheetTitle)
						await SheetsClient.RenameSpreadsheet(Configuration.SpreadsheetTitle);
				}

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

				Log.LogInformation("All done!");
			}
			catch (Exception ex)
			{
				Log.LogError(ex, "Uh-oh, something went wrong: {0}", ex.Message);
			}
		}
	}
}
