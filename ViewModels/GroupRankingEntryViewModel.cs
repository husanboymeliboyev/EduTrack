namespace EduTrack.ViewModels
{
    public class GroupRankingEntryViewModel
    {
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public double AveragePercentage { get; set; }
        public int Rank { get; set; }
    }
}