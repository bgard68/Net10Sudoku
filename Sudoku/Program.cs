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

// Sudoku DI registrations (Clean Architecture style)
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
// Scoped, not singleton: in Blazor Server a singleton is shared by every circuit,
// so one visitor toggling dark mode would change the theme for all of them.
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<GameStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
