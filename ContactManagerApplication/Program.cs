using AspNet.Security.OAuth.LinkedIn;

using ContactManagerApplication.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity.UI.Services;
using ContactManagerApplication.Settings;
using SendGrid.Extensions.DependencyInjection;
using ContactManagerApplication.Services;


var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
.AddDefaultTokenProviders();

builder.Services.AddTransient<IEmailSender, EmailSenderService>();

builder.Services.Configure<SendGridSettings>(builder.Configuration.GetSection("SendGridSettings"));
builder.Services.AddSendGrid(options =>
{
    options.ApiKey = builder.Configuration.GetSection("SendGridSettings").GetValue<string>("ApiKey");
});

builder.Services.AddScoped<IEmailSender,EmailSenderService >(); 

//Add LinkedIn Authentication
builder.Services.AddAuthentication()
    .AddLinkedIn(options => {
        options.ClientId = builder.Configuration["Authentication:LinkedIn:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:LinkedIn:ClientSecret"];

        // Configure OAuth Scopes and Endpoints
        
        options.Scope.Add("email");
        options.UserInformationEndpoint = "https://api.linkedin.com/v2/userinfo";

        options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                //processing user info
                var userJson = await response.Content.ReadAsStringAsync();
                using (var user = JsonDocument.Parse(userJson))
                {
                  
                    var email = user.RootElement.GetProperty("email").GetString();

                  if (string.IsNullOrEmpty(email))
                    {
                        throw new InvalidOperationException("One or more required user properties are missing.");
                    }


                  
                    context.Identity.AddClaim(new Claim(ClaimTypes.Email, email));
                }

            },

            //Handling Remote Authentication Failure
            OnRemoteFailure = context =>
            {
                context.Response.Redirect("/Account/Login?error=" + context.Failure.Message);
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

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
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    // Create "Admin" and "User" roles if they don't exist
    string[] roleNames = { "Admin", "User" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Create admin user
    var adminEmail = "natnael@gmail.com"; // Specify admin email
    var adminPassword = ""; // Specify a secure admin password
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        // Create the admin user and set EmailConfirmed to true
        var newAdminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var createAdminResult = await userManager.CreateAsync(newAdminUser, adminPassword);

        if (createAdminResult.Succeeded)
        {
            // Assign the "Admin" role to the new admin user
            await userManager.AddToRoleAsync(newAdminUser, "Admin");
        }
    }
    else
    {
        // If the admin user exists but is not in the "Admin" role, assign the role
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}

app.Run();

