namespace Library.SharedMethods
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class SharedMethods
    {
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
