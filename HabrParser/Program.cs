using System.Globalization;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace HabrParser;

internal class Program
{
    private static async Task Main(string[] args)
    {
        //var articlesDotNetTask = DownloadHubArticles("net", 247);
        //var articlesCSharpTask = DownloadHubArticles("csharp", 176);
        var articlesDotNetTask = GetHubArticles("net");
        var articlesCSharpTask = GetHubArticles("csharp");

        await Task.WhenAll([articlesCSharpTask, articlesDotNetTask]);

        var articlesCSharp = await articlesCSharpTask;
        var articlesDotNet = await articlesDotNetTask;


        var articlesCSharp2025 = articlesCSharp.Where(x => x.Date is {Year: 2025}).ToList();
        var articlesDotNet2025 = articlesDotNet.Where(x => x.Date is {Year: 2025}).ToList();

        var articlesOnlyCSharp = articlesCSharp2025.Where(x => articlesDotNet2025.All(y => y.Id != x.Id)).ToList();
        var articlesOnlyDotNet = articlesDotNet2025.Where(x => articlesCSharp2025.All(y => y.Id != x.Id)).ToList();
        var articlesCommon = articlesDotNet2025.Where(x => articlesCSharp2025.Any(y => y.Id == x.Id)).ToList();
        var allArticles = articlesOnlyCSharp.Union(articlesCommon).Union(articlesOnlyDotNet).ToList();
        Console.WriteLine(
            $"Общее количество статей по хабам .NET-общее-C#: {articlesOnlyDotNet.Count()}-{articlesCommon.Count()}-{articlesOnlyCSharp.Count()} = {allArticles.Count()}");


        articlesCSharp2025 = articlesCSharp2025.Where(x => !string.IsNullOrWhiteSpace(x.CompanyName)).ToList();
        articlesDotNet2025 = articlesDotNet2025.Where(x => !string.IsNullOrWhiteSpace(x.CompanyName)).ToList();

        articlesOnlyCSharp = articlesCSharp2025.Where(x => articlesDotNet2025.All(y => y.Id != x.Id)).ToList();
        articlesOnlyDotNet = articlesDotNet2025.Where(x => articlesCSharp2025.All(y => y.Id != x.Id)).ToList();
        articlesCommon = articlesDotNet2025.Where(x => articlesCSharp2025.Any(y => y.Id == x.Id)).ToList();
        allArticles = articlesOnlyCSharp.Union(articlesCommon).Union(articlesOnlyDotNet).ToList();
        Console.WriteLine(
            $"Общее количество статей от компаний по хабам .NET-общее-C#: {articlesOnlyDotNet.Count()}-{articlesCommon.Count()}-{articlesOnlyCSharp.Count()} = {allArticles.Count()}");


        ShowStatistics(allArticles);
    }

    private static async Task<List<Article>> DownloadHubArticles(string hubName, int pagesToParse)
    {
        var articles = new List<Article>();

        var baseUrl = "https://habr.com";
        var hubUrl = $"/ru/hub/{hubName}/";

        Directory.CreateDirectory(hubName);

        using (var client = new HttpClient())
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            for (var page = 1; page <= pagesToParse; page++)
            {
                var url = $"{hubUrl}page{page}/";
                try
                {
                    Console.WriteLine($"Парсинг страницы: {url}");
                    var html = await client.GetStringAsync(url);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(html);

                    var articleNodes =
                        doc.DocumentNode.SelectNodes("//article[contains(@class, 'tm-articles-list__item')]");

                    if (articleNodes == null)
                    {
                        Console.WriteLine($"На странице {page} не найдено статей.");
                        continue;
                    }

                    foreach (var node in articleNodes)
                    {
                        var titleNode = node.SelectSingleNode(".//a[contains(@class, 'tm-title__link')]");
                        var authorNode = node.SelectSingleNode(".//a[contains(@class, 'tm-user-info__username')]");
                        var dateNode = node.SelectSingleNode(".//time");
                        var ratingNode = node.SelectSingleNode(".//span[contains(@class, 'tm-votes-meter__value')]");
                        var viewsNode = node.SelectSingleNode(".//span[contains(@class, 'tm-icon-counter__value')]");
                        var commentsNode =
                            node.SelectSingleNode(".//a[contains(@class, 'article-comments-counter-link')]")
                                .SelectSingleNode(".//span[contains(@class, 'value')]");
                        var companyNode = node.SelectSingleNode(".//div[contains(@class, 'tm-publication-hubs')]")
                            ?.SelectSingleNode(".//span[contains(text(), 'Блог компании')]");


                        var article = new Article
                        {
                            Title = titleNode?.InnerText.Trim(),
                            Link = titleNode?.GetAttributeValue("href", ""),
                            Author = authorNode?.InnerText.Trim(),
                            Date =
                                dateNode?.GetAttributeValue("datetime", null) is string dateStr
                                    ? DateTime.Parse(dateStr)
                                    : null,
                            Rating =
                                ratingNode != null && int.TryParse(ratingNode.InnerText.Trim(), out var rating)
                                    ? rating
                                    : 0,
                            Views = ConvertToInt(viewsNode?.InnerText.Trim()),
                            Comments = commentsNode != null && int.TryParse(commentsNode.InnerText.Trim(),
                                NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var comments)
                                ? comments
                                : 0,
                            CompanyName = companyNode?.InnerText.Replace("Блог компании", "").Trim()
                        };

                        if (!string.IsNullOrEmpty(article.Link) && !article.Link.StartsWith("http"))
                        {
                            article.Link = baseUrl + article.Link;
                        }

                        if (!string.IsNullOrEmpty(article.Link))
                        {
                            var linkUri = new Uri(article.Link.TrimEnd('/'));
                            article.Id = linkUri.Segments[^1].Replace("/", "");
                        }

                        var articleJsonString = JsonConvert.SerializeObject(article, Formatting.Indented);
                        var fileName = $"{hubName}/{article.Id}.json";
                        await File.WriteAllTextAsync(fileName, articleJsonString);

                        articles.Add(article);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при парсинге страницы {page}: {ex.Message}");
                }
            }
        }

        if (!articles.Any())
        {
            Console.WriteLine("Не удалось получить данные. Проверьте подключение к интернету или структуру сайта.");
            return articles;
        }

        return articles;
    }

