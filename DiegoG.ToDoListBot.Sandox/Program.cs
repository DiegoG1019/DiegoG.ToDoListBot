using HtmlAgilityPack;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiegoG.ToDoListBot.Sandox;

internal class Program
{
    static string GetNextUri(int page)
        => $"https://www.study-in-germany.de/en/plan-your-studies/study-options/programme/higher-education-compass/?hec-p={page}";

    static void ReportMatch(int page, string uri, string courseTitle, string courseUri)
    {
        Console.ResetColor();
        Console.Write("\t\tMatch found on page ");

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write(page);

        Console.ResetColor();
        Console.WriteLine(": ");

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write("\t\t\t");
        Console.WriteLine(courseTitle);

        Console.Write("\t\t\t");
        Console.WriteLine(courseUri);

        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("\t\t");
        Console.WriteLine(uri);
    }

    static async Task Main(string[] args)
    {
        const string ArticleXpath
            = @"//*[@id=""__layout""]/div/div/div/div/main/div/section/div/div/div/div/div/div/ul/li/div/article";

        const string CourseTitleXpath
            = @"div/h3/a/span[2]";

        const string AnchorXpath
            = @"div/h3/a";

        const string DegreeXpath
            = @"dl/div[1]/dd";

        var http = new HttpClient();

        HtmlDocument doc = new();

        Console.ResetColor();
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Filtering university website");
        for (int i = 0; i < 1116; i++)
        {
            Console.ResetColor();
            Console.Write("\tSearching page ");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(i);

            var uri = GetNextUri(i);
            doc.Load(await http.GetStreamAsync(uri));
            Console.WriteLine(" ✔️");

            foreach (var node in doc.DocumentNode.SelectNodes(ArticleXpath))
            {
                var courseTitle = node.SelectSingleNode(CourseTitleXpath).InnerText;

                if (courseTitle.Contains("physics", StringComparison.OrdinalIgnoreCase)
                 || courseTitle.Contains("astro", StringComparison.OrdinalIgnoreCase)
                 || courseTitle.Contains("biolog", StringComparison.OrdinalIgnoreCase)
                 || courseTitle.Contains("history", StringComparison.OrdinalIgnoreCase))
                {
                    var degreeTitle = node.SelectSingleNode(DegreeXpath).InnerText;

                    if (degreeTitle.Contains("bachelor", StringComparison.OrdinalIgnoreCase))
                    {
                        var courseUri = "https://www.study-in-germany.de" + node.SelectSingleNode(AnchorXpath).GetAttributeValue("href", (string)null!);
                        ReportMatch(i, uri, courseTitle, courseUri);
                    }
                }
            }
        }
    }
}
