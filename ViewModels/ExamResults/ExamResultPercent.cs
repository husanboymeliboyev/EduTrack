namespace EduTrack.ViewModels
{
    // Foiz ranglari uchun yagona chegara mantiqi: sahifa va Excel bir xil
    // qoidalardan foydalanadi (>= 86 yashil, >= 60 sariq, qolgani qizil).
    public enum ExamResultPercentBand
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public static class ExamResultPercent
    {
        public const double HighThreshold = 86;
        public const double MediumThreshold = 60;

        public static ExamResultPercentBand GetBand(double percent)
        {
            if (percent >= HighThreshold)
            {
                return ExamResultPercentBand.High;
            }

            if (percent >= MediumThreshold)
            {
                return ExamResultPercentBand.Medium;
            }

            return ExamResultPercentBand.Low;
        }
    }
}
