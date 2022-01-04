using System;
using AutoFixture;
using AYSOScoreSheetGenerator.Services;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.DependencyInjection;
using StandingsGoogleSheetsHelper;
using Xunit;
using Xunit.Abstractions;

namespace AYSOScoreSheetGenerator.UnitTests
{
	public class RequestCreatorTests : BaseTest
	{
		public RequestCreatorTests(ITestOutputHelper outputHelper) : base(outputHelper)
		{
		}

		[Fact]
		public void TestTotalPointsRequestCreator()
		{
			// we have both ref and volunteer points columns so the formula should include both
			DivisionSheetHelper helper = CreateDivisionSheetHelper();
			IServiceProvider provider = new ServiceCollection()
				.AddSingleton<StandingsSheetHelper>(helper)
				.AddSingleton<FormulaGenerator>()
				.AddSingleton<TotalPointsRequestCreator>()
				.BuildServiceProvider();

			TotalPointsRequestCreator creator = provider.GetRequiredService<TotalPointsRequestCreator>();

			Fixture f = new Fixture();
			StandingsRequestCreatorConfig config = f.Build<StandingsRequestCreatorConfig>().With(x => x.SheetId, () => f.Create<int>()).Create();

			Request request = creator.CreateRequest(config);
			Assert.NotNull(request.RepeatCell);
			Assert.Equal(config.SheetId, request.RepeatCell.Range.SheetId);
			Assert.Equal(config.SheetStartRowIndex, request.RepeatCell.Range.StartRowIndex);
			Assert.Equal(config.SheetStartRowIndex + config.NumTeams, request.RepeatCell.Range.EndRowIndex);
			Assert.Equal(helper.GetColumnIndexByHeader(Constants.HDR_TOTAL_PTS), request.RepeatCell.Range.StartColumnIndex);

			string expectedFormula = $"={helper.GamePointsColumnName}{config.StartGamesRowNum} + {helper.RefPointsColumnName}{config.StartGamesRowNum} + {helper.VolunteerPointsColumnName}{config.StartGamesRowNum}";
			string formula = request.RepeatCell.Cell.UserEnteredValue.FormulaValue;
			Assert.Equal(expectedFormula, formula);
		}
		
		[Theory]
		[InlineData(true, 1)]
		[InlineData(true, 2)]
		[InlineData(false, 1)]
		[InlineData(false, 2)]
		public void TestPointsAdjustmentRequestCreator(bool valueIsCumulative, int roundNum)
		{
			// in the second round the formula must account for the value from the first round

			DivisionSheetHelper helper = CreateDivisionSheetHelper();
			IServiceProvider provider = new ServiceCollection()
				.AddSingleton<StandingsSheetHelper>(helper)
				.AddSingleton<FormulaGenerator>()
				.AddSingleton<RefPointsRequestCreator>()
				.BuildServiceProvider();

			RefPointsRequestCreator creator = provider.GetRequiredService<RefPointsRequestCreator>();

			Fixture f = new Fixture();
			PointsAdjustmentRequestCreatorConfig config = f.Build<PointsAdjustmentRequestCreatorConfig>()
				.With(x => x.ValueIsCumulative, valueIsCumulative)
				.With(x => x.RoundNumber, roundNum)
				.With(x => x.FirstTeamsSheetCell, "Teams!A2")
				.With(x => x.LastRoundStartRowNum, roundNum == 1 ? 0 : f.Create<int>())
				.Create();

			Request request = creator.CreateRequest(config);
			Assert.NotNull(request.RepeatCell);
			Assert.Equal(config.SheetId, request.RepeatCell.Range.SheetId);
			Assert.Equal(config.SheetStartRowIndex, request.RepeatCell.Range.StartRowIndex);
			Assert.Equal(config.SheetStartRowIndex + config.NumTeams, request.RepeatCell.Range.EndRowIndex);
			Assert.Equal(helper.GetColumnIndexByHeader(Constants.HDR_REF_PTS), request.RepeatCell.Range.StartColumnIndex);

			string refPtsCell = $"'{config.SheetName}'!{(roundNum == 1 ? "B" : "C")}3"; // column name depends on round number, row number is derived from FirstTeamsSheetCell
			string expectedFormula;
			if (valueIsCumulative)
			{
				// when the value is cumulative, then you have to use the value from the previous round if there are no points entered in the current round
				string elseValue = roundNum == 1 ? "0" : $"{helper.RefPointsColumnName}{config.LastRoundStartRowNum}";
				expectedFormula = $"=IF({refPtsCell}=\"\", {elseValue}, {refPtsCell})";
			}
			else
			{
				// when the value is not cumulative, then you have to add the value from the previous round (when round > 1)
				expectedFormula = $"={refPtsCell}";
				if (roundNum > 1)
					expectedFormula += $"+{helper.RefPointsColumnName}{config.LastRoundStartRowNum}";
			}
			string formula = request.RepeatCell.Cell.UserEnteredValue.FormulaValue;
			Assert.Equal(expectedFormula, formula);
		}
	}
}
