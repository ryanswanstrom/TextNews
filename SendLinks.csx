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
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Linq;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"SendLinks function executed at: {DateTime.Now}");
    // query DB for topics
    DocumentClient client = new DocumentClient(new Uri(Environment.GetEnvironmentVariable("DOCDB_URL")), 
            Environment.GetEnvironmentVariable("DOCDB_KEY"));

    // Set some common query options
    FeedOptions queryOptions = new FeedOptions { MaxDegreeOfParallelism = -1, MaxItemCount = -1 };

    // Here we find the active topics
    IQueryable<Topic> topics = client.CreateDocumentQuery<Topic>(
            UriFactory.CreateDocumentCollectionUri("NewsMessages", "Topics"), queryOptions)
            .Where(f => f.Active == true);
    // loop through the topics
    foreach (Topic tp in topics)
    {
        log.Info($"the topic: {tp.Title} {tp.Id}");

        //query for the links
        // Here we find the active links with max date
        string maxD = client.CreateDocumentQuery<string>(
            UriFactory.CreateDocumentCollectionUri("NewsMessages", "Links"), 
            $"Select Value Max(f.Created) from Links f where f.Active = true and f.TopicTitle = '{tp.Title}'",
            queryOptions).AsEnumerable().FirstOrDefault();
        log.Info($"The max date is {maxD}");
        IQueryable<Link> links = client.CreateDocumentQuery<Link>(
            UriFactory.CreateDocumentCollectionUri("NewsMessages", "Links"), queryOptions)
            .Where(f => f.Active == true && f.TopicTitle == tp.Title && f.Created == DateTime.Parse(maxD));


        // query for all active subscribers with that topic
        // Here we find the active topics
        IQueryable<Subscriber> subs = client.CreateDocumentQuery<Subscriber>(
            UriFactory.CreateDocumentCollectionUri("NewsMessages", "Subscribers"),
            $"Select * from Subscribers s where s.Active = true and ARRAY_CONTAINS(s.Topics, '{tp.Id}')", queryOptions);
        
        // for all subscribers
        foreach (Subscriber sb in subs)
        {
            string message = String.Format("Daily {1} News {0}", Environment.NewLine, tp.Title);
            foreach (Link lk in links)
            {
                log.Info($"  link is {lk.Name}");
                message += String.Format("{1}: {2}  {0}", Environment.NewLine, lk.Name, HttpUtility.UrlDecode(lk.URL));
            }
            message += "... from Matisia Labs";
            log.Info($"message is {message}");
            log.Info($"phone to {sb.Phone}");
            // send txt to sb.Phone
            sendTextMessage(sb.Phone, message);
        }
            
    }
        

    log.Info($"SendLinks function finished at: {DateTime.Now}");
}

public class Subscriber
{
    public string Phone { get; set; }
    public bool Active { get; set; }
    public List<string> Topics {get; set; }
    public DateTime Created { get; set; }
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }

}

public static void addSubscribers(DocumentClient client)
{
    var subsUri = UriFactory.CreateDocumentCollectionUri("NewsMessages", "Subscribers");
    Subscriber person = new Subscriber{
        Phone = "4259987875",
        Topics = new List<string> {"1","3"},
        Active = true,
        Created = DateTime.Now

    };
    client.CreateDocumentAsync(subsUri, person);
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

private static void sendTextMessage(string number, string message) 
{

    string AccountSid = Environment.GetEnvironmentVariable("Twilio_SID");
    string AuthToken = Environment.GetEnvironmentVariable("Twilio_AuthToken");
    Twilio.TwilioClient.Init(AccountSid, AuthToken);
    var to = new PhoneNumber(number);
    MessageResource.Create(
        to,
        from: new PhoneNumber(Environment.GetEnvironmentVariable("Twilio_From_Number")),
        body: message);
}
