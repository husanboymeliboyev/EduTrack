namespace EduTrack.ViewModels
{
    public class ComponentScoreViewModel
    {
        public string ComponentName { get; set; } = string.Empty;
        public int MaxScore { get; set; }
        public double? Score { get; set; } // null = hali kiritilmagan
    }

    public class StudentSubjectGradeViewModel
    {
        public string SubjectName { get; set; } = string.Empty;
        public List<ComponentScoreViewModel> Components { get; set; } = new();

        public double TotalScore => Components.Sum(c => c.Score ?? 0);
        public int MaxTotal => Components.Sum(c => c.MaxScore);
        public double Percentage => MaxTotal == 0 ? 0 : Math.Round(TotalScore / MaxTotal * 100, 1);
    }
}