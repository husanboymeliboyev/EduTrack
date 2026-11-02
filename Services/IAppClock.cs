namespace EduTrack.Services
{
    /// <summary>
    /// Ilova vaqti uchun yagona manba. Biznes vaqtlari (CreatedDate, CompletedDate,
    /// StartedDate va h.k.) <see cref="Now"/> orqali olinadi va konfiguratsiyada
    /// ko'rsatilgan vaqt mintaqasidagi mahalliy vaqt sifatida saqlanadi.
    /// Nomida "...Utc" bo'lgan maydonlar uchun <see cref="UtcNow"/> ishlatiladi.
    /// </summary>
    public interface IAppClock
    {
        /// <summary>Konfiguratsiyadagi vaqt mintaqasidagi joriy mahalliy vaqt.</summary>
        DateTime Now { get; }

        /// <summary>Konfiguratsiyadagi vaqt mintaqasidagi bugungi sana (vaqtsiz).</summary>
        DateTime Today { get; }

        /// <summary>Joriy UTC vaqt (faqat ...Utc maydonlari uchun).</summary>
        DateTime UtcNow { get; }

        /// <summary>Ilova ishlatayotgan vaqt mintaqasi.</summary>
        TimeZoneInfo TimeZone { get; }
    }
}
