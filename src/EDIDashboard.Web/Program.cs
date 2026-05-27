var builder = WebApplication.CreateBuilder(args);

// UI-only host. API endpoints are served by EDIDashboard.Api.
builder.Services.AddControllersWithViews();
var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
