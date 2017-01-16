#region Using References
using System;
using System.Net;
using System.Web;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
#endregion
public sealed class PSNMessageAPI
{
    #region Singleton
    private static PSNMessageAPI instance = null;
    private static readonly object padlock = new object();
    PSNMessageAPI()
    {
    }
    public static PSNMessageAPI Instance
    {
        get
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    instance = new PSNMessageAPI();
                }
                return instance;
            }
        }
    }
    #endregion
    private const string clientid = "b7cbf451-6bb6-4a5a-8913-71e61f462787";
    private const string clientsecret = "zsISsjmCx85zgCJg";
    private CookieContainer sonyCookies = new CookieContainer();
    private Uri AUTH_URL = new Uri("https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/authorize");
    private Uri OAUTH_URL = new Uri("https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/token");
    private Uri SSO_URL = new Uri("https://auth.api.sonyentertainmentnetwork.com/2.0/ssocookie");
    private Uri MESSAGING_URL = new Uri("https://us-gmsg.np.community.playstation.net/groupMessaging/v1/messageGroups");
    private string NPSSO;
    private string Oauth;
    private string refreshToken;
    #region Dictonary Post Data
    private static Dictionary<string, string> login_request = new Dictionary<string, string>()
        {
            { "authentication_type", "password" },
            { "username", null },
            { "password", null },
            { "client_id", clientid }
        };
    private static Dictionary<string, string> code_request = new Dictionary<string, string>()
        {
            { "state", "06d7AuZpOmJAwYYOWmVU63OMY" },
            { "duid", "0000000d000400808F4B3AA3301B4945B2E3636E38C0DDFC" },
            { "app_context", "inapp_ios" },
            { "client_id", clientid },
            { "scope", "capone:report_submission,psn:sceapp,user:account.get,user:account.settings.privacy.get,user:account.settings.privacy.update,user:account.realName.get,user:account.realName.update,kamaji:get_account_hash,kamaji:ugc:distributor,oauth:manage_device_usercodes" },
            { "response_type", "code" }
        };
    private static Dictionary<string, string> oauth_request = new Dictionary<string, string>()
        {
            { "app_context", "inapp_ios" },
            { "client_id", clientid },
            { "client_secret", clientsecret },
            { "code", null },
            { "duid", "0000000d000400808F4B3AA3301B4945B2E3636E38C0DDFC" },
            { "grant_type", "authorization_code" },
            { "scope", "capone:report_submission,psn:sceapp,user:account.get,user:account.settings.privacy.get,user:account.settings.privacy.update,user:account.realName.get,user:account.realName.update,kamaji:get_account_hash,kamaji:ugc:distributor,oauth:manage_device_usercodes" }
        };
    private static Dictionary<string, string> refresh_request = new Dictionary<string, string>()
        {
            { "app_context", "inapp_ios" },
            { "client_id", clientid },
            { "client_secret", clientsecret },
            { "refresh_token", null },
            { "duid", "0000000d000400808F4B3AA3301B4945B2E3636E38C0DDFC" },
            { "grant_type", "refresh_token" },
            { "scope", "capone:report_submission,psn:sceapp,user:account.get,user:account.settings.privacy.get,user:account.settings.privacy.update,user:account.realName.get,user:account.realName.update,kamaji:get_account_hash,kamaji:ugc:distributor,oauth:manage_device_usercodes" }
        };
    #endregion
    #region Data Classes
    private class Message : messageContent
    {
        public List<string> to { get; set; }
        //public Message message { get; set; }
    }
    private class messageContent
    {
        public int fakeMessageUid = 1234;
        public string body { get; set; }
        public int messageKind = 1;
    }
    public class playstationAccount
    {
        public string username { get; set; }
        public string password { get; set; }
        public string Oauth { get; set; }
        public string refreshToken { get; set; }
        public DateTime oauthExpiry { get; set; }
    }
    #endregion
    #region Authorization Functions
    private async Task<bool> retrieveNPSSO()
    {
        try
        {
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = sonyCookies, UseCookies = true, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            using (HttpClient client = new HttpClient(handler))
            using (HttpRequestMessage request = new HttpRequestMessage() { Method = HttpMethod.Post, RequestUri = SSO_URL, Content = new FormUrlEncodedContent(login_request) })
            {
                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                if (!responseString.Contains("npsso"))
                    return false;
                dynamic Json = JsonConvert.DeserializeObject(responseString);
                NPSSO = Json.npsso;
                sonyCookies.Add(AUTH_URL, new Cookie("npsso", NPSSO));
                return true;
            }
        }
        catch (Exception) { return false; }
    }
    private async Task<bool> retrieveCode()
    {
        try
        {
            using (var content = new FormUrlEncodedContent(code_request))
                AUTH_URL = new Uri($"{AUTH_URL}?{await content.ReadAsStringAsync()}");
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = sonyCookies, UseCookies = true, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            using (HttpClient client = new HttpClient(handler))
            using (HttpRequestMessage request = new HttpRequestMessage() { Method = HttpMethod.Get, RequestUri = AUTH_URL })
            {
                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                var code = request.RequestUri.AbsoluteUri;
                if (code.Contains("mobile-success.jsp"))
                {
                    code = HttpUtility.UrlDecode(code);
                    code = code.Remove(0, code.IndexOf("e=") + 2);
                    code = code.Substring(0, code.IndexOf("&"));
                    oauth_request["code"] = code;
                    return true;
                }
                return false;
            }
        }
        catch (Exception) { return false; }
    }
    private async Task<bool> retrieveOauth()
    {
        try
        {
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = sonyCookies, UseCookies = true, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            using (HttpClient client = new HttpClient(handler))
            using (HttpRequestMessage request = new HttpRequestMessage() { Method = HttpMethod.Post, RequestUri = OAUTH_URL, Content = new FormUrlEncodedContent(oauth_request) })
            {
                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                if (responseString.Contains("access_token"))
                {
                    dynamic Json = JsonConvert.DeserializeObject(responseString);
                    Oauth = Json.access_token;
                    refreshToken = Json.refresh_token;
                    return true;
                }
                return false;
            }
        }
        catch (Exception) { return false; }
    }
    #endregion
    #region Public Functions
    public async Task<playstationAccount> authorizeAccount(string username, string password)
    {
        playstationAccount psAccount = new playstationAccount();
        login_request["username"] = username;
        login_request["password"] = password;
        psAccount.username = username;
        psAccount.password = password;

        if (await retrieveNPSSO() && await retrieveCode() && await retrieveOauth())
        {
            psAccount.Oauth = Oauth;
            psAccount.refreshToken = refreshToken;
            psAccount.oauthExpiry = DateTime.UtcNow.AddHours(1);
            return psAccount;
        }
        return psAccount;
    }
    public async Task<playstationAccount> reauthorizeAccount(playstationAccount psAccount)
    {
        try
        {
            refresh_request["refresh_token"] = psAccount.refreshToken;
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = new CookieContainer(), UseCookies = true, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            using (HttpClient client = new HttpClient(handler))
            using (HttpRequestMessage request = new HttpRequestMessage() { Method = HttpMethod.Post, RequestUri = OAUTH_URL, Content = new FormUrlEncodedContent(refresh_request) })
            {
                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();
                if (responseString.Contains("access_token"))
                {
                    dynamic Json = JsonConvert.DeserializeObject(responseString);
                    psAccount.Oauth = Json.access_token;
                    psAccount.refreshToken = Json.refresh_token;
                    psAccount.oauthExpiry = DateTime.UtcNow.AddHours(1);
                    return psAccount;
                }
                return psAccount;
            }
        }
        catch (Exception) { return psAccount; }
    }
    public void sendMessage(string Oauth, List<string> Recipients, string Message)
    {
        MultipartContent Multipart = new MultipartContent("mixed", "gc0p4Jq0M2Yt08jU534c0p");
        StringContent Content = new StringContent(JsonConvert.SerializeObject(new Message() { to = Recipients, body = Message }), Encoding.UTF8, "application/json");
        Content.Headers.Add("Content-Description", "message");
        Multipart.Add(Content);
        using (HttpClient client = new HttpClient())
        using (HttpRequestMessage request = new HttpRequestMessage() { Method = HttpMethod.Post, RequestUri = MESSAGING_URL, Content = Multipart })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Oauth);
            client.SendAsync(request);
        }
    }
    #endregion
}
