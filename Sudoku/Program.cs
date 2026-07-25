using Sudoku.Components;
using Sudoku.Application.Interfaces;
using Sudoku.Application.Services;
using Sudoku.Infrastructure;
using Sudoku.Application;
using Sudoku.Services;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Antiforgery to work with HTTP in development
builder.Services.AddAntiforgery(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        options.FormFieldName = "RequestVerificationToken";
        options.HeaderName = "X-CSRF-TOKEN";
    }
});

// Sudoku DI registrations (Clean Architecture style)
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// Scoped = per Blazor circuit, so each connected player gets their own storage.
builder.Services.AddScoped<GameStorage>();
// Same instance behind the application's persistence port.
builder.Services.AddScoped<Sudoku.Application.Interfaces.IGameStore>(sp => sp.GetRequiredService<GameStorage>());
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
