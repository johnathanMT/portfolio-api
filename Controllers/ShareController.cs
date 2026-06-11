using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Controllers;

/// <summary>
/// Server-rendered Open Graph endpoint for social sharing. Social crawlers
/// (Facebook, LinkedIn, X) do NOT run JavaScript, so per-article previews must
/// come from server-rendered HTML. This returns article-specific og: tags and
/// redirects human visitors to the live blog post.
/// </summary>
[ApiController]
[AllowAnonymous]
public class ShareController : ControllerBase
{
    private readonly IArticleService _articles;
    private readonly IConfiguration _config;
    public ShareController(IArticleService articles, IConfiguration config)
    {
        _articles = articles;
        _config   = config;
    }

    [HttpGet("/share/{id:int}")]
    public async Task<IActionResult> Share(int id)
    {
        var res = await _articles.GetByIdAsync(id);
        if (!res.Success || res.Data is null)
            return NotFound("Article not found.");

        var a = res.Data;

        // Domain-agnostic: read the live frontend origin from config (Frontend:Url,
        // overridable via the Frontend__Url env var on Render). Defaults to the
        // current site so previews always link to the right place.
        var siteUrl = (_config["Frontend:Url"] ?? "https://myothant.dev").TrimEnd('/');

        // NOTE: blog.js reads the post id from the QUERY string (?id=), not the
        // hash — so the human redirect must use ?id= or it lands on the list.
        var target = $"{siteUrl}/blog.html?id={id}";
        var image = string.IsNullOrWhiteSpace(a.ImageUrl)
            ? $"{siteUrl}/Myweb_photo/My_profile2_for_myweb.jpg"
            : a.ImageUrl;

        string snippet = a.Content.Length > 160 ? a.Content[..160].TrimEnd() + "…" : a.Content;

        string E(string s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var html = $@"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>{E(a.Title)}</title>
  <meta property=""og:type"" content=""article"">
  <meta property=""og:site_name"" content=""MTN.Digitosphere"">
  <meta property=""og:title"" content=""{E(a.Title)}"">
  <meta property=""og:description"" content=""{E(snippet)}"">
  <meta property=""og:image"" content=""{E(image)}"">
  <meta property=""og:url"" content=""{E(target)}"">
  <meta name=""twitter:card"" content=""summary_large_image"">
  <meta name=""twitter:title"" content=""{E(a.Title)}"">
  <meta name=""twitter:description"" content=""{E(snippet)}"">
  <meta name=""twitter:image"" content=""{E(image)}"">
  <meta http-equiv=""refresh"" content=""0; url={E(target)}"">
  <link rel=""canonical"" href=""{E(target)}"">
</head>
<body style=""background:#0a0a0f;color:#e6edf3;font-family:system-ui;text-align:center;padding:3rem"">
  Redirecting to the article… <a href=""{E(target)}"" style=""color:#9d6fef"">Continue</a>
</body>
</html>";

        return Content(html, "text/html");
    }
}
