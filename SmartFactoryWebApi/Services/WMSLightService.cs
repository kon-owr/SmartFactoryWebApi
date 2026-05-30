using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 调用普通料架灯控接口，并把库位集合转换为灯控服务要求的请求报文。
    /// </summary>
    public class WMSLightService : IWMSLightService
    {
        /// <summary>
        /// 普通料架批量亮灯接口的固定地址。
        /// </summary>
        private static readonly Uri LightUpSomeLampUri = new("http://10.50.77.246:8091/api/services/app/LightBarOtherRuleService/LightUpSomeLampBeads");

        /// <summary>
        /// 复用到普通料架灯控服务的 HTTP 连接。
        /// </summary>
        private static readonly HttpClient HttpClient = new();

        /// <summary>
        /// 灯控请求和响应解析使用的 JSON 配置。
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// 普通料架灯控接口的单次请求超时时间。
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 将库位列表转换为灯控指令并调用普通料架接口切换灯光颜色。
        /// </summary>
        public async Task<string> ChangeBinNoLightStatus(List<string> binNoList, LightColorCode lightColor, CancellationToken cancellationToken = default)
        {
            if (binNoList is null)
            {
                throw new ArgumentNullException(nameof(binNoList));
            }

            var lightCommands = binNoList
                .Where(binNo => !string.IsNullOrWhiteSpace(binNo))
                .Select(binNo => new LightCommand(binNo.Trim(), lightColor))
                .ToArray();

            if (lightCommands.Length == 0)
            {
                throw new ArgumentException("需要至少一个有效的 binNo。", nameof(binNoList));
            }

            var jsonData = JsonSerializer.Serialize(lightCommands, SerializerOptions);
            var requestBody = JsonSerializer.Serialize(new { jsonData }, SerializerOptions);

            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(RequestTimeout);

            try
            {
                using var response = await HttpClient.PostAsync(LightUpSomeLampUri, content, timeoutCts.Token).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"调用亮灯接口失败（{(int)response.StatusCode}）：{responseBody}");
                }
                return TryGetResultMsg(responseBody);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return "调用接口超时：联系IT检查服务器是否开启";
            }
        }

        /// <summary>
        /// 提取灯控接口响应中的 <c>result.message</c>，解析失败时回退为原始响应正文。
        /// </summary>
        private static string TryGetResultMsg(string responseBody)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("result", out var resultElement) &&
                    resultElement.ValueKind == JsonValueKind.Object &&
                    resultElement.TryGetProperty("message", out var msgElement))
                {
                    return msgElement.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                // ignore and fall back to raw body
            }

            return responseBody;
        }

        /// <summary>
        /// 表示发送给普通料架灯控服务的单个库位亮灯指令。
        /// </summary>
        private sealed record LightCommand(
            [property: JsonPropertyName("location")] string Location,
            [property: JsonPropertyName("color")] LightColorCode Color);
    }
}
