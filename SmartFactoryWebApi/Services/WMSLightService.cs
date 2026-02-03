using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    public class WMSLightService : IWMSLightService
    {
        private static readonly Uri LightUpSomeLampUri = new("http://10.50.77.246:8091/api/services/app/LightBarOtherRuleService/LightUpSomeLampBeads");
        // HTTP 客户端和序列化选项可以复用
        private static readonly HttpClient HttpClient = new();
        // 序列化选项，使用 Web 默认设置
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        // 参数说明：binNoList - 货架编号列表 cancellationToken - 取消令牌（可选）
        public async Task<string> ChangeBinNoLightStatus(List<string> binNoList, LightColorCode lightColor, CancellationToken cancellationToken = default)
        {
            if (binNoList is null)
            {
                throw new ArgumentNullException(nameof(binNoList));
            }

            // 构建亮灯报文列表
            var lightCommands = binNoList
                .Where(binNo => !string.IsNullOrWhiteSpace(binNo))
                .Select(binNo => new LightCommand(binNo.Trim(), lightColor))
                .ToArray();

            if (lightCommands.Length == 0)
            {
                throw new ArgumentException("需要至少一个有效的 binNo。", nameof(binNoList));
            }

            // 序列化亮灯报文列表
            var jsonData = JsonSerializer.Serialize(lightCommands, SerializerOptions);
            // 构建请求体
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
                // 返回result中message中的信息
                return TryGetResultMsg(responseBody);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // 返回result中message中的信息
                return "调用接口超时：联系IT检查服务器是否开启";
            }
        }


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

        private sealed record LightCommand(
            [property: JsonPropertyName("location")] string Location,
            [property: JsonPropertyName("color")] LightColorCode Color);
    }
}
