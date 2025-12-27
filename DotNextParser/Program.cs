using HtmlAgilityPack;

namespace DotNextParser;

class Program
{
  static async Task Main(string[] args)
  {
    // URL расписания с официального сайта DotNext
    string scheduleUrl = "https://dotnext.ru/schedule/table/";

    try
    {
      // Загрузка и анализ HTML-страницы
      var htmlDocument = await LoadHtmlDocument(scheduleUrl);

      if (htmlDocument != null)
      {
        // Извлечение данных о докладах
        List<Presentation> presentations = ParsePresentations(htmlDocument);

        // Группировка по компаниям и создание отчета
        List<CompanyReport> report = GenerateReport(presentations);

        // Вывод результатов
        Console.WriteLine("Статистика конференции: {0}", scheduleUrl);
        Console.WriteLine();
        Console.WriteLine();
        PrintPresentations(presentations);
        Console.WriteLine();
        Console.WriteLine();
        PrintReport(report);
      }
      else
      {
        Console.WriteLine("Не удалось загрузить документ.");
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Произошла ошибка: {ex.Message}");
    }
  }

  static async Task<HtmlDocument> LoadHtmlDocument(string url)
  {
    using (HttpClient client = new HttpClient())
    {
      // Важно: установка User-Agent помогает избежать блокировки сервером
      client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

      try
      {
        string htmlContent = await client.GetStringAsync(url);
        HtmlDocument htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);
        return htmlDocument;
      }
      catch (HttpRequestException e)
      {
        Console.WriteLine($"Ошибка при запросе к {url}: {e.Message}");
        return null;
      }
    }
  }

  static List<Presentation> ParsePresentations(HtmlDocument htmlDocument)
  {
    var presentations = new List<Presentation>();

    // ЭТОТ КОД ТРЕБУЕТ НАСТРОЙКИ ПОД АКТУАЛЬНУЮ HTML-СТРУКТУРУ САЙТА
    // Пример селектора для ячейки расписания (требует уточнения)
    var scheduleNodes = htmlDocument.DocumentNode.SelectNodes("//div[contains(@class, 'talkCard__main')]");

    if (scheduleNodes != null)
    {
      foreach (var node in scheduleNodes)
      {
        // Пример извлечения данных (селекторы необходимо проверить и обновить)
        string company = node.SelectSingleNode(".//p[contains(@class, 'speakerCard__company')]")?.InnerText.Trim() ?? "-";
        string speaker = node.SelectSingleNode(".//a[contains(@class, 'speakerCard__link')]")?.InnerText.Trim() ?? "Спикер не указан";
        string title = node.SelectSingleNode(".//h3[contains(@class, 'talkCard__heading')]")?.InnerText.Trim() ?? "Без названия";

        if (!string.IsNullOrEmpty(title) && speaker != "Спикер не указан")
        {
          presentations.Add(new Presentation
          {
            Company = company,
            Speaker = speaker,
            Title = title
          });
        }
      }
    }
    else
    {
      Console.WriteLine("На странице не найдено элементов расписания. Возможно, изменилась структура HTML.");
    }

    return presentations;
  }

  static List<CompanyReport> GenerateReport(List<Presentation> presentations)
  {
    // Группируем доклады по компании и спикеру, затем считаем количество
    var report = presentations
      .GroupBy(p => new { p.Company })
      .Select(g => new CompanyReport
      {
        CompanyName = g.Key.Company,
        TalkCount = g.Count()
      })
      .OrderByDescending(r => r.TalkCount)
      .ThenBy(r => r.CompanyName)
      .ToList();

    return report;
  }

  private static void PrintPresentations(List<Presentation> presentations)
  {
    // Вывод таблицы в консоль
    Console.WriteLine();
    Console.WriteLine("|{0,-40}|{1,-35}|{2,-98}|", "Название компании", "Спикер", "Доклад");
    Console.WriteLine("|{0,-40}|{1,-35}|{2,-98}|", new string('-', 40), new string('-', 35), new string('-', 98));

    foreach (var entry in presentations
               .OrderBy(x => x.Company)
               .ThenBy(x => x.Speaker)
               .ThenBy(x => x.Title))
    {
      Console.WriteLine("|{0,-40}|{1,-35}|{2,-98}|", entry.Company, entry.Speaker, entry.Title);
    }
    Console.WriteLine();
    Console.WriteLine("Всего: {0} докладов", presentations.Count);
  }

  static void PrintReport(List<CompanyReport> report)
  {
    // Вывод таблицы в консоль
    Console.WriteLine();
    Console.WriteLine("|{0,-40}|{1,-20}|", "Название компании", "Количество докладов");
    Console.WriteLine("|{0,-40}|{1,-20}|", new string('-', 40), new string('-', 20));

    foreach (var entry in report)
    {
      Console.WriteLine("|{0,-40}|{1,-20}|", entry.CompanyName, entry.TalkCount);
    }
    Console.WriteLine();
    Console.WriteLine("Всего: {0} компаний", report.Count);
  }
}

// Вспомогательные классы для хранения данных
public class Presentation
{
  public string Company { get; set; }
  public string Speaker { get; set; }
  public string Title { get; set; }
}

public class CompanyReport
{
  public string CompanyName { get; set; }
  public int TalkCount { get; set; }
}
