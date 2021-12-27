using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AYSOScoreSheetGenerator.Objects
{
	/// <summary>
	/// Configuration values for each division
	/// </summary>
	public class DivisionConfiguration
	{
		/// <summary>
		/// Name of the division (e.g., "10U Boys")
		/// </summary>
		public string? DivisionName { get; set; }
		/// <summary>
		/// For divisions with an odd number of teams, the odd team out can either have a bye week (i.e., no game) or have a "friendly" (scrimmage) against another team which won't count for points
		/// </summary>
		public bool HasFriendlyGamesEachRound { get; set; }
		/// <summary>
		/// In some divisions, you might say that the first two weeks are practice games only and they don't count towards standings. In this example, if you have 10 rounds of games, then this value would be 8.
		/// </summary>
		public int RoundsThatCountTowardsStandings { get; set; }
		/// <summary>
		/// In the Team Detail Report, the program name used for teams from other AYSO regions (if you have interregional play)
		/// </summary>
		public string? ProgramNameForOtherRegions { get; set; }
		/// <summary>
		/// Indicates that teams for this region will play those from other regions
		/// </summary>
		public bool HasInterregionalPlay { get => !string.IsNullOrEmpty(ProgramNameForOtherRegions); }
		/// <summary>
		/// If you have interregional play, should the teams from other regions also be included in the standings (default: false)
		/// </summary>
		public bool IncludeOtherRegionsInStandings { get; set; }
	}
}
