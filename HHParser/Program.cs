using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HHParser;

internal class Program
{
    private static readonly HttpClient client = new();

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Начинаем сбор данных...\n");

        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        try
        {
            //await DownloadHubItems("vacancies");

            var vacancies = GetHubItems("vacancies");

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

    private static List<Vacancy> GetHubItems(string itemsGroupName)
    {
        var articles = new List<Vacancy>();

        var files = Directory.GetFiles(itemsGroupName);

        foreach (var file in files)
        {
            var article = JsonConvert.DeserializeObject<Vacancy>(File.ReadAllText(file));
            if (article != null)
            {
                articles.Add(article);
            }
        }

        return articles;
    }

    // Метод для получения всех вакансий
    private static async Task DownloadHubItems(string itemsGroupName)
    {
        var baseUrl = $"https://api.hh.ru/{itemsGroupName}";
        const string searchText = "C%23+OR+.NET";
        const int pagesToProcess = 20; // Обработать 20 страниц (максимум 2000 вакансий)

        for (var page = 0; page < pagesToProcess; page++)
        {
            Console.WriteLine($"Обрабатывается страница {itemsGroupName} {page + 1}...");

            var requestUrl = $"{baseUrl}?text={searchText}&page={page}&per_page=100";

            try
            {
                var response = await client.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);

                var items = json["items"] as JArray;
                if (items == null || items.Count == 0)
                {
                    break;
                }

                Directory.CreateDirectory(itemsGroupName);

                foreach (var item in items)
                {
                    try
                    {
                        var itemString = item.ToString();
                        var itemId = item["id"]?.ToString();
                        if (itemId != null)
                        {
                            var fileName = $"{itemsGroupName}/{itemId}.json";
                            await File.WriteAllTextAsync(fileName, itemString);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при обработке вакансии: {ex.Message}");
                    }
                }

                // Проверяем, есть ли следующая страница
                if ((int)json["pages"] <= page + 1)
                {
                    break;
                }

                // Задержка для соблюдения лимитов API (не более 5 запросов в секунду)
                await Task.Delay(200);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Ошибка HTTP при запросе страницы {page}: {ex.Message}");
                break;
            }
        }
    }

    // Метод для анализа вакансий
    private static AnalysisResult AnalyzeVacancies(List<Vacancy> vacancies)
    {
        var result = new AnalysisResult();

        // Статистика по работодателям
        result.EmployerStats = vacancies
            .GroupBy(v => v.Employer?.Name ?? "Не указано")
            .Select(g => new EmployerStat
            {
                Name = g.Key, Count = g.Count(), AvgSalary = CalculateAverageSalary(g.Where(v => v.Salary != null))
            })
            .OrderByDescending(e => e.Count)
            .ThenByDescending(e => e.AvgSalary)
            .ThenBy(e => e.Name)
            .ToList();

        // Статистика по городам
        result.CityStats = vacancies
            .Where(v => v.Area != null)
            .GroupBy(v => v.Area.Name)
            .Select(g => new CityStat
            {
                Name = g.Key, Count = g.Count(), AvgSalary = CalculateAverageSalary(g.Where(v => v.Salary != null))
            })
            .OrderByDescending(c => c.Count)
            .ThenByDescending(e => e.AvgSalary)
            .ThenBy(e => e.Name)
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
    private static int CalculateAverageSalary(IEnumerable<Vacancy> vacancies)
    {
        var salaries = vacancies
            .Where(v => v.Salary != null)
            .Select(v => v.Salary)
            .ToList();

        if (salaries.Count == 0)
        {
            return 0;
        }

        return (int)salaries.Average(s =>
            s.From.HasValue && s.To.HasValue ? (s.From.Value + s.To.Value) / 2 :
            s.From.HasValue ? s.From.Value :
            s.To.HasValue ? s.To.Value : 0);
    }

    // Метод для вывода результатов анализа
    private static void PrintAnalysisResults(AnalysisResult result)
    {
        Console.WriteLine();
        Console.WriteLine("СТАТИСТИКА ПО ВАКАНСИЯМ '.NET OR C#' С HH.RU");
        Console.WriteLine("==============================================\n");

        Console.WriteLine($"Общее количество вакансий: {result.TotalVacancies}");
        Console.WriteLine($"Вакансий с указанной зарплатой: {result.VacanciesWithSalary}");
        Console.WriteLine($"Максимальная зарплата: {result.MaxSalary} руб.");
        Console.WriteLine($"Средняя зарплата: {result.AvgSalary} руб.");
        Console.WriteLine($"Минимальная зарплата: {result.MinSalary} руб.");

        // работодатели
        Console.WriteLine("\nКомпании:");
        Console.WriteLine();
        Console.WriteLine("|{0,-140}|{1,-19}|{2,-16}|",
            "Компания",
            "Количество вакансий",
            "Средняя зарплата");
        Console.WriteLine("|{0,-140}|{1,-19}|{2,-16}|",
            new string('-', 140),
            new string('-', 19),
            new string('-', 16));
        foreach (var employer in result.EmployerStats)
        {
            Console.WriteLine("|{0,-140}|{1,-19}|{2,-16}|",
                employer.Name,
                employer.Count,
                employer.AvgSalary == 0 ? "" : employer.AvgSalary);
        }
        Console.WriteLine($"Всего компаний: {result.EmployerStats.Count}");

        // города
        Console.WriteLine("\nГОРОДА:");
        Console.WriteLine();
        Console.WriteLine("|{0,-40}|{1,-19}|{2,-16}|",
            "Город",
            "Количество вакансий",
            "Средняя зарплата");
        Console.WriteLine("|{0,-40}|{1,-19}|{2,-16}|",
            new string('-', 40),
            new string('-', 19),
            new string('-', 16));

        foreach (var city in result.CityStats)
        {
            Console.WriteLine("|{0,-40}|{1,-19}|{2,-16}|",
                city.Name,
                city.Count,
                city.AvgSalary == 0 ? "" : city.AvgSalary);
        }
        Console.WriteLine($"Всего городов: {result.CityStats.Count}");
    }
}

// Классы для десериализации JSON
public class Vacancy
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("salary")] public Salary Salary { get; set; }

    [JsonProperty("employer")] public Employer Employer { get; set; }

    [JsonProperty("area")] public Area Area { get; set; }

    [JsonProperty("published_at")] public DateTime PublishedAt { get; set; }
}

public class Salary
{
    [JsonProperty("from")] public int? From { get; set; }

    [JsonProperty("to")] public int? To { get; set; }

    [JsonProperty("currency")] public string Currency { get; set; }

    [JsonProperty("gross")] public bool? Gross { get; set; }
}

public class Employer
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; }
}

public class Area
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; }
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
