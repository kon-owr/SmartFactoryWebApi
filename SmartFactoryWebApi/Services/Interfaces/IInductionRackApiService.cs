namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 定义与外部感应料架服务交互的原始接口。
    /// </summary>
    public interface IInductionRackApiService
    {
        /// <summary>
        /// 向料架提交上架请求。
        /// </summary>
        Task<string> DepositLabelAsync(string labelId, string shelfCode, string operationTime, string detailsJson);

        /// <summary>
        /// 从料架侧移除已提交的标签。
        /// </summary>
        Task<string> RemoveLabelAsync(string labelId);

        /// <summary>
        /// 控制指定条码集合所在库位的亮灯或出库行为。
        /// </summary>
        Task<string> LightUpLabelAsync(string labelIdListJson, int color, int outStockType, string detailsJson);

        /// <summary>
        /// 控制指定料架下所有空库位的灯光状态。
        /// </summary>
        Task<string> LightOnAllEmptyLocationAsync(string shelfCode, int color);
    }
}
