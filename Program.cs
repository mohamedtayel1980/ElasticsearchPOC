using Elasticsearch.Net;
using Nest;
using OfficeOpenXml;
using System;
using System.Configuration;
using System.Xml;
using Newtonsoft.Json;
using System.Text;
class Program
{
    static void Main(string[] args)
    {
        // Set console to UTF-8 to handle Arabic characters
        Console.OutputEncoding = Encoding.UTF8;

        // Change the console code page to UTF-8 (this is for Windows)
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Read from App.config
        string elasticUri = ConfigurationManager.AppSettings["ElasticsearchUri"];
        string username = ConfigurationManager.AppSettings["ElasticsearchUsername"];
        string password = ConfigurationManager.AppSettings["ElasticsearchPassword"];
        string excelFilePath = ConfigurationManager.AppSettings["excelFilePath"];


        // Set up the Elasticsearch client
        var settings = new ConnectionSettings(new Uri(elasticUri))
                       .DefaultIndex("my_test_index")
                       .BasicAuthentication(username, password)
                         .DisableDirectStreaming(true)  // Enable this for better debugging
                       .ServerCertificateValidationCallback(CertificateValidations.AllowAll);




        var client = new ElasticClient(settings);
        // Verify if the index exists
        var existsResponse = client.Indices.Exists("my_test_index");
        if (!existsResponse.Exists)
        {
            Console.WriteLine("The specified index does not exist.");
            return;
        }



        // Load names from Excel
        var names = LoadNamesFromExcel(excelFilePath);

        // Index the names into Elasticsearch
        IndexNames(client, names);

        // Name to search for using fuzzy search
        string searchNameAr = @"حازم سيد عبدالعاطى موسى";
        string searchName = @"Hazem Sayed Abd El Moaty Moussa";

        // Perform a fuzzy search and generate the response JSON
        var jsonResponse = PerformFuzzySearchWithBataches(client, names, searchName);
        var jsonResponse1 = PerformFuzzyArabic(client, names, searchNameAr);

        // Output the JSON response
        Console.WriteLine(jsonResponse);
        Console.WriteLine(jsonResponse1);
    }
    static string PerformFuzzySearchWithBataches(ElasticClient client, List<(string FullNameAr, string FullNameEn)> names, string searchName)
    {
        var allResults = new List<SearchResult>();

        // Batch size can be adjusted based on your maxClauseCount setting
        int batchSize = 1000;

        foreach (var batch in Batch(names, batchSize))
        {
            var searchResponse = client.Search<Person>(s => s
                .Query(q => q
                    .Bool(b => b
                        .Should(batch.Select(name => (Func<QueryContainerDescriptor<Person>, QueryContainer>)(f => f
                            .Fuzzy(fz => fz
                                .Field(p => p.FullName)
                                .Value(searchName)
                                .Fuzziness(Fuzziness.Auto)

                            )
                        )).ToArray())
                    )
                )
            );

            if (searchResponse.IsValid)
            {
                foreach (var hit in searchResponse.Hits)
                {
                    var exactMatch = hit.Source.FullName == searchName;
                    var matchingScore = hit.Score ?? 0;

                    allResults.Add(new SearchResult
                    {
                        FullName = hit.Source.FullName,
                        FullNameEn = hit.Source.FullNameEn,
                        MatchingScore = matchingScore,
                        ExactMatching = exactMatch
                    });
                }
            }
            else
            {
                Console.WriteLine($"Batch search failed: {searchResponse.ServerError?.Error?.Reason}");
            }
        }



        return JsonConvert.SerializeObject(allResults, Newtonsoft.Json.Formatting.Indented);
    }

