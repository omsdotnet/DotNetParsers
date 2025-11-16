using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HHParser;

class Program
{
  private static readonly HttpClient client = new HttpClient();
  private const string BaseUrl = "https://api.hh.ru/vacancies";
  private const string SearchText = "C# OR .NET";
  private const int PagesToProcess = 20; // Обработать 20 страниц (максимум 2000 вакансий)

  static async Task Main(string[] args)
  {
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    Console.WriteLine("Начинаем сбор вакансий по направлению .NET...\n");

    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

    try
    {
      // Сбор всех вакансий
      var vacancies = await GetAllVacancies();

      // Анализ данных
      var analysisResult = AnalyzeVacancies(vacancies);

      // Вывод результатов
      PrintAnalysisResults(analysisResult);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Произошла ошибка: {ex.Message}");
    }
  }

  // Метод для получения всех вакансий
  static async Task<List<Vacancy>> GetAllVacancies()
  {
    var vacancies = new List<Vacancy>();

    for (int page = 0; page < PagesToProcess; page++)
    {
      Console.WriteLine($"Обрабатывается страница {page + 1}...");

      string requestUrl = $"{BaseUrl}?text={SearchText}&page={page}&per_page=100";

      try
      {
        var response = await client.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);

        var items = json["items"] as JArray;
        if (items == null || items.Count == 0) break;

        foreach (var item in items)
        {
          try
          {
            var vacancy = JsonConvert.DeserializeObject<Vacancy>(item.ToString());
            if (vacancy != null)
            {
              vacancies.Add(vacancy);
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Ошибка при обработке вакансии: {ex.Message}");
          }
        }

        // Проверяем, есть ли следующая страница
        if ((int)json["pages"] <= page + 1) break;

        // Задержка для соблюдения лимитов API (не более 5 запросов в секунду)
        await Task.Delay(200);
      }
      catch (HttpRequestException ex)
      {
        Console.WriteLine($"Ошибка HTTP при запросе страницы {page}: {ex.Message}");
        break;
      }
    }

    return vacancies;
  }

  // Метод для анализа вакансий
  static AnalysisResult AnalyzeVacancies(List<Vacancy> vacancies)
  {
    var result = new AnalysisResult();

    // Статистика по работодателям
    result.EmployerStats = vacancies
      .GroupBy(v => v.Employer?.Name ?? "Не указано")
      .Select(g => new EmployerStat
      {
        Name = g.Key,
        Count = g.Count(),
        AvgSalary = CalculateAverageSalary(g.Where(v => v.Salary != null))
      })
      .OrderByDescending(e => e.Count)
      .ToList();

    // Статистика по городам
    result.CityStats = vacancies
      .Where(v => v.Area != null)
      .GroupBy(v => v.Area.Name)
      .Select(g => new CityStat
      {
        Name = g.Key,
        Count = g.Count(),
        AvgSalary = CalculateAverageSalary(g.Where(v => v.Salary != null))
      })
      .OrderByDescending(c => c.Count)
      .ToList();

    // Общая статистика по зарплатам
    var salaries = vacancies
      .Where(v => v.Salary != null)
      .Select(v => v.Salary)
      .ToList();

    if (salaries.Count > 0)
    {
      result.MinSalary = salaries.Min(s => s.From ?? s.To ?? 0);
      result.MaxSalary = salaries.Max(s => s.To ?? s.From ?? 0);
      result.AvgSalary = (int)salaries.Average(s =>
        s.From.HasValue && s.To.HasValue ? (s.From.Value + s.To.Value) / 2 :
        s.From.HasValue ? s.From.Value :
        s.To.HasValue ? s.To.Value : 0);
    }

    result.TotalVacancies = vacancies.Count;
    result.VacanciesWithSalary = salaries.Count;

    return result;
  }

