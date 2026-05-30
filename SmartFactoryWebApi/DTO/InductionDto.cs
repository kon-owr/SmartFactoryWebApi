using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmartFactoryWebApi.DTO
{
    public class InductionShelfValidateRequest
    {
        public string ShelfCode { get; set; } = string.Empty;
        public string WarehouseLocation { get; set; } = string.Empty;
    }

    public class InductionShelfValidation
    {
        public bool IsValid { get; set; }
        public string ShelfCode { get; set; } = string.Empty;
        public string WarehouseNo { get; set; } = string.Empty;
        public int EmptyLocationCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class InductionDepositRequest
    {
        public string Barcode { get; set; } = string.Empty;
        public string ShelfCode { get; set; } = string.Empty;
        public string WarehouseLocation { get; set; } = string.Empty;
    }

    public class InductionDepositCallbackRequest
    {
        public string LabelId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? DetailsJson { get; set; }
    }

    public class DepositCallbackMessage
    {
        public bool Success { get; set; }
        public string LabelId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class DepositedItem
    {
        public string BarNo { get; set; } = string.Empty;
        public string BinNo { get; set; } = string.Empty;
        public decimal BarQty { get; set; }
        public DateTime DepositTime { get; set; }
        public int Status { get; set; }
    }

    public class InductionPickQueryRequest
    {
        public string ItemNo { get; set; } = string.Empty;
        public decimal? RequiredQty { get; set; }
        public string WarehouseLocation { get; set; } = string.Empty;
        public int Color { get; set; } = 6;
    }

    public class InductionPickStartRequest
    {
        public List<string> LabelIds { get; set; } = new();
        public string WarehouseLocation { get; set; } = string.Empty;
        public int Color { get; set; } = 6;
    }

    public class InductionPickSuggestionRequest
    {
        public string Keyword { get; set; } = string.Empty;
        public string WarehouseLocation { get; set; } = string.Empty;
        public int Limit { get; set; } = 20;
    }

    public class InductionPickCallbackRequest
    {
        public string LabelId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? DetailsJson { get; set; }
    }

    public class PickCallbackMessage
    {
        public bool Success { get; set; }
        public string LabelId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsIllegal { get; set; }
    }

    public class InductionPickItem
    {
        public string BarNo { get; set; } = string.Empty;
        public string ItemNo { get; set; } = string.Empty;
        public decimal BarQty { get; set; }
        public string BinNo { get; set; } = string.Empty;
        public DateTime InstockDate { get; set; }
        public int Status { get; set; }
    }

    public class InductionLightRequest
    {
        public string ShelfCode { get; set; } = string.Empty;
        public int Color { get; set; } = 2;
    }

    public class InductionLabelLightRequest
    {
        public List<string> LabelIds { get; set; } = new();
        public int Color { get; set; } = 6;
        public int OutStockType { get; set; } = 1;
    }

    public class InductionCancelRequest
    {
        public string Barcode { get; set; } = string.Empty;
    }

    public class InductionPickCancelRequest
    {
        public List<string> LabelIds { get; set; } = new();
    }

    public class InductionDepositDetails
    {
        public string WarehouseLocation { get; set; } = string.Empty;
        public string? BarGuid { get; set; }
        public string? SourceBinNo { get; set; }
        public string? OperationTime { get; set; }
    }

    public class InductionPickDetails
    {
        public string WarehouseLocation { get; set; } = string.Empty;
        public int OutStockType { get; set; }
        public string? OperationTime { get; set; }
    }

    public class VendorCallbackResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("Message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("Data")]
        public object? Data { get; set; }
    }
}
