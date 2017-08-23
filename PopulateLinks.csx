#r "System.Web"
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Linq;

public static void Run(TimerInfo ShouldBe615, TraceWriter log)
{
    log.Info($"PopulateLinks function executed at: {DateTime.Now}");
    // query DB for topics
    DocumentClient client = new DocumentClient(new Uri(Environment.GetEnvironmentVariable("DOCDB_URL")), 
            Environment.GetEnvironmentVariable("DOCDB_KEY"));
    
    // create some topics
    //PopulateTopics(client);

    // Set some common query options
    FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

    // Here we find the active topics
    IQueryable<Topic> topics = client.CreateDocumentQuery<Topic>(
            UriFactory.CreateDocumentCollectionUri("NewsMessages", "Topics"), queryOptions)
            .Where(f => f.Active == true);
    // loop through the topics
    foreach (Topic tp in topics)
    {
        log.Info($"creating links for this topic: {tp.Title}");
        createAndStoreLinks(client, tp);
        // call the cog services API for each topic
        // store results for each topic
    }
        


    log.Info($"PopulateLinks function finished at: {DateTime.Now}");
}

public static void createAndStoreLinks(DocumentClient client, Topic topic) 
{
    DateTime now = DateTime.Now;
    var linksUri = UriFactory.CreateDocumentCollectionUri("NewsMessages", "Links");
    // call the rest API
    var httpClient = new HttpClient();
    var queryString = HttpUtility.ParseQueryString(string.Empty);

    // Request headers
    httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Environment.GetEnvironmentVariable("SEARCH_KEY"));

    // Request parameters
    queryString["q"] = topic.Terms;
    queryString["count"] = "3";
    queryString["offset"] = "0";
    queryString["freshness"] = "Day";
    queryString["mkt"] = "en-us";
    queryString["safeSearch"] = "Strict";
    var uri = "https://api.cognitive.microsoft.com/bing/v5.0/news/search?" + queryString;

    var response = httpClient.GetAsync(uri).Result;
    var contents = response.Content.ReadAsStringAsync().Result;

    dynamic json = JsonConvert.DeserializeObject(contents);
    dynamic values = json.value;
    foreach (var v in values)
    {
        string url = v.url;     
        string lk = url.Substring(url.LastIndexOf("http"), (url.LastIndexOf("&p=DevEx") - url.LastIndexOf("http")));  

        Link link = new Link{Created=now, Active=true};
        link.URL = lk;
        link.Published = v.datePublished;
        link.Name = v.name;
        link.Description = v.description;
        link.TopicTitle = topic.Title;
        link.TopicTerms = topic.Terms;

        client.CreateDocumentAsync(linksUri, link);
    }

}

public class Topic
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }
    public string Title { get; set; }
    public string Terms { get; set; }
    public bool Active { get; set; }
    public DateTime Created { get; set; }
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}

public class Link
{
    public string URL { get; set; }
    public DateTime Published { get; set; }
    public string TopicTitle { get; set; }
    public string TopicTerms { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Active { get; set; }
    public DateTime Created { get; set; }
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}

public static void PopulateTopics(DocumentClient client)
{
    var topicsUri = UriFactory.CreateDocumentCollectionUri("NewsMessages", "Topics");
    Topic datasci = new Topic{
        Id = "1",
        Title = "Data Science",
        Terms = "'data science' or 'machine learning'",
        Active = true,
        Created = DateTime.Now

    };
    client.CreateDocumentAsync(topicsUri, datasci);
    Topic innov = new Topic{
        Id = "2",
        Title = "Business Innovation",
        Terms = "'business innovation'",
        Active = true,
        Created = DateTime.Now

    };
    client.CreateDocumentAsync(topicsUri, innov);
    Topic ns = new Topic{
        Id = "3",
        Title = "Network Security",
        Terms = "'network security'",
        Active = true,
        Created = DateTime.Now

    };
    client.CreateDocumentAsync(topicsUri, ns);
    Topic ai = new Topic{
        Id = "4",
        Title = "AI",
        Terms = "AI or 'deep learning'",
        Active = true,
        Created = DateTime.Now

    };
    client.CreateDocumentAsync(topicsUri, ai);
}

