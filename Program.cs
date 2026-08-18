using EduTrack.Data;
using EduTrack.Models;
using EduTrack.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IStudentShowcaseService, StudentShowcaseService>();
// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// SQLite faylini Railway Volume'ga saqlash.
// Railway'da EDUTRACK_DATA_DIR berilsa, shu papka ishlatiladi;
// production muhitida esa standart yo'l /app/data hisoblanadi.
var sqliteConnection = new SqliteConnectionStringBuilder(connectionString);
if (!string.Equals(sqliteConnection.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
{
    var configuredDataDirectory = builder.Configuration["EDUTRACK_DATA_DIR"];
    var dataDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
        ? (builder.Environment.IsProduction() ? "/app/data" : null)
        : configuredDataDirectory;

    if (!string.IsNullOrWhiteSpace(dataDirectory) && !Path.IsPathRooted(sqliteConnection.DataSource))
    {
        sqliteConnection.DataSource = Path.Combine(dataDirectory, Path.GetFileName(sqliteConnection.DataSource));
        connectionString = sqliteConnection.ConnectionString;
    }

    var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(sqliteConnection.DataSource));
    if (!string.IsNullOrWhiteSpace(databaseDirectory))
    {
        Directory.CreateDirectory(databaseDirectory);
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        // ===== Parol siyosati (xavfsizlik) =====
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // ===== Login urinishlarini cheklash (Account Lockout) =====
        // Ketma-ket 5 marta noto'g'ri parol kiritilsa, hisob 15 daqiqaga bloklanadi.
        // Bu tizimni parolni "taxmin qilib topish" (brute-force) hujumlaridan himoya qiladi.
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// ===== Cookie xavfsizligi =====
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    // Development muhitida (http orqali test qilinganda) cookie ishlashi to'xtamasligi uchun,
    // faqat production muhitida cookie faqat HTTPS orqali yuborilishini majburiy qilamiz.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews();

// ===== Xizmatlarni (Services) ro'yxatdan o'tkazish =====
// Fayl yuklash va o'qituvchi ruxsatlarini tekshirish mantig'i endi kontrollerlar
// ichida emas, shu xizmatlarda markazlashtirilgan.
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<ITeacherAccessService, TeacherAccessService>();
builder.Services.AddScoped<IPasswordGeneratorService, PasswordGeneratorService>();
builder.Services.AddScoped<IQuestionImportService, QuestionImportService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();
builder.Services.AddScoped<IGradeSyncService, GradeSyncService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DbInitializer.InitializeAsync(services);
}
app.Run();
