using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

SortedSet<RateLimitTimeStamp> requests = new SortedSet<RateLimitTimeStamp>();
const string NEWS_URL = "https://thehill.com/news/";


app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host;
    var now = DateTime.Now;
    var lastRequest = requests.FirstOrDefault(x => x.Host == host);
    if (lastRequest.IsNull != 0 )
    {
        var timeSinceLastRequest = now - lastRequest.Time;
        if (timeSinceLastRequest.TotalSeconds > 10)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync($"You are being rate limited. Wait atleast {2 - timeSinceLastRequest.Seconds} seconds");
            //await context.Response.BodyWriter.WriteAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes($"You are being rate limited. Wait atleast {2-timeSinceLastRequest.Seconds} seconds")));
            return;
        }
    }
    requests.Add(new RateLimitTimeStamp { Host = host, Time = now, IsNull = 1 });
    await next();
});

app.MapGet("/getnews", async ([FromServices] HttpClient client) =>
{
    var html = await client.GetStreamAsync(NEWS_URL).ConfigureAwait(false);
    var htmlDoc = new HtmlDocument();
    
    htmlDoc.Load(html);
    var mainSection = htmlDoc.GetElementbyId("primary");
    var newsItems = mainSection.ChildNodes[3].ChildNodes.ToList();
    newsItems = newsItems.Where(e => e.Name != "#text").ToList();
    newsItems.RemoveAt(0);
    
    return newsItems.Select(e =>
    {
        var ImageUri = e.ChildNodes.WhereList(e => e.Name != "#text")[0].ChildNodes.WhereList(e => e.Name != "#text")[0].ChildNodes.WhereList(e => e.Name != "#text")[0].ChildNodes.WhereList(e => e.Name != "#text")[0].Attributes["src"].Value.Split("?")[0];
        var Url = e.ChildNodes.WhereList(e => e.Name != "#text")[0].ChildNodes.WhereList(e => e.Name != "#text")[0].Attributes["href"].Value!;
        var Title = Regex.Unescape(e.ChildNodes.WhereList(e => e.Name != "#text")[1].ChildNodes.WhereList(e => e.Name != "#text")[0].ChildNodes.WhereList(e => e.Name != "#text")[0].InnerText).Trim();
        return new NewsItem
        {
            ImageUri = ImageUri,
            Url = Url,
            Title = Title
        };
    });
});



app.Run();


readonly struct NewsItem
{
    public string? ImageUri { get; init; }
    public string Title { get; init; }
    public string Url { get; init;}

}
readonly struct RateLimitTimeStamp : IEquatable<RateLimitTimeStamp>, IComparable<RateLimitTimeStamp>
{
    public DateTime Time { get; init; }
    public string Host { get; init; }
    public byte IsNull { get; init; }

    public int CompareTo(RateLimitTimeStamp other)
    {
        return Time.CompareTo(other.Time);
    }

    public bool Equals(RateLimitTimeStamp other)
    {
        return (Time, Host, IsNull) == (other.Time, other.Host, other.IsNull);
    }
}


 static class Extensions
{
    public static List<T> WhereList<T>(this IEnumerable<T> list, Func<T, bool> predicate)
    {
        return list.Where(predicate).ToList();
    }
}

