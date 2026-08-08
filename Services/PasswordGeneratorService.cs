using System.Security.Cryptography;

namespace EduTrack.Services
{
    /// <summary>
    /// Tizim parol siyosatiga (katta/kichik harf, raqam, maxsus belgi, kamida 8 belgi)
    /// mos, tasodifiy va xavfsiz parol generatsiya qiladi. Admin foydalanuvchi
    /// yaratganda parolni qo'lda o'ylab topishi shart emas.
    /// </summary>
    public class PasswordGeneratorService : IPasswordGeneratorService
    {
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // I, O kabi chalkash harflar olib tashlandi
        private const string Lower = "abcdefghijkmnpqrstuvwxyz";
        private const string Digits = "23456789"; // 0, 1 kabi chalkash raqamlar olib tashlandi
        private const string Special = "!@#$%";

        public string Generate()
        {
            var all = Upper + Lower + Digits + Special;
            var chars = new List<char>
            {
                PickRandom(Upper),
                PickRandom(Lower),
                PickRandom(Digits),
                PickRandom(Special)
            };

            while (chars.Count < 10)
            {
                chars.Add(PickRandom(all));
            }

            // Belgilarni aralashtiramiz, aks holda parol har doim "Katta-kichik-raqam-belgi..." tartibida bo'lib qoladi
            return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
        }

        private static char PickRandom(string source)
        {
            return source[RandomNumberGenerator.GetInt32(source.Length)];
        }
    }
}