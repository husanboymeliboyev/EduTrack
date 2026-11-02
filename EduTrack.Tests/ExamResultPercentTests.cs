using EduTrack.ViewModels;
using Xunit;

namespace EduTrack.Tests
{
    public class ExamResultPercentTests
    {
        [Theory]
        [InlineData(59.9, ExamResultPercentBand.Low)]
        [InlineData(60.0, ExamResultPercentBand.Medium)]
        [InlineData(85.9, ExamResultPercentBand.Medium)]
        [InlineData(86.0, ExamResultPercentBand.High)]
        public void GetBand_UsesExpectedBoundaries(
            double percent,
            ExamResultPercentBand expected)
        {
            Assert.Equal(expected, ExamResultPercent.GetBand(percent));
        }
    }
}
