using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    public  interface IWMSLightService
    {

        Task<string> ChangeBinNoLightStatus(List<string> binNoList, LightColorCode lightColorCode, CancellationToken cancellationToken = default);
    }
}