    static string PerformFuzzyArabic(ElasticClient client, List<(string FullNameAr, string FullNameEn)> names, string searchName)
    {

        //       var searchResponse = client.Search<Person>(s => s
        //    .Index("my_test_index")  // Specify your index name
        //    .Size(100)               // Set the size to 100 to retrieve the first 100 results
        //    .Query(q => q
        //        .Bool(b => b
        //            .Should(names.Take(100).Select(name =>
        //                (Func<QueryContainerDescriptor<Person>, QueryContainer>)(f => f
        //                    .Fuzzy(fz => fz
        //                        .Field(p => p.FullNameEn)  // Ensure you are using the correct field
        //                         .Query(searchName)
        //                        .Analyzer("my_arabic_analyzer")
        //                    //.Value(searchName.Substring(0,3))
        //                    //.Fuzziness(Fuzziness.Auto)
        //                    )
        //                )
        //            ).ToArray())
        //        )
        //    )
        //);
        var searchResponse = client.Search<Person>(s => s
           .Index("my_test_index")
               .Query(q => q
               .Match(m => m
                   .Field(p => p.FullName)  // Ensure this is mapped with the arabic analyzer
                   .Query(searchName)
                   .Analyzer("my_arabic_analyzer")
               )
           )
       );
        if (!searchResponse.IsValid)
        {
            Console.WriteLine($"Search failed: {searchResponse.ServerError?.Error?.Reason}");
            return string.Empty;
        }
        else
        {
            Console.WriteLine($"Search term: {searchName}");
            Console.WriteLine($"Total Matches: {searchResponse.Total}");
            var results = new List<SearchResult>();

            foreach (var hit in searchResponse.Hits)
            {
                var exactMatch = hit.Source.FullName == searchName;
                var matchingScore = hit.Score ?? 0;

                results.Add(new SearchResult
                {
                    FullName = hit.Source.FullName,
                    FullNameEn = hit.Source.FullNameEn,
                    MatchingScore = matchingScore,
                    ExactMatching = exactMatch
                });
            }

            // Serialize the results to JSON
            return JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
        }
    }
    public static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int batchSize)
    {
        var batch = new List<T>(batchSize);

        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    static List<(string FullNameAr, string FullNameEn)> LoadNamesFromExcel(string filePath)
    {
        var names = new List<(string FullNameAr, string FullNameEn)>();

        // Set the license context for EPPlus
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        // Load the Excel file
        using (var package = new ExcelPackage(new FileInfo(filePath)))
        {
            // Get the first worksheet in the workbook
            ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

            // Assume the Arabic names are in the first column and English names are in the second column
            int rowCount = worksheet.Dimension.Rows;
            for (int row = 2; row <= rowCount; row++) // Start from row 2 to skip header
            {
                string fullNameAr = worksheet.Cells[row, 1].Text;
                string fullNameEn = worksheet.Cells[row, 2].Text;

                if (!string.IsNullOrEmpty(fullNameAr) && !string.IsNullOrEmpty(fullNameEn))
                {
                    names.Add((fullNameAr, fullNameEn));
                }
            }
        }

        return names;
    }
    static void IndexNames(ElasticClient client, List<(string FullNameAr, string FullNameEn)> names)
    {
        foreach (var name in names)
        {
            var person = new Person
            {
                FullName = name.FullNameAr,
                FullNameEn = name.FullNameEn
            };
            var indexResponse = client.IndexDocument(person);

            if (indexResponse.IsValid)
            {
                //Console.WriteLine($"Indexed document in index: {indexResponse.Index}");
                //Console.WriteLine($"Indexed: {name.FullNameAr} ({name.FullNameEn})");
            }
            else
            {
                Console.WriteLine($"Failed to index: {name.FullNameAr} - Reason: {indexResponse.ServerError.Error.Reason}");
            }
        }
    }
    static void PerformaElasticSearch(ElasticClient client)
    {
        // Load data from Excel

        var person = new
        {
            Id = 1,
            Name = "John Doe",
            Age = 30,
            Occupation = "Software Developer"
        };

        var indexResponse = client.IndexDocument(person);

        if (indexResponse.IsValid)
        {
            Console.WriteLine("Document indexed successfully");
        }
        else
        {
            Console.WriteLine("Failed to index document");
        }
        var searchResponse = client.Search<object>(s => s
            .Query(q => q
                .Match(m => m
                    .Field("name")
                    .Query("John Doe")
                )
            )
        );

        foreach (var hit in searchResponse.Hits)
        {
            Console.WriteLine($"Found document with ID: {hit.Id}");
        }
    }
}
