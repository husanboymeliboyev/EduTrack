# 1-bosqich: loyihani build qilish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# .csproj faylini alohida nusxalab, paketlarni oldindan tiklaymiz (tezroq bo'lishi uchun)
COPY *.csproj ./
RUN dotnet restore

# Qolgan barcha fayllarni nusxalaymiz va build qilamiz
COPY . .
RUN dotnet publish -c Release -o /app/publish

# 2-bosqich: faqat ishga tushirish uchun kerakli qism (yengil image)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway PORT o'zgaruvchisini avtomatik beradi, shuni ishlatamiz
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:$PORT dotnet EduTrack.dll"]
