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
			, ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<SpreadsheetBuilderService> log) 
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
					Log.LogInformation("Created a new spreadsheet with ID {id}", SheetsClient.SpreadsheetId);
				}
				else
				{
					Spreadsheet spreadsheet = await SheetsClient.LoadSpreadsheet(Configuration.SpreadsheetId);
					Log.LogInformation("Using the existing spreadsheet with ID {id}", Configuration.SpreadsheetId);
					if (spreadsheet.Properties.Title != Configuration.SpreadsheetTitle)
						await SheetsClient.RenameSpreadsheet(Configuration.SpreadsheetTitle);
				}

				IDictionary<string, IList<Team>> divisions = _readerSvc.GetTeams();

				foreach (ITeamListSheetService teamSheetSvc in _teamSheetServices.OrderBy(x => x.Ordinal))
				{
					await teamSheetSvc.BuildSheet(divisions);
				}

				string[] divisionNames = divisions.Keys.OrderBy(x => x).ToArray();
				foreach (string divisionName in divisionNames)
				{
					IList<Team> divisionTeams = divisions[divisionName];
					IDivisionSheetService? divSheetSvc = _divisionSheetServices.SingleOrDefault(x => x.IsApplicableToDivision(divisionName));
					if (divSheetSvc == null)
						throw new InvalidOperationException($"No {nameof(IDivisionSheetService)} instance set up for division {divisionName}");

					await divSheetSvc.BuildSheet(divisionTeams);
				}

				Log.LogInformation("All done!");
			}
			catch (Exception ex)
			{
				Log.LogError(ex, "Uh-oh, something went wrong: {message}", ex.Message);
			}
		}
	}
}