    private static async Task<List<Article>> GetHubArticles(string hubName)
    {
        var articles = new List<Article>();

        var files = Directory.GetFiles(hubName);

        foreach (var file in files)
        {
            var article = JsonConvert.DeserializeObject<Article>(await File.ReadAllTextAsync(file));
            if (article != null)
            {
                articles.Add(article);
            }
        }

        return articles;
    }

    private static void ShowStatistics(List<Article> allArticles)
    {
        // Самые популярные статьи по рейтингу
        var topRated = allArticles.OrderByDescending(a => a.Rating).Take(5);
        Console.WriteLine("\nТоп-5 статей по рейтингу:");
        foreach (var article in topRated)
        {
            Console.WriteLine($"   - {article.CompanyName}: {article.Title} (Рейтинг: {article.Rating})");
        }

        // Самые обсуждаемые статьи
        var mostCommented = allArticles.OrderByDescending(a => a.Comments).Take(5);
        Console.WriteLine("\nТоп-5 статей по количеству комментариев:");
        foreach (var article in mostCommented)
        {
            Console.WriteLine($"   - {article.CompanyName}: {article.Title} (Комментарии: {article.Comments})");
        }

        // Самые просматриваемые статьи
        var mostViewed = allArticles.OrderByDescending(a => a.Views).Take(5);
        Console.WriteLine("\nТоп-5 статей по просмотрам:");
        foreach (var article in mostViewed)
        {
            Console.WriteLine($"   - {article.CompanyName}: {article.Title} (Просмотры: {article.Views})");
        }

        // Средние показатели
        Console.WriteLine("\nСредние показатели:");
        Console.WriteLine($"   - Средний рейтинг: {allArticles.Average(a => a.Rating):F2}");
        Console.WriteLine($"   - Среднее количество комментариев: {allArticles.Average(a => a.Comments):F0}");
        Console.WriteLine($"   - Среднее количество просмотров: {allArticles.Average(a => a.Views):F0}");

        // Общая сумма просмотров и комментариев
        Console.WriteLine("\nСуммарные показатели:");
        Console.WriteLine($"   - Всего комментариев: {allArticles.Sum(a => a.Comments)}");
        Console.WriteLine($"   - Всего просмотров: {allArticles.Sum(a => a.Views)}");


        var separator = new string('-', 12);

        // Статистика по месяцам
        var articlesByMonth = allArticles.Where(a => a.Date.HasValue)
            .GroupBy(a => new {a.Date.Value.Year, a.Date.Value.Month})
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Count(),
                Rating = g.Average(p => p.Rating),
                Comments = g.Average(p => p.Comments),
                Views = g.Average(p => p.Views)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month);
        Console.WriteLine("\nКоличество статей по месяцам:");
        Console.WriteLine();
        Console.WriteLine("|{0,-10}|{1,-10}|{2,-12}|{3,-12}|{4,-12}|{5,-12}|",
            "Год",
            "Месяц",
            "Статей",
            "Рейтинг",
            "Комментариев",
            "Просмотров");
        Console.WriteLine("|{0,-10}|{1,-10}|{2,-12}|{3,-12}|{4,-12}|{5,-12}|",
            new string('-', 10),
            new string('-', 10),
            separator,
            separator,
            separator,
            separator);
        foreach (var group in articlesByMonth)
        {
            Console.WriteLine("|{0,-10}|{1,-10}|{2,-12}|{3,-12}|{4,-12}|{5,-12}|",
                group.Year,
                group.Month,
                group.Count,
                (int)Math.Round(group.Rating),
                (int)Math.Round(group.Comments),
                (int)Math.Round(group.Views));
        }

