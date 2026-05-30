using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 与外部感应料架 HTTP 服务交互的底层实现。
    /// </summary>
    public class InductionRackApiService : IInductionRackApiService
    {
        /// <summary>
        /// 复用到外部感应料架服务的 HTTP 连接。
        /// </summary>
        private static readonly HttpClient HttpClient = new();

        /// <summary>
        /// 外部料架请求和响应解析使用的 JSON 配置。
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// 外部料架接口的单次请求超时时间。
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        /// <summary>
        /// 外部感应料架接口基础地址。
        /// </summary>
        private readonly string _apiBaseAddress;

        /// <summary>
        /// 初始化外部感应料架接口地址，配置缺失时使用默认设备地址。
        /// </summary>
        public InductionRackApiService(IConfiguration configuration)
        {
            _apiBaseAddress = configuration["InductionRack:ApiBaseAddress"]
                ?? "http://10.50.77.246:8091";
        }

        /// <summary>
        /// 向外部料架发送条码入库指令，并携带库存事务回调上下文。
        /// </summary>
        public async Task<string> DepositLabelAsync(string labelId, string shelfCode, string operationTime, string detailsJson)
        {
            var url = $"{_apiBaseAddress}/api/services/app/InductionRackService/DepositLabel";
            var requestBody = new
            {
                labelId,
                shelfCode,
                operationTime,
                detailsJson
            };

            return await PostAsync(url, requestBody);
        }

        /// <summary>
        /// 请求外部料架移除指定标签的待入库状态。
        /// </summary>
        public async Task<string> RemoveLabelAsync(string labelId)
        {
            var url = $"{_apiBaseAddress}/api/services/app/InductionRackService/RemoveLabel";
            var requestBody = new { labelId };

            return await PostAsync(url, requestBody);
        }

        /// <summary>
        /// 请求外部料架点亮指定标签列表，并携带出库类型和回调上下文。
        /// </summary>
        public async Task<string> LightUpLabelAsync(string labelIdListJson, int color, int outStockType, string detailsJson)
        {
            var url = $"{_apiBaseAddress}/api/services/app/InductionRackService/LightUpLabel";
            var requestBody = new
            {
                labelIdListJson,
                color,
                outStockType,
                detailsJson
            };

            return await PostAsync(url, requestBody);
        }

        /// <summary>
        /// 请求外部料架点亮或熄灭指定料架的全部空库位。
        /// </summary>
        public async Task<string> LightOnAllEmptyLocationAsync(string shelfCode, int color)
        {
            var url = $"{_apiBaseAddress}/api/services/app/InductionRackService/LightOnAllEmptyLocation";
            var requestBody = new
            {
                shelfCode,
                color
            };

            return await PostAsync(url, requestBody);
        }

        /// <summary>
        /// 统一处理请求序列化、超时控制和响应报文解析。
        /// </summary>
        private async Task<string> PostAsync(string url, object requestBody)
        {
            var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var timeoutCts = new CancellationTokenSource(RequestTimeout);

            try
            {
                using var response = await HttpClient.PostAsync(url, content, timeoutCts.Token);
                var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    return $"调用感应料架接口失败（{(int)response.StatusCode}）：{responseBody}";
                }

                return TryGetResultMsg(responseBody);
            }
            catch (TaskCanceledException) when (!timeoutCts.Token.IsCancellationRequested)
            {
                return "调用感应料架接口超时：联系IT检查服务器是否开启";
            }
            catch (HttpRequestException ex)
            {
                return $"调用感应料架接口失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 提取外部料架响应中的结果消息，解析失败时回退为原始响应。
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
                // 第三方服务偶尔直接返回文本，解析失败时退回原始报文。
            }

            return responseBody;
        }
    }
}
