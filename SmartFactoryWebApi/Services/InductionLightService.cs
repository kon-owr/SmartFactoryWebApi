using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 供业务层使用的感应料架灯光服务。
    /// </summary>
    public class InductionLightService : IInductionLightService
    {
        /// <summary>
        /// 调用外部感应料架 HTTP 接口的底层服务。
        /// </summary>
        private readonly IInductionRackApiService _rackApi;

        /// <summary>
        /// 初始化感应灯光服务依赖的外部料架接口。
        /// </summary>
        public InductionLightService(IInductionRackApiService rackApi)
        {
            _rackApi = rackApi;
        }

        /// <summary>
        /// 点亮指定料架的所有空库位，用于引导感应入库。
        /// </summary>
        public async Task<string> LightOnAllEmptyLocationAsync(string shelfCode, int color)
        {
            return await _rackApi.LightOnAllEmptyLocationAsync(shelfCode, color);
        }

        /// <summary>
        /// 点亮一批标签对应库位，用于感应拣货预览或启动出库。
        /// </summary>
        public async Task<string> LightUpLabelAsync(List<string> labelIds, int color, int outStockType)
        {
            var labelIdListJson = JsonSerializer.Serialize(labelIds);
            var detailsJson = string.Empty;

            return await _rackApi.LightUpLabelAsync(labelIdListJson, color, outStockType, detailsJson);
        }
    }
}
