using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Xml;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");

app.MapGet("/protected", (ClaimsPrincipal user, HttpContext context) =>
{
    var sb = new StringBuilder();
    sb.Append("<!DOCTYPE html>");
    sb.Append("<html lang='en'>");
    sb.Append("<head>");
    sb.Append("<meta charset='UTF-8'>");
    sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
    sb.Append("<title>User Profile</title>");
    sb.Append("<style>");
    sb.Append(@"
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f0f2f5; color: #333; margin: 0; padding: 40px; display: flex; justify-content: center; align-items: center; min-height: 100vh; }
        .container { background-color: #ffffff; border-radius: 12px; box-shadow: 0 8px 24px rgba(0,0,0,0.1); max-width: 600px; width: 100%; padding: 40px; text-align: center; }
        h1 { color: #1877f2; margin-bottom: 20px; font-size: 28px; }
        .profile-table { width: 100%; border-collapse: collapse; margin-top: 25px; text-align: left; }
        .profile-table th, .profile-table td { padding: 12px 15px; border-bottom: 1px solid #ddd; }
        .profile-table th { background-color: #f7f7f7; font-weight: 600; width: 30%; }
        .profile-table tr:last-child td { border-bottom: none; }
        .logout-btn { background-color: #dc3545; color: white; border: none; padding: 12px 25px; border-radius: 8px; font-size: 16px; cursor: pointer; text-decoration: none; display: inline-block; margin-top: 30px; transition: background-color 0.3s; }
        .logout-btn:hover { background-color: #c82333; }
    ");
    sb.Append("</style>");
    sb.Append("</head>");
    sb.Append("<body>");
    sb.Append("<div class='container'>");
    sb.Append($"<h1>Welcome, {user.Identity.Name}!</h1>");
    sb.Append("<p>Here is the information from your CAS authentication:</p>");
    
    sb.Append("<table class='profile-table'>");
    sb.Append("<tr><th>Claim Type</th><th>Claim Value</th></tr>");
    foreach (var claim in user.Claims)
    {
        sb.Append($"<tr><td>{claim.Type}</td><td>{claim.Value}</td></tr>");
    }
    sb.Append("</table>");

    sb.Append("<a href='/logout' class='logout-btn'>Logout</a>");
    sb.Append("</div>");
    sb.Append("</body>");
    sb.Append("</html>");

    return Results.Content(sb.ToString(), "text/html");
}).RequireAuthorization();

app.MapGet("/login", (HttpContext context) =>
{
    var casServerUrl = builder.Configuration["Cas:CasServerUrlBase"];
    var serviceUrl = Uri.EscapeDataString("https://5000-firebase-cas-client-demo2-c-1768815771970.cluster-nle52mxuvfhlkrzyrq6g2cwb52.cloudworkstations.dev/signin-cas");
    var redirectUrl = $"{casServerUrl}/login?service={serviceUrl}";
    return Results.Redirect(redirectUrl);
});

app.MapGet("/signin-cas", async (HttpContext context, string ticket, ILogger<Program> logger) =>
{
    var casServerUrl = builder.Configuration["Cas:CasServerUrlBase"];
    if (string.IsNullOrEmpty(casServerUrl))
    {
        logger.LogError("CAS Server URL is not configured. Please set 'Cas:CasServerUrlBase' in appsettings.json.");
        return Results.Problem("CAS server is not configured.", statusCode: 500);
    }
    
    var serviceUrl = Uri.EscapeDataString("https://5000-firebase-cas-client-demo2-c-1768815771970.cluster-nle52mxuvfhlkrzyrq6g2cwb52.cloudworkstations.dev/signin-cas");
    var validationUrl = $"{casServerUrl}/serviceValidate?ticket={ticket}&service={serviceUrl}";

    logger.LogInformation("Validating ticket at URL: {ValidationUrl}", validationUrl);

    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(validationUrl);

    var responseBody = await response.Content.ReadAsStringAsync();
    logger.LogInformation("CAS validation response status code: {StatusCode}", response.StatusCode);
    logger.LogInformation("CAS validation response body: {ResponseBody}", responseBody);

    if (response.IsSuccessStatusCode)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.XmlResolver = null;
        xmlDoc.LoadXml(responseBody);

        var nsmgr = GetNamespaceManager(xmlDoc);
        var userNode = xmlDoc.SelectSingleNode("//cas:authenticationSuccess/cas:user", nsmgr) ?? xmlDoc.SelectSingleNode("//cas:user", nsmgr);

        if (userNode != null)
        {
            var username = userNode.InnerText;
            var claims = new List<Claim> { new Claim(ClaimTypes.Name, username) };

            var attributesNode = xmlDoc.SelectSingleNode("//cas:authenticationSuccess/cas:attributes", nsmgr);
            if (attributesNode != null)
            {
                logger.LogInformation("Found attributes node. Parsing attributes.");
                foreach (XmlNode childNode in attributesNode.ChildNodes)
                {
                    if (childNode.NodeType == XmlNodeType.Element)
                    {
                        var claimType = childNode.LocalName;
                        var claimValue = childNode.InnerText;
                        claims.Add(new Claim(claimType, claimValue));
                        logger.LogInformation("Added claim: Type='{ClaimType}', Value='{ClaimValue}'", claimType, claimValue);
                    }
                }
            } else {
                logger.LogWarning("No <cas:attributes> node found in the CAS response.");
            }
            
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            logger.LogInformation("Successfully created and signed in principal for user: {Username}", username);

            return Results.Redirect("/protected");
        }
        else
        {
            var failureNode = xmlDoc.SelectSingleNode("//cas:authenticationFailure", nsmgr);
            if (failureNode != null)
            {
                var errorCode = failureNode.Attributes?["code"]?.Value;
                var errorMessage = failureNode.InnerText.Trim();
                logger.LogWarning("CAS ticket validation failed. Code: {ErrorCode}, Message: {ErrorMessage}", errorCode, errorMessage);
            }
            else
            {
                logger.LogWarning("CAS ticket validation failed. The 'cas:user' node was not found and no 'cas:authenticationFailure' node was found either.");
            }
        }
    }
    else
    {
        logger.LogError("Request to CAS validation URL failed with status code: {StatusCode}", response.StatusCode);
    }

    return Results.Unauthorized();
});

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var casServerUrl = builder.Configuration["Cas:CasServerUrlBase"];
    var serviceUrl = Uri.EscapeDataString($"{context.Request.Scheme}://{context.Request.Host}/");
    var redirectUrl = $"{casServerUrl}/logout?service={serviceUrl}";
    return Results.Redirect(redirectUrl);
});

XmlNamespaceManager GetNamespaceManager(XmlDocument doc)
{
    var nsmgr = new XmlNamespaceManager(doc.NameTable);
    nsmgr.AddNamespace("cas", "http://www.yale.edu/tp/cas");
    return nsmgr;
}

app.Run();
