using System.Diagnostics.CodeAnalysis;
using AYSOScoreSheetGenerator.Objects;
using Google.Apis.Sheets.v4.Data;
using GoogleSheetsHelper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AYSOScoreSheetGenerator.Services
{
	/// <summary>
	/// Interface for classes that build sheets containing lists of teams
	/// </summary>
	public interface ITeamListSheetService
	{
		/// <summary>
		/// Builds the sheet
		/// </summary>
		/// <param name="divisions"></param>
		/// <returns></returns>
		Task BuildSheet(IDictionary<string, IList<Team>> divisions);
		/// <summary>
		/// The order in which the sheet should come in the document
		/// </summary>
		int Ordinal { get; }
	}

	/// <summary>
	/// Service class to build the sheet containing the list of teams (and nothing else). This sheet must be the first one created!
	/// </summary>
	public class TeamListSheetService : SheetService, ITeamListSheetService
	{
		private IOptionsMonitorCache<ScoreSheetConfiguration> _optionsCache;

		public virtual int Ordinal { get => 1; }

		public TeamListSheetService([NotNull] ISheetsClient sheetsClient, IOptionsMonitor<ScoreSheetConfiguration> configOptions, ILogger<TeamListSheetService> log
			, IOptionsMonitorCache<ScoreSheetConfiguration> optionsCache)
			: base(sheetsClient, configOptions, log)
		{
			_optionsCache = optionsCache;
		}

		public async Task BuildSheet(IDictionary<string, IList<Team>> divisions)
		{
			Sheet teamSheet = await SetUpSheet();

			int rowIndex = 0;

			// create requests for each division -- use UpdateRequests because this seems to leave the first row blank
			List<UpdateRequest> requests = new List<UpdateRequest>();
			foreach (KeyValuePair<string, IList<Team>> division in divisions)
			{
				List<GoogleSheetRow> rows = new List<GoogleSheetRow>(division.Value.Count + 1);
				UpdateRequest request = new UpdateRequest(teamSheet.Properties.Title)
				{
					Rows = rows,
					RowStart = rowIndex,
				};
				// header row
				GoogleSheetRow headerRow = new GoogleSheetRow
				{
					new GoogleSheetCell // first column is the team name
					{
						StringValue = division.Key,
						Bold = true,
						BackgroundColor = Configuration.TeamsSheetHeaderColor,
					}
				};
				rows.Add(headerRow);
				rowIndex++;

				// team names
				IEnumerable<Team> teams = division.Value.OrderBy(x => x.TeamName);
				foreach (Team team in teams)
				{
					GoogleSheetCell cell = CreateGoogleSheetCellForTeam(team, rowIndex);
					rows.Add(new GoogleSheetRow { cell });
					rowIndex++;
				}
				rows.Add(new GoogleSheetRow { new GoogleSheetCell { StringValue = string.Empty } }); // blank row
				rowIndex++;

				requests.Add(request);
			}

			await SheetsClient.Update(requests);

			// resize the first column to the longest team name and update the options cache
			ScoreSheetConfiguration config = Configuration;
			config.TeamNameColumnWidth = await SheetsClient.AutoResizeColumn(teamSheet.Properties.Title, 0);
			_optionsCache.TryRemove(string.Empty);
			_optionsCache.TryAdd(string.Empty, config);
		}

		private async Task<Sheet> SetUpSheet()
		{
			Log.LogInformation("Beginning the team list sheet");
			IList<string> sheetNames = await SheetsClient.GetSheetNames();
			if (sheetNames.Count > 1)
			{
				// delete all other sheets
				foreach (string sheetName in sheetNames.Skip(1))
				{
					await SheetsClient.DeleteSheet(sheetName);
				}
			}
			if (sheetNames.First() != Configuration.TeamsSheetName)
				return await SheetsClient.RenameSheet(sheetNames.First(), Configuration.TeamsSheetName);

			await SheetsClient.ClearSheet(Configuration.TeamsSheetName);

			return await SheetsClient.GetOrAddSheet(Configuration.TeamsSheetName);
		}

		private GoogleSheetCell CreateGoogleSheetCellForTeam(Team team, int rowIndex)
		{
			string teamName = team.TeamName!;
			if (Configuration.TeamNameTransformer != null)
				teamName = Configuration.TeamNameTransformer(teamName);
			GoogleSheetCell cell = new GoogleSheetCell
			{
				StringValue = teamName
			};

			team.TeamSheetCell = $"A{rowIndex + 1}";

			return cell;
		}
	}
}
