using MesLibrary.HttpConnector.MesWebApi;
using MesLibrary.HttpConnector.SAP;
using MesLibrary.HttpConnector.SapWebApi;
using MesLibrary.Model.APC;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MesLibrary.Model.SapToMes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegistryService
{
    public class WorkCenterService : BackgroundService
    {

        private readonly IConfiguration _configuration;
        private readonly RegistryServiceConnector _mesWebApi;
        private readonly ILogger<RegistryServices> _logger;
        private readonly RegistryServiceSapConnector _sapWebApi;
        public WorkCenterService(IConfiguration configuration, RegistryServiceConnector registryServiceConnector, RegistryServiceSapConnector registrySapConnector, ILogger<RegistryServices> logger)
        {
            _configuration = configuration;
            _mesWebApi = registryServiceConnector;
            _sapWebApi = registrySapConnector;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ExecuteOps();
        }

        private async Task ExecuteOps()
        {
            bool b = true;
            b = await ManageWorkCenters();
        }

        private async Task<bool> ManageWorkCenters()
        {
            List<SapWorkCenter>? workCenters = await GetSapWorkCentersAsync();
            if (workCenters == null) return false;
            foreach (SapWorkCenter workCenter in workCenters)
            {
                try
                {
                    await UpsertApcWorkCenter(workCenter);
                }
                catch (Exception ex)
                {
                    _logger.LogError("WorkCenterService.ManageWorkCenters ERROR: {ex}", ex);

                }
            }
            return true;
        }

        private async Task UpsertApcWorkCenter(SapWorkCenter workCenter)
        {
            HttpResponseMessage resp;
            An_Centro_Di_Lavoro anCentroDiLavoro = ConvertApcWorkCenters(workCenter);
            resp = await _mesWebApi.UpsertApcWorkCenter(anCentroDiLavoro);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"WorkCenterService.UpsertApcWorkCenter: ERRORE impossibile aggiornare {anCentroDiLavoro.IdCentroDiLavoro} \n ERROR: {resp.ReasonPhrase}");
            }
        }

        private An_Centro_Di_Lavoro ConvertApcWorkCenters(SapWorkCenter workCenter)
        {
            try
            {
                An_Centro_Di_Lavoro anCentroDiLavoro = new();
                anCentroDiLavoro.IdCentroDiLavoro = workCenter.WorkCenter;
                anCentroDiLavoro.Divisione = workCenter.Plant;
                anCentroDiLavoro.DescCentroDiLavoro = workCenter.WorkCenterDesc;
                anCentroDiLavoro.GruppoCollegamento = workCenter.SubsystemGrouping;
                anCentroDiLavoro.Atv1Nome = workCenter.StandardWorkFormulaParam1;
                anCentroDiLavoro.Atv1Desc = workCenter.StandardWorkFormulaParamVal1;
                anCentroDiLavoro.Atv1Tipo = workCenter.StandardWorkFormulaParamName1;
                anCentroDiLavoro.Atv2Nome = workCenter.StandardWorkFormulaParam2;
                anCentroDiLavoro.Atv2Desc = workCenter.StandardWorkFormulaParamVal2;
                anCentroDiLavoro.Atv2Tipo = workCenter.StandardWorkFormulaParamName2;
                anCentroDiLavoro.Atv3Nome = workCenter.StandardWorkFormulaParam3;
                anCentroDiLavoro.Atv3Desc = workCenter.StandardWorkFormulaParamVal3;
                anCentroDiLavoro.Atv3Tipo = workCenter.StandardWorkFormulaParamName3;
                anCentroDiLavoro.Atv4Nome = workCenter.StandardWorkFormulaParam4;
                anCentroDiLavoro.Atv4Desc = workCenter.StandardWorkFormulaParamVal4;
                anCentroDiLavoro.Atv4Tipo = workCenter.StandardWorkFormulaParamName4;
                anCentroDiLavoro.Atv5Nome = workCenter.StandardWorkFormulaParam5;
                anCentroDiLavoro.Atv5Desc = workCenter.StandardWorkFormulaParamVal5;
                anCentroDiLavoro.Atv5Tipo = workCenter.StandardWorkFormulaParamName5;
                anCentroDiLavoro.Atv6Nome = workCenter.StandardWorkFormulaParam6;
                anCentroDiLavoro.Atv6Desc = workCenter.StandardWorkFormulaParamVal6;
                anCentroDiLavoro.Atv6Tipo = workCenter.StandardWorkFormulaParamName6;

                return anCentroDiLavoro;
            }
            catch (Exception ex) { return null; }
        }

        private async Task<List<SapWorkCenter>?> GetSapWorkCentersAsync()
        {
            try
            {
                string resp = await _sapWebApi.GetAllWorkCenters();
                if (string.IsNullOrEmpty(resp))
                {
                    _logger.LogDebug("Impossibile ottenere i dati SAP");
                    return null;
                }
                List<SapWorkCenter>? workCenters = JsonConvert.DeserializeObject<List<SapWorkCenter>>(resp);

                return workCenters;
            }
            catch (Exception ex)
            {
                _logger.LogError($"WorkCenterService.GetSapWorkCentersAsync: {ex.Message}", ex);
                return null;
            }
        }





    }
}
