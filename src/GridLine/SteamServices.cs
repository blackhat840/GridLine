using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GridLine
{
    [DataContract]
    internal sealed class SteamEntry
    {
        [DataMember(Name = "appid")] public uint AppId { get; set; }
        [DataMember(Name = "name")] public string Title { get; set; }

        public string Id { get { return AppId.ToString(CultureInfo.InvariantCulture); } }
        public string Name
        {
            get
            {
                if (String.IsNullOrWhiteSpace(Title)) return "Steam App " + Id;
                var safe = new string(Title.Take(160).Select(c => Char.IsControl(c) ? ' ' : c).ToArray());
                return safe.Trim();
            }
        }
    }

    [DataContract]
    internal sealed class SteamApproval
    {
        [DataMember] public string Id;
        [DataMember] public string Title;
        [DataMember] public string Category;
    }

    [DataContract]
    internal sealed class SteamState
    {
        [DataMember] public string AccountId;
        [DataMember] public string EncryptedKey;
        [DataMember] public DateTime SyncedUtc;
        [DataMember] public List<string> OwnedIds = new List<string>();
        [DataMember] public List<SteamApproval> Approved = new List<SteamApproval>();

        public bool Connected { get { return !String.IsNullOrEmpty(EncryptedKey); } }

        public static SteamState Load(string path)
        {
            var state = JsonDisk.Load<SteamState>(path) ?? new SteamState();
            if (state.OwnedIds == null) state.OwnedIds = new List<string>();
            if (state.Approved == null) state.Approved = new List<SteamApproval>();
            return state;
        }
    }

    [DataContract]
    internal sealed class SteamRating
    {
        [DataMember(Name = "total_positive")] public long Positive { get; set; }
        [DataMember(Name = "total_negative")] public long Negative { get; set; }
        [DataMember] public DateTime FetchedUtc;

        public long Total { get { return Positive + Negative; } }
    }

    internal static class JsonDisk
    {
        public static T Load<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (var file = File.OpenRead(path))
                    return new DataContractJsonSerializer(typeof(T)).ReadObject(file) as T;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (SerializationException) { return null; }
        }

        public static void Save<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = File.Create(pending))
                    new DataContractJsonSerializer(typeof(T)).WriteObject(file, value);
                if (File.Exists(path)) File.Replace(pending, path, null);
                else File.Move(pending, path);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }

    internal static class SteamServices
    {
        private const string Api = "https://api.steampowered.com/";
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(14),
            MaxResponseContentBufferSize = 8 * 1024 * 1024
        };

        public static bool IsAppId(string id)
        {
            uint parsed;
            return UInt32.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) &&
                parsed != 0 && parsed.ToString(CultureInfo.InvariantCulture) == id;
        }

        public static bool IsAccountId(string id)
        {
            ulong parsed;
            return id != null && id.Length == 17 &&
                UInt64.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
        }

        public static bool IsKey(string key)
        {
            return key != null && key.Length == 32 && key.All(Uri.IsHexDigit);
        }

        public static string ProtectKey(string key)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key),
                null, DataProtectionScope.CurrentUser));
        }

        public static string ReadKey(string encryptedKey)
        {
            if (String.IsNullOrEmpty(encryptedKey)) return null;
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encryptedKey),
                    null, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException) { return null; }
            catch (FormatException) { return null; }
        }

        public static async Task<List<SteamEntry>> GetOwned(string account, string key, CancellationToken cancel)
        {
            string input = Uri.EscapeDataString("{\"steamid\":\"" + account +
                "\",\"include_appinfo\":true,\"include_played_free_games\":true}");
            var payload = await GetJson<OwnedResponse>(Api + "IPlayerService/GetOwnedGames/v1/?input_json=" + input,
                "x-webapi-key", key, cancel).ConfigureAwait(false);
            return ParseOwned(payload);
        }

        internal static List<SteamEntry> ParseOwned(OwnedResponse payload)
        {
            if (payload == null || payload.Response == null || payload.Response.Games == null ||
                payload.Response.Games.Count == 0)
                throw new InvalidDataException("Steam returned no visible games. Check the account and Game Details privacy, then retry.");
            var result = payload.Response.Games.Where(game => game != null && game.AppId != 0)
                .GroupBy(game => game.AppId).Select(group => group.First()).ToList();
            if (result.Count == 0 || payload.Response.GameCount > result.Count)
                throw new InvalidDataException("Steam returned an incomplete library. Retry to keep your current library intact.");
            return result;
        }

        public static async Task<SteamRating> GetRating(string id, CancellationToken cancel)
        {
            string input = Uri.EscapeDataString("{\"appid\":" + id + ",\"num_per_page\":1,\"filter\":1,\"purchase_type\":1}");
            var payload = await GetJson<ReviewResponse>(Api + "IUserReviewsService/GetAppReviews/v1/?input_json=" + input,
                null, null, cancel).ConfigureAwait(false);
            var rating = payload == null ? null : payload.Summary ?? (payload.Response == null ? null : payload.Response.Summary);
            if (rating == null || rating.Positive < 0 || rating.Negative < 0 ||
                rating.Positive > Int32.MaxValue || rating.Negative > Int32.MaxValue) return null;
            rating.FetchedUtc = DateTime.UtcNow;
            return rating;
        }

        internal static async Task<T> GetJson<T>(string url, string header, string secret, CancellationToken cancel) where T : class
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (header != null) request.Headers.TryAddWithoutValidation(header, secret);
                using (var response = await Client.SendAsync(request, cancel).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new ProviderException((int)response.StatusCode);
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        return new DataContractJsonSerializer(typeof(T)).ReadObject(stream) as T;
                }
            }
        }

        internal static async Task<byte[]> GetImage(Uri uri, CancellationToken cancel)
        {
            using (var response = await Client.GetAsync(uri, cancel).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw new ProviderException((int)response.StatusCode);
                var type = response.Content.Headers.ContentType;
                if (type == null || (type.MediaType != "image/png" && type.MediaType != "image/jpeg"))
                    throw new InvalidDataException("The provider did not return a supported image.");
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        [DataContract]
        internal sealed class OwnedResponse
        {
            [DataMember(Name = "response")] public OwnedGames Response { get; set; }
        }

        [DataContract]
        internal sealed class OwnedGames
        {
            [DataMember(Name = "game_count")] public int GameCount { get; set; }
            [DataMember(Name = "games")] public List<SteamEntry> Games { get; set; }
        }

        [DataContract]
        private sealed class ReviewResponse
        {
            [DataMember(Name = "response")] public ReviewResponse Response { get; set; }
            [DataMember(Name = "query_summary")] public SteamRating Summary { get; set; }
        }
    }

    internal sealed class ProviderException : IOException
    {
        public readonly int Status;

        public ProviderException(int status) : base("The provider returned HTTP " +
            status.ToString(CultureInfo.InvariantCulture) + ".")
        {
            Status = status;
        }
    }
}
