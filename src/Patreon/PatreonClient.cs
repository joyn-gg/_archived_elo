using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;

namespace Patreon.NET
{
    public class PatreonClient : IDisposable
    {
        public const string SAFE_ROOT = "https://www.patreon.com/api/oauth2/api/";

        public const string PUBLIC_ROOT = "https://www.patreon.com/api/";

        public static string CampaignURL(string campaignId) => SAFE_ROOT + $"campaigns/{campaignId}/";

        public static string PledgesURL(string campaignId) => CampaignURL(campaignId) + "pledges";

        public static string UserURL(string userId) => PUBLIC_ROOT + "user/" + userId;

        public static string PLEDGE_FIELDS => "fields%5Bpledge%5D=amount_cents,created_at,declined_since,pledge_cap_cents,patron_pays_fees,total_historical_amount_cents,is_paused,has_shipping_address";

        private HttpClient httpClient;

        public PatreonClient(string accessToken)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        }

        public async Task<HttpResponseMessage> GET(string url) => await httpClient.GetAsync(url);

        public class PledgesInfo
        {
            public List<Included> Included;

            public List<Datum> Data;

            public List<LinkedPatron> Users;
        }

        public class LinkedPatron
        {
            public Datum UserData;

            public List<Included> Rewards;

            public Included UserIncluded;
        }

        public async Task<PledgesInfo> GetCampaignPledges(string campaignId)
        {
            string next = PledgesURL(campaignId);
            var info = new PledgesInfo
            {
                Included = new List<Included>(),
                Data = new List<Datum>(),
                Users = new List<LinkedPatron>()
            };

            do
            {
                var url = next;
                if (url.Contains("?"))
                    url += "&" + PLEDGE_FIELDS;
                else
                    url += "?" + PLEDGE_FIELDS;

                var response = await GET(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var content = PledgesResponse.FromJson(json);
                    info.Included.AddRange(content.Included);
                    info.Data.AddRange(content.Data);

                    foreach (var dataPacket in content.Data)
                    {
                        var linkedPatron = new LinkedPatron
                        {
                            UserData = dataPacket,
                            Rewards = content.Included.AsQueryable().Where(x => x.Type.Equals("reward") && x.Id.Equals(dataPacket.Relationships.Reward.Data.Id)).ToList(),
                            UserIncluded = content.Included.FirstOrDefault(x => x.Type.Equals("user") && x.Id.Equals(dataPacket.Relationships.Patron.Data.Id))
                        };

                        info.Users.Add(linkedPatron);
                    }

                    next = content.Links.Next?.ToString();
                }
                else
                {
                    next = null;
                }
            } while (next != null);

            return info;
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}