using MesLibrary.HttpConnector.MesWebApi;
using MesLibrary.HttpConnector.SAP;
using MesLibrary.HttpConnector.SapWebApi;
using MesLibrary.Model.APC;
using MesLibrary.Model.SapToMes;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace RegistryService
{
    public class RegistryServices : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly RegistryServiceConnector _mesWebApi;
        private readonly ILogger<RegistryServices> _logger;
        private readonly RegistryServiceSapConnector _sapWebApi;
        private readonly RegistrySapConnector _SAPApi;
        public RegistryServices(IConfiguration configuration, RegistryServiceConnector registryServiceConnector, RegistryServiceSapConnector registryServiceSapConnector, ILogger<RegistryServices> logger)
        {
            _configuration = configuration;
            _mesWebApi = registryServiceConnector;
            _sapWebApi = registryServiceSapConnector;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            /* while (!stoppingToken.IsCancellationRequested)
             {*/
            await ExecuteOps();
            //await Task.Delay(10000000, stoppingToken);
            //}
        }

        private async Task ExecuteOps()
        {
            bool b = true;
            //b = await ManageCharacteristics();
            b = await ManageLotClasses();
            //await ManageMaterials();           

        }


        #region CHARACTERISTICS
        private async Task<Boolean> ManageCharacteristics()
        {
            string resp = string.Empty;
            List<SapCharacteristic>? characterists = await GetSapCharacteristicsAsync();
            if (characterists == null) return false;//gestire
            _logger.LogDebug("INIZIO OPERAZIONI CARATTERISTICHE");
            foreach (var characterist in characterists)
            {
                try
                {
                    await UpsertCharacteristic(characterist);
                    await DeleteCaratValues(characterist);
                    await InsertCaratValues(characterist);
                }
                catch (Exception ex)
                {
                    _logger.LogError("RegistryService.ManageCharacteristics: {ex}", ex);
                }
            }
            _logger.LogDebug("FINE OPERAZIONI CARATTERISTICHE");
            return true;
        }
        private async Task UpsertCharacteristic(SapCharacteristic sapCharacteristic)
        {
            HttpResponseMessage resp;
            An_Carat? anCarat = ConvertApcCharacteristic(sapCharacteristic);
            if (anCarat == null) return;
            resp = await _mesWebApi.UpsertCarat(anCarat);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"RegistryService.UpsertCharacteristic: ERRORE impossibile aggiornare {sapCharacteristic.Characteristic} \n ERROR: {resp.ReasonPhrase}");
            }
        }
        private async Task DeleteCaratValues(SapCharacteristic sapCharacteristic)
        {
            HttpResponseMessage resp;
            resp = await _mesWebApi.DeleteCaratVal(sapCharacteristic.Characteristic);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"RegistryService.DeleteCaratValues: ERRORE impossibile eliminare {sapCharacteristic.Characteristic} \n ERROR: {resp.ReasonPhrase}");
            }
        }
        private async Task InsertCaratValues(SapCharacteristic sapCharacteristic)
        {
            HttpResponseMessage resp;
            List<An_Carat_Val>? anCaratClassePartitaList = ConvertApcCaratValues(sapCharacteristic);
            if (anCaratClassePartitaList == null) return;
            resp = await _mesWebApi.InsertApcCaratValues(anCaratClassePartitaList);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"RegistryService.InsertCaratValues: ERRORE impossibile inserire {sapCharacteristic.Characteristic} \n ERROR: {resp.ReasonPhrase}");
            }
        }
        private List<An_Carat_Val>? ConvertApcCaratValues(SapCharacteristic sapCharacteristic)
        {
            List<An_Carat_Val> caratValues = new();
            if (sapCharacteristic.to_CharacteristicValue!= null && !sapCharacteristic.to_CharacteristicValue.results.IsNullOrEmpty() )
            {
                int prog = 0;
                foreach(var value in sapCharacteristic.to_CharacteristicValue.results)
                {
                    An_Carat_Val an_Carat_Val = new();
                    prog++;
                    an_Carat_Val.IdCarat = sapCharacteristic.Characteristic;
                    an_Carat_Val.Definizione = value.to_CharcValueDesc.results[0].CharcValueDescription;
                    an_Carat_Val.Valore = value.CharcValue;
                    an_Carat_Val.Progressivo = prog;

                    caratValues.Add(an_Carat_Val);
                }
            }
            return caratValues;
        }
        private An_Carat? ConvertApcCharacteristic(SapCharacteristic sapCharacteristic)
        {
            try
            {
                An_Carat anCarat = new();
                anCarat.IdCarat = sapCharacteristic.Characteristic;
                anCarat.IdUnitaMisura = sapCharacteristic.CharcValueUnit;
                anCarat.Tipo = sapCharacteristic.CharcDataType;
                anCarat.NumPos = sapCharacteristic.CharcLength;
                anCarat.NumDec = sapCharacteristic.CharcDecimals;
                sapCharacteristic.EntryIsRequired ??= "false";
                anCarat.FlgObbligatorio = sapCharacteristic.EntryIsRequired.Equals("false") ? "F" : "T"; ;
                sapCharacteristic.ValueIsCaseSensitive??= "false";
                anCarat.FlgMinuscole = sapCharacteristic.ValueIsCaseSensitive.Equals("false") ? "F" : "T";
                anCarat.Definizione = sapCharacteristic.to_CharacteristicDesc != null && !sapCharacteristic.to_CharacteristicDesc.results.IsNullOrEmpty() ? sapCharacteristic.to_CharacteristicDesc.results[0].CharcDescription : null;
                //anCarat.Definizione = sapCharacteristic.to_CharacteristicDesc.results[0].CharcDescription;
                return anCarat;
            }
            catch (Exception ex) 
            {
                _logger.LogError($"RegistryService.ConvertApcCharacteristic ERROR: {ex.Message}");
                return null; 
            }
        }
        private async Task<List<SapCharacteristic>?> GetSapCharacteristicsAsync()
        {
            try
            {
                string resp = await _sapWebApi.GetAllCharacteristics();
                if (string.IsNullOrEmpty(resp))
                {
                    _logger.LogDebug("Impossibile ottenere i dati SAP");
                    return null;
                }
                List<SapCharacteristic>? characteristics = JsonConvert.DeserializeObject<List<SapCharacteristic>>(resp);
                return characteristics;
            }
            catch (Exception ex)
            {
                _logger.LogError($"RegistryService.GetSapCharacteristicsAsync: ERROR {ex.Message}");
                return null;
            }
        }
        private JArray DeserializeResponse(JToken jCharat, An_Carat anCarat, JArray jCaratValues)
        {

            //JObject json = JObject.Parse(resp);          

            //JToken? jCharat = json["d"]["results"];
            anCarat.IdCarat = jCharat["Characteristic"].ToString();//JResult[0]["Characteristic"].ToString();
            anCarat.Tipo = jCharat["CharcDataType"].ToString();
            anCarat.Definizione = jCharat["to_CharacteristicDesc"]["results"][0]["CharcDescription"].ToString();
            anCarat.NumPos = (int)jCharat["CharcLength"];
            anCarat.NumDec = (int)jCharat["CharcDecimals"];
            anCarat.FlgMinuscole = jCharat["ValueIsCaseSensitive"].ToString().Equals("False") ? "F" : "T";
            anCarat.FlgObbligatorio = jCharat["EntryIsRequired"].ToString().Equals("False") ? "F" : "T";
            anCarat.IdUnitaMisura = jCharat["CharcValueUnit"].ToString();
            return JArray.Parse(jCharat["to_CharacteristicValue"]["results"].ToString());

        }

        #endregion

        #region LOT CLASSES

        private async Task<bool> ManageLotClasses()
        {
            HttpResponseMessage resp;
            List<SapLotClass>? lotClasses = await GetSapLotClassesAsync();
            if (lotClasses == null) return false;//gestire
            _logger.LogDebug($"INIZIO OPERAZIONI CLASSI - PARTITE");
            foreach (SapLotClass lotClass in lotClasses) //"QUALITA"
            {
                try
                {
                    if (lotClass.Class == null) continue;
                    await UpsertLotClass(lotClass);
                    await DeleteCaratLotClass(lotClass);
                    await InsertCaratLotClasses(lotClass);
                }
                catch (Exception ex)
                {
                    _logger.LogError("RegistryService.ManageLotClasses: {ex}", ex);
                }
            }
            _logger.LogDebug($"FINE OPERAZIONI CLASSI - PARTITE");
            return true;
        }
        private async Task UpsertLotClass(SapLotClass sapLotClass)
        {
            HttpResponseMessage resp;
            An_Classe_Partita? anClassePartita = ConvertApcLotClass(sapLotClass);
            if (anClassePartita == null) return;
            resp = await _mesWebApi.UpsertApcLotClass(anClassePartita);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"WorkCenterService.UpsertLotClass: ERRORE impossibile aggiornare {sapLotClass.Class} \n ERROR: {resp.ReasonPhrase}");
            }
        }
        private async Task DeleteCaratLotClass(SapLotClass sapLotClass)
        {
            HttpResponseMessage resp;
            resp = await _mesWebApi.DeleteApcCaratLotClass(sapLotClass.Class);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"WorkCenterService.DeleteCaratLotClass: ERRORE impossibile eliminare {sapLotClass.Class} \n ERROR: {resp.ReasonPhrase}");
            }
        }
        private async Task InsertCaratLotClasses(SapLotClass sapLotClass)
        {
            HttpResponseMessage resp;
            List<An_Carat_Classe_Partita>? anCaratClassePartitaList = ConvertApcCaratsLotClass(sapLotClass);
            if (anCaratClassePartitaList == null) return;
            resp = await _mesWebApi.InsertApcCaratLotClasses(anCaratClassePartitaList);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"WorkCenterService.InsertCaratLotClasses: ERRORE impossibile inerire {sapLotClass.Class} \n ERROR: {resp.ReasonPhrase}");
            }
        }
        private List<An_Carat_Classe_Partita>? ConvertApcCaratsLotClass(SapLotClass sapLotClass)
        {
            try
            {
                List<An_Carat_Classe_Partita>? anCaratClassePartitaList = new();
                if (!sapLotClass.to_ClassCharacteristic.results.IsNullOrEmpty())
                {
                    foreach (var characteristic in sapLotClass.to_ClassCharacteristic.results)
                    {
                        An_Carat_Classe_Partita an_Carat_Classe_Partita = new();
                        an_Carat_Classe_Partita.IdClassePartita = sapLotClass.Class;
                        an_Carat_Classe_Partita.IdCarat = characteristic.Characteristic;
                        anCaratClassePartitaList.Add(an_Carat_Classe_Partita);
                    }
                }
                return anCaratClassePartitaList;
            }
            catch (Exception ex) 
            {
                _logger.LogError($"RegistryService.ConvertApcCaratsLotClass ERROR: {ex.Message}");
                return null; 
            }
        }
        private An_Classe_Partita? ConvertApcLotClass(SapLotClass sapLotClass)
        {
            try
            {
                An_Classe_Partita anClassePartita = new();
                anClassePartita.IdClassePartita = sapLotClass.Class;
                anClassePartita.DescClassePartita = sapLotClass.to_ClassDescription!=null && !sapLotClass.to_ClassDescription.results.IsNullOrEmpty() ? sapLotClass.to_ClassDescription.results[0].ClassDescription : null;
                return anClassePartita;
            }
            catch (Exception ex) 
            {
                _logger.LogError($"RegistryService.ConvertApcLotClass ERROR: {ex.Message}");
                return null; 
            }
        }
        private async Task<List<SapLotClass>?> GetSapLotClassesAsync()
        {
            try
            {
                string resp = await _sapWebApi.GetAllLotClasses();
                if (string.IsNullOrEmpty(resp))
                {
                    _logger.LogDebug("Impossibile ottenere i dati SAP");
                    return null;
                }
                List<SapLotClass>? lotClasses = JsonConvert.DeserializeObject<List<SapLotClass>>(resp);
                return lotClasses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"RegistryService.GetSapLotClassesAsync: ERROR {ex.Message}");
                return null;
            }
        }
        private List<An_Carat_Classe_Partita> DeserializeResponse(JToken jLotClass, An_Classe_Partita anClassePartita)
        {

            anClassePartita.IdClassePartita = jLotClass["Class"].ToString();
            anClassePartita.DescClassePartita = !jLotClass["to_ClassDescription"]["results"].IsNullOrEmpty() ? jLotClass["to_ClassDescription"]["results"][0]["ClassDescription"].ToString() : null;
            JArray a = JArray.Parse(jLotClass["to_ClassCharacteristic"]["results"].ToString());
            List<An_Carat_Classe_Partita> list = new List<An_Carat_Classe_Partita>();
            if (a.IsNullOrEmpty()) return null;
            foreach (JToken item in a)
            {
                An_Carat_Classe_Partita anCCP = new An_Carat_Classe_Partita();
                anCCP.IdClassePartita = anClassePartita.IdClassePartita;
                anCCP.IdCarat = item["Characteristic"].ToString();
                list.Add(anCCP);

            }
            return list;
        }

        #endregion

        #region MATERIAL
        /// <summary>
        /// FARE GLI IF per le varie casistiche
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ManageMaterials()
        {
            List<SapProduct>? sapProducts = await GetSapProductsAsync();
            if (sapProducts == null) return false;//gestire
            _logger.LogDebug("INIZIO OPERAZIONI MATERIALI");
            foreach (var product in sapProducts) //"QUALITA"
            {
                try
                {
                    await UpsertProducts(product);
                    
                    _logger.LogDebug($"FINE operazioni : ");
                }
                catch (Exception ex)
                {
                    _logger.LogError("RegistryService.ManageMaterials: {ex}", ex);

                }
            }
            _logger.LogDebug("FINE OPERAZIONI MATERIALI");
            return true;
        }

        private async Task UpsertProducts(SapProduct sapProduct)
        {
            HttpResponseMessage resp;
            An_Materiale? anMateriale = ConvertApcProduct(sapProduct);
            GetMagazzino(anMateriale.TipoMagazzino);
            if (anMateriale == null) return;
            resp = await _mesWebApi.UpsertApcMaterial(anMateriale);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"WorkCenterService.UpsertProducts: ERRORE impossibile aggiornare {sapProduct.Product} \n ERROR: {resp.ReasonPhrase}");
            }
        }

        private async Task<List<SapProduct>?> GetSapProductsAsync()
        {
            try
            {
                string resp = await _sapWebApi.GetAllProducts();
                if (string.IsNullOrEmpty(resp))
                {
                    _logger.LogDebug("Impossibile ottenere i dati SAP");
                    return null;
                }
                List<SapProduct>? products = JsonConvert.DeserializeObject<List<SapProduct>>(resp);

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return null;
            }
        }

        private An_Materiale? ConvertApcProduct(SapProduct sapProduct)
        {
            try
            {
                An_Materiale an_Materiale = new();
                if(sapProduct.Product == null)  return null;
                an_Materiale.IdMateriale = sapProduct.Product;
                an_Materiale.DescMateriale = sapProduct.to_Description.results[0].ProductDescription;
                an_Materiale.IdClassePartita = sapProduct.to_ProductClass.results[0].to_ClassDetails.Class;
                an_Materiale.TipoMateriale = sapProduct.ProductType;
                an_Materiale.TipoMagazzino = "";
                an_Materiale.FlgCoprodotto = sapProduct.to_Plant.results[0].IsMarkedForDeletion.ToString();
                an_Materiale.FlgFondente = sapProduct.to_Plant.results[0].IsMarkedForDeletion.ToString();
                return an_Materiale;
            }
            catch (Exception ex)
            {
                _logger.LogError($"RegistryService.ConvertApcProduct ERROR: {ex.Message}");
                return null; 
            }
        }
        /// <summary>
        /// Capire come gestire tutte le if sulla zinf2
        /// Vedere se sono ancora necessarie
        /// </summary>
        /// <param name="sapProduct"></param>
        /// <returns></returns>
        private string? GetMagazzino(string materiale)
        {
            
            return null;
        }

        private void DeserializeResponse(JToken jMaterial, An_Materiale anMateriale)
        {

            anMateriale.IdMateriale = jMaterial["Product"].ToString();
            anMateriale.TipoMateriale = jMaterial["ProductType"].ToString();
            anMateriale.DescMateriale = jMaterial["to_Description"]["results"][0]["ProductDescription"].ToString();


        }

        #endregion

    }
}

