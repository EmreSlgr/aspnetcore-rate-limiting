using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. Rate Limiting Servisinin Yapılandırılması (Harici kütüphane gerektirmez)
const string SlidingPolicyName = "sliding-policy";
const int WindowSeconds = 10;

builder.Services.AddRateLimiter(options =>
{
    // Limit aşıldığında dönecek HTTP durum kodu
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Sliding Window (Kayan Pencere) Politikası:
    // 10 saniyelik zaman diliminde en fazla 5 istek kabul edilir.
    // Kuyruk (QueueLimit) 0 olduğu için limit aşılınca anında 429 döner.
    options.AddSlidingWindowLimiter(policyName: SlidingPolicyName, limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromSeconds(WindowSeconds);
        limiterOptions.SegmentsPerWindow = 2;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // İstek reddedildiğinde çalışacak özel yanıt mekanizması
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        // SlidingWindowLimiter'da metadata gelmezse varsayılan pencere süresini kullanırız
        int retryAfterSeconds = WindowSeconds;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) && retryAfter.TotalSeconds > 0)
        {
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
        }

        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status429TooManyRequests,
            error = "Too Many Requests",
            message = "İstek limiti aşıldı. Lütfen daha sonra tekrar deneyiniz.",
            retryAfterSeconds = retryAfterSeconds,
            timestamp = DateTime.UtcNow
        }, cancellationToken: cancellationToken);
    };
});

var app = builder.Build();

// 2. Rate Limiting Middleware'inin Pipeline'a Eklenmesi
app.UseRateLimiter();

// 3. Endpoint Tanımları
app.MapGet("/", () => Results.Ok(new
{
    status = "OK",
    message = "API çalışıyor. Rate limit test endpoint'i: /api/resource"
}));

app.MapGet("/api/resource", () => Results.Ok(new
{
    status = "Success",
    message = "İstek başarıyla işlendi!",
    timestamp = DateTime.UtcNow
}))
.RequireRateLimiting(SlidingPolicyName);

app.Run();
