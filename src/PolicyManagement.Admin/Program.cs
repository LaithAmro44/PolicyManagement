using Microsoft.EntityFrameworkCore;
using PolicyManagement.Admin.Components;
using PolicyManagement.Admin.Infrastructure;
using PolicyManagement.Admin.Services;
using PolicyManagement.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
    options.OperationFilter<TenantHeaderOperationFilter>());
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddDbContextFactory<PolicyDbContext>(
    (sp, options) => options.UseSqlServer(
        builder.Configuration.GetConnectionString("PolicyDatabase")),
    ServiceLifetime.Scoped);
builder.Services.AddScoped<AdminPolicyService>();
builder.Services.AddSingleton<PolicySocketHub>();
builder.Services.AddScoped<PolicyWebSocketHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.UseSwagger();
app.UseSwaggerUI();
app.UseWebSockets();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapStaticAssets();
app.MapControllers();
app.Map("/ws/policies", async context =>
{
    var handler = context.RequestServices.GetRequiredService<PolicyWebSocketHandler>();
    await handler.HandleAsync(context);
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
