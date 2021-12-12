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
		/// The name of the sheet for entering referee points (default: "Ref Pts", set to null if you don't need a sheet)
		/// </summary>
		public string RefPointsSheetName { get; set; } = "Ref Pts";

		/// <summary>
		/// The name of the sheet for entering volunteer points (default: "Vol Pts", set to null if you don't need a sheet)
		/// </summary>
		public string VolunteerPointsSheetName { get; set; } = "Volunteer Pts";

		/// <summary>
		/// The name of the sheet for entering sportsmanship points (default: "Sptship Pts", set to null if you don't need a sheet)
		/// </summary>
		public string SportsmanshipPointsSheetName { get; set; } = "Sptship Pts";

		/// <summary>
		/// The name of the sheet for entering points deductions (default: "Pts Deductions", set to null if you don't need a sheet)
		/// </summary>
		public string PointsDeductionSheetName { get; set; } = "Pts Deductions";

		public bool RefPointsValueIsCumulative { get; set; }
		public bool VolunteerPointsValueIsCumulative { get; set; }
		public bool SportsmanshipPointsValueIsCumulative { get; set; }
		public bool PointsDeductionValueIsCumulative { get; set; }

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
	}
}
