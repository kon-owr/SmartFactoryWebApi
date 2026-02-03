using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.DTO
{
    public class Result<T>
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public T? Data { get; init; }

        public static Result<T> Ok(T? data, string? message)
        {
            return new Result<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static Result<T> Fail(string message)
        {
            return new Result<T>
            {
                Success = false,
                Message = message,
                Data = default
            };
        }
    }
}
