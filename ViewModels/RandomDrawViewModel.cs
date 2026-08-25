using EduTrack.Models;

namespace EduTrack.ViewModels
{
    public class RandomDrawViewModel
    {
        public List<int> SelectedGroupIds { get; set; } = new();
        public List<Group> Groups { get; set; } = new();

        // Adminning eng so'nggi sessiyasi (agar mavjud bo'lsa) — Current/Next ko'rinishi uchun.
        public PitchSessionViewModel? ActiveSession { get; set; }
    }

    public class PitchSessionViewModel
    {
        public int SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public List<string> GroupNames { get; set; } = new();
        public int RemainingEligibleCount { get; set; }
        public int TotalParticipants { get; set; }
        public PitchParticipantViewModel? Current { get; set; }
        public PitchParticipantViewModel? Next { get; set; }
    }

    public class PitchParticipantViewModel
    {
        public int ParticipantId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