        // Статистика по компаниям
        var companies = allArticles.GroupBy(a => a.CompanyName)
            .Select(g => new
            {
                CompanyName = g.Key,
                Count = g.Count(),
                Rating = g.Average(p => p.Rating),
                Comments = g.Average(p => p.Comments),
                Views = g.Average(p => p.Views)
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Rating)
            .ThenByDescending(x => x.Comments)
            .ThenByDescending(x => x.Views)
            .ThenBy(x => x.CompanyName)
            .ToList();
        Console.WriteLine("\nСтатистика по компаниям:");
        Console.WriteLine();
        Console.WriteLine("|{0,-40}|{1,-12}|{2,-12}|{3,-12}|{4,-12}|",
            "Название компании",
            "Статей",
            "Рейтинг",
            "Комментариев",
            "Просмотров");
        Console.WriteLine("|{0,-40}|{1,-12}|{2,-12}|{3,-12}|{4,-12}|",
            new string('-', 40),
            separator,
            separator,
            separator,
            separator);
        foreach (var company in companies)
        {
            Console.WriteLine("|{0,-40}|{1,-12}|{2,-12}|{3,-12}|{4,-12}|",
                company.CompanyName,
                company.Count,
                (int)Math.Round(company.Rating),
                (int)Math.Round(company.Comments),
                (int)Math.Round(company.Views));
        }

        Console.WriteLine($"\nВсего компаний: {companies.Count}");

        // Статистика по авторам
        var authors = allArticles.GroupBy(a => a.Author)
            .Select(g => new
            {
                Author = g.Key,
                Count = g.Count(),
                Rating = g.Average(p => p.Rating),
                Comments = g.Average(p => p.Comments),
                Views = g.Average(p => p.Views)
            })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Rating)
            .ThenByDescending(x => x.Comments)
            .ThenByDescending(x => x.Views)
            .ThenBy(x => x.Author)
            .ToList();
        Console.WriteLine("\nСтатистика по авторам:");
        Console.WriteLine();
        Console.WriteLine("|{0,-40}|{1,-12}|{2,-12}|{3,-12}|{4,-12}|",
            "Автор",
            "Статей",
            "Рейтинг",
            "Комментариев",
            "Просмотров");
        Console.WriteLine("|{0,-40}|{1,-12}|{2,-12}|{3,-12}|{4,-12}|",
            new string('-', 40),
            separator,
            separator,
            separator,
            separator);
        foreach (var author in authors)
        {
            Console.WriteLine("|{0,-40}|{1,-12}|{2,-12}|{3,-12}|{4,-12}|",
                author.Author,
                author.Count,
                (int)Math.Round(author.Rating),
                (int)Math.Round(author.Comments),
                (int)Math.Round(author.Views));
        }

        Console.WriteLine($"\nВсего авторов: {authors.Count}");

        // Весь список статей
        var articles = allArticles
            .OrderByDescending(x => x.Rating)
            .ThenByDescending(x => x.Comments)
            .ThenByDescending(x => x.Views)
            .ThenBy(x => x.CompanyName)
            .ThenBy(x => x.Author)
            .ToList();
        Console.WriteLine("\nВесь список статей:");
        Console.WriteLine();
        Console.WriteLine("|{0,-40}|{1,-30}|{2,-130}|{3,-12}|{4,-12}|{5,-12}|",
            "Компания",
            "Автор",
            "Статья",
            "Рейтинг",
            "Комментариев",
            "Просмотров");
        Console.WriteLine("|{0,-40}|{1,-30}|{2,-130}|{3,-12}|{4,-12}|{5,-12}|",
            new string('-', 40),
            new string('-', 30),
            new string('-', 130),
            separator,
            separator,
            separator);
        foreach (var article in articles)
        {
            Console.WriteLine("|{0,-40}|{1,-30}|{2,-130}|{3,-12}|{4,-12}|{5,-12}|",
                article.CompanyName,
                article.Author,
                article.Title,
                article.Rating,
                article.Comments,
                article.Views);
        }

        Console.WriteLine($"\nВсего статей: {articles.Count}");
    }

    private static int ConvertToInt(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return 0;
        }

        var multiplexer = 1;

        if (source.EndsWith("K"))
        {
            multiplexer = 1_000;
        }
        else if (source.EndsWith("M"))
        {
            multiplexer = 1_000_000;
        }


        if (decimal.TryParse(source
                .Replace(" ", "")
                .Replace("\u00A0", "")
                .Replace("K", "")
                .Replace("M", ""), out var result))
        {
            return (int)(result * multiplexer);
        }

        return 0;
    }
}

public class Article
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Link { get; set; }
    public string Author { get; set; }
    public DateTime? Date { get; set; }
    public int Rating { get; set; }
    public int Views { get; set; }
    public int Comments { get; set; }
    public string CompanyName { get; set; }
}
