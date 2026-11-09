using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduTrack.Data
{
    /// <summary>
    /// Faqat Development muhiti uchun sinov ma'lumotlari.
    ///
    /// • Mavjud foydalanuvchi/guruh/fanlarga tegmaydi — faqat qo'shadi.
    /// • Qayta ishga tushirilsa, marker login mavjud bo'lsa hech narsa qilmaydi.
    /// • Barcha yangi hisoblar uchun bir martalik parol: <see cref="Password"/>,
    ///   birinchi kirishda almashtirish majburiy (MustChangePassword = true).
    /// • Shanba/Yakshanba darslari yaratilmaydi.
    /// </summary>
    public static class TrialDataSeeder
    {
        public const string Password = "parol123";

        // Marker: shu login mavjud bo'lsa, sinov ma'lumotlari allaqachon yaratilgan.
        private const string MarkerLoginId = "30101";

        private static readonly string[] TeacherLogins = { "30101", "30102", "30103", "30104" };
        private static readonly string[] TeacherNames =
        {
            "Rustamov Alisher",
            "Qosimova Malika",
            "Toshpulatov Diyor",
            "Yo'ldosheva Nodira"
        };

        private static readonly string[] SubjectNames =
        {
            "Matematika",
            "Ingliz tili",
            "Fizika",
            "Ona tili"
        };

        private static readonly string[] StudentNames =
        {
            "Abdullayev Sardor",
            "Karimova Madina",
            "Ismoilov Javohir",
            "Tursunova Nilufar",
            "Rahimov Bekzod",
            "Yuldasheva Sevinch",
            "Sattorov Aziz",
            "Mirzayeva Dilnoza",
            "Qodirov Umid",
            "Xolmatova Zilola",
            "Ergashev Jahongir",
            "Nabiyeva Mohinur"
        };

        private static readonly string[] ParentNames =
        {
            "Abdullayev Anvar",
            "Karimova Gulbahor",
            "Ismoilov Nodir",
            "Tursunova Feruza",
            "Rahimov Sherzod",
            "Yuldasheva Dilbar",
            "Sattorov Baxtiyor",
            "Mirzayeva Nargiza",
            "Qodirov Rustam",
            "Xolmatova Ozoda",
            "Ergashev Akmal",
            "Nabiyeva Saodat"
        };

        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var clock = services.GetRequiredService<IAppClock>();

            if (await db.Users.AnyAsync(u => u.LoginId == MarkerLoginId))
            {
                return;
            }

            var today = clock.Today;

            // ==================== 1. O'qituvchilar ====================
            var teachers = new List<ApplicationUser>();
            for (var i = 0; i < TeacherLogins.Length; i++)
            {
                teachers.Add(await CreateUserAsync(
                    userManager, TeacherLogins[i], TeacherNames[i], "Teacher"));
            }

            // ==================== 2. O'quvchilar va ota-onalar ====================
            var students = new List<ApplicationUser>();
            var parents = new List<ApplicationUser>();
            var links = new List<ParentStudentLink>();

            for (var i = 0; i < StudentNames.Length; i++)
            {
                var student = await CreateUserAsync(
                    userManager, $"302{i + 1:00}", StudentNames[i], "Student");
                var parent = await CreateUserAsync(
                    userManager, $"303{i + 1:00}", ParentNames[i], "Parent");

                students.Add(student);
                parents.Add(parent);
                links.Add(new ParentStudentLink
                {
                    ParentId = parent.Id,
                    StudentId = student.Id,
                    CreatedDate = clock.UtcNow
                });
            }

            // ==================== 3. Sinflar, yo'nalishlar, fanlar ====================
            var classA = new Group { Name = "8-A sinf", Code = "8A", Type = GroupType.Class };
            var classB = new Group { Name = "8-B sinf", Code = "8B", Type = GroupType.Class };
            var streamEnglish = new Group
            {
                Name = "Ingliz tili yo'nalishi",
                Code = "EN",
                Type = GroupType.Stream
            };
            var streamMath = new Group
            {
                Name = "Matematika yo'nalishi",
                Code = "MATH",
                Type = GroupType.Stream
            };
            var groups = new[] { classA, classB, streamEnglish, streamMath };
            db.Groups.AddRange(groups);

            var subjects = new List<Subject>();
            for (var i = 0; i < SubjectNames.Length; i++)
            {
                subjects.Add(new Subject
                {
                    Name = SubjectNames[i],
                    TeacherId = teachers[i].Id
                });
            }
            db.Subjects.AddRange(subjects);

            // Fanlar va guruhlar avval saqlanadi: bank/schedule kabi yozuvlar
            // ularning haqiqiy Id'lariga tayanishi kerak.
            await db.SaveChangesAsync();

            var banks = new List<QuestionBank>();
            for (var i = 0; i < subjects.Count; i++)
            {
                banks.Add(new QuestionBank
                {
                    Name = $"{subjects[i].Name} — sinov banki",
                    SubjectId = subjects[i].Id
                });
            }
            db.QuestionBanks.AddRange(banks);

            await db.SaveChangesAsync();

            // Har bir guruhda barcha fanlar o'qitiladi; o'qituvchi fan bo'yicha.
            for (var g = 0; g < groups.Length; g++)
            {
                for (var s = 0; s < subjects.Count; s++)
                {
                    db.GroupSubjects.Add(new GroupSubject
                    {
                        GroupId = groups[g].Id,
                        SubjectId = subjects[s].Id,
                        TeacherId = teachers[s].Id
                    });
                }
            }

            // ==================== 4. A'zoliklar (sinf + yo'nalish) ====================
            // O'tkazish tarixini ko'rsatish uchun bir o'quvchining eski sinf
            // a'zoligi o'chirilmaydi — IsActive=false + EndDate bilan saqlanadi.
            var studentClass = new Dictionary<string, Group>(StringComparer.Ordinal);
            var studentStream = new Dictionary<string, Group>(StringComparer.Ordinal);

            for (var i = 0; i < students.Count; i++)
            {
                var student = students[i];
                var isClassA = i < 6;
                var classGroup = isClassA ? classA : classB;
                var streamGroup = i % 2 == 0 ? streamEnglish : streamMath;

                studentClass[student.Id] = classGroup;
                studentStream[student.Id] = streamGroup;

                // Eski sinfdan o'tkazilgan o'quvchi (tarix saqlanadi).
                if (i == 0)
                {
                    db.GroupMemberships.Add(new GroupMembership
                    {
                        StudentId = student.Id,
                        GroupId = classB.Id,
                        Scope = GroupType.Class,
                        StartDate = today.AddMonths(-5),
                        EndDate = today.AddMonths(-2),
                        EndReason = "8-A sinfga o'tkazildi",
                        IsActive = false
                    });
                }

                db.GroupMemberships.Add(new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = classGroup.Id,
                    Scope = GroupType.Class,
                    StartDate = today.AddMonths(-2),
                    IsActive = true
                });

                db.GroupMemberships.Add(new GroupMembership
                {
                    StudentId = student.Id,
                    GroupId = streamGroup.Id,
                    Scope = GroupType.Stream,
                    StartDate = today.AddMonths(-2),
                    IsActive = true
                });

                // Eski maydon (o'tish davri uchun) — sinf guruhini ko'rsatadi.
                student.GroupId = classGroup.Id;
            }

            db.ParentStudentLinks.AddRange(links);
            await db.SaveChangesAsync();

            // ==================== 5. Dars jadvali (Shanbasiz) ====================
            var weekdays = new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            };

            var schedules = new List<Schedule>();
            for (var g = 0; g < groups.Length; g++)
            {
                var group = groups[g];
                var isClass = group.Type == GroupType.Class;
                var shift = isClass ? ScheduleShift.Morning : ScheduleShift.Afternoon;
                var firstLessonMinutes = isClass ? 8 * 60 : 13 * 60;

                for (var d = 0; d < weekdays.Length; d++)
                {
                    for (var period = 1; period <= 3; period++)
                    {
                        var subjectIndex = (g + d + period) % subjects.Count;
                        var start = TimeSpan.FromMinutes(firstLessonMinutes + (period - 1) * 60);

                        schedules.Add(new Schedule
                        {
                            GroupId = group.Id,
                            SubjectId = subjects[subjectIndex].Id,
                            TeacherId = teachers[subjectIndex].Id,
                            DayOfWeek = weekdays[d],
                            Shift = shift,
                            Period = period,
                            Kind = ScheduleKind.Regular,
                            StartTime = start,
                            EndTime = start.Add(TimeSpan.FromMinutes(45))
                        });
                    }
                }
            }
            db.Schedules.AddRange(schedules);
            await db.SaveChangesAsync();

            // ==================== 6. Imtihonlar va natijalar ====================
            var exams = new List<Exam>();
            foreach (var subject in subjects)
            {
                var bank = banks.First(b => b.SubjectId == subject.Id);

                exams.Add(new Exam
                {
                    Title = $"{subject.Name} — 1-nazorat",
                    SubjectId = subject.Id,
                    QuestionBankId = bank.Id,
                    QuestionCount = 100,
                    DurationMinutes = 45,
                    CreatedDate = today.AddDays(-25)
                });
                exams.Add(new Exam
                {
                    Title = $"{subject.Name} — 2-nazorat",
                    SubjectId = subject.Id,
                    QuestionBankId = bank.Id,
                    QuestionCount = 100,
                    DurationMinutes = 45,
                    CreatedDate = today.AddDays(-12)
                });
                exams.Add(new Exam
                {
                    Title = $"{subject.Name} — oylik nazorat",
                    SubjectId = subject.Id,
                    QuestionBankId = bank.Id,
                    QuestionCount = 100,
                    DurationMinutes = 45,
                    CreatedDate = today.AddDays(-5),
                    Kind = ExamKind.Monthly,
                    PeriodYear = today.Year,
                    PeriodMonth = today.Month
                });
            }
            db.Exams.AddRange(exams);
            await db.SaveChangesAsync();

            var results = new List<ExamResult>();
            for (var i = 0; i < students.Count; i++)
            {
                for (var s = 0; s < subjects.Count; s++)
                {
                    var subjectExams = exams
                        .Where(e => e.SubjectId == subjects[s].Id)
                        .OrderBy(e => e.Id)
                        .ToList();

                    for (var e = 0; e < subjectExams.Count; e++)
                    {
                        var score = 52 + ((i * 7 + s * 5 + e * 11) % 44); // 52..95
                        results.Add(CreateResult(subjectExams[e].Id, students[i].Id, score,
                            today.AddDays(-(20 - e * 4))));

                        // Retake namunasi: ba'zi o'quvchilarda ikkinchi urinish bor.
                        if (i == 1 && e == 0)
                        {
                            results.Add(CreateResult(subjectExams[e].Id, students[i].Id,
                                Math.Max(0, score - 15), today.AddDays(-19)));
                        }
                        else if (i == 4 && e == 1)
                        {
                            results.Add(CreateResult(subjectExams[e].Id, students[i].Id,
                                Math.Min(100, score + 8), today.AddDays(-10)));
                        }
                    }
                }
            }
            db.ExamResults.AddRange(results);

            // Qo'lda qo'yilgan baholar: imtihon o'rtachasidan ustun turadi.
            db.SubjectGrades.AddRange(
                new SubjectGrade
                {
                    StudentId = students[0].Id,
                    SubjectId = subjects[0].Id,
                    Score = 97,
                    UpdatedDate = today.AddDays(-1),
                    TeacherId = teachers[0].Id
                },
                new SubjectGrade
                {
                    StudentId = students[3].Id,
                    SubjectId = subjects[1].Id,
                    Score = 58,
                    UpdatedDate = today.AddDays(-2),
                    TeacherId = teachers[1].Id
                },
                new SubjectGrade
                {
                    StudentId = students[7].Id,
                    SubjectId = subjects[2].Id,
                    Score = 91,
                    UpdatedDate = today.AddDays(-1),
                    TeacherId = teachers[2].Id
                });

            await db.SaveChangesAsync();

            // ==================== 7. Davomat (oxirgi 12 o'quv kuni) ====================
            var schoolDays = new List<DateTime>();
            for (var day = today.AddDays(-20); day < today; day = day.AddDays(1))
            {
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                schoolDays.Add(day);
            }

            var recentDays = schoolDays.TakeLast(12).ToList();
            var attendances = new List<Attendance>();

            for (var i = 0; i < students.Count; i++)
            {
                var student = students[i];
                var classGroup = studentClass[student.Id];

                for (var d = 0; d < recentDays.Count; d++)
                {
                    var date = recentDays[d];
                    var lesson = schedules
                        .Where(s => s.GroupId == classGroup.Id && s.DayOfWeek == date.DayOfWeek)
                        .OrderBy(s => s.Period)
                        .FirstOrDefault();

                    if (lesson is null)
                    {
                        continue;
                    }

                    var marker = (i * 3 + d) % 14;
                    var status = marker switch
                    {
                        0 => AttendanceStatus.Kelmadi,
                        5 => AttendanceStatus.KechQoldi,
                        9 => AttendanceStatus.Sababli,
                        _ => AttendanceStatus.Keldi
                    };

                    attendances.Add(new Attendance
                    {
                        Date = date,
                        StudentId = student.Id,
                        SubjectId = lesson.SubjectId,
                        Status = status
                    });
                }
            }
            db.Attendances.AddRange(attendances);

            // ==================== 8. Topshiriqlar (bahoga ta'sir qilmaydi) ====================
            // Guruhga bog'lanmagan (GroupId = null) — barcha o'quvchilarga ko'rinadi.
            var assignments = new List<Assignment>();
            var submissions = new List<Submission>();

            for (var s = 0; s < 2; s++) // Matematika va Ingliz tili
            {
                var assignment = new Assignment
                {
                    Title = $"{subjects[s].Name} — amaliy topshiriq",
                    Description = "Sinov ma'lumoti: mavzu bo'yicha amaliy topshiriq.",
                    DueDate = today.AddDays(-2),
                    CreatedDate = today.AddDays(-9),
                    SubjectId = subjects[s].Id,
                    GroupId = null
                };
                assignments.Add(assignment);

                for (var i = 0; i < students.Count; i++)
                {
                    // Har to'rtinchi o'quvchida topshiriq topshirilmagan holat qoladi.
                    if (i % 4 == 3)
                    {
                        continue;
                    }

                    submissions.Add(new Submission
                    {
                        Assignment = assignment,
                        StudentId = students[i].Id,
                        SubmittedDate = today.AddDays(-3),
                        Grade = 60 + ((i * 7 + s * 5) % 41),
                        TeacherComment = "Sinov ma'lumoti uchun tekshirilgan."
                    });
                }
            }

            db.Assignments.AddRange(assignments);
            db.Submissions.AddRange(submissions);

            await db.SaveChangesAsync();
        }

        private static async Task<ApplicationUser> CreateUserAsync(
            UserManager<ApplicationUser> userManager,
            string loginId,
            string fullName,
            string role)
        {
            var user = new ApplicationUser
            {
                UserName = loginId,
                LoginId = loginId,
                FullName = fullName,
                EmailConfirmed = true,
                MustChangePassword = true
            };

            var created = await userManager.CreateAsync(user, Password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{loginId} hisobini yaratib bo'lmadi: " +
                    string.Join(", ", created.Errors.Select(e => e.Description)));
            }

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{loginId} uchun '{role}' rolini qo'shib bo'lmadi: " +
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }

            return user;
        }

        private static ExamResult CreateResult(int examId, string studentId, int score, DateTime completed)
        {
            var correct = Math.Clamp(score, 0, 100);

            return new ExamResult
            {
                ExamId = examId,
                StudentId = studentId,
                TotalQuestions = 100,
                CorrectAnswers = correct,
                CompletedDate = completed,
                IsApproved = true
            };
        }
    }
}
