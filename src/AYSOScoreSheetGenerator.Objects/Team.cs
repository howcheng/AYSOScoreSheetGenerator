namespace AYSOScoreSheetGenerator.Objects
{
	/// <summary>
	/// Represents a team (duh)
	/// </summary>
	public class Team
	{
		/// <summary>
		/// Name of the program the team belongs to (e.g., "Fall season", "All-Stars")
		/// </summary>
		public string? ProgramName { get; set; }
		/// <summary>
		/// Name of the division the team belongs to (e.g., "10U Boys")
		/// </summary>
		public string? DivisionName { get; set; }
		/// <summary>
		/// Name of the team (e.g. "Sharks", "Team 01: Smith")
		/// </summary>
		public string? TeamName { get; set; }
		/// <summary>
		/// The cell reference for this team on the team list sheet (e.g., A2)
		/// </summary>
		public string? TeamSheetCell { get; set; }
	}
}