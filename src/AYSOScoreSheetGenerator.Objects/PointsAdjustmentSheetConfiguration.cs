using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AYSOScoreSheetGenerator.Objects
{
	public enum PointsAdjustmentSheetType
	{
		Referee,
		Volunteer,
		Sportsmanship,
		Deduction
	}

	/// <summary>
	/// Configuration values for sheets for entering values that can make adjustments to the game points (e.g., referee or volunteer points)
	/// </summary>
	public abstract class PointsAdjustmentSheetConfiguration
	{
		/// <summary>
		/// Name of the sheet
		/// </summary>
		public string? SheetName { get; set; }

		/// <summary>
		/// Flag that indicates that the values entered on the sheet are cumulative
		/// </summary>
		public bool ValueIsCumulative { get; set; }

		/// <summary>
		/// Flag that indicates the values will affect game standings
		/// </summary>
		public bool AffectsStandings { get; set; } = true;

		public PointsAdjustmentSheetType Type { get; }

		public PointsAdjustmentSheetConfiguration(PointsAdjustmentSheetType type)
		{
			Type = type;
		}
	}

	public class RefPointsSheetConfiguration : PointsAdjustmentSheetConfiguration
	{
		public RefPointsSheetConfiguration() : base(PointsAdjustmentSheetType.Referee)
		{
			SheetName = "Ref Pts";
		}
	}

	public class VolunteerPointsSheetConfiguration : PointsAdjustmentSheetConfiguration
	{
		public VolunteerPointsSheetConfiguration() : base(PointsAdjustmentSheetType.Volunteer)
		{
			SheetName = "Vol Pts";
		}
	}

	public class SportsmanshipPointsSheetConfiguration : PointsAdjustmentSheetConfiguration
	{
		public SportsmanshipPointsSheetConfiguration() : base(PointsAdjustmentSheetType.Sportsmanship)
		{
			SheetName = "Spts Pts";
		}
	}

	public class PointsDeductionSheetConfiguration : PointsAdjustmentSheetConfiguration
	{
		public PointsDeductionSheetConfiguration() : base(PointsAdjustmentSheetType.Deduction)
		{
			SheetName = "Pts Deduct";
		}
	}
}
