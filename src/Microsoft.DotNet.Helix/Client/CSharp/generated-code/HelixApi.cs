using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.Helix.Client
{
    public partial interface IHelixApi : IDisposable
    {
        Uri BaseUri { get; set; }

        IAggregate Aggregate { get; }
        IAnalysis Analysis { get; }
        IInformation Information { get; }
        IJob Job { get; }
        IMachine Machine { get; }
        IRepository Repository { get; }
        IScaleSets ScaleSets { get; }
        IStorage Storage { get; }
        ITelemetry Telemetry { get; }
        IWorkItem WorkItem { get; }
    }

    public partial class HelixApi : ServiceClient<HelixApi>, IHelixApi
    {
        /// <summary>
        ///   The base URI of the service.
        /// </summary>
        public Uri BaseUri { get; set; }

        /// <summary>
        ///   Credentials to authenticate requests.
        /// </summary>
        public ServiceClientCredentials Credentials { get; set; }

        public JsonSerializerSettings SerializerSettings { get; }

        public IAggregate Aggregate { get; }

        public IAnalysis Analysis { get; }

        public IInformation Information { get; }

        public IJob Job { get; }

        public IMachine Machine { get; }

        public IRepository Repository { get; }

        public IScaleSets ScaleSets { get; }

        public IStorage Storage { get; }

        public ITelemetry Telemetry { get; }

        public IWorkItem WorkItem { get; }


        public HelixApi(params DelegatingHandler[] handlers)
            :this(null, null, handlers)
        {
        }

        public HelixApi(Uri baseUri, params DelegatingHandler[] handlers)
            :this(baseUri, null, handlers)
        {
        }

        public HelixApi(ServiceClientCredentials credentials, params DelegatingHandler[] handlers)
            :this(null, credentials, handlers)
        {
        }

        public HelixApi(Uri baseUri, ServiceClientCredentials credentials, params DelegatingHandler[] handlers)
            :base(handlers)
        {
            HttpClientHandler.SslProtocols = SslProtocols.Tls12;
            BaseUri = baseUri ?? new Uri("https://helix.dot.net/");
            Credentials = credentials;
            Aggregate = new Aggregate(this);
            Analysis = new Analysis(this);
            Information = new Information(this);
            Job = new Job(this);
            Machine = new Machine(this);
            Repository = new Repository(this);
            ScaleSets = new ScaleSets(this);
            Storage = new Storage(this);
            Telemetry = new Telemetry(this);
            WorkItem = new WorkItem(this);
            SerializerSettings = new JsonSerializerSettings
            {
                Converters =
                {
                    new StringEnumConverter()
                },
                NullValueHandling = NullValueHandling.Ignore,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void OnFailedRequest(RestApiException ex)
        {
            HandleFailedRequest(ex);
        }

        partial void HandleFailedRequest(RestApiException ex);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(string value)
        {
            return value;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(bool value)
        {
            return value ? "true" : "false";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value, SerializerSettings);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(string value)
        {
            return JsonConvert.DeserializeObject<T>(value, SerializerSettings);
        }

        public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return HttpClient.SendAsync(request, cancellationToken);
        }
    }

    public class AllPropertiesContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);

            if (!prop.Writable)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    var hasPrivateSetter = property.GetSetMethod(true) != null;
                    prop.Writable = hasPrivateSetter;
                }
            }

            return prop;
        }
    }

    [Serializable]
    public partial class RestApiException : Exception
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new AllPropertiesContractResolver(),
        };

        private static string FormatMessage(HttpResponseMessageWrapper response)
        {
            var result = $"The response contained an invalid status code {(int)response.StatusCode} {response.ReasonPhrase}";
            if (!string.IsNullOrEmpty(response.Content))
            {
                result += "\n\nBody: ";
                result += response.Content.Length < 300 ? response.Content : response.Content.Substring(0, 300);
            }
            return result;
        }

        public HttpRequestMessageWrapper Request { get; }

        public HttpResponseMessageWrapper Response { get; }

        public RestApiException(HttpRequestMessageWrapper request, HttpResponseMessageWrapper response)
           :this(FormatMessage(response), request, response)
        {
        }

        public RestApiException(string message, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response)
           :base(message)
        {
            Request = request;
            Response = response;
        }

        protected RestApiException(SerializationInfo info, StreamingContext context)
            :base(info, context)
        {
            var requestString = info.GetString("Request");
            var responseString = info.GetString("Response");
            Request = JsonConvert.DeserializeObject<HttpRequestMessageWrapper>(requestString, SerializerSettings);
            Response = JsonConvert.DeserializeObject<HttpResponseMessageWrapper>(responseString, SerializerSettings);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            var requestString = JsonConvert.SerializeObject(Request, SerializerSettings);
            var responseString = JsonConvert.SerializeObject(Response, SerializerSettings);

            info.AddValue("Request", requestString);
            info.AddValue("Response", responseString);
            base.GetObjectData(info, context);
        }
    }

    [Serializable]
    public partial class RestApiException<T> : RestApiException
    {
        public T Body { get; }

        public RestApiException(HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, T body)
           :base(request, response)
        {
            Body = body;
        }

        public RestApiException(string message, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, T body)
           :base(message, request, response)
        {
            Body = body;
        }

        protected RestApiException(SerializationInfo info, StreamingContext context)
            :base(info, context)
        {
            Body = JsonConvert.DeserializeObject<T>(info.GetString("Body"));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            info.AddValue("Body", JsonConvert.SerializeObject(Body));
            base.GetObjectData(info, context);
        }
    }

    public partial class QueryBuilder : List<KeyValuePair<string, string>>
    {
        public QueryBuilder()
        {
        }

        public QueryBuilder(IEnumerable<KeyValuePair<string, string>> parameters)
            :base(parameters)
        {
        }

        public void Add(string key, IEnumerable<string> values)
        {
            foreach (string str in values)
                Add(new KeyValuePair<string, string>(key, str));
        }

        public void Add(string key, string value)
        {
            Add(new KeyValuePair<string, string>(key, value));
        }

        public override string ToString()
        {
          var builder = new StringBuilder();
          for (int index = 0; index < Count; ++index)
          {
            KeyValuePair<string, string> keyValuePair = this[index];
            if (index != 0)
            {
                builder.Append("&");
            }
            builder.Append(UrlEncoder.Default.Encode(keyValuePair.Key));
            builder.Append("=");
            builder.Append(UrlEncoder.Default.Encode(keyValuePair.Value));
          }
          return builder.ToString();
        }
    }
}
