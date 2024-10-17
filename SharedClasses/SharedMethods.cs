namespace Shared.SharedMethods
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class SharedMethods
    {
        public enum Status
        {
            Disabled = 0,
            Enabled = 1,
        }

        public static string GetOneDeserializedValue(string scriptParam)
        {
            if (scriptParam.Contains("[") && scriptParam.Contains("]"))
            {
                return JsonConvert.DeserializeObject<List<string>>(scriptParam)[0];
            }
            else
            {
                return scriptParam;
            }
        }
    }
}
