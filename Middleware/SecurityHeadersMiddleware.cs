namespace EduTrack.Middleware
{
    /// <summary>
    /// Har bir javobga asosiy xavfsizlik sarlavhalari qo'shadi:
    /// — X-Content-Type-Options: brauzerlarda MIME sniffingni cheklaydi;
    /// — X-Frame-Options / frame-ancestors: sahifani boshqa saytlarda iframe orqali
    ///   ochishni (clickjacking) taqiqlaydi;
    /// — Referrer-Policy: tashqi saytlarga maxfiy ma'lumotlar (parol, token)
    ///   Referrer sarlavhasi orqali o'tib ketishining oldini oladi.
    /// Statik fayllar ham shu qatlamdan o'tadi — hech narsa o'chirilmaydi.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                // Mavjud CSP sarlavhasi bo'lmasa, zamonaviy brauzerlar uchun
                // frame-ancestors ham beriladi. Mavjud CSP buzilmaydi.
                if (!headers.ContainsKey("Content-Security-Policy"))
                {
                    headers["Content-Security-Policy"] = "frame-ancestors 'self'";
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
