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
	}

	/// <summary>
	/// Service class to build the sheet containing the list of teams (and nothing else)
	/// </summary>
	public class TeamListSheetService : SheetService, ITeamListSheetService
	{
		public TeamListSheetService([NotNull] ISheetsClient sheetsClient, IOptionsSnapshot<ScoreSheetConfiguration> configOptions, ILogger<TeamListSheetService> log)
			: base(sheetsClient, configOptions, log)
		{
		}

		public virtual async Task BuildSheet(IDictionary<string, IList<Team>> divisions)
		{
			Sheet teamSheet = await SetUpSheet();

			int rowIndex = 0;

			// create requests for each division
			List<AppendRequest> requests = new List<AppendRequest>();
			foreach (KeyValuePair<string, IList<Team>> division in divisions)
			{
				List<GoogleSheetRow> rows = new List<GoogleSheetRow>(division.Value.Count + 1);
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
				headerRow.AddRange(CreatePointsAdjustmentColumnHeaders());
				rows.Add(headerRow);
				rowIndex++;

				// team names
				IEnumerable<Team> teams = GetTeamsForDivision(division.Value);
				foreach (Team team in teams)
				{
					GoogleSheetCell cell = CreateGoogleSheetCellForTeam(team, rowIndex);
					rows.Add(new GoogleSheetRow { cell });
					rowIndex++;
				}
				rows.Add(new GoogleSheetRow { new GoogleSheetCell { StringValue = string.Empty } }); // blank row
				rowIndex++;

				AppendRequest request = new AppendRequest(teamSheet.Properties.Title)
				{
					Rows = rows,
				};
				requests.Add(request);
			}

			await SheetsClient.Append(requests);

			// resize the first column to the longest team name
			await SheetsClient.AutoResizeColumn(teamSheet.Properties.Title, 0);
		}

		protected virtual async Task<Sheet> SetUpSheet()
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

			return await SheetsClient.GetOrAddSheet(Configuration.TeamsSheetName);
		}

		protected virtual IEnumerable<Team> GetTeamsForDivision(IList<Team> teams) => teams;

		protected virtual GoogleSheetCell CreateGoogleSheetCellForTeam(Team team, int rowIndex)
		{
			GoogleSheetCell cell = new GoogleSheetCell
			{
				StringValue = team.TeamName
			};

			team.TeamSheetCell = $"A{rowIndex + 1}";

			return cell;
		}

		protected virtual IEnumerable<GoogleSheetCell> CreatePointsAdjustmentColumnHeaders() => new GoogleSheetCell[0];
	}
}
