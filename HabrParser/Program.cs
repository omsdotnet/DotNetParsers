using System.Globalization;
using HtmlAgilityPack;

namespace HabrParser;

class Program
{
  static async Task Main(string[] args)
  {
    const string baseUrl = "https://habr.com";
    const string hubUrl = "/ru/hub/net/"; // URL хаба .NET
    int pagesToParse = 245; // Количество страниц для анализа

    var articles = new List<Article>();

    using (var client = new HttpClient())
    {
      client.BaseAddress = new Uri(baseUrl);
      client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

      for (int page = 1; page <= pagesToParse; page++)
      {
        string url = $"{hubUrl}page{page}/";
        try
        {
          Console.WriteLine($"Парсинг страницы: {url}");
          var html = await client.GetStringAsync(url);
          var doc = new HtmlDocument();
          doc.LoadHtml(html);

          var articleNodes = doc.DocumentNode.SelectNodes("//article[contains(@class, 'tm-articles-list__item')]");

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
            var commentsNode = node.SelectSingleNode(".//span[contains(@class, 'tm-article-comments-counter-link__value')]");

            var article = new Article
            {
              Title = titleNode?.InnerText.Trim(),
              Link = titleNode?.GetAttributeValue("href", ""),
              Author = authorNode?.InnerText.Trim(),
              Date = dateNode?.GetAttributeValue("datetime", null) is string dateStr ? DateTime.Parse(dateStr) : (DateTime?)null,
              Rating = ratingNode != null && int.TryParse(ratingNode.InnerText.Trim(), out int rating) ? rating : 0,
              Views = viewsNode != null && int.TryParse(viewsNode.InnerText.Trim(), NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out int views) ? views : 0,
              Comments = commentsNode != null && int.TryParse(commentsNode.InnerText.Trim(), NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out int comments) ? comments : 0
            };

            if (!string.IsNullOrEmpty(article.Link) && !article.Link.StartsWith("http"))
            {
              article.Link = baseUrl + article.Link;
            }

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
      return;
    }

    // Формирование статистики
    Console.WriteLine("\n=== СТАТИСТИКА ПО ХАБУ .NET ===\n");

    // 1. Общее количество статей
    Console.WriteLine($"1. Общее количество статей: {articles.Count}");

    // 2. Самые популярные статьи по рейтингу
    var topRated = articles.OrderByDescending(a => a.Rating).Take(5);
    Console.WriteLine("\n2. Топ-5 статей по рейтингу:");
    foreach (var article in topRated)
    {
      Console.WriteLine($"   - {article.Title} (Рейтинг: {article.Rating})");
    }

    // 3. Самые обсуждаемые статьи
    var mostCommented = articles.OrderByDescending(a => a.Comments).Take(5);
    Console.WriteLine("\n3. Топ-5 статей по количеству комментариев:");
    foreach (var article in mostCommented)
    {
      Console.WriteLine($"   - {article.Title} (Комментарии: {article.Comments})");
    }

    // 4. Самые просматриваемые статьи
    var mostViewed = articles.OrderByDescending(a => a.Views).Take(5);
    Console.WriteLine("\n4. Топ-5 статей по просмотрам:");
    foreach (var article in mostViewed)
    {
      Console.WriteLine($"   - {article.Title} (Просмотры: {article.Views})");
    }

    // 5. Авторы с наибольшим количеством статей
    var topAuthors = articles.GroupBy(a => a.Author)
      .Select(g => new { Author = g.Key, Count = g.Count() })
      .OrderByDescending(x => x.Count)
      .Take(5);
    Console.WriteLine("\n5. Топ-5 авторов по количеству статей:");
    foreach (var author in topAuthors)
    {
      Console.WriteLine($"   - {author.Author} (Статей: {author.Count})");
    }

    // 6. Распределение статей по месяцам
    var articlesByMonth = articles.Where(a => a.Date.HasValue)
      .GroupBy(a => new { a.Date.Value.Year, a.Date.Value.Month })
      .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
      .OrderBy(x => x.Year)
      .ThenBy(x => x.Month);
    Console.WriteLine("\n6. Количество статей по месяцам:");
    foreach (var group in articlesByMonth)
    {
      Console.WriteLine($"   - {group.Year}-{group.Month:D2}: {group.Count} статей");
    }

    // 7. Средние показатели
    Console.WriteLine("\n7. Средние показатели:");
    Console.WriteLine($"   - Средний рейтинг: {articles.Average(a => a.Rating):F2}");
    Console.WriteLine($"   - Среднее количество просмотров: {articles.Average(a => a.Views):F0}");
    Console.WriteLine($"   - Среднее количество комментариев: {articles.Average(a => a.Comments):F0}");

    // 8. Общая сумма просмотров и комментариев
    Console.WriteLine("\n8. Суммарные показатели:");
    Console.WriteLine($"   - Всего просмотров: {articles.Sum(a => a.Views)}");
    Console.WriteLine($"   - Всего комментариев: {articles.Sum(a => a.Comments)}");

    // 9. Статья с максимальным рейтингом
    var bestArticle = articles.OrderByDescending(a => a.Rating).FirstOrDefault();
    if (bestArticle != null)
    {
      Console.WriteLine("\n9. Статья с максимальным рейтингом:");
      Console.WriteLine($"   - Заголовок: {bestArticle.Title}");
      Console.WriteLine($"   - Автор: {bestArticle.Author}");
      Console.WriteLine($"   - Рейтинг: {bestArticle.Rating}");
      Console.WriteLine($"   - Ссылка: {bestArticle.Link}");
    }

    // 10. Статья с минимальным рейтингом
    var worstArticle = articles.OrderBy(a => a.Rating).FirstOrDefault();
    if (worstArticle != null)
    {
      Console.WriteLine("\n10. Статья с минимальным рейтингом:");
      Console.WriteLine($"   - Заголовок: {worstArticle.Title}");
      Console.WriteLine($"   - Автор: {worstArticle.Author}");
      Console.WriteLine($"   - Рейтинг: {worstArticle.Rating}");
      Console.WriteLine($"   - Ссылка: {worstArticle.Link}");
    }
  }
}

public class Article
{
  public string Title { get; set; }
  public string Link { get; set; }
  public string Author { get; set; }
  public DateTime? Date { get; set; }
  public int Rating { get; set; }
  public int Views { get; set; }
  public int Comments { get; set; }
}
