# EduTrack — Loyiha holati

> Bu fayl har bir ish sessiyasi oxirida yangilanishi kerak. Yangi Claude sessiyasini boshlaganda, avval shu faylni ko'rsating.

## Loyiha haqida
- **Nomi:** EduTrack — HEMIS uslubidagi ta'lim platformasi
- **Backend:** C#, ASP.NET Core MVC (.NET 8), Entity Framework Core, ASP.NET Core Identity
- **Frontend:** Razor (CSHTML), Bootstrap 5, JavaScript
- **Ma'lumotlar bazasi:** SQLite
- **GitHub repo:** https://github.com/husanboymeliboyev/EduTrack

## Arxitektura (asosiy qismlar)
- `Areas/Identity/Pages` — login, register, parol o'zgartirish
- `Controllers` — asosiy controllerlar (Users, Assignments, Attendance, va h.k.)
- `Services` — biznes-logika (masalan ExcelExportService)
- `ViewModels` — ma'lumotlarni View'ga uzatish uchun modellar
- `Views` — Razor sahifalar
- `wwwroot` — statik fayllar (css, js, rasmlar)

## Hozirgi holat
<!-- Har safar shu qismni yangilang -->
- **Oxirgi yangilanish sanasi:** 2026-08-08
- **Nima tugagan:** Core infratuzilma (Models, DbContext, Identity, layout), Guruhlar/Fanlar/Foydalanuvchilar bosqichlari (3-bosqich)
- **Nima ustida ishlanmoqda:** —
- **Keyingi bosqich:** Davomat (Attendance) moduli

## Muhim qarorlar / eslatmalar
<!-- Masalan: "Imtihon vaqti server-side tekshiriladi, client-side emas (xavfsizlik)" -->
-

## Bilinigan muammolar / TODO
-
