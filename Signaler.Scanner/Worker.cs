using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Args;
using System.IO;

namespace Signaler.Scanner
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient httpClient;
        public string requestData;
        public string RequestUrl = "https://scanner.tradingview.com/america/scan";
        public ITelegramBotClient botclient;
        private readonly JsonSerializer signalsSerializer;
        private List<Signal> signals;

        public Worker(ILogger<Worker> logger, string filter = null)
        {
            _logger = logger;
            this.httpClient = new HttpClient();
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
                );
            this.httpClient.DefaultRequestHeaders.Add(
                "Cache-Control", "no-cache, no-store, max-age=0, must-revalidate"
                );
            if (filter != null)
            {
                this.requestData = filter;
            }
            this.signalsSerializer = new JsonSerializer();
            this.signalsSerializer.Converters.Add(new TradingViewCryptoSignalConverter());
            this.botclient = new TelegramBotClient("999776373:AAEVAnWPiH1RYx0ZmCYz_sPF-RB4stmlhSA");
            using (StreamReader r = new StreamReader("search.json"))
            {
                this.requestData = r.ReadToEnd();
                _logger.LogInformation("Search String: {0}", JsonConvert.SerializeObject(this.requestData, Formatting.Indented));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await botclient.SendTextMessageAsync(
                    chatId: -1001236171354,
                    text: "Start Analyzing companies with RSI and CCI on 1D time frame over the maximum. Short them."
                );
                var requestContent = new StringContent(this.requestData, Encoding.UTF8, "application/json");
                try
                {
                    using (var response = this.httpClient.PostAsync(this.RequestUrl, requestContent).Result)
                    {
                        var responseContent = response.Content.ReadAsStringAsync().Result;
                        var jtokens = JObject.Parse(responseContent).SelectTokens("data[*].d");
                        signals = jtokens.Select(t =>
                        {
                            var signal = t.ToObject<Signal>(signalsSerializer);
                            return signal;
                        }).ToList();
                        foreach (var s in signals) {
                            await botclient.SendTextMessageAsync(
                                chatId: -1001236171354,
                                text: String.Format("Company {0}\n Close: {1}\n Description: {2}\n Sector: {3}", s.Name, s.Close, s.Description, s.Sector)
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(String.Format("Error fetching information with filter: {0}", JsonConvert.SerializeObject(this.requestData, Formatting.Indented)));
                    throw ex;
                }


                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(100000, stoppingToken);
            }
        }
    }

    internal class TradingViewCryptoSignalConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Signal);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var array = JArray.Load(reader);
                var item = (existingValue as Signal ?? new Signal());
                item.Name = (string)array.ElementAtOrDefault(0);
                item.Close = (decimal?)array.ElementAtOrDefault(1);
                item.Change = (decimal?)array.ElementAtOrDefault(2);
                item.ChangeAbs = (decimal?)array.ElementAtOrDefault(3);
                item.Volume = (decimal?)array.ElementAtOrDefault(4);
                item.MarketCap = (decimal?)array.ElementAtOrDefault(5);
                item.PriceEarningsTTM = (decimal?)array.ElementAtOrDefault(6);
                item.EarningPerShareTTM = (decimal?)array.ElementAtOrDefault(7);
                item.Sector = (string)array.ElementAtOrDefault(8);
                item.Description = (string)array.ElementAtOrDefault(9);
                return item;
            }
            else
            {
                return null;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();

        }
    }

    public class Signal
    {
        public string Name { get; set; }
        public decimal? Close { get; set; }
        public decimal? Change { get; set; }
        public decimal? ChangeAbs { get; set; }
        public decimal? Volume { get; set; }
        public decimal? MarketCap { get; set; }
        public decimal? PriceEarningsTTM { get; set; }
        public decimal? EarningPerShareTTM { get; set; }
        public string Sector { get; set; }
        public string Description { get; set; }
    }
}