  // Вспомогательный метод для расчета средней зарплаты
  static int CalculateAverageSalary(IEnumerable<Vacancy> vacancies)
  {
    var salaries = vacancies
      .Where(v => v.Salary != null)
      .Select(v => v.Salary)
      .ToList();

    if (salaries.Count == 0) return 0;

    return (int)salaries.Average(s =>
      s.From.HasValue && s.To.HasValue ? (s.From.Value + s.To.Value) / 2 :
      s.From.HasValue ? s.From.Value :
      s.To.HasValue ? s.To.Value : 0);
  }

  // Метод для вывода результатов анализа
  static void PrintAnalysisResults(AnalysisResult result)
  {
    Console.WriteLine("\n==============================================");
    Console.WriteLine("СТАТИСТИКА ПО ВАКАНСИЯМ .NET С HH.RU");
    Console.WriteLine("==============================================\n");

    Console.WriteLine($"Общее количество вакансий: {result.TotalVacancies}");
    Console.WriteLine($"Вакансий с указанной зарплатой: {result.VacanciesWithSalary}");
    Console.WriteLine($"Средняя зарплата: {result.AvgSalary} руб.");
    Console.WriteLine($"Минимальная зарплата: {result.MinSalary} руб.");
    Console.WriteLine($"Максимальная зарплата: {result.MaxSalary} руб.");

    // Топ-10 работодателей
    Console.WriteLine("\nТОП-10 РАБОТОДАТЕЛЕЙ:");
    Console.WriteLine("--------------------------------------------------------------");
    Console.WriteLine("| Работодатель | Количество вакансий | Средняя зарплата |");
    Console.WriteLine("--------------------------------------------------------------");

    foreach (var employer in result.EmployerStats.Take(10))
    {
      Console.WriteLine($"| {employer.Name,-40} | {employer.Count,-19} | {employer.AvgSalary,-16} |");
    }
    Console.WriteLine("--------------------------------------------------------------");

    // Топ-10 городов
    Console.WriteLine("\nТОП-10 ГОРОДОВ:");
    Console.WriteLine("--------------------------------------------------------------");
    Console.WriteLine("| Город | Количество вакансий | Средняя зарплата |");
    Console.WriteLine("--------------------------------------------------------------");

    foreach (var city in result.CityStats.Take(10))
    {
      Console.WriteLine($"| {city.Name,-30} | {city.Count,-19} | {city.AvgSalary,-16} |");
    }
    Console.WriteLine("--------------------------------------------------------------");
  }
}

// Классы для десериализации JSON
public class Vacancy
{
  [JsonProperty("id")]
  public string Id { get; set; }

  [JsonProperty("name")]
  public string Name { get; set; }

  [JsonProperty("salary")]
  public Salary Salary { get; set; }

  [JsonProperty("employer")]
  public Employer Employer { get; set; }

  [JsonProperty("area")]
  public Area Area { get; set; }

  [JsonProperty("published_at")]
  public DateTime PublishedAt { get; set; }
}

public class Salary
{
  [JsonProperty("from")]
  public int? From { get; set; }

  [JsonProperty("to")]
  public int? To { get; set; }

  [JsonProperty("currency")]
  public string Currency { get; set; }

  [JsonProperty("gross")]
  public bool? Gross { get; set; }
}

public class Employer
{
  [JsonProperty("id")]
  public string Id { get; set; }

  [JsonProperty("name")]
  public string Name { get; set; }
}

public class Area
{
  [JsonProperty("id")]
  public string Id { get; set; }

  [JsonProperty("name")]
  public string Name { get; set; }
}

// Классы для хранения результатов анализа
public class AnalysisResult
{
  public List<EmployerStat> EmployerStats { get; set; }
  public List<CityStat> CityStats { get; set; }
  public int TotalVacancies { get; set; }
  public int VacanciesWithSalary { get; set; }
  public int AvgSalary { get; set; }
  public int MinSalary { get; set; }
  public int MaxSalary { get; set; }
}

public class EmployerStat
{
  public string Name { get; set; }
  public int Count { get; set; }
  public int AvgSalary { get; set; }
}

public class CityStat
{
  public string Name { get; set; }
  public int Count { get; set; }
  public int AvgSalary { get; set; }
}