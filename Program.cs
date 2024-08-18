﻿using Elasticsearch.Net;
using Nest;

class Program
{
    static void Main(string[] args)
    {
        var settings = new ConnectionSettings(new Uri("https://localhost:9200"))
                       .DefaultIndex("my_index")
                       .BasicAuthentication("elastic", "RU=GYmQ3Dn8u*IUuu-BJ")
                       .ServerCertificateValidationCallback(CertificateValidations.AllowAll);

        var client = new ElasticClient(settings);

        //var person = new
        //{
        //    Id = 1,
        //    Name = "John Doe",
        //    Age = 30,
        //    Occupation = "Software Developer"
        //};

        //var indexResponse = client.IndexDocument(person);

        //if (indexResponse.IsValid)
        //{
        //    Console.WriteLine("Document indexed successfully");
        //}
        //else
        //{
        //    Console.WriteLine("Failed to index document");
        //}
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