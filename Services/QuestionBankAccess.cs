using System.Linq.Expressions;
using EduTrack.Models;

namespace EduTrack.Services
{
    /// <summary>
    /// Savollar bankining o'qituvchiga tegishliligini aniqlash qoidasi.
    /// Bitta fanga bir nechta o'qituvchi biriktirilgan bo'lsa ham, banklar
    /// o'qituvchilar orasida aralashmaydi:
    /// <list type="bullet">
    /// <item>Yangi banklar <see cref="QuestionBank.TeacherId"/> bilan yaratiladi.</item>
    /// <item>Eski (legacy) banklarda TeacherId null bo'lishi mumkin — ular fan
    /// egasiga (yoki fan bo'yicha dars o'tadigan o'qituvchiga) tegishli deb qaraladi.
    /// Migration mavjud banklarni fan egasiga biriktiradi.</item>
    /// </list>
    /// </summary>
    public static class QuestionBankAccess
    {
        /// <summary>
        /// Berilgan o'qituvchi ko'ra oladigan banklar uchun EF filtrida ishlatiladigan ifoda.
        /// </summary>
        public static Expression<Func<QuestionBank, bool>> VisibleTo(string teacherId)
        {
            return bank => bank.TeacherId == teacherId
                || (bank.TeacherId == null
                    && bank.Subject != null
                    && (bank.Subject.TeacherId == teacherId
                        || bank.Subject.GroupSubjects.Any(gs => gs.TeacherId == teacherId)));
        }
    }
}
