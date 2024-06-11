using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace migris
{
    internal class Program
    {
        private class Language
        {
            [JsonProperty("key")]
            public string Key { get; set; }
            [JsonProperty("code")]
            public string Code { get; set; }
            [JsonProperty("titleLt")]
            public string TitleLt { get; set; }
            [JsonProperty("titleEn")]
            public string TitleEn { get; set; }
        }

        private class Service
        {
            [JsonProperty("key")]
            public string Key { get; set; }
            [JsonProperty("code")]
            public string Code { get; set; }
            [JsonProperty("titleLt")]
            public string TitleLt { get; set; }
            [JsonProperty("titleEn")]
            public string TitleEn { get; set; }
            [JsonProperty("properties")]
            public ServiceProperties Properties { get; set; }
        }

        private class ServiceProperties
        {
            [JsonProperty("VISIT_LENGTH")]
            public string VisitLength { get; set; }
            [JsonProperty("VIZITAS_SU_PRASYMU")]
            public string VizitasSuPrasymu { get; set; }
        }

        private class Institution
        {
            [JsonProperty("key")]
            public string Key { get; set; }
            [JsonProperty("code")]
            public string Code { get; set; }
            [JsonProperty("titleLt")]
            public string TitleLt { get; set; }
            [JsonProperty("titleEn")]
            public string TitleEn { get; set; }
            [JsonProperty("properties")]
            public InstitutionProperties Properties { get; set; }
        }

        private class InstitutionProperties
        {
            [JsonProperty("ORG_UNIT_IDS")]
            public string OrgUnitIds { get; set; }
            [JsonProperty("INSTITUTION_UNIT_TYPE")]
            public string InstitutionUnitType { get; set; }
            [JsonProperty("TAR_KODAS")]
            public string TarKodas { get; set; }
        }

        private class Ticket
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("surname")]
            public string Surname { get; set; }
            [JsonProperty("birthday")]
            public DateTime Birthday { get; set; }
            [JsonProperty("phone")]
            public string Phone { get; set; }
            [JsonProperty("email")]
            public string Email { get; set; }
            [JsonProperty("language")]
            public Language Language { get; set; }

            [JsonProperty("docDeliveryTypeEnum")]
            public string DocDeliveryTypeEnum { get; set; }

            [JsonProperty("visitOtherPerson")]
            public object VisitOtherPerson { get; set; }

            [JsonProperty("date")]
            public DateTime Date { get; set; }

            [JsonProperty("service")]
            public Service Service { get; set; }

            [JsonProperty("institution")]
            public Institution Institution { get; set; }
        }

        private static IEnumerable<Service> GetServices()
        {
            var source = "https://www.migracija.lt/external/classifiers/MIGRIS_KL45_VIZITU_TIPAI?lang=lt";
            var client = new HttpClient();
            var response = client
                .GetStringAsync(source).Result;
            return JsonConvert.DeserializeObject<Service[]>(response);
        }

        private static IEnumerable<Institution> GetInstitutions(Service service)
        {
            var source = $"https://www.migracija.lt/external/tickets/classif/{service.Key}/institutions";
            var client = new HttpClient();
            var response = client
                .GetStringAsync(source).Result;
            return JsonConvert.DeserializeObject<Institution[]>(response);
        }

        private static IEnumerable<Language> GetLanguages()
        {
            var source = "https://www.migracija.lt/external/classifiers/MIGRIS_KL46_PASLAUGU_TEIKIMO_KALBOS?lang=lt";
            var client = new HttpClient();
            var response = client
                .GetStringAsync(source).Result;
            return JsonConvert.DeserializeObject<Language[]>(response);
        }


        private static long GetUnixTimestamp(DateTime time)
        {
            return ((DateTimeOffset)time).ToUnixTimeMilliseconds();
        }

        private static IEnumerable<DateTime> GetTimes(Service service, Institution institution, DateTime? from = null, DateTime? to = null)
        {
            var source = $"https://www.migracija.lt/external/tickets/classif/{service.Key}/{institution.Key}";
            var client = new HttpClient();
            var response = client
                .GetStringAsync(
                    $"{source}/dates?t={GetUnixTimestamp(DateTime.Now)}").Result;
            foreach (var date in JsonConvert.DeserializeObject<DateTime[]>(response))
            {
                if (date < from || date > to)
                    continue;
                response = client.GetStringAsync(
                        $"{source}/{date:yyyy-MM-dd}/times?t={GetUnixTimestamp(DateTime.Now)}")
                    .Result;
                foreach (var time in JsonConvert.DeserializeObject<DateTime[]>(response))
                {
                    yield return time;
                }
            }
        }

        private static Ticket FormatTicket(Service service, Institution institution, Language language, string name, string surname, DateTime birthDate, string phone,
            string email, DateTime proposeDate)
        {
            return new Ticket
            {
                Name = name,
                Surname = surname,
                Birthday = birthDate,
                Phone = phone,
                Email = email,
                Language = language,
                DocDeliveryTypeEnum = "ORDER_DEPARTMENT",
                Date = proposeDate,
                Service = service,
                Institution = institution
            };
        }

        private static bool OrderTicket(Ticket ticket)
        {
            try
            {
                var destination = "https://www.migracija.lt/external/tickets/ticket";

                var settings = new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc
                };
                var ticketJson = JsonConvert.SerializeObject(ticket, settings);
                var client = new HttpClient();
                var response = client
                    .PostAsync(destination, new StringContent(ticketJson, Encoding.UTF8, "application/json")).Result;
                if (response.IsSuccessStatusCode) return true;
                Console.WriteLine($"Failed to order the ticket: {response.Content.ReadAsStringAsync()}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to order the ticket. Reason: {ex.Message}");
                return false;
            }
        }


        static void Main(string[] args)
        {
            var serviceKey = "KL45_02"; // https://www.migracija.lt/external/classifiers/MIGRIS_KL45_VIZITU_TIPAI?lang=lt
            var institutionKey = "KL02_86"; // https://www.migracija.lt/external/tickets/classif/{serviceKey}/institutions
            var languageKey = "KL46_01"; // https://www.migracija.lt/external/classifiers/MIGRIS_KL46_PASLAUGU_TEIKIMO_KALBOS?lang=lt

            var name = "Testas";
            var surname = "Testauskas";
            var birthdate = new DateTime(1970, 01, 01);
            var phone = "+37060000000";
            var email = "testas@testauskas.com";

            var desiredDateRangeFrom = DateTime.Now;
            var desiredDateRangeTo = desiredDateRangeFrom.AddDays(10);
            var desiredTimeFrom = 9;
            var desiredTimeTo = 11;

            var service = GetServices().First(x => x.Key == serviceKey);
            var institution = GetInstitutions(service).First(x => x.Key == institutionKey);
            var language = GetLanguages().First(x => x.Key == languageKey);
            
            for (;;)
            {
                Console.WriteLine("Check in progress ...");
                var times = GetTimes(service, institution, desiredDateRangeFrom, desiredDateRangeTo).OrderBy(x => x).ToArray();
                var goodTimes = times.Where(x => x.Hour >= desiredTimeFrom && x.Hour < desiredTimeTo).ToArray();
                if (goodTimes.Any())
                {
                    foreach (var time in goodTimes)
                    {
                        Console.WriteLine($"Time found: {time}. Ordering ...");
                        var ticket = FormatTicket(service, institution, language, name, surname, birthdate, phone,
                            email, time);
                        if (!OrderTicket(ticket))
                            continue;

                        Console.WriteLine("Ticket ordered!");
                        for (;;)
                        {
                            Console.Beep();
                            Thread.Sleep(1000);
                        }
                    }
                }
                Console.WriteLine("Sleep");
                Thread.Sleep(60000);
            }
        }
    }
}
