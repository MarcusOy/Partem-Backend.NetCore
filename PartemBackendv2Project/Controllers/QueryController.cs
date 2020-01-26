using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Boilerpipe.Net.Extractors;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.AutoML.V1;
using Google.Cloud.Language.V1;
using Grpc.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nager.PublicSuffix;
using static PartemBackendProject.APIModels;

namespace PartemBackendProject.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class QueryController : Controller
    {
        public ActionResult<ServiceResponse> Post([Bind("Url")][FromBody]ServiceRequest request)
        {
            // Boilerpipe.net code below
            string html = "";
            string headline = "";
            string source = "";
            string text = "";
            string img = "";

            try
            {
                if (Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
                {
                    using (WebClient webClient = new WebClient())
                    {
                        html = webClient.DownloadString(request.Url);
                    }
                    text = RemoveSpecialCharacters(CommonExtractors.LargestContentExtractor.GetText(html));
                    headline = RemoveSpecialCharacters(Regex.Match(html, @"\<title\b[^>]*\>\s*(?<Title>[\s\S]*?)\</title\>",
                        RegexOptions.IgnoreCase).Groups["Title"].Value);

                    var domainParser = new DomainParser(new WebTldRuleProvider());
                    var domainName = domainParser.Get(request.Url);
                    source = domainName.RegistrableDomain;

                    img = "http://" + FetchLinksFromSource(html)[0].AbsolutePath;
                }
                else
                {
                    return BadRequest(new ServiceResponse
                    {
                        Success = false,
                        Error = "Bad URL."
                    });
                }

                // Google Cloud API code below
                var credential = GoogleCredential.FromJson(ServiceAccountJSON)
                    .CreateScoped(LanguageServiceClient.DefaultScopes);
                var channel = new Grpc.Core.Channel(
                    AutoMlClient.DefaultEndpoint.ToString(),
                    credential.ToChannelCredentials());
                PredictionServiceClient client = PredictionServiceClient.Create(channel);

                string modelFullId = ModelName.Format("partem-1579924523879", "us-central1", "TCN7494917767758348288");

                PredictRequest predictRequest = new PredictRequest
                {
                    Name = modelFullId,
                    Payload = new ExamplePayload
                    {
                        TextSnippet = new TextSnippet
                        {
                            Content = text,
                            MimeType = "text/plain"
                        }
                    }
                };

                PredictResponse response = client.Predict(predictRequest);

                var leftPerc = response.Payload.First(x => x.DisplayName == "left").Classification.Score;
                var rightPerc = response.Payload.First(x => x.DisplayName == "right").Classification.Score;
                return Ok(new ServiceResponse
                {
                    LeftPercentage = leftPerc,
                    RightPercentage = rightPerc,
                    CenterPercentage = 1 - Math.Abs(leftPerc - rightPerc),

                    Headline = headline,
                    Source = source,
                    Image = img,

                    Success = true,
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ServiceResponse
                {
                    Success = false,
                    Error = "An error has occured. Please try again later."
                });
            }
        }

        public static List<string> ArticleSplitter(string article_input)
        {
            char[] delimiters = { '.', '!', '?', '"' };
            List<string> editedText = article_input.Split(delimiters).ToList();

            return editedText;
        }

        public static List<Uri> FetchLinksFromSource(string htmlSource)
        {
            List<Uri> links = new List<Uri>();
            string regexImgSrc = @"<img[^>]*?src\s*=\s*[""']?([^'"" >]+?)[ '""][^>]*?>";
            MatchCollection matchesImgSrc = Regex.Matches(htmlSource, regexImgSrc, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in matchesImgSrc)
            {
                string href = m.Groups[1].Value;
                links.Add(new Uri(href));
            }
            return links;
        }

        public static string RemoveSpecialCharacters(string str)
        {
            return HttpUtility.HtmlDecode(str);
        }

        public string ServiceAccountJSON = @"{Removed to prevent stealing of API access}";
    }
}

