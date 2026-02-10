using System.Diagnostics;
using System.Net;
using ESI.NET;

namespace HISA.EVEData
{
    public class ESIHelpers
    {
        public static bool ValidateESICall<T>(EsiResponse<T> esiR)
        {
            if (esiR == null)
            {
                Debug.WriteLine("ESI Error : Response object was null.");
                return false;
            }

            bool statusOk = esiR.StatusCode == HttpStatusCode.OK ||
                            esiR.StatusCode == HttpStatusCode.Created ||
                            esiR.StatusCode == HttpStatusCode.NoContent;
            if (!statusOk)
            {
                Debug.WriteLine("ESI Error : " + esiR.Message + " Status: " + esiR.StatusCode + " Endpoint: " + esiR.Endpoint + " Error Limit Remaining : " + esiR.ErrorLimitRemain);
                return false;
            }

            if (esiR.Exception != null)
            {
                Debug.WriteLine("ESI Deserialize Error : " + esiR.Exception + " Endpoint: " + esiR.Endpoint + " Raw Message: " + esiR.Message);
                return false;
            }

            if (esiR.StatusCode != HttpStatusCode.NoContent && esiR.Data == null && typeof(T) != typeof(string))
            {
                Debug.WriteLine("ESI Error : Response data was null. Endpoint: " + esiR.Endpoint + " Status: " + esiR.StatusCode + " Raw Message: " + esiR.Message);
                return false;
            }

            return true;
        }
    }
}
