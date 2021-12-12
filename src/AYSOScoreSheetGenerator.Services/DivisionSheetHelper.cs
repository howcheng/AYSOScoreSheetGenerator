using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StandingsGoogleSheetsHelper;

namespace AYSOScoreSheetGenerator.Services
{
	/// <summary>
	/// Extension of the <see cref="StandingsSheetHelper"/> specific to making regular season division sheets
	/// </summary>
	public class DivisionSheetHelper : StandingsSheetHelper
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="columnHeaders">A collection of all the column headers (including the ones in <paramref name="standingsTableHeaders"/>)</param>
		/// <param name="standingsTableHeaders">A collection of all the column header that make up the standings table</param>
		public DivisionSheetHelper(IEnumerable<string> columnHeaders, IEnumerable<string> standingsTableHeaders)
			: base(columnHeaders, standingsTableHeaders)
		{
		}

		public string RefPointsColumnName { get { return GetColumnNameByHeader(Constants.HDR_REF_PTS); } }
		public string VolunteerPointsColumnName { get { return GetColumnNameByHeader(Constants.HDR_VOL_PTS); } }
		public string SportsmanshipPointsColumnName { get { return GetColumnNameByHeader(Constants.HDR_SPORTSMANSHIP_PTS); } }
		public string PointsDeducationColumnName { get { return GetColumnNameByHeader(Constants.HDR_PTS_DEDUCTION); } }
	}
}
