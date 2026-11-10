using System;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class FinishRulesTests
    {
        [Theory]
        [InlineData(ChipVerdict.NotSupported, ReportFile.Step1, CleanupScope.NotSupported)]
        [InlineData(ChipVerdict.SupportedOnly, ReportFile.Step1, CleanupScope.SupportedOnly)]
        [InlineData(ChipVerdict.Indeterminate, ReportFile.Step1, CleanupScope.SupportedOnly)]
        public void AfterFirstRun_AChipThatIsNotProgrammed_FinishesWithTheFirstReport(ChipVerdict verdict, ReportFile source, CleanupScope cleanup)
        {
            Assert.Equal(new FinishPlan(source, cleanup), FinishRules.AfterFirstRun(verdict));
        }

        [Fact]
        public void AfterFirstRun_AProgrammableChip_DoesNotFinishYet()
        {
            Assert.Null(FinishRules.AfterFirstRun(ChipVerdict.Programmable));
        }

        [Fact]
        public void AfterFirstRun_AnUnknownVerdict_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FinishRules.AfterFirstRun((ChipVerdict)42));
        }

        [Theory]
        [InlineData(true, ReportFile.Step3)]
        [InlineData(false, ReportFile.Step2)]
        public void AfterProgramming_FinishesWithTheReadOutReport_OrTheOneBeforeIfThereIsNone(bool readOutReportExists, ReportFile source)
        {
            Assert.Equal(new FinishPlan(source, CleanupScope.Programmed), FinishRules.AfterProgramming(readOutReportExists));
        }
    }
}
