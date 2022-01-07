using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AYSOScoreSheetGenerator.Objects
{
	/// <summary>
	/// Configuration values for building the score sheet
	/// </summary>
	public class ScoreSheetConfiguration
	{
		/// <summary>
		/// The Google ID of the spreadsheet; get it from the URL of the sheet: docs.google.com/spreadsheets/d/SPREADHEET_ID/edit#gid=0
		/// </summary>
		public string? SpreadsheetId { get; set; }

		/// <summary>
		/// The spreadsheet title, e.g. "2021 Region 42 Scores and Standings"
		/// </summary>
		public string? SpreadsheetTitle { get; set; }

		/// <summary>
		/// The program name from SportsConnect that we are building the spreadsheet for, e.g., "2021 Fall Core"
		/// </summary>
		public string? ProgramName { get; set; }

		/// <summary>
		/// The list of divisions that the program should create scores and standings sheets for
		/// </summary>
		public IEnumerable<string> Divisions { get; set; } = new List<string>();

		/// <summary>
		/// A function to return the contents of the Team Details Report as a Stream
		/// </summary>
		public Func<Stream>? TeamDetailsReportProvider { get; set; }

		/// <summary>
		/// A function to transform the team name as entered into SportsConnect into something more suitable for the score sheet
		/// </summary>
		public Func<string, string>? TeamNameTransformer { get; set; }

		/// <summary>
		/// The name of the sheet that lists all the teams (default: "Teams")
		/// </summary>
		public string TeamsSheetName { get; set; } = "Teams";

		/// <summary>
		/// A collection of dates when games will be played
		/// </summary>
		public IEnumerable<DateTime> GameDates { get; set; } = new List<DateTime>();

		/// <summary>
		/// The number of games in a season
		/// </summary>
		public int TotalNumberOfRounds { get => GameDates.Count(); }

		/// <summary>
		/// A collection of configuration values for sheets for entering values that can affect game points (e.g., referee or volunteer)
		/// </summary>
		private IList<PointsAdjustmentSheetConfiguration> _pointsAdjustmentSheetConfigurations = new List<PointsAdjustmentSheetConfiguration>();

		/// <summary>
		/// The configuration values for the referee points sheet
		/// </summary>
		public RefPointsSheetConfiguration? RefPointsSheetConfiguration
		{
			get => (RefPointsSheetConfiguration?)_pointsAdjustmentSheetConfigurations.SingleOrDefault(x => x.Type == PointsAdjustmentSheetType.Referee);
			set
			{
				if (value != null)
					_pointsAdjustmentSheetConfigurations.Add(value);
			}
		}

		/// <summary>
		/// The configuration values for the volunteer points sheet
		/// </summary>
		public VolunteerPointsSheetConfiguration? VolunteerPointsSheetConfiguration
		{
			get => (VolunteerPointsSheetConfiguration?)_pointsAdjustmentSheetConfigurations.SingleOrDefault(x => x.Type == PointsAdjustmentSheetType.Volunteer);
			set
			{
				if (value != null)
					_pointsAdjustmentSheetConfigurations.Add(value);
			}
		}

		/// <summary>
		/// The configuration values for the sportsmanship points sheet
		/// </summary>
		public SportsmanshipPointsSheetConfiguration? SportsmanshipPointsSheetConfiguration
		{
			get => (SportsmanshipPointsSheetConfiguration?)_pointsAdjustmentSheetConfigurations.SingleOrDefault(x => x.Type == PointsAdjustmentSheetType.Sportsmanship);
			set
			{
				if (value != null)
					_pointsAdjustmentSheetConfigurations.Add(value);
			}
		}

		/// <summary>
		/// The configuration values for the points deduction sheet
		/// </summary>
		public PointsDeductionSheetConfiguration? PointsDeductionSheetConfiguration
		{
			get => (PointsDeductionSheetConfiguration?)_pointsAdjustmentSheetConfigurations.SingleOrDefault(x => x.Type == PointsAdjustmentSheetType.Deduction);
			set
			{
				if (value != null)
					_pointsAdjustmentSheetConfigurations.Add(value);
			}
		}

		/// <summary>
		/// The background color for the header row for the teams sheet (default: #999999, or medium gray)
		/// </summary>
		public System.Drawing.Color TeamsSheetHeaderColor { get; set; } = System.Drawing.Color.FromArgb(0x99, 0x99, 0x99);
		/// <summary>
		/// The background color for the round header (default: #A4C2F4, or light blue)
		/// </summary>
		public System.Drawing.Color StandingsSheetRoundHeaderColor { get; set; } = System.Drawing.Color.FromArgb(0xA4, 0xC2, 0xF4);
		/// <summary>
		/// The background color for the header rows for the standings sheet (default: #999999, or medium gray)
		/// </summary>
		public System.Drawing.Color StandingsSheetHeaderColor { get; set; } = System.Drawing.Color.FromArgb(0x99, 0x99, 0x99);

		/// <summary>
		/// A collection of configuration values for each division that the application will make a score sheet for
		/// </summary>
		public IList<DivisionConfiguration> DivisionConfigurations { get; set; } = new List<DivisionConfiguration>();

		/// <summary>
		/// Width of the team name column; do not set a value for this, as it's used internally once we auto-resize the team name column to accommodate the longest team name
		/// </summary>
		public int TeamNameColumnWidth { get; set; }
	}
}
